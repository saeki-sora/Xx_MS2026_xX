using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Provider;
using MS2026.GripInputBridge.Recording;
using MS2026.GripInputBridge.Transports;

namespace MS2026.GripInputBridge
{
    /// <summary>
    /// ゲームコードから使う唯一の入口（ファサード）。
    /// 使い方: <c>GripInputBridge.Provider.GetGripValue(playerIndex)</c>
    /// 設計書 18.2 APIクイックリファレンス 参照。
    /// </summary>
    public static class GripInputBridge
    {
        private static GripInputProvider _provider;
        private static GripSessionRecorder _recorder;

        /// <summary>
        /// ゲームコードが実際に呼び出す窓口。未初期化の場合、既定でキーボードシミュレータを
        /// トランスポートとして自動生成する（実機が無くても即座に動作を確認できるようにするため）。
        /// </summary>
        public static IGripInputProvider Provider => _provider ??= CreateDefaultProvider();

        /// <summary>記録中のログファイルの絶対パス。記録していない場合はnull。</summary>
        public static string CurrentRecordingFilePath => _recorder?.FilePath;

        /// <summary>記録中かどうか。</summary>
        public static bool IsRecording => _recorder != null;

        /// <summary>
        /// 入力元を差し替える（ホットスワップ）。実機接続(Phase 3)・記録再生(Phase 5)が
        /// 揃った際は、ここでトランスポートを切り替えるだけでよい。
        /// </summary>
        public static void SetTransport(IGripTransport transport)
        {
            EnsureProvider().SetTransport(transport);
        }

        /// <summary>ノイズフィルタの設定を差し替える。設計書 8章のシミュレータ・実機どちらにも適用される。</summary>
        public static void SetFilterSettings(GripFilterSettings filterSettings)
        {
            EnsureProvider().SetFilterSettings(filterSettings);
        }

        /// <summary>プレイヤーごとのキャリブレーションプロファイルを差し替える。</summary>
        public static void SetCalibrationProfiles(GripCalibrationProfile[] profiles)
        {
            EnsureProvider().SetCalibrationProfiles(profiles);
        }

        /// <summary>
        /// セッションログの記録を開始する。設計書 10.1 参照。既に記録中の場合は一度止めてから開始し直す。
        /// </summary>
        /// <param name="directoryPath">保存先。省略時はプロジェクト直下の "GripLogs" フォルダ。</param>
        public static void StartRecording(string directoryPath = null)
        {
            StopRecording();
            _recorder = new GripSessionRecorder(directoryPath);
        }

        /// <summary>セッションログの記録を停止し、ファイルを確定させる。記録していない場合は何もしない。</summary>
        public static void StopRecording()
        {
            _recorder?.Dispose();
            _recorder = null;
        }

        /// <summary>
        /// 毎フレーム呼び出し、エッジイベント検出等の内部状態を更新する。記録中であればログにも書き込む。
        /// 通常は <see cref="GripInputBridgeRuntimeDriver"/> が自動的に呼び出すため、
        /// ゲームコードから明示的に呼ぶ必要はない。
        /// </summary>
        internal static void Tick()
        {
            var provider = EnsureProvider();
            provider.Update();

            if (_recorder == null)
            {
                return;
            }

            for (var i = 0; i < GripInputProvider.PlayerCount; i++)
            {
                _recorder.RecordSample(i, provider.GetRawValue(i), provider.GetFilteredValue(i), provider.GetGripValue(i));
            }
        }

        private static GripInputProvider EnsureProvider()
        {
            return _provider ??= CreateDefaultProvider();
        }

        private static GripInputProvider CreateDefaultProvider()
        {
            return new GripInputProvider(new SimulatedGripTransport());
        }
    }
}
