using System;
using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Processing;

namespace MS2026.GripInputBridge.Provider
{
    /// <summary>
    /// <see cref="IGripInputProvider"/> の標準実装。
    /// パイプラインは 生値 → ノイズフィルタ → キャリブレーション(デッドゾーン→min-max→応答カーブ)
    /// → ヒステリシス付きエッジ検出 の順。設計書 7.2 参照。
    ///
    /// 重要: フィルタ（EMA/メディアン/移動平均）とエッジ検出器は内部状態を持つため、
    /// 同じ「サンプル」を複数回 <see cref="Processing.IGripSignalFilter.Filter"/> や
    /// <see cref="Processing.GripEdgeDetector.Update"/> に通すと値が壊れる。
    /// そのため生値の取得〜エッジ検出は <see cref="Sample"/> でフレームに1回だけ行い、
    /// <see cref="GetGripValue"/> 等の読み取りはキャッシュを返すだけにしている。
    /// </summary>
    public sealed class GripInputProvider : IGripInputProvider
    {
        public const int PlayerCount = GripInputBridgeConstants.PlayerCount;

        // GripFilterSettingsが未割り当ての場合に使う既定のヒステリシスしきい値。
        // GripFilterSettingsのデフォルト値(onThreshold=0.12 / offThreshold=0.08)と揃えてある。
        private const float DefaultOnThreshold = 0.12f;
        private const float DefaultOffThreshold = 0.08f;

        private readonly IGripSignalFilter[] _filters = new IGripSignalFilter[PlayerCount];
        private readonly GripEdgeDetector[] _edgeDetectors = new GripEdgeDetector[PlayerCount];
        private readonly GripCalibrationProfile[] _calibrationProfiles = new GripCalibrationProfile[PlayerCount];
        private readonly float[] _cachedRawValue = new float[PlayerCount];
        private readonly float[] _cachedFilteredValue = new float[PlayerCount];
        private readonly float[] _cachedGripValue = new float[PlayerCount];
        private readonly bool[] _cachedEdgeChanged = new bool[PlayerCount];

        private IGripTransport _transport;
        private GripFilterSettings _filterSettings;
        private bool _hasSampledOnce;

        public event Action<int> OnGripStarted;
        public event Action<int> OnGripReleased;

        public GripInputProvider(
            IGripTransport initialTransport,
            GripFilterSettings filterSettings = null,
            GripCalibrationProfile[] calibrationProfiles = null)
        {
            SetTransport(initialTransport);
            SetFilterSettings(filterSettings);
            SetCalibrationProfiles(calibrationProfiles);
        }

        /// <summary>
        /// 入力元を差し替える（ホットスワップ）。実機/シミュレータ/記録再生のいずれでも、
        /// このメソッドを呼ぶだけで以後の <see cref="GetGripValue"/> 等の返り値が切り替わる。
        /// </summary>
        public void SetTransport(IGripTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>
        /// ノイズフィルタ・ヒステリシスしきい値の設定を差し替える。
        /// 4人分のフィルタ・エッジ検出器の内部状態を作り直す
        /// （それまでの生値の記憶を持ち越さないようにするため）。
        /// </summary>
        public void SetFilterSettings(GripFilterSettings filterSettings)
        {
            _filterSettings = filterSettings;
            for (var i = 0; i < PlayerCount; i++)
            {
                _filters[i] = GripFilterFactory.Create(_filterSettings);
                _edgeDetectors[i] = CreateEdgeDetector(_filterSettings);
            }
        }

        /// <summary>
        /// プレイヤーごとのキャリブレーションプロファイルを差し替える。
        /// 未割り当て(null)のプレイヤーには、線形・デッドゾーン0.03の既定プロファイルを自動で割り当てる
        /// （キャリブレーション未実施でも動作を止めないため）。
        /// </summary>
        public void SetCalibrationProfiles(GripCalibrationProfile[] profiles)
        {
            for (var i = 0; i < PlayerCount; i++)
            {
                var profile = profiles != null && i < profiles.Length ? profiles[i] : null;
                _calibrationProfiles[i] = profile != null ? profile : GripCalibrationProfile.CreateDefault(i);
            }
        }

        public float GetGripValue(int playerIndex)
        {
            EnsureSampledAtLeastOnce();
            return IsValidIndex(playerIndex) ? _cachedGripValue[playerIndex] : 0f;
        }

        public float GetRawValue(int playerIndex)
        {
            EnsureSampledAtLeastOnce();
            return IsValidIndex(playerIndex) ? _cachedRawValue[playerIndex] : 0f;
        }

        /// <summary>
        /// フィルタ適用後・キャリブレーション適用前の値。設計書10.1のログ("filtered"列)や
        /// 診断表示で、ノイズフィルタと較正のどちらに問題があるかを切り分けるために使う。
        /// </summary>
        public float GetFilteredValue(int playerIndex)
        {
            EnsureSampledAtLeastOnce();
            return IsValidIndex(playerIndex) ? _cachedFilteredValue[playerIndex] : 0f;
        }

        public bool IsGripping(int playerIndex)
        {
            EnsureSampledAtLeastOnce();
            return IsValidIndex(playerIndex) && _edgeDetectors[playerIndex].IsGripping;
        }

        public GripDeviceStatus GetStatus(int playerIndex)
        {
            if (!IsValidIndex(playerIndex))
            {
                return GripDeviceStatus.Disconnected;
            }

            return _transport.IsConnected(playerIndex) ? GripDeviceStatus.Connected : GripDeviceStatus.Disconnected;
        }

        /// <summary>
        /// 毎フレーム呼び出し、新しいサンプルをフィルタ・キャリブレーション・エッジ検出に通し、
        /// 握り始め/離しのイベントを発火する。
        /// (通常は <see cref="GripInputBridge"/> のランタイムドライバーが自動的に呼び出す)
        /// </summary>
        public void Update()
        {
            Sample();

            for (var i = 0; i < PlayerCount; i++)
            {
                if (!_cachedEdgeChanged[i])
                {
                    continue;
                }

                if (_edgeDetectors[i].IsGripping)
                {
                    OnGripStarted?.Invoke(i);
                }
                else
                {
                    OnGripReleased?.Invoke(i);
                }
            }
        }

        private void EnsureSampledAtLeastOnce()
        {
            if (!_hasSampledOnce)
            {
                Sample();
            }
        }

        private void Sample()
        {
            for (var i = 0; i < PlayerCount; i++)
            {
                var raw = _transport.GetRawValue(i);
                _cachedRawValue[i] = raw;

                var filtered = _filters[i].Filter(raw);
                _cachedFilteredValue[i] = filtered;

                var normalized = _calibrationProfiles[i].Normalize(filtered);
                _cachedGripValue[i] = normalized;

                _cachedEdgeChanged[i] = _edgeDetectors[i].Update(normalized);
            }

            _hasSampledOnce = true;
        }

        private static GripEdgeDetector CreateEdgeDetector(GripFilterSettings settings)
        {
            var onThreshold = settings != null ? settings.onThreshold : DefaultOnThreshold;
            var offThreshold = settings != null ? settings.offThreshold : DefaultOffThreshold;
            return new GripEdgeDetector(onThreshold, offThreshold);
        }

        private static bool IsValidIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < PlayerCount;
        }
    }
}
