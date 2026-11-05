using UnityEngine;

namespace MS2026.Title
{
    /// <summary>
    /// 連番の絵（<see cref="TitleFrameSequence"/>）を、同じオブジェクトの SpriteRenderer でくり返し再生する（タイトルの背景のループなど）。
    /// 大きさは同じオブジェクトの <see cref="TitleSpriteFit"/> が合わせる（全部の絵が同じ大きさなら、見た目の大きさは変わらない）。
    /// 動くのは再生中だけ。編集中のシーンには今の絵（ふつうは1枚目）がそのまま映る。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TitleSpriteLoop : MonoBehaviour
    {
        [Tooltip("くり返す連番の絵。1秒の枚数は、このファイルの「Frame Rate」。")]
        public TitleFrameSequence sequence;
        [Tooltip("再生の速さの倍率（1 = 連番の1秒の枚数のまま、0.5 = 半分の速さ）。")]
        [Min(0f)] public float speed = 1f;

        private SpriteRenderer spriteRenderer;
        private float time;
        private int shownFrame = -1;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            shownFrame = -1;
            Show();
        }

        private void Update()
        {
            time += Time.deltaTime * speed;
            Show();
        }

        private void Show()
        {
            if (sequence == null || !sequence.IsUsable || spriteRenderer == null) return;
            var duration = sequence.Duration;
            if (duration <= 0f) return;
            time = Mathf.Repeat(time, duration);
            var frame = sequence.FrameAt(time);
            if (frame == shownFrame) return;
            shownFrame = frame;
            spriteRenderer.sprite = sequence.frames[frame];
        }
    }
}
