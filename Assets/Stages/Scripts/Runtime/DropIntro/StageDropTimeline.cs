using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>落ちてくる物 1 つの、ある瞬間の姿（<see cref="StageDropTimeline.Evaluate"/> の結果）。</summary>
    public readonly struct StageDropPose
    {
        /// <summary>着地点からの高さ（1 = 落ち始めの位置、0 = 着地）。</summary>
        public readonly float Height01;

        /// <summary>落ちる向きへ伸びている量（0 = 伸びていない）。</summary>
        public readonly float Stretch;

        /// <summary>着地でつぶれている量（0 = つぶれていない）。</summary>
        public readonly float Squash;

        /// <summary>もう着地したか。</summary>
        public readonly bool Landed;

        public StageDropPose(float height01, float stretch, float squash, bool landed)
        {
            Height01 = height01;
            Stretch = stretch;
            Squash = squash;
            Landed = landed;
        }

        /// <summary>着地して元の形に戻った姿。</summary>
        public static StageDropPose Rest => new StageDropPose(0f, 0f, 0f, true);
    }

    /// <summary>
    /// 「上から落ちてくる」演出の時間の計算（Unity の物に触らない。テストできる）。
    /// 順番: 障害物（ばらばらの順）→ コア → プレイヤー（1P → 4P）。
    /// </summary>
    public static class StageDropTimeline
    {
        /// <summary>
        /// t 秒目（その物が落ち始めてからの秒数。マイナスはまだ落ちていない）の姿。
        /// 落ちる間は重力のようにだんだん速く（高さ = 1 − 進み²）、着地したらつぶれて弾むように戻る。
        /// </summary>
        public static StageDropPose Evaluate(float t, float fallSeconds, float fallStretch, float squash, float squashSeconds)
        {
            if (t < 0f)
            {
                return new StageDropPose(1f, 0f, 0f, false);
            }

            var fall = Mathf.Max(0.0001f, fallSeconds);
            if (t < fall)
            {
                var p = t / fall;
                return new StageDropPose(1f - p * p, fallStretch * p, 0f, false);
            }

            var u = (t - fall) / Mathf.Max(0.0001f, squashSeconds);
            if (u >= 1f)
            {
                return StageDropPose.Rest;
            }

            // 一気につぶれて、少しゆっくり戻る。
            var k = Mathf.Sin(Mathf.PI * u) * (1f - u * 0.5f);
            return new StageDropPose(0f, 0f, squash * k, true);
        }

        /// <summary>
        /// 障害物 count 個の、落ち始めの秒数。全部を spread 秒の中に収め、間隔は maxInterval 秒まで。
        /// </summary>
        public static float[] PropDelays(int count, float spread, float maxInterval)
        {
            var delays = new float[Mathf.Max(0, count)];
            if (count <= 1)
            {
                return delays;
            }

            var interval = Mathf.Min(Mathf.Max(0f, maxInterval), Mathf.Max(0f, spread) / (count - 1));
            for (var i = 0; i < count; i++)
            {
                delays[i] = i * interval;
            }

            return delays;
        }

        /// <summary>
        /// 0〜count−1 を、毎回同じばらばらの順に並べる（seed が同じなら同じ順。全PCで同じ見え方になる）。
        /// </summary>
        public static int[] ShuffledOrder(int count, int seed)
        {
            var order = new int[Mathf.Max(0, count)];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            var random = new System.Random(seed);
            for (var i = order.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }
    }
}
