using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 絵（スプライト）で描いている物（砲台・コア・壊せる物）の下に、ぼんやりした丸い影を落として、床から浮いて見えないようにする。
    /// 砲台・コア・壊せる物が Play 開始時に自動で付ける（シーンに手で付けた場合は、その値が使われる）。
    /// 影は持ち主の子にしない別のオブジェクトで、毎フレーム持ち主の位置へ動かす。Play中のシーンの物として作るので、Playを止めれば必ず消える
    /// （DontSave にすると、Playを止めてもシーンの外に残ってしまう）。
    /// 子にしないので、コアの汚れ（CoreDirtVisual）・壊せる物の見た目・ビルボード（絵を立たせる）が影の色や向きを変えることはない。
    /// 持ち主の絵が全部見えていないとき（壊れて消えた等）は影も消える。持ち主にスプライトが無ければ何もしない（3Dモデルは本物の影が出る）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundContactShadow : MonoBehaviour
    {
        [Tooltip("影の直径（持ち主の絵の大きさに対する倍率）。")]
        [Range(0.2f, 3f)]
        public float size = 1.1f;

        [Tooltip("影の濃さ（0〜1）。")]
        [Range(0f, 1f)]
        public float darkness = 0.45f;

        [Tooltip("影の縦の長さ（横幅に対する割合。地面の上での形）。1で丸、小さいほど横長。")]
        [Range(0.2f, 1f)]
        public float roundness = 1f;

        private static Sprite _softCircle;
        private static Material _material;

        private readonly List<SpriteRenderer> _owners = new List<SpriteRenderer>();
        private SpriteRenderer _shadow;
        private float _diameter;

        private void Start()
        {
            // 自分自身に絵があればそれだけ（砲台なら本体。回る砲身は含めない）、無ければ子の絵全部（コア・壊せる物）。
            if (TryGetComponent<SpriteRenderer>(out var own))
            {
                _owners.Add(own);
            }
            else
            {
                GetComponentsInChildren(true, _owners);
            }
        }

        private void OnDisable()
        {
            if (_shadow != null)
            {
                _shadow.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_shadow != null)
            {
                Destroy(_shadow.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (_owners.Count == 0 || darkness <= 0f || !TryGetVisibleBounds(out var bounds))
            {
                if (_shadow != null)
                {
                    _shadow.enabled = false;
                }

                return;
            }

            if (!EnsureShadow())
            {
                return;
            }

            // 絵の大きさは、最初に見えたときの大きさを使う（立たせる・揺れる等で毎フレーム変わらないように）。
            if (_diameter <= 0f)
            {
                _diameter = Mathf.Max(bounds.size.x, bounds.size.y);
            }

            var diameter = _diameter * size;
            var position = transform.position;
            // カメラは +Z を向いているので、少しだけ手前(-Z)に置いて床とのちらつきを防ぐ。
            _shadow.transform.SetPositionAndRotation(new Vector3(position.x, position.y, position.z - 0.005f), Quaternion.identity);
            _shadow.transform.localScale = new Vector3(diameter, diameter * roundness, 1f);
            _shadow.color = new Color(0f, 0f, 0f, darkness);
            _shadow.sortingLayerID = _owners[0].sortingLayerID;
            _shadow.sortingOrder = LowestSortingOrder() - 1;
            _shadow.enabled = true;
        }

        private bool TryGetVisibleBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            for (var i = 0; i < _owners.Count; i++)
            {
                var renderer = _owners[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private int LowestSortingOrder()
        {
            var lowest = int.MaxValue;
            for (var i = 0; i < _owners.Count; i++)
            {
                if (_owners[i] != null)
                {
                    lowest = Mathf.Min(lowest, _owners[i].sortingOrder);
                }
            }

            return lowest == int.MaxValue ? 0 : lowest;
        }

        private bool EnsureShadow()
        {
            if (_shadow != null)
            {
                return true;
            }

            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    return false;
                }

                _material = new Material(shader) { name = "GroundContactShadow (runtime)", hideFlags = HideFlags.DontSave };
            }

            var go = new GameObject($"[ContactShadow] {name}");
            _shadow = go.AddComponent<SpriteRenderer>();
            _shadow.sharedMaterial = _material;
            _shadow.sprite = SoftCircle;
            return true;
        }

        /// <summary>ふちがやわらかい丸（1×1メートル）。</summary>
        private static Sprite SoftCircle
        {
            get
            {
                if (_softCircle != null)
                {
                    return _softCircle;
                }

                const int Size = 64;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = "GroundContactShadow (runtime)",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                var pixels = new Color32[Size * Size];
                for (var y = 0; y < Size; y++)
                {
                    for (var x = 0; x < Size; x++)
                    {
                        var dx = (x + 0.5f) / Size * 2f - 1f;
                        var dy = (y + 0.5f) / Size * 2f - 1f;
                        var d = Mathf.Sqrt(dx * dx + dy * dy);
                        var edge = Mathf.Clamp01((d - 0.3f) / 0.7f);
                        var alpha = 1f - edge * edge * (3f - 2f * edge);
                        pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _softCircle = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
                _softCircle.name = "GroundContactShadow (runtime)";
                _softCircle.hideFlags = HideFlags.DontSave;
                return _softCircle;
            }
        }
    }
}
