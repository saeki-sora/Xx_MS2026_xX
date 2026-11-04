using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// カットインの形と動きの計算だけ（Unity の部品は触らない）。
    /// 窓は4つの角（左下・左上・右上・右下）で表し、「開いた形」「帯につぶれた形」「横一本の線」の間をなめらかに変える。
    /// </summary>
    public static class SkillCutInMath
    {
        /// <summary>カットインの今の様子。</summary>
        public struct Pose
        {
            /// <summary>窓の4つの角（左下・左上・右上・右下）。</summary>
            public Vector2 BottomLeft, TopLeft, TopRight, BottomRight;

            /// <summary>窓全体の濃さ（最後に消えるとき 1 → 0）。</summary>
            public float Alpha;

            /// <summary>窓のふちの稲妻の濃さ。</summary>
            public float EdgeAlpha;

            /// <summary>消えるときの横一本の稲妻の濃さ。</summary>
            public float LineAlpha;

            /// <summary>窓を白く光らせる量（0〜1）。</summary>
            public float Flash;

            /// <summary>絵の拡大率（1 = 元の大きさ）。</summary>
            public float Zoom;

            /// <summary>表情違いの絵に切り替わっているか。</summary>
            public bool Later;

            /// <summary>技名の濃さ。</summary>
            public float TextAlpha;

            /// <summary>技名の横のずれ（0 = 定位置、1 = 文字の高さ1つ分左）。</summary>
            public float TextSlide;

            /// <summary>窓全体の大きさ（<see cref="Shapes.Pivot"/> を中心に。1 = 元の大きさ。吸い込まれて消えるときに 1 → 0）。</summary>
            public float Scale;

            /// <summary>閉じ始めたか（閉じる音を鳴らす合図）。</summary>
            public bool Closing;

            /// <summary>閉じる動きの進み具合（閉じる前は 0、消え終わると 1）。</summary>
            public float CloseProgress;

            /// <summary>消え終わったか。</summary>
            public bool Done;
        }

        /// <summary>窓の形（開いた形・帯・線の3つ）。</summary>
        public struct Shapes
        {
            public Vector2[] Open;
            public Vector2[] Band;
            public Vector2[] Line;

            /// <summary>開くときに窓が伸び始める左の端の x。</summary>
            public float StartX;

            /// <summary>吸い込まれて消えるときの行き先（小さい窓では欄の丸いアイコンの中心）。</summary>
            public Vector2 Pivot;
        }

        /// <summary>
        /// 時刻 <paramref name="t"/>（秒）の様子。<paramref name="closeAt"/> が正なら、その時刻から（見せる時間の途中でも）閉じ始める
        /// （次のカットインに切り替わるときに使う）。
        /// <paramref name="shrinkOnClose"/> なら、閉じるときは帯・線にならず、開いた形のまま <see cref="Shapes.Pivot"/> へ縮んで消える。
        /// </summary>
        public static Pose Evaluate(SkillCutInMotion motion, Shapes shapes, float t, float closeAt, bool shrinkOnClose = false)
        {
            var open = Mathf.Max(0.01f, motion.openSeconds);
            var hold = Mathf.Max(0f, motion.holdSeconds);
            var close = Mathf.Max(0.01f, motion.closeSeconds);
            var closeStart = closeAt >= 0f ? Mathf.Min(closeAt, open + hold) : open + hold;

            var pose = new Pose { Alpha = 1f, Zoom = 1f, Scale = 1f };
            var corners = new Vector2[4];

            if (t < closeStart && t < open)
            {
                // 開く: 左の端から右へ、窓が一気に伸びる。
                var s = EaseOut(t / open);
                for (var i = 0; i < 4; i++)
                {
                    corners[i] = new Vector2(Mathf.Lerp(shapes.StartX, shapes.Open[i].x, s), shapes.Open[i].y);
                }

                pose.EdgeAlpha = 1f;
                pose.Flash = motion.flash;
            }
            else if (t < closeStart)
            {
                CopyTo(shapes.Open, corners);
                pose.EdgeAlpha = 1f;
                var sinceOpen = t - open;
                pose.Flash = motion.flash * Mathf.Clamp01(1f - sinceOpen / 0.14f);
            }
            else if (shrinkOnClose)
            {
                // 閉じる: 開いた形のまま、行き先（欄のアイコン）へ吸い込まれるように縮む。
                pose.Closing = true;
                var u = Mathf.Clamp01((t - closeStart) / close);
                pose.CloseProgress = u;
                CopyTo(shapes.Open, corners);
                pose.Scale = 1f - EaseIn(u);
                pose.Alpha = Mathf.Lerp(1f, 0.3f, u);
                pose.EdgeAlpha = Mathf.Clamp01(1f - u / 0.15f);
                pose.Done = u >= 1f;
            }
            else
            {
                // 閉じる: 開いた形 → 目の高さの帯 → 横一本の線 → 消える。
                pose.Closing = true;
                var u = Mathf.Clamp01((t - closeStart) / close);
                pose.CloseProgress = u;
                if (u < 0.45f)
                {
                    LerpShape(shapes.Open, shapes.Band, EaseIn(u / 0.45f), corners);
                }
                else if (u < 0.7f)
                {
                    LerpShape(shapes.Band, shapes.Line, EaseOut((u - 0.45f) / 0.25f), corners);
                }
                else
                {
                    CopyTo(shapes.Line, corners);
                }

                pose.EdgeAlpha = Mathf.Clamp01(1f - u / 0.15f);
                pose.LineAlpha = u < 0.45f ? 0f : u < 0.55f ? (u - 0.45f) / 0.1f : u < 0.75f ? 1f : Mathf.Clamp01(1f - (u - 0.75f) / 0.25f);
                pose.Alpha = u < 0.7f ? 1f : Mathf.Clamp01(1f - (u - 0.7f) / 0.15f);
                pose.Done = u >= 1f;
            }

            // 絵の寄り: 見せている間ゆっくり寄り、途中で一瞬グッと寄る（表情違いの絵もこの瞬間に切り替わる）。
            var holdT = hold > 0f ? Mathf.Clamp01((Mathf.Min(t, closeStart) - open) / hold) : 0f;
            var punchTime = open + hold * motion.punchAt;
            var punch = EaseOut(Mathf.Clamp01((t - punchTime) / 0.06f));
            pose.Zoom = Mathf.Lerp(1f, motion.slowZoom, holdT) + motion.punchZoom * punch;
            pose.Later = t >= punchTime;
            if (t >= punchTime && t < closeStart)
            {
                pose.Flash = Mathf.Max(pose.Flash, motion.flash * 0.4f * Mathf.Clamp01(1f - (t - punchTime) / 0.1f));
            }

            // 技名: 開いた直後に左からすべり込み、閉じ始めたらすぐ消える。
            var textIn = Mathf.Clamp01((t - open * 0.5f) / 0.18f);
            pose.TextSlide = 1f - EaseOut(textIn);
            pose.TextAlpha = pose.Closing ? Mathf.Clamp01(1f - (t - closeStart) / (close * 0.3f)) * textIn : textIn;

            pose.BottomLeft = corners[0];
            pose.TopLeft = corners[1];
            pose.TopRight = corners[2];
            pose.BottomRight = corners[3];
            return pose;
        }

        /// <summary>
        /// 大きい窓（画面の左）の形。<paramref name="screen"/> は画面の大きさ、座標は左下が (0, 0)。
        /// 帯と線は画面の横いっぱいで、<paramref name="bandCenterY"/>（目の高さ）を通る。
        /// </summary>
        public static Shapes BigShapes(SkillCutInBigWindow big, Vector2 screen, float bandCenterY)
        {
            var w = screen.x;
            var h = screen.y;
            var bh = big.bandHeight * h * 0.5f;
            var tilt = big.bandTilt * h;
            var lh = Mathf.Max(1f, h * 0.004f);
            return new Shapes
            {
                Open = new[]
                {
                    new Vector2(0f, 0f), new Vector2(0f, h), new Vector2(big.edgeTop * w, h), new Vector2(big.edgeBottom * w, 0f)
                },
                Band = new[]
                {
                    new Vector2(0f, bandCenterY - bh), new Vector2(0f, bandCenterY + bh),
                    new Vector2(w, bandCenterY + bh + tilt), new Vector2(w, bandCenterY - bh + tilt)
                },
                Line = new[]
                {
                    new Vector2(0f, bandCenterY - lh), new Vector2(0f, bandCenterY + lh),
                    new Vector2(w, bandCenterY + lh + tilt), new Vector2(w, bandCenterY - lh + tilt)
                },
                StartX = 0f
            };
        }

        /// <summary>
        /// 小さい窓の形。<paramref name="window"/> は窓の外枠（右のふちは上の端が <paramref name="slant"/> だけ左に寄る）。
        /// <paramref name="pivot"/> は吸い込まれて消えるときの行き先（欄の丸いアイコンの中心）。
        /// </summary>
        public static Shapes SmallShapes(Rect window, float slant, Vector2 pivot)
        {
            var cy = window.center.y;
            var bh = window.height * 0.12f;
            var lh = 1.5f;
            var tilt = window.height * 0.15f;
            return new Shapes
            {
                Open = new[]
                {
                    new Vector2(window.xMin, window.yMin), new Vector2(window.xMin, window.yMax),
                    new Vector2(window.xMax - slant, window.yMax), new Vector2(window.xMax, window.yMin)
                },
                Band = new[]
                {
                    new Vector2(window.xMin, cy - bh), new Vector2(window.xMin, cy + bh),
                    new Vector2(window.xMax, cy + bh + tilt), new Vector2(window.xMax, cy - bh + tilt)
                },
                Line = new[]
                {
                    new Vector2(window.xMin, cy - lh), new Vector2(window.xMin, cy + lh),
                    new Vector2(window.xMax, cy + lh + tilt), new Vector2(window.xMax, cy - lh + tilt)
                },
                StartX = window.xMin,
                Pivot = pivot
            };
        }

        /// <summary>
        /// 欄のアイコンの上に出す顔の丸の、大きさの倍率と濃さ（<paramref name="t"/> は窓が出てからの秒数、<paramref name="pose"/> は窓の今の様子）。
        /// 出た瞬間にポンと大きく弾んでから落ち着き、窓が吸い込まれ終わったあと <paramref name="tail"/> 秒かけて消える。
        /// </summary>
        public static void IconFace(float t, Pose pose, float sinceDone, float tail, out float scale, out float alpha)
        {
            const float pop = 0.08f, settle = 0.11f;
            if (t < pop)
            {
                var s = EaseOut(t / pop);
                scale = Mathf.Lerp(1f, 1.25f, s);
                alpha = s;
            }
            else
            {
                scale = Mathf.Lerp(1.25f, 1f, EaseOut((t - pop) / settle));
                alpha = 1f;
            }

            // 窓が吸い込まれた瞬間に少し膨らみ、そのあと消える。
            if (pose.Done)
            {
                var u = tail > 0f ? Mathf.Clamp01(sinceDone / tail) : 1f;
                scale *= 1f + 0.15f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 2f));
                alpha *= 1f - EaseIn(u);
            }
        }

        /// <summary>
        /// 絵を置く範囲。絵の中の <paramref name="face"/>（左上0・右下1）が <paramref name="anchor"/> に来るようにし、
        /// <paramref name="cover"/>（窓の外枠）を必ず覆うように位置と大きさを直す（すき間から絵の外が見えないように）。
        /// </summary>
        public static Rect PictureRect(Vector2 pictureSize, Vector2 face, Vector2 anchor, float height, Rect cover)
        {
            var aspect = pictureSize.y > 0f ? pictureSize.x / pictureSize.y : 1f;
            height = Mathf.Max(height, cover.height, cover.width / Mathf.Max(0.01f, aspect));
            var width = height * aspect;
            var x = anchor.x - face.x * width;
            var y = anchor.y - (1f - face.y) * height;
            x = Mathf.Clamp(x, cover.xMax - width, cover.xMin);
            y = Mathf.Clamp(y, cover.yMax - height, cover.yMin);
            return new Rect(x, y, width, height);
        }

        /// <summary>範囲を <paramref name="pivot"/> を中心に <paramref name="scale"/> 倍する。</summary>
        public static Rect ScaleAround(Rect rect, Vector2 pivot, float scale)
        {
            var min = pivot + (rect.min - pivot) * scale;
            return new Rect(min, rect.size * scale);
        }

        /// <summary>4つの角を囲む長方形。</summary>
        public static Rect Bounds(Vector2[] corners)
        {
            var min = corners[0];
            var max = corners[0];
            for (var i = 1; i < corners.Length; i++)
            {
                min = Vector2.Min(min, corners[i]);
                max = Vector2.Max(max, corners[i]);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// 稲妻の折れ線（端は a・b、真ん中ほど大きく横に揺れる）。
        /// 乱数は見た目専用の <paramref name="random"/> を使う（ゲームの UnityEngine.Random の並びを変えないため）。
        /// </summary>
        public static void BoltPoints(Vector2 a, Vector2 b, int segments, float jag, System.Random random, List<Vector2> result)
        {
            result.Clear();
            segments = Mathf.Max(1, segments);
            var dir = b - a;
            var len = dir.magnitude;
            var normal = len > 1e-4f ? new Vector2(-dir.y, dir.x) / len : Vector2.zero;
            result.Add(a);
            for (var i = 1; i < segments; i++)
            {
                var t = (float)i / segments;
                var offset = ((float)random.NextDouble() * 2f - 1f) * jag * Mathf.Sin(Mathf.PI * t)
                             + ((float)random.NextDouble() * 2f - 1f) * jag * 0.35f;
                result.Add(a + dir * t + normal * offset);
            }

            result.Add(b);
        }

        /// <summary>折れ線の両端を a・b に付け替える（途中の点は、元の線に対する位置関係を保つ）。</summary>
        public static void RefitPoints(List<Vector2> points, Vector2 a, Vector2 b)
        {
            if (points.Count < 2)
            {
                return;
            }

            var oldA = points[0];
            var oldB = points[points.Count - 1];
            var oldDir = oldB - oldA;
            var oldLen2 = oldDir.sqrMagnitude;
            if (oldLen2 < 1e-6f)
            {
                return;
            }

            var oldNormal = new Vector2(-oldDir.y, oldDir.x) / Mathf.Sqrt(oldLen2);
            var newDir = b - a;
            var newLen = newDir.magnitude;
            var newNormal = newLen > 1e-4f ? new Vector2(-newDir.y, newDir.x) / newLen : Vector2.zero;
            for (var i = 0; i < points.Count; i++)
            {
                var d = points[i] - oldA;
                var along = Vector2.Dot(d, oldDir) / oldLen2;
                var side = Vector2.Dot(d, oldNormal);
                points[i] = a + newDir * along + newNormal * side;
            }
        }

        public static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x) * (1f - x);
        }

        public static float EaseIn(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x;
        }

        private static void LerpShape(Vector2[] from, Vector2[] to, float s, Vector2[] result)
        {
            for (var i = 0; i < 4; i++)
            {
                result[i] = Vector2.Lerp(from[i], to[i], s);
            }
        }

        private static void CopyTo(Vector2[] from, Vector2[] result)
        {
            for (var i = 0; i < 4; i++)
            {
                result[i] = from[i];
            }
        }
    }
}
