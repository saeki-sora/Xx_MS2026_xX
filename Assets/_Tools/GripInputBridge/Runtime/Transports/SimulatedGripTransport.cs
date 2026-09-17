using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 実機センサーの代わりに、キーボードのホールド操作で握力を模擬するトランスポート。
    /// 設計書 8.1〜8.2 参照（デフォルトキー割当: P1=Q, P2=W, P3=O, P4=P）。
    /// キーを押している間は値が上昇し、離すと一定速度で減衰する
    /// （単純なON=1/OFF=0にしないことで「握り続けると強くなる」感触に近づける）。
    /// </summary>
    public sealed class SimulatedGripTransport : IGripTransport
    {
        public const int PlayerCount = 4;

        /// <summary>設計書 8.2 のデフォルトキー割当。</summary>
        public static readonly Key[] DefaultKeyBindings = { Key.Q, Key.W, Key.O, Key.P };

        private const float DefaultRiseSpeedPerSecond = 3.0f;
        private const float DefaultDecaySpeedPerSecond = 1.5f;

        private readonly Key[] _keyBindings;
        private readonly float _riseSpeedPerSecond;
        private readonly float _decaySpeedPerSecond;
        private readonly float[] _values;
        private readonly bool[] _connected;
        private readonly int[] _lastUpdatedFrame;
        private readonly GripWaveformPreset[] _presets;
        private readonly float[] _presetElapsedSeconds;

        public event Action<int> OnConnected;
        public event Action<int> OnDisconnected;

        public SimulatedGripTransport(
            Key[] keyBindings = null,
            float riseSpeedPerSecond = DefaultRiseSpeedPerSecond,
            float decaySpeedPerSecond = DefaultDecaySpeedPerSecond)
        {
            _keyBindings = keyBindings ?? DefaultKeyBindings;
            _riseSpeedPerSecond = riseSpeedPerSecond;
            _decaySpeedPerSecond = decaySpeedPerSecond;

            _values = new float[_keyBindings.Length];
            _lastUpdatedFrame = new int[_keyBindings.Length];
            _connected = new bool[_keyBindings.Length];
            _presets = new GripWaveformPreset[_keyBindings.Length];
            _presetElapsedSeconds = new float[_keyBindings.Length];
            for (var i = 0; i < _connected.Length; i++)
            {
                _connected[i] = true;
            }
        }

        /// <summary>
        /// 波形プリセットを設定する。設計書 8.3 参照。<see cref="GripWaveformPreset.None"/> 以外を指定すると、
        /// そのプレイヤーはキーボード入力を無視してプリセットの波形を自動再生する。
        /// </summary>
        public void SetPreset(int playerIndex, GripWaveformPreset preset)
        {
            if (!IsValidIndex(playerIndex))
            {
                return;
            }

            _presets[playerIndex] = preset;
            _presetElapsedSeconds[playerIndex] = 0f;
        }

        /// <summary>現在そのプレイヤーに設定されている波形プリセット。</summary>
        public GripWaveformPreset GetPreset(int playerIndex)
        {
            return IsValidIndex(playerIndex) ? _presets[playerIndex] : GripWaveformPreset.None;
        }

        public bool IsConnected(int playerIndex)
        {
            return IsValidIndex(playerIndex) && _connected[playerIndex];
        }

        public float GetRawValue(int playerIndex)
        {
            if (!IsValidIndex(playerIndex))
            {
                return 0f;
            }

            UpdateIfNeeded(playerIndex);
            return _values[playerIndex];
        }

        /// <summary>
        /// 診断・会場運営フォールバック用。特定プレイヤーを疑似的に「未接続」にする。
        /// （Phase 3 で実機切断が起きた際、スタッフがこのシミュレータへ切り替えるユースケースを想定）
        /// </summary>
        public void SetConnected(int playerIndex, bool connected)
        {
            if (!IsValidIndex(playerIndex) || _connected[playerIndex] == connected)
            {
                return;
            }

            _connected[playerIndex] = connected;
            if (connected)
            {
                OnConnected?.Invoke(playerIndex);
            }
            else
            {
                OnDisconnected?.Invoke(playerIndex);
            }
        }

        private bool IsValidIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < _keyBindings.Length;
        }

        private void UpdateIfNeeded(int playerIndex)
        {
            var frame = Time.frameCount;
            if (_lastUpdatedFrame[playerIndex] == frame)
            {
                // 同一フレーム内で複数回呼ばれても二重に加算しない。
                return;
            }

            _lastUpdatedFrame[playerIndex] = frame;

            if (_presets[playerIndex] != GripWaveformPreset.None)
            {
                _presetElapsedSeconds[playerIndex] += Time.deltaTime;
                _values[playerIndex] = GripWaveformPresetMath.Evaluate(_presets[playerIndex], _presetElapsedSeconds[playerIndex]);
                return;
            }

            var keyboard = Keyboard.current;
            var isHeld = _connected[playerIndex] && keyboard != null && keyboard[_keyBindings[playerIndex]].isPressed;

            _values[playerIndex] = GripSimulationMath.StepValue(
                _values[playerIndex],
                isHeld,
                Time.deltaTime,
                _riseSpeedPerSecond,
                _decaySpeedPerSecond);
        }
    }
}
