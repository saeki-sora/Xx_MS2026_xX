using System;

namespace MS2026.Fortress
{
    /// <summary>
    /// 破壊可能物の「種類」に当たる設定一式。プリセット(<see cref="DestructiblePreset"/>)にそのまま保存・適用できる。
    /// 個体ごとの設定（連動・無敵）はここに含めず、<see cref="DestructibleObstacle"/> 側に持たせる。
    /// </summary>
    [Serializable]
    public sealed class DestructibleSettings
    {
        public DestructibleDurability durability = new DestructibleDurability();
        public DestructibleVisualSettings visual = new DestructibleVisualSettings();
        public DestructibleFeedbackSettings feedback = new DestructibleFeedbackSettings();
        public DestructibleDropSettings drops = new DestructibleDropSettings();
        public DestructibleCollisionSettings collision = new DestructibleCollisionSettings();
    }
}
