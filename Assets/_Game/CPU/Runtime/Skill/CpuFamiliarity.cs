using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 本体の上手さの分野（差し込む側の分野は、差し込む側が <see cref="CpuPlugins.RegisterSkillArea"/> で足す）。
    /// </summary>
    public static class CpuSkillAreas
    {
        public const string Reaction = "reaction";
        public const string Aim = "aim";
        public const string Heat = "heat";
        public const string Skill = "skill";
        public const string Awareness = "awareness";
        public const string Brushing = "brushing";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            CpuPlugins.RegisterSkillArea(Reaction, "反応");
            CpuPlugins.RegisterSkillArea(Aim, "狙い");
            CpuPlugins.RegisterSkillArea(Heat, "熱の読み");
            CpuPlugins.RegisterSkillArea(Skill, "スキルの判断");
            CpuPlugins.RegisterSkillArea(Awareness, "周りを見る");
            CpuPlugins.RegisterSkillArea(Brushing, "歯みがき");
        }
    }

    /// <summary>
    /// CPU 1人の、分野ごとの慣れ（0 = 試合の始め＝チュートリアルを終えたばかり、1 = その強さの上限）。
    /// 試合ごとに作り直すので、学んだことは試合をまたがない。上げるのは学び（段7）。
    /// </summary>
    public sealed class CpuFamiliarity
    {
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>();

        public CpuFamiliarity(float start = 0f)
        {
            Start = Mathf.Clamp01(start);
        }

        /// <summary>まだ一度も上げていない分野の慣れ。</summary>
        public float Start { get; }

        /// <summary>変わるたびに増える番号（上手さを作り直すかの見分けに使う）。</summary>
        public int Version { get; private set; }

        /// <summary>変わったとき（分野の Id）。</summary>
        public event Action<string> Changed;

        public float Get(string area) => area != null && _values.TryGetValue(area, out var value) ? value : Start;

        public void Set(string area, float value01)
        {
            if (string.IsNullOrEmpty(area))
            {
                return;
            }

            value01 = Mathf.Clamp01(value01);
            if (Mathf.Approximately(Get(area), value01) && _values.ContainsKey(area))
            {
                return;
            }

            _values[area] = value01;
            Version++;
            Changed?.Invoke(area);
        }

        public void Add(string area, float amount) => Set(area, Get(area) + amount);

        /// <summary>今までに値を入れた分野。</summary>
        public IEnumerable<string> Areas => _values.Keys;
    }
}
