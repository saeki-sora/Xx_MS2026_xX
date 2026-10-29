using System;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>「握って飛距離を決める」のやり方。</summary>
    public enum BlueGripDistanceMode
    {
        /// <summary>使わない。握った瞬間に振りかぶって、「落ちる場所」の距離に投げる（元の動き）。</summary>
        [InspectorName("使わない（いつも同じ距離）")]
        Off,

        /// <summary>握っている間、距離が最小から伸びていき、最大で止まる。離した瞬間に投げる。</summary>
        [InspectorName("握るほど遠く（最大で止まる）")]
        StopAtMax,

        /// <summary>握っている間、距離が最小と最大の間を行ったり来たりする。離した瞬間に投げる。</summary>
        [InspectorName("握るほど遠く（最大と最小を往復）")]
        PingPong
    }

    /// <summary>
    /// 握っている長さで投げる距離を変える設定（<see cref="BlueVortexSkill.gripDistance"/>）。
    /// 使うときは、スキルを使える状態で握ると振りかぶったまま構え、握っている間に落ちる場所が最小の距離から伸びていく。
    /// 離した瞬間（またはオーバーヒートしたとき・「自動で投げる」の時間がたったとき）に、その距離へ投げる。
    /// このときは「タイミング」の「振りかぶる時間」は使わない（握っている間が振りかぶり）。
    /// </summary>
    [Serializable]
    public sealed class BlueGripDistance
    {
        [Tooltip("使わない（いつも「落ちる場所」の距離）／握るほど遠く（最大で止まる）／握るほど遠く（最大と最小を往復）。")]
        [SkillLabel("やり方")]
        public BlueGripDistanceMode mode = BlueGripDistanceMode.StopAtMax;

        [Tooltip("握ってすぐ離したときの距離（m）。測り方・左右のずれ・壁で止めるかは「落ちる場所」の設定を使う。")]
        [SkillLabel("いちばん近い距離（m）")]
        [Range(0f, 30f)]
        public float minDistance = 3f;

        [Tooltip("いちばん遠い距離（m）。")]
        [SkillLabel("いちばん遠い距離（m）")]
        [Range(0f, 30f)]
        public float maxDistance = 12f;

        [Tooltip("いちばん近い距離からいちばん遠い距離まで伸びるのにかかる時間（秒）。往復のときは、片道の時間。")]
        [SkillLabel("伸びきるまでの時間（秒）")]
        [Range(0.1f, 5f)]
        public float secondsToMax = 1f;

        [Tooltip("距離の伸び方（横=握った長さ 0〜1、縦=近い→遠いの割合 0〜1）。まっすぐなら一定の速さで伸びる。")]
        [SkillLabel("伸び方のカーブ")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("強く握るほど速く伸びるようにする強さ（0 = 握る強さは関係なし、1 = 弱く握ると 0.3 倍・強く握ると 2 倍の速さ）。")]
        [SkillLabel("強く握るほど速く伸びる")]
        [Range(0f, 1f)]
        public float gripSpeedInfluence = 0f;

        [Tooltip("握ったままこの時間（秒）がたつと、離さなくても投げる（0 なら、離すまで投げない）。")]
        [SkillLabel("握ったまま自動で投げる（秒）")]
        [Range(0f, 10f)]
        public float autoThrowSeconds = 0f;

        [Header("離したときの遅れを減らす")]
        [Tooltip("ON なら、握りをゆるめ始めた瞬間に距離を止める（握力は離してもすぐ 0 にならず、完全に離したと分かるまで少しかかるため。往復で特に効く）。握り直すとまた動く。")]
        [SkillLabel("ゆるめ始めたら距離を止める")]
        public bool freezeWhileLoosening = true;

        [Tooltip("さっきまでの強さからこれだけ握力が下がったら、完全に離すのを待たずに投げる（0〜1。0 なら完全に離すまで待つ）。小さいほど早く投げるが、握ったまま少しゆるめただけでも投げやすくなる。")]
        [SkillLabel("ゆるめたら投げる（下がり幅）")]
        [Range(0f, 1f)]
        public float releaseDrop = 0.2f;

        /// <summary>「さっきまでの強さ」からこれだけ下がったら、ゆるめ始めたと見なす（握力のふらつきで止まらないように少し余裕をとる）。</summary>
        public const float LoosenMargin = 0.05f;

        /// <summary>「さっきまでの強さ」が下がっていく速さ（1秒あたり）。強く握ってから少し弱めて握り続けたときに、また距離が動き出すように。</summary>
        public const float ReferenceDecayPerSecond = 0.5f;

        /// <summary>握って飛距離を決めるか。</summary>
        public bool Enabled => mode != BlueGripDistanceMode.Off;

        /// <summary>
        /// 握っている間に1フレームで進める量（最小→最大が 1）。grip01 = 握る強さ 0〜1。
        /// </summary>
        public float Advance(float grip01, float dt)
        {
            var strength = Mathf.Lerp(0.3f, 2f, Mathf.Clamp01(grip01));
            var speed = Mathf.Lerp(1f, strength, Mathf.Clamp01(gripSpeedInfluence));
            return speed * dt / Mathf.Max(0.05f, secondsToMax);
        }

        /// <summary>
        /// 進めた量を、扱いやすい範囲に収める（最大で止まる: 0〜1、往復: 0〜2 でくり返す）。
        /// </summary>
        public float Wrap(float progress)
        {
            return mode == BlueGripDistanceMode.PingPong ? Mathf.Repeat(progress, 2f) : Mathf.Clamp01(progress);
        }

        /// <summary>進めた量から、近い→遠いの割合（0〜1。カーブをかける前）を出す。</summary>
        public float Ratio01(float progress)
        {
            return mode == BlueGripDistanceMode.PingPong ? Mathf.PingPong(progress, 1f) : Mathf.Clamp01(progress);
        }

        /// <summary>近い→遠いの割合（0〜1）から、落ちる距離（m）を出す。</summary>
        public float Distance(float ratio01)
        {
            var t = Mathf.Clamp01(ratio01);
            if (curve != null && curve.length > 0)
            {
                t = Mathf.Clamp01(curve.Evaluate(t));
            }

            return Mathf.Lerp(Mathf.Max(0f, minDistance), Mathf.Max(0f, maxDistance), t);
        }
    }
}
