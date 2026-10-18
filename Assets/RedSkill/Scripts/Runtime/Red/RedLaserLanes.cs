using UnityEngine;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// 極太レーザーを横に何本かの「筋」に分けたときの、筋ごとの止まり方（<see cref="RedLaserSkillRunner"/> が毎フレーム作り、
    /// <see cref="RedLaserVisuals"/> が見た目に使う）。筋は見た目の太さ全体に等間隔で並ぶ（<see cref="Fraction"/> = -1 左ふち 〜 +1 右ふち）。
    /// 壁に当たった筋だけがそこで止まり、当たっていない筋は先まで伸びる（太いレーザーの一部だけが壁にぶつかる表現）。
    /// </summary>
    internal sealed class RedLaserLanes
    {
        /// <summary>筋の境目の数（筋の数 + 1）。</summary>
        public int Count { get; private set; }

        /// <summary>横の位置（-1 = 左ふち、0 = 真ん中、+1 = 右ふち。見た目の太さに対する割合）。</summary>
        public float[] Fraction = new float[0];

        /// <summary>今フレームの長さ（伸びている途中なら伸びた所まで、壁に当たっていれば壁まで）。</summary>
        public float[] Length = new float[0];

        /// <summary>止める物までの距離（何も無ければ届く距離）。</summary>
        public float[] HitDistance = new float[0];

        /// <summary>今フレーム、壁・障害物に当たって止まっているか。</summary>
        public bool[] Blocked = new bool[0];

        /// <summary>当たった場所（<see cref="Blocked"/> のときだけ意味がある）。</summary>
        public Vector2[] HitPoint = new Vector2[0];

        /// <summary>当たった面の向き（砲台側を向く）。</summary>
        public Vector2[] HitNormal = new Vector2[0];

        /// <summary>
        /// 見た目用の「帯」（となりの筋の境目どうしの間）。止まる距離が大きく違う境目の間は、障害物のかどの位置を探して2つの帯に分ける
        /// （分けないと、壁で止まった筋と止まっていない筋の間に、壁の向こうへ斜めに伸びるくさび形ができてしまう）。
        /// 帯ごとに 左の横の位置・右の横の位置・左の長さ・右の長さ・左/右が壁で止まっているか。
        /// </summary>
        public int StripCount { get; private set; }

        public float[] StripLeft = new float[0];
        public float[] StripRight = new float[0];
        public float[] StripLengthLeft = new float[0];
        public float[] StripLengthRight = new float[0];
        public bool[] StripBlockedLeft = new bool[0];
        public bool[] StripBlockedRight = new bool[0];

        /// <summary>帯の数の上限（筋の数 × 2）。</summary>
        public int StripCapacity => StripLeft.Length;

        public void ClearStrips()
        {
            StripCount = 0;
        }

        public void AddStrip(float left, float right, float lengthLeft, float lengthRight, bool blockedLeft, bool blockedRight)
        {
            if (StripCount >= StripCapacity)
            {
                return;
            }

            var i = StripCount++;
            StripLeft[i] = left;
            StripRight[i] = right;
            StripLengthLeft[i] = lengthLeft;
            StripLengthRight[i] = lengthRight;
            StripBlockedLeft[i] = blockedLeft;
            StripBlockedRight[i] = blockedRight;
        }

        /// <summary>どれかの筋が当たって止まっているか。</summary>
        public bool AnyBlocked { get; set; }

        /// <summary>一番長い筋の長さ。</summary>
        public float MaxLength { get; set; }

        public void Resize(int count)
        {
            count = Mathf.Max(2, count);
            if (count == Count)
            {
                return;
            }

            Count = count;
            Fraction = new float[count];
            Length = new float[count];
            HitDistance = new float[count];
            Blocked = new bool[count];
            HitPoint = new Vector2[count];
            HitNormal = new Vector2[count];
            var strips = (count - 1) * 2;
            StripLeft = new float[strips];
            StripRight = new float[strips];
            StripLengthLeft = new float[strips];
            StripLengthRight = new float[strips];
            StripBlockedLeft = new bool[strips];
            StripBlockedRight = new bool[strips];
            StripCount = 0;
            for (var i = 0; i < count; i++)
            {
                Fraction[i] = -1f + 2f * i / (count - 1);
            }
        }

        /// <summary>撃ち始め・撃ち終わりに、全部の筋の長さを 0 に戻す。</summary>
        public void ResetLengths()
        {
            for (var i = 0; i < Count; i++)
            {
                Length[i] = 0f;
                Blocked[i] = false;
            }

            AnyBlocked = false;
            MaxLength = 0f;
            StripCount = 0;
        }

        /// <summary>横の位置 fraction（-1〜+1）での長さ（となりの筋の間はなめらかにつなぐ）。</summary>
        public float LengthAt(float fraction)
        {
            if (Count < 2)
            {
                return 0f;
            }

            var position = (Mathf.Clamp(fraction, -1f, 1f) + 1f) * 0.5f * (Count - 1);
            var index = Mathf.Min(Mathf.FloorToInt(position), Count - 2);
            return Mathf.Lerp(Length[index], Length[index + 1], position - index);
        }
    }
}
