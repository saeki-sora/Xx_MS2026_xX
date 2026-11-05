using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 盾1枚分（<see cref="EnemyShieldDirector"/> が盾持ちごとに作って使い回す。Play 中だけの物）。
    /// ・コの字の板3枚の当たり判定（BoxCollider2D）: ビームの判定（Physics2D のレイ）がここで止まる。群衆の押し合い・経路には関係しない
    /// ・仮の見た目（板を立てた形のメッシュ）か、差し替え用の Prefab
    /// ・にらんでいる人の色の印
    /// +X が正面（にらんでいる向き）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyShieldView : MonoBehaviour
    {
        private static Material s_material;
        private static Sprite s_dot;

        private readonly ShieldBox[] _boxes = new ShieldBox[3];
        private readonly BoxCollider2D[] _colliders = new BoxCollider2D[3];
        private EnemyShieldSettings _settings;
        private GameObject _visual;
        private Mesh _mesh;
        private SpriteRenderer _marker;
        private SpriteRenderer _pupil;
        private int _focus = int.MinValue;
        private int _signature;

        /// <summary>今にらんでいる人（-1 は誰も）。</summary>
        public int Focus { get; set; } = -1;

        /// <summary>向き（度。0 = +X）。</summary>
        public float AngleDegrees { get; set; }

        public bool Initialized { get; set; }

        public int NetId { get; set; }

        public EnemyShieldSettings Settings => _settings;

        public static EnemyShieldView Create(Transform parent)
        {
            var go = new GameObject("[Shield]");
            go.transform.SetParent(parent, false);
            // 板の見た目は地面に立てたメッシュ、印は地面に寝かせる。ビルボードで立たせない。
            go.AddComponent<BillboardIgnore>();
            var view = go.AddComponent<EnemyShieldView>();
            for (var i = 0; i < view._colliders.Length; i++)
            {
                var part = new GameObject(i == 0 ? "Front" : i == 1 ? "Left" : "Right");
                part.transform.SetParent(go.transform, false);
                view._colliders[i] = part.AddComponent<BoxCollider2D>();
            }

            return view;
        }

        /// <summary>種類の設定に合わせて形を作り直す（同じ設定なら何もしない）。</summary>
        public void Bind(EnemyShieldSettings settings)
        {
            // Play 中に種類の設定を変えても形が追いつくよう、形に関わる値が変わったら作り直す。
            var signature = Signature(settings);
            if (ReferenceEquals(_settings, settings) && _visual != null && signature == _signature)
            {
                return;
            }

            _settings = settings;
            _signature = signature;
            EnemyShieldMath.Boxes(settings, _boxes);
            for (var i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].offset = _boxes[i].center;
                _colliders[i].size = _boxes[i].size;
            }

            if (_visual != null)
            {
                Destroy(_visual);
            }

            if (settings.visualPrefab != null)
            {
                _visual = Instantiate(settings.visualPrefab, transform, false);
            }
            else
            {
                _visual = new GameObject("Placeholder");
                _visual.transform.SetParent(transform, false);
                _visual.AddComponent<MeshFilter>().sharedMesh = BuildMesh();
                _visual.AddComponent<MeshRenderer>().sharedMaterial = Material;
            }

            BuildMarker();
            _focus = int.MinValue;
        }

        /// <summary>位置・向き・印の色を反映する。</summary>
        public void Apply(Vector2 position, float angleDegrees, int focus)
        {
            AngleDegrees = angleDegrees;
            Focus = focus;
            transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.Euler(0f, 0f, angleDegrees));
            if (_focus == focus)
            {
                return;
            }

            _focus = focus;
            var show = _settings.showFocusMarker && focus >= 0;
            _marker.enabled = show;
            _pupil.enabled = show;
            if (show)
            {
                _marker.color = FortressColors.PlayerColor(focus);
            }
        }

        private void BuildMarker()
        {
            if (_marker == null)
            {
                _marker = new GameObject("FocusMarker").AddComponent<SpriteRenderer>();
                _marker.transform.SetParent(transform, false);
                _marker.sprite = Dot;
                _pupil = new GameObject("Pupil").AddComponent<SpriteRenderer>();
                _pupil.transform.SetParent(_marker.transform, false);
                _pupil.sprite = Dot;
                _pupil.color = new Color(0.08f, 0.08f, 0.1f, 1f);
                _pupil.transform.localScale = Vector3.one * 0.45f;
                _pupil.transform.localPosition = new Vector3(0.12f, 0f, -0.005f);
                _pupil.sortingOrder = 1;
            }

            var s = _settings;
            _marker.transform.localPosition = new Vector3(s.frontDistance + s.thickness * 0.5f + 0.3f, 0f, -s.wallHeight - 0.02f);
            _marker.transform.localScale = Vector3.one * 0.42f;
        }

        /// <summary>板3枚を、地面（z=0）から上（-Z）へ立てた箱にする。上の面を明るく、横の面を暗くして形を見やすくする。</summary>
        private Mesh BuildMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "ShieldPlaceholder" };
            }

            _mesh.Clear();
            var h = _settings.wallHeight;
            var top = Brighten(_settings.color, 1.15f);
            var side = Brighten(_settings.color, 0.72f);
            var vertices = new Vector3[_boxes.Length * 20];
            var colors = new Color[vertices.Length];
            var triangles = new int[_boxes.Length * 30];
            var v = 0;
            var t = 0;
            foreach (var box in _boxes)
            {
                var min = box.center - box.size * 0.5f;
                var max = box.center + box.size * 0.5f;
                var c = new[]
                {
                    new Vector3(min.x, min.y, 0f), new Vector3(max.x, min.y, 0f), new Vector3(max.x, max.y, 0f), new Vector3(min.x, max.y, 0f),
                    new Vector3(min.x, min.y, -h), new Vector3(max.x, min.y, -h), new Vector3(max.x, max.y, -h), new Vector3(min.x, max.y, -h)
                };
                AddQuad(c[4], c[5], c[6], c[7], top);
                AddQuad(c[0], c[1], c[5], c[4], side);
                AddQuad(c[1], c[2], c[6], c[5], side);
                AddQuad(c[2], c[3], c[7], c[6], side);
                AddQuad(c[3], c[0], c[4], c[7], side);
            }

            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            return _mesh;

            void AddQuad(Vector3 a, Vector3 b, Vector3 c2, Vector3 d, Color color)
            {
                vertices[v] = a;
                vertices[v + 1] = b;
                vertices[v + 2] = c2;
                vertices[v + 3] = d;
                for (var k = 0; k < 4; k++)
                {
                    colors[v + k] = color;
                }

                triangles[t++] = v;
                triangles[t++] = v + 1;
                triangles[t++] = v + 2;
                triangles[t++] = v;
                triangles[t++] = v + 2;
                triangles[t++] = v + 3;
                v += 4;
            }
        }

        private static int Signature(EnemyShieldSettings s)
        {
            unchecked
            {
                var h = s.frontDistance.GetHashCode();
                h = h * 31 + s.halfWidth.GetHashCode();
                h = h * 31 + s.sideLength.GetHashCode();
                h = h * 31 + s.thickness.GetHashCode();
                h = h * 31 + s.wallHeight.GetHashCode();
                h = h * 31 + s.color.GetHashCode();
                h = h * 31 + (s.visualPrefab != null ? s.visualPrefab.GetInstanceID() : 0);
                return h;
            }
        }

        private static Color Brighten(Color c, float k)
        {
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        private static Material Material
        {
            get
            {
                if (s_material == null)
                {
                    // 頂点の色をそのまま出す、両面・ライトなしのシェーダー（ビルドにも入っている）。
                    s_material = new Material(Shader.Find("Sprites/Default")) { name = "ShieldPlaceholder", hideFlags = HideFlags.DontSave };
                }

                return s_material;
            }
        }

        private static Sprite Dot
        {
            get
            {
                if (s_dot == null)
                {
                    const int size = 64;
                    var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ShieldDot", hideFlags = HideFlags.DontSave };
                    var pixels = new Color32[size * size];
                    for (var y = 0; y < size; y++)
                    {
                        for (var x = 0; x < size; x++)
                        {
                            var dx = (x + 0.5f) / size * 2f - 1f;
                            var dy = (y + 0.5f) / size * 2f - 1f;
                            var alpha = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * size * 0.5f);
                            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                        }
                    }

                    texture.SetPixels32(pixels);
                    texture.Apply();
                    s_dot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                    s_dot.hideFlags = HideFlags.DontSave;
                }

                return s_dot;
            }
        }
    }
}
