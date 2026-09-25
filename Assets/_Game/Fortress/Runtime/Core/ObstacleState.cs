namespace MS2026.Fortress
{
    /// <summary>破壊可能な地形障害物の現在状態。企画書の「削れる→破壊→時間経過で再生」に対応。</summary>
    public enum ObstacleState
    {
        /// <summary>健在。レーザーを遮り、ダメージを受けると削れる。</summary>
        Intact,

        /// <summary>破壊済み。レーザーは素通りする。再生タイマーが進行中。</summary>
        Destroyed
    }
}
