using System;

namespace MS2026.GripInputBridge
{
    /// <summary>
    /// ゲーム側が実際に呼び出す唯一の窓口。トランスポート（実機/シミュレータ/記録再生）が
    /// 何であってもこのAPIの意味は変わらない。設計書 3章・7.1 参照。
    /// </summary>
    public interface IGripInputProvider
    {
        /// <summary>
        /// 正規化済みの握力値（0.0-1.0）。ゲーム側はこの値だけを見ればよい。
        /// Phase 1 時点ではキャリブレーション・フィルタ未実装のため、トランスポートの生値を
        /// そのまま返す（Phase 2 で信号処理パイプラインに置き換える）。
        /// </summary>
        float GetGripValue(int playerIndex);

        /// <summary>デバッグ用。フィルタ・キャリブレーション適用前の値。</summary>
        float GetRawValue(int playerIndex);

        /// <summary>デッドゾーンを超えて「握っている」とみなせるか。</summary>
        bool IsGripping(int playerIndex);

        /// <summary>握り始めた瞬間（エッジ）に発火する。</summary>
        event Action<int> OnGripStarted;

        /// <summary>離した瞬間（エッジ）に発火する。</summary>
        event Action<int> OnGripReleased;

        /// <summary>そのプレイヤーの接続状態・異常フラグを取得する。</summary>
        GripDeviceStatus GetStatus(int playerIndex);
    }
}
