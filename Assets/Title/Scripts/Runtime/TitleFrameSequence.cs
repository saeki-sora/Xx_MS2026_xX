using System;
using UnityEngine;

namespace MS2026.Title
{
    /// <summary>
    /// After Effects などで作った連番の絵（オープニングの歯磨き粉）。<see cref="TitleOpening"/> の「連番アニメ」に入れて使う。
    /// メニュー「Tools/タイトル/歯磨き粉の連番を取り込む」が作る（透明な周りを切り落とし、各コマの口の位置と、キャップが飛ぶコマを自動で求める）。
    /// 1秒の枚数とキャップが飛ぶコマは、このファイルの Inspector であとから変えられる（再生中に変えてもそのまま残る）。
    /// </summary>
    public sealed class TitleFrameSequence : ScriptableObject
    {
        [Tooltip("連番の絵（順番どおり）。")]
        public Sprite[] frames = Array.Empty<Sprite>();
        [Tooltip("1秒に何枚進めるか（AE の書き出しに合わせる。30 なら 120枚で4秒）。")]
        [Min(1f)] public float frameRate = 30f;
        [Tooltip("キャップが飛ぶ絵の番号（最初の絵が 0）。この絵になった瞬間に、口から歯磨き粉が噴き出す（泡・カメラの揺れ・効果音もここ）。")]
        [Min(0)] public int ejectFrame = 82;
        [Tooltip("各コマの口（チューブの上端）の位置。絵の中心からの距離（100px = 1）。取り込むときに自動で求める。噴き出す歯磨き粉の根元がここについていく。")]
        public Vector2[] mouths = Array.Empty<Vector2>();
        [Tooltip("書き出した画面の真ん中から見た、絵の中心の位置（100px = 1）。取り込むときに求める。「書き出したときと同じ位置・大きさ」で置くときに使う。")]
        public Vector2 screenOffset;

        public int Count => frames != null ? frames.Length : 0;
        public bool IsUsable => Count > 0;
        public float Duration => Count / Mathf.Max(1f, frameRate);
        public float EjectTime => Mathf.Clamp(ejectFrame, 0, Mathf.Max(0, Count - 1)) / Mathf.Max(1f, frameRate);

        /// <summary>経過秒数のコマ番号（最後のコマで止まる）。</summary>
        public int FrameAt(float seconds) => Mathf.Clamp(Mathf.FloorToInt(seconds * Mathf.Max(1f, frameRate)), 0, Mathf.Max(0, Count - 1));

        /// <summary>口の位置（絵の中心から。無ければ絵の上端の真ん中）。</summary>
        public Vector2 MouthAt(int frame)
        {
            if (mouths != null && frame >= 0 && frame < mouths.Length) return mouths[frame];
            var sprite = frames != null && frame >= 0 && frame < frames.Length ? frames[frame] : null;
            return sprite != null ? new Vector2(sprite.bounds.center.x, sprite.bounds.max.y) : Vector2.zero;
        }
    }
}
