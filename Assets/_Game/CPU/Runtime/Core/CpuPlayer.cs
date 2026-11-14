using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU 1人（目 → 頭 → 手）。作るのは <see cref="CpuDirector"/>。結果を決める PC（Host・オフライン）だけで動く。
    /// 段0: 頭と手と行き先の枠だけ（行動が何も登録されていなければ、何もしない CPU が席に座る）。
    /// </summary>
    public sealed class CpuPlayer
    {
        private readonly ICpuMovePlanner _movePlanner;

        public CpuPlayer(int player, CpuLevel level, int seed, float thinkInterval)
        {
            Hands = new CpuHands(player);
            Context = new CpuContext(player, level, seed, Hands);
            Brain = new CpuBrain(CpuPlugins.CreateBehaviors()) { ThinkInterval = thinkInterval };
            _movePlanner = CpuPlugins.CreateMovePlanner();
        }

        public int Player => Context.Player;
        public CpuLevel Level => Context.Level;
        public CpuContext Context { get; }
        public CpuHands Hands { get; }
        public CpuBrain Brain { get; }

        /// <summary>行き先を決める係があるか（実験中の移動が差し込まれているか）。</summary>
        public bool CanMove => _movePlanner != null;

        /// <summary>最後に決めた行きたい向き（頭の中の表示用）。</summary>
        public Vector2 MoveIntent { get; private set; }

        internal void Tick(LaserTurret turret, float deltaTime)
        {
            Context.Turret = turret;
            Context.Time += deltaTime;

            Brain.Tick(Context, deltaTime);
            Hands.Tick(deltaTime);
            TickMove(deltaTime);
        }

        internal void Stop()
        {
            Brain.Stop(Context);
            _movePlanner?.Reset();
            MoveIntent = Vector2.zero;
            CpuPlugins.MoveOutput?.ClearMove(Player);
        }

        private void TickMove(float deltaTime)
        {
            var output = CpuPlugins.MoveOutput;
            if (_movePlanner == null || output == null)
            {
                MoveIntent = Vector2.zero;
                return;
            }

            var direction = _movePlanner.Plan(Context, deltaTime);
            MoveIntent = Vector2.ClampMagnitude(direction, 1f);
            output.SetMove(Player, MoveIntent);
        }
    }
}
