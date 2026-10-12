using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラを切り替えるときの演出「歯磨き粉で塗り替えて切り替え」。区画の立ち絵の枠に付ける。
    /// ・歯磨き粉の線が、右に傾いたジグザグ（ユーザーの絵 2026-10-12: 左の真ん中から右上へ書き始め、「／」を往復しながら右へ進み、最後は右上へ抜ける）で、キャラの上を塗って前のキャラを覆う
    /// ・歯磨き粉は道すじのとおりに、キャラの外まで引く（ユーザー 2026-10-12「これどおりに引いてほしい」）。区画の外には出ない（組み立てで区画の大きさの RectMask2D に入れる）
    /// ・<see cref="clipToCharacter"/> をオンにすると、前の版のようにキャラの形の中にだけ見える（<see cref="pasteMask"/> = キャラの絵を型にした Mask）
    /// ・塗り終わったら新しいキャラに替わり、歯磨き粉は泡になって消える（または同じ道をたどって拭き取られる）
    /// ・消えきると、キラーンと光る
    /// </summary>
    [AddComponentMenu("キャラ選択/演出/歯磨き粉で塗り替えて切り替え (CharacterSwapFx)")]
    public sealed class CharacterSwapFx : MonoBehaviour
    {
        public enum RevealStyle
        {
            [InspectorName("泡になって消える")]
            FoamAway,

            [InspectorName("同じ道をたどって拭き取る")]
            WipeAlongPath
        }

        [Header("立ち絵")]
        [Tooltip("新しい立ち絵（区画の立ち絵）。塗り終わるまでは見えない。")]
        public Image current;

        [Tooltip("前の立ち絵（塗り終わるまで見えている方。ふだんは隠れている）。")]
        public Image previous;

        [Header("ジグザグに塗る")]
        [Tooltip("歯磨き粉の線（下の「型」の中に、型と同じ大きさで置く）。")]
        public PasteStroke stroke;

        [Tooltip("歯磨き粉を切り抜く型（Mask を付けた Image。キャラの絵が入る）。「キャラの上だけに塗る」がオンのときだけ使う。")]
        public Image pasteMask;

        [Tooltip("オン: 歯磨き粉はキャラの形の中にだけ見える。オフ（ふつう）: 道すじのとおりにキャラの外まで引く。")]
        public bool clipToCharacter;

        [Tooltip("オン: 線を自動で太くして、すき間なく塗りつぶす（前のキャラが完全に隠れる）。オフ（ふつう）: 線の太さ（Stroke の Width）のまま、ジグザグの線として見せる。")]
        public bool fillGaps;

        [Tooltip("ジグザグの傾き（度）。上の折り返しが下の折り返しより右にずれ、「／」の向きに傾く。0 で縦に往復。マイナスで「＼」の向き。")]
        [Range(-60f, 60f)]
        public float lean = 35f;

        [Tooltip("塗る線の本数（上り・下りを1本ずつ数える。最初と最後は真ん中の高さから・までの半分の線）。最後が右上へ抜けるよう、偶数なら1本増やす。")]
        [Range(3, 21)]
        public int legs = 11;

        [Tooltip("ジグザグ全体の大きさ（立ち絵の枠に合わせた大きさを 1 として。小さいほどキャラのまわりにまとまる）。")]
        [Range(0.3f, 1.5f)]
        public float size = 0.55f;

        [Tooltip("枠の外まで塗る量（ピクセル。上下・左右）。")]
        public Vector2 overshoot = new Vector2(20f, 40f);

        [Tooltip("毎回少しずつ形を変える量（ピクセル）。手で塗ったように見せる。")]
        [Min(0f)]
        public float jitter = 8f;

        [Tooltip("塗り終わるまでの秒数。")]
        [Min(0.05f)]
        public float paintSeconds = 0.45f;

        [Tooltip("塗り終わってから消え始めるまでの秒数（この間にキャラが替わる）。")]
        [Min(0f)]
        public float holdSeconds = 0.06f;

        [Header("消え方")]
        [Tooltip("塗った歯磨き粉の消え方。")]
        public RevealStyle revealStyle = RevealStyle.FoamAway;

        [Tooltip("消えるまでの秒数。")]
        [Min(0.05f)]
        public float revealSeconds = 0.3f;

        [Header("泡とキラーン")]
        [Tooltip("泡を立てる部品（枠の真ん中に置く。空でもよい）。")]
        public CharacterSelectBurst foam;

        [Tooltip("泡を立てる間隔（秒）。")]
        [Min(0.01f)]
        public float foamInterval = 0.025f;

        [Tooltip("消えきったときにキラーンと光る星（いくつでも。順番に少しずつ遅れて光る）。")]
        public RectTransform[] shines = System.Array.Empty<RectTransform>();

        [Tooltip("キラーンの秒数。")]
        [Min(0.05f)]
        public float shineSeconds = 0.5f;

        [Tooltip("キラーンの星の大きさ（ピクセル）。")]
        public float shineSize = 70f;

        private enum Stage
        {
            Idle,
            Painting,
            Holding,
            Revealing
        }

        private readonly List<Vector2> _path = new List<Vector2>();
        private Stage _stage;
        private float _t;
        private float _shine = 1f;
        private float _foamTimer;
        private Color _strokeColor = Color.white;

        /// <summary>塗り替えている途中か。</summary>
        public bool IsPlaying => _stage != Stage.Idle;

        private void Awake()
        {
            if (stroke != null)
            {
                _strokeColor = stroke.color;
            }

            Finish();
            foreach (var shine in shines)
            {
                if (shine != null)
                {
                    shine.sizeDelta = Vector2.zero;
                }
            }
        }

        /// <summary>前の立ち絵（絵・色・大きさ・位置）を残して、歯磨き粉で塗り替える。新しい立ち絵は先に current に入れておく。</summary>
        public void Play(Sprite oldSprite, Color oldColor, Vector3 oldScale, Vector2 oldPosition)
        {
            // 塗っている途中でまた替わったら、そのまま塗り続ける（前の絵はまだ見えているので入れ替えない）。
            if (_stage == Stage.Painting || _stage == Stage.Holding)
            {
                SetCurrentVisible(_stage == Stage.Holding);
                return;
            }

            if (previous != null)
            {
                previous.sprite = oldSprite;
                previous.color = oldColor;
                previous.rectTransform.localScale = oldScale;
                previous.rectTransform.anchoredPosition = oldPosition;
                previous.enabled = oldSprite != null;
            }

            SetCurrentVisible(false);
            FitMask(oldSprite, oldScale, oldPosition);
            BuildPath();
            if (stroke != null)
            {
                stroke.color = _strokeColor;
                stroke.enabled = true;
                stroke.SetRange(0f, 0f);
            }

            _stage = Stage.Painting;
            _t = 0f;
            _foamTimer = 0f;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            switch (_stage)
            {
                case Stage.Painting:
                    _t = Mathf.Min(1f, _t + dt / paintSeconds);
                    // 少しためてから一気に塗る。
                    var head = _t * _t * (3f - 2f * _t);
                    stroke?.SetRange(0f, head);
                    if (_t >= 1f)
                    {
                        // 塗り終わった: ここで新しいキャラに替える（前のキャラは隠す）。
                        if (previous != null)
                        {
                            previous.enabled = false;
                        }

                        SetCurrentVisible(true);
                        if (current != null)
                        {
                            FitMask(current.sprite, current.rectTransform.localScale, current.rectTransform.anchoredPosition);
                        }

                        _stage = Stage.Holding;
                        _t = 0f;
                    }

                    break;
                case Stage.Holding:
                    _t += dt;
                    if (_t >= holdSeconds)
                    {
                        _stage = Stage.Revealing;
                        _t = 0f;
                        if (revealStyle == RevealStyle.FoamAway)
                        {
                            // 塗った所のあちこちから、一度に泡がはじける。
                            for (var i = 0; i < 6; i++)
                            {
                                SpawnFoam(Random.value, 3);
                            }
                        }
                    }

                    break;
                case Stage.Revealing:
                    _t = Mathf.Min(1f, _t + dt / revealSeconds);
                    if (stroke != null)
                    {
                        if (revealStyle == RevealStyle.WipeAlongPath)
                        {
                            stroke.SetRange(_t * _t * (3f - 2f * _t), 1f);
                        }
                        else
                        {
                            var c = _strokeColor;
                            c.a *= 1f - _t;
                            stroke.color = c;
                        }
                    }

                    _foamTimer -= dt;
                    if (_foamTimer <= 0f)
                    {
                        _foamTimer = foamInterval;
                        SpawnFoam(revealStyle == RevealStyle.WipeAlongPath ? _t : Random.value, 2);
                    }

                    if (_t >= 1f)
                    {
                        Finish();
                        _shine = 0f;
                    }

                    break;
            }

            if (_shine < 1f)
            {
                _shine = Mathf.Min(1f, _shine + dt / shineSeconds);
                for (var i = 0; i < shines.Length; i++)
                {
                    var shine = shines[i];
                    if (shine == null)
                    {
                        continue;
                    }

                    // 星ごとに少しずつ遅れて、ぱっと大きくなって、くるっと回りながら消える。
                    var local = Mathf.Clamp01((_shine - i * 0.18f) / 0.6f);
                    var k = Mathf.Sin(local * Mathf.PI);
                    shine.sizeDelta = Vector2.one * (shineSize * k * (i == 0 ? 1f : 0.65f));
                    shine.localRotation = Quaternion.Euler(0f, 0f, local * 90f);
                }
            }
        }

        /// <summary>
        /// ジグザグの通り道（ユーザーの絵の形）: 左の真ん中の高さから始めて、上 → 下 → 上 … と折り返しながら右へ進み、最後は右上へ抜けて真ん中の高さで終わる。
        /// 折り返しは塗る範囲の少し上・下の外。高さに合わせて横にずらす（上ほど右。lean 度）ので、線は「／」の向きに傾く。
        /// 塗る範囲（線の枠）の四隅をずらしたあとの横位置で測って、全部を覆うように並べる。線の太さが折り返しの間隔より太いので、塗り終わると全部が隠れる。
        /// </summary>
        private void BuildPath()
        {
            if (stroke == null)
            {
                return;
            }

            var rect = ((RectTransform)stroke.transform).rect;
            // 高さ v の所は、横に v × shear だけずらす（上ほど右）。
            var shear = Mathf.Tan(Mathf.Clamp(lean, -60f, 60f) * Mathf.Deg2Rad);
            var halfW = rect.width * 0.5f;
            var halfH = rect.height * 0.5f;
            // ずらす前の横位置で、四隅がどこに来るか（上の左隅がいちばん左、下の右隅がいちばん右）。ここまで広げると、ジグザグがユーザーの絵のように大きく開く。
            var reach = halfW + halfH * Mathf.Abs(shear);
            var uMin = -reach - overshoot.x;
            var uMax = reach + overshoot.x;
            var vTop = halfH + overshoot.y;
            var vBottom = -vTop;
            var count = Mathf.Max(3, legs);
            if (count % 2 == 0)
            {
                // 最後が右上へ抜けるように、本数は奇数にする（上 → 下 … → 下 → 真ん中）。
                count++;
            }

            _path.Clear();
            for (var i = 0; i <= count; i++)
            {
                var u = Mathf.Lerp(uMin, uMax, i / (float)count) + Random.Range(-jitter, jitter);
                // 最初と最後は真ん中の高さ。あいだは上（奇数番目）と下（偶数番目）を行き来する。
                var v = i == 0 || i == count ? 0f : i % 2 == 1 ? vTop : vBottom;
                v += Random.Range(-jitter, jitter);
                _path.Add(rect.center + new Vector2(u + v * shear, v) * size);
            }

            // すき間なく塗りつぶすとき: 線が折り返しの間隔より細いと、すき間が残る（傾けると線どうしが重なるので、1.4 倍あれば足りる。画面の外で確かめた値）。
            var gap = (uMax - uMin) / count * size;
            if (fillGaps && stroke.width < gap * 1.4f)
            {
                stroke.width = gap * 1.4f;
            }

            stroke.SetPath(_path);
        }

        /// <summary>歯磨き粉を切り抜く型を、キャラの絵（と大きさ・位置）に合わせる。切り抜かないときは型を止めて、線の枠を立ち絵の枠と同じにする。</summary>
        private void FitMask(Sprite sprite, Vector3 scale, Vector2 position)
        {
            if (pasteMask == null)
            {
                return;
            }

            var mask = pasteMask.GetComponent<Mask>();
            if (mask != null)
            {
                mask.enabled = clipToCharacter;
            }

            // 型の絵は見せない（Mask が止まっていると、そのまま見えてしまう）。
            pasteMask.enabled = clipToCharacter;
            if (!clipToCharacter)
            {
                pasteMask.rectTransform.localScale = Vector3.one;
                pasteMask.rectTransform.anchoredPosition = Vector2.zero;
                return;
            }

            pasteMask.sprite = sprite;
            pasteMask.rectTransform.localScale = scale;
            pasteMask.rectTransform.anchoredPosition = position;
        }

        /// <summary>線の上（0〜1）のあたりに泡を立てる。</summary>
        private void SpawnFoam(float along, int count)
        {
            if (foam == null || stroke == null)
            {
                return;
            }

            var rect = ((RectTransform)stroke.transform).rect;
            var point = stroke.PointAt(along) + Random.insideUnitCircle * (stroke.width * 0.4f);
            if (clipToCharacter)
            {
                // キャラの上にだけ塗っているときは、泡も枠の中に出す。
                point.x = Mathf.Clamp(point.x, rect.xMin, rect.xMax);
                point.y = Mathf.Clamp(point.y, rect.yMin, rect.yMax);
            }
            // 線の中の位置 → 泡の部品の中の位置（型がキャラに合わせて大きさ・位置を変えていても合うように）。
            var world = stroke.transform.TransformPoint(point);
            foam.Play(Color.white, count, foam.transform.InverseTransformPoint(world));
        }

        private void SetCurrentVisible(bool visible)
        {
            // 立ち絵の色は区画が毎フレーム入れ直すので、色ではなく描画の濃さで隠す。
            if (current != null)
            {
                current.canvasRenderer.SetAlpha(visible ? 1f : 0f);
            }
        }

        private void Finish()
        {
            _stage = Stage.Idle;
            SetCurrentVisible(true);
            if (previous != null)
            {
                previous.enabled = false;
            }

            if (stroke != null)
            {
                stroke.SetRange(0f, 0f);
                stroke.color = _strokeColor;
            }
        }
    }
}
