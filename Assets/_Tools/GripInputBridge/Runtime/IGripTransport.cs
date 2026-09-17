using System;

namespace MS2026.GripInputBridge
{
    /// <summary>
    /// 握力の生データをどこから取得するかを抽象化するインターフェース。
    /// 実装例: <see cref="Transports.SimulatedGripTransport"/>（キーボード）、
    /// 実機シリアル接続（Phase 3）、記録データ再生（Phase 5）。
    /// 設計書 7.1 参照。
    /// </summary>
    public interface IGripTransport
    {
        /// <summary>指定プレイヤーが現在接続されているか。</summary>
        bool IsConnected(int playerIndex);

        /// <summary>
        /// 較正・フィルタ適用前の生値を 0-1 の範囲で返す（機器依存レンジの一次変換はここで完了させる）。
        /// </summary>
        float GetRawValue(int playerIndex);

        /// <summary>そのプレイヤーの接続が確立したときに発火する。</summary>
        event Action<int> OnConnected;

        /// <summary>そのプレイヤーの接続が失われたときに発火する。</summary>
        event Action<int> OnDisconnected;
    }
}
