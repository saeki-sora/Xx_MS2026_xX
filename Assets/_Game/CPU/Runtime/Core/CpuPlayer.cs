using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU 1人（目 → 頭 → 手）。作るのは <see cref="CpuDirector"/>。結果を決める PC（Host・オフライン）だけで動く。
    /// 毎フレーム: 目が新しい写しを見る → 頭が行動を選んで進める → 手が握力を動かす → 行き先（差し込まれていれば）。
    /// </summary>
    public sealed class CpuPlayer
    {
        private readonly ICpuMovePlanner _movePlanner;
        private bool _wasOverheated;

        public CpuPlayer(int player, CpuLevel level, int seed, float thinkInterval, in CpuSkillValues skill)
        {
            Hands = new CpuHands(player, unchecked(seed * 31 + 7));
            Eyes = new CpuEyes();
            Context = new CpuContext(player, level, seed, Hands, Eyes) { Skill = skill };
            Hands.Apply(skill);
            Brain = new CpuBrain(CpuPlugins.CreateBehaviors()) { ThinkInterval = thinkInterval };
            _movePlanner = CpuPlugins.CreateMovePlanner();
        }

        public int Player => Context.Player;
        public CpuLevel Level => Context.Level;
        public CpuContext Context { get; }
        public CpuHands Hands { get; }
        public CpuEyes Eyes { get; }
        public CpuBrain Brain { get; }

        /// <summary>行き先を決める係があるか（実験中の移動が差し込まれているか）。</summary>
        public bool CanMove => _movePlanner != null;

        /// <summary>最後に決めた行きたい向き（頭の中の表示用）。</summary>
        public Vector2 MoveIntent { get; private set; }

        /// <summary>上手さを入れ替える（段6 から、慣れで上がるたびに呼ばれる）。</summary>
        public void SetSkill(in CpuSkillValues skill)
        {
            Context.Skill = skill;
            Hands.Apply(skill);
        }

        internal void Tick(LaserTurret turret, CpuSwarmSampler sampler, float deltaTime)
        {
            Context.Turret = turret;
            Context.Time += deltaTime;

            Eyes.Tick(Context, sampler);
            Brain.Tick(Context, deltaTime);
            Hands.Tick(deltaTime);
            TickMove(deltaTime);
            Record(turret, deltaTime);
        }

        // 本体が数える記録（撃った秒・空振り・オーバーヒート・熱の最大・行動ごとの秒数）。
        private void Record(LaserTurret turret, float deltaTime)
        {
            var stats = Context.Stats;
            stats.Tick(deltaTime);

            var current = Brain.Current;
            if (current != null)
            {
                stats.Add(CpuStats.BehaviorSecondsPrefix + current.Id, deltaTime);
            }

            var info = new CpuTurretInfo(turret);
            if (!info.Valid)
            {
                return;
            }

            stats.Max(CpuStats.MaxHeat, info.Heat01 * 100f);
            if (info.IsOverheated && !_wasOverheated)
            {
                stats.Add(CpuStats.Overheats, 1f);
            }

            _wasOverheated = info.IsOverheated;
            if (!info.IsFiring)
            {
                return;
            }

            stats.Add(CpuStats.FiringSeconds, deltaTime);

            // 空振り: 撃っているのに、今の向き（と次の向き）にほとんど敵がいない（いちばん新しく見た様子で数える）。
            var weights = Eyes.Latest.Weights;
            if (Eyes.HasSeen && CpuSectorMath.MaxAhead(weights, info.AimDegrees, info.RotationSign, 0f, 360f / weights.Length) < 0.5f)
            {
                stats.Add(CpuStats.WhiffSeconds, deltaTime);
            }
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
