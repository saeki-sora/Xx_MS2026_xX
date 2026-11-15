using System;
using System.Collections.Generic;
using MS2026.Skills;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>上手さの分野1つ（反応・狙い・熱の読み・動き など）。</summary>
    public readonly struct CpuSkillArea
    {
        /// <summary>見分けるための名前（英数字）。</summary>
        public readonly string Id;

        /// <summary>画面に出す名前（日本語）。</summary>
        public readonly string Label;

        public CpuSkillArea(string id, string label)
        {
            Id = id;
            Label = string.IsNullOrEmpty(label) ? id : label;
        }
    }

    /// <summary>
    /// CPU の差し込み口をまとめた所。行動・行き先・移動の出口・見える範囲・仲間の調子・上手さの分野を、ここへ登録する。
    /// ・本体（このアセンブリ）は「登録された物があれば使う」だけ。差し込む側が消えても、本体は直さずに動く（何も無い所は、しない・動かない・ふつう、になる）。
    /// ・差し込む側は <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]</c> で自分を登録する
    ///   （ここの中身は SubsystemRegistration で空になり、CPU は AfterSceneLoad から動き出すので、その間に登録する）。
    /// ・同じ Id で登録し直すと置き換わる。
    /// </summary>
    public static class CpuPlugins
    {
        private sealed class BehaviorEntry
        {
            public string Id;
            public Func<ICpuBehavior> Create;
        }

        private static readonly List<BehaviorEntry> Behaviors = new List<BehaviorEntry>();
        private static readonly List<CpuSkillArea> Areas = new List<CpuSkillArea>();
        private static readonly List<ICpuSkillJudge> SkillJudges = new List<ICpuSkillJudge>();
        private static readonly Dictionary<string, Func<CpuWaveStats, float>> AreaReviews = new Dictionary<string, Func<CpuWaveStats, float>>();
        private static Func<ICpuMovePlanner> _movePlanner;

        /// <summary>行き先を決めた向きを砲台の移動へ渡す口（無ければ動かない）。</summary>
        public static ICpuMoveOutput MoveOutput { get; set; }

        /// <summary>画面に映る範囲を知らせる口（無ければ席のカメラの決まった視点）。</summary>
        public static ICpuViewSource ViewSource { get; set; }

        /// <summary>仲間の調子を知らせる口（無ければ全員ふつう）。</summary>
        public static ICpuTeammateStatus TeammateStatus { get; set; }

        /// <summary>登録が変わったとき（CPU は次に作られる人から新しい登録を使う）。</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Behaviors.Clear();
            Areas.Clear();
            SkillJudges.Clear();
            AreaReviews.Clear();
            _movePlanner = null;
            MoveOutput = null;
            ViewSource = null;
            TeammateStatus = null;
            Changed = null;
        }

        // ───────── 行動 ─────────

        /// <summary>行動の作り方を登録する（CPU 1人ごとに create が呼ばれる）。</summary>
        public static void RegisterBehavior(string id, Func<ICpuBehavior> create)
        {
            if (string.IsNullOrEmpty(id) || create == null)
            {
                return;
            }

            var index = Behaviors.FindIndex(e => e.Id == id);
            var entry = new BehaviorEntry { Id = id, Create = create };
            if (index >= 0)
            {
                Behaviors[index] = entry;
            }
            else
            {
                Behaviors.Add(entry);
            }

            Changed?.Invoke();
        }

        public static void UnregisterBehavior(string id)
        {
            if (Behaviors.RemoveAll(e => e.Id == id) > 0)
            {
                Changed?.Invoke();
            }
        }

        public static int BehaviorCount => Behaviors.Count;

        /// <summary>登録されている行動を、CPU 1人ぶん新しく作る（登録した順）。</summary>
        public static List<ICpuBehavior> CreateBehaviors()
        {
            var list = new List<ICpuBehavior>(Behaviors.Count);
            foreach (var entry in Behaviors)
            {
                try
                {
                    var behavior = entry.Create();
                    if (behavior != null)
                    {
                        list.Add(behavior);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return list;
        }

        // ───────── 行き先 ─────────

        /// <summary>行き先を決める係の作り方を登録する（null で外す）。</summary>
        public static void SetMovePlanner(Func<ICpuMovePlanner> create)
        {
            _movePlanner = create;
            Changed?.Invoke();
        }

        public static bool HasMovePlanner => _movePlanner != null;

        public static ICpuMovePlanner CreateMovePlanner()
        {
            try
            {
                return _movePlanner?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        // ───────── スキルの判断 ─────────

        /// <summary>スキル（色）の判断を登録する（あとから登録した物が先に選ばれる）。</summary>
        public static void RegisterSkillJudge(ICpuSkillJudge judge)
        {
            if (judge == null || SkillJudges.Contains(judge))
            {
                return;
            }

            SkillJudges.Insert(0, judge);
            Changed?.Invoke();
        }

        /// <summary>そのスキルに合う判断（無ければ、砲身の向きに敵が多い時に撃つ「ふつうの判断」）。</summary>
        public static ICpuSkillJudge FindSkillJudge(CharacterSkill skill)
        {
            if (skill != null)
            {
                foreach (var judge in SkillJudges)
                {
                    if (judge.Handles(skill))
                    {
                        return judge;
                    }
                }
            }

            return CpuGenericSkillJudge.Instance;
        }

        // ───────── 上手さの分野 ─────────

        /// <summary>上手さの分野を登録する（同じ Id なら名前を置き換える）。</summary>
        public static void RegisterSkillArea(string id, string label)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            var area = new CpuSkillArea(id, label);
            var index = Areas.FindIndex(a => a.Id == id);
            if (index >= 0)
            {
                Areas[index] = area;
            }
            else
            {
                Areas.Add(area);
            }

            Changed?.Invoke();
        }

        public static IReadOnlyList<CpuSkillArea> SkillAreas => Areas;

        // ───────── 振り返り（学び） ─────────

        /// <summary>
        /// 分野の振り返りを登録する。ウェーブが終わると、その CPU のそのウェーブの記録から「苦手さ」（0 = うまくいった … 1 = 失敗が多い）を出す。
        /// 苦手な分野ほど大きく慣れが上がる。振り返りが無い分野は、ふつう（0.5）として少しずつ上がる。
        /// </summary>
        public static void RegisterAreaReview(string area, Func<CpuWaveStats, float> need)
        {
            if (string.IsNullOrEmpty(area) || need == null)
            {
                return;
            }

            AreaReviews[area] = need;
            Changed?.Invoke();
        }

        /// <summary>そのウェーブの、その分野の苦手さ（振り返りが無い・失敗したらふつう 0.5）。</summary>
        public static float ReviewArea(string area, CpuWaveStats wave)
        {
            if (area == null || wave == null || !AreaReviews.TryGetValue(area, out var review))
            {
                return CpuLearningMath.NeutralNeed;
            }

            try
            {
                var need = review(wave);
                return float.IsNaN(need) ? CpuLearningMath.NeutralNeed : Mathf.Clamp01(need);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return CpuLearningMath.NeutralNeed;
            }
        }
    }
}
