using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>スマッシュボールが割られた瞬間の記録・通知1件分。履歴表示や、将来のFinal Smash的な能力発動のフックに使う。</summary>
    public readonly struct SmashBallBreakInfo
    {
        public readonly SmashBallModule Module;
        public readonly DestructibleObstacle Obstacle;
        public readonly DestructibleAttacker Attacker;
        public readonly Vector3 WorldPosition;
        public readonly float Time;

        public SmashBallBreakInfo(SmashBallModule module, DestructibleObstacle obstacle, DestructibleAttacker attacker, Vector3 worldPosition, float time)
        {
            Module = module;
            Obstacle = obstacle;
            Attacker = attacker;
            WorldPosition = worldPosition;
            Time = time;
        }

        /// <summary>このスマッシュボールを識別する表示名（設定した名札があればそれ、無ければオブジェクト名）。</summary>
        public string DisplayName => Module != null && !string.IsNullOrWhiteSpace(Module.settings.label)
            ? Module.settings.label
            : (Obstacle != null ? Obstacle.name : "スマッシュボール");
    }
}
