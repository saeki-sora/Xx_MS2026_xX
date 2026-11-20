using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Stage
{
    /// <summary>
    /// 丸いリング（ステージの枠など）の輪郭を光らせる。輪郭から外へ広く、内へ薄く、柔らかく消えていく光の帯を描く。
    /// ・リングの形（中心・内と外の半径・向き）はメッシュから自動で読む。背景オブジェクトの一番上（StageProp のある所）に付けるだけでよい。
    /// ・光の帯はリングの裏側の高さに置くので、リング自体が光の上に乗る（リングの下に光が透けない）。
    /// ・リングが動く・揺れる・大きさが変わっても付いていく。編集中もシーンに表示される。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/Stage/リングの輪郭を光らせる")]
    public sealed class StageRingGlow : MonoBehaviour
    {
        [Tooltip("光の色。")]
        [ColorUsage(false, true)]
        public Color color = new Color(0.45f, 0.5f, 1f);

        [Tooltip("光の強さ。0 で消える。")]
        [Range(0f, 4f)]
        public float intensity = 1.2f;

        [Tooltip("輪郭から外へ光が広がる幅（ワールドの長さ）。")]
        [Min(0f)]
        public float outerWidth = 2.5f;

        [Tooltip("輪郭から内へ光が広がる幅（ワールドの長さ）。0 で内側は光らない（外側だけ光る）。")]
        [Min(0f)]
        public float innerWidth;

        [Tooltip("内側の光の強さ（外側を 1 としたときの割合）。")]
        [Range(0f, 1f)]
        public float innerStrength = 0.35f;

        [Tooltip("光の消え方。小さいほど遠くまでぼんやり残り、大きいほど輪郭の近くでスッと消える。")]
        [Range(0.5f, 6f)]
        public float softness = 2.2f;

        [Tooltip("ゆっくり明るさが上下する量。0 で動かない。")]
        [Range(0f, 1f)]
        public float pulse;

        [Tooltip("明るさが上下する間隔（秒）。")]
        [Min(0.1f)]
        public float pulsePeriod = 3f;

        [Tooltip("光の始まりを輪郭から内へずらす長さ（ワールドの長さ）。円盤の絵のふちがメッシュより少し内側にあって、光との間にすき間が見えるときに使う。")]
        [Min(0f)]
        public float edgeInset;

        [Tooltip("光らせるリングのメッシュ。空なら子の中から自動で探す。")]
        public MeshFilter target;

        [SerializeField, HideInInspector]
        private Shader shader;

        private const string ShaderName = "MS2026/Stage/RingGlow";
        private const int Segments = 192;

        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int InnerStrengthId = Shader.PropertyToID("_InnerStrength");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int PulsePeriodId = Shader.PropertyToID("_PulsePeriod");

        private GameObject _drawer;
        private Material _material;
        private Mesh _mesh;
        private MeshFilter _builtFor;
        private Mesh _builtSource;
        private Vector3 _builtScale;
        private float _builtOuter = -1f;
        private float _builtInner = -1f;
        private float _builtInset = -1f;

        private void OnEnable()
        {
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }

            if (shader == null)
            {
                Debug.LogWarning($"[StageRingGlow] シェーダー {ShaderName} が見つからないので光を出せません。", this);
                return;
            }

            _material = new Material(shader) { name = "StageRingGlow (自動)", hideFlags = HideFlags.HideAndDontSave };
            _mesh = new Mesh { name = "StageRingGlow Band", hideFlags = HideFlags.HideAndDontSave };
            Refresh();
        }

        private void OnDisable()
        {
            DestroySafe(_drawer);
            DestroySafe(_material);
            DestroySafe(_mesh);
            _drawer = null;
            _material = null;
            _mesh = null;
            _builtFor = null;
            _builtSource = null;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnValidate()
        {
            // 値を変えたら次の LateUpdate で作り直す（OnValidate の中では物を作れない）。
            _builtOuter = -1f;
        }

        private void Refresh()
        {
            if (_material == null)
            {
                return;
            }

            var filter = target != null ? target : FindRingMesh();
            if (filter == null || filter.sharedMesh == null)
            {
                SetDrawerActive(false);
                return;
            }

            EnsureDrawer(filter);
            if (NeedsRebuild(filter))
            {
                Rebuild(filter);
            }

            SetDrawerActive(intensity > 0f);
            _material.SetColor(GlowColorId, color);
            _material.SetFloat(GlowIntensityId, intensity);
            _material.SetFloat(InnerStrengthId, innerStrength);
            _material.SetFloat(SoftnessId, softness);
            _material.SetFloat(PulseAmountId, pulse);
            _material.SetFloat(PulsePeriodId, pulsePeriod);
        }

        /// <summary>光らせているリングのメッシュ（target が空なら自動で探したもの）。エディタの画面から線の色を変えるのに使う。</summary>
        public MeshFilter ResolveRingMesh()
        {
            return target != null ? target : FindRingMesh();
        }

        private MeshFilter FindRingMesh()
        {
            foreach (var f in GetComponentsInChildren<MeshFilter>(true))
            {
                // 足あと（Footprint）など表示しないメッシュは除き、MeshRenderer で描かれているものだけを選ぶ。
                if ((_drawer == null || f.gameObject != _drawer) && f.TryGetComponent<MeshRenderer>(out _))
                {
                    return f;
                }
            }

            return null;
        }

        private void EnsureDrawer(MeshFilter filter)
        {
            if (_drawer != null && _drawer.transform.parent == filter.transform)
            {
                return;
            }

            DestroySafe(_drawer);
            _drawer = new GameObject("[自動] リングの光") { hideFlags = HideFlags.HideAndDontSave, layer = filter.gameObject.layer };
            _drawer.transform.SetParent(filter.transform, false);
            _drawer.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var r = _drawer.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _builtFor = null;
        }

        private void SetDrawerActive(bool active)
        {
            if (_drawer != null && _drawer.activeSelf != active)
            {
                _drawer.SetActive(active);
            }
        }

        private bool NeedsRebuild(MeshFilter filter)
        {
            return _builtFor != filter
                   || _builtSource != filter.sharedMesh
                   || _builtScale != filter.transform.lossyScale
                   || !Mathf.Approximately(_builtOuter, outerWidth)
                   || !Mathf.Approximately(_builtInner, innerWidth)
                   || !Mathf.Approximately(_builtInset, edgeInset);
        }

        private void Rebuild(MeshFilter filter)
        {
            _builtFor = filter;
            _builtSource = filter.sharedMesh;
            _builtScale = filter.transform.lossyScale;
            _builtOuter = outerWidth;
            _builtInner = innerWidth;
            _builtInset = edgeInset;

            if (!RingShape.TryMeasure(_builtSource, out var shape))
            {
                _mesh.Clear();
                return;
            }

            // 幅はワールドの長さで指定するので、メッシュの中の長さに直す。
            var scale = shape.PlaneScale(filter.transform.lossyScale);
            RingShape.BuildBand(_mesh, shape, outerWidth / scale, innerWidth / scale, edgeInset / scale, Segments);
        }

        private static void DestroySafe(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }

    /// <summary>リングのメッシュの形（向き・中心・半径）を測り、光の帯のメッシュを作る。</summary>
    internal struct RingShape
    {
        /// <summary>リングの厚みの向き（一番薄い軸: 0=X, 1=Y, 2=Z）。</summary>
        public int normalAxis;

        /// <summary>リングの面の2つの軸。</summary>
        public int axisU;
        public int axisV;

        public Vector2 center;
        public float innerRadius;
        public float outerRadius;

        /// <summary>光を置く高さ（厚みの軸の上で、カメラから遠い側＝リングの裏）。</summary>
        public float backHeight;

        /// <summary>
        /// メッシュの範囲（Bounds）から測る。頂点は読まない（FBX の Read/Write がオフでもビルドで同じ結果になるように）。
        /// 外の半径 = 面の幅の半分。リングの断面は丸いので、太さ（内と外の半径の差）は厚みと同じとみなす。
        /// </summary>
        public static bool TryMeasure(Mesh mesh, out RingShape shape)
        {
            shape = default;
            if (mesh == null)
            {
                return false;
            }

            var b = mesh.bounds;
            shape.normalAxis = SmallestAxis(b.size);
            shape.axisU = (shape.normalAxis + 1) % 3;
            shape.axisV = (shape.normalAxis + 2) % 3;
            shape.center = new Vector2(b.center[shape.axisU], b.center[shape.axisV]);
            shape.backHeight = BackHeight(b, shape.normalAxis);
            shape.outerRadius = Mathf.Min(b.size[shape.axisU], b.size[shape.axisV]) * 0.5f;
            shape.innerRadius = Mathf.Clamp(shape.outerRadius - b.size[shape.normalAxis], 0f, shape.outerRadius);
            return shape.outerRadius > 0f;
        }

        private static int SmallestAxis(Vector3 size)
        {
            if (size.x <= size.y && size.x <= size.z)
            {
                return 0;
            }

            return size.y <= size.z ? 1 : 2;
        }

        /// <summary>
        /// 光を置く高さ。厚みの真ん中だと、リングの面の内側に光が食い込んで見えるので、裏側（中心から遠い側）に置く。
        /// 床のリングは「表＝カメラ側」が分からないので、厚みの中心より原点から遠い面を裏とみなす
        /// （このゲームのリングはモデルの原点が床の高さで、表はカメラ側に浮いている）。
        /// </summary>
        private static float BackHeight(Bounds b, int axis)
        {
            var lo = b.min[axis];
            var hi = b.max[axis];
            return Mathf.Abs(lo) < Mathf.Abs(hi) ? lo : hi;
        }

        /// <summary>メッシュの中の長さ 1 が、ワールドでどれだけの長さか（面の2軸の平均）。</summary>
        public float PlaneScale(Vector3 lossyScale)
        {
            var s = (Mathf.Abs(lossyScale[axisU]) + Mathf.Abs(lossyScale[axisV])) * 0.5f;
            return s > 1e-6f ? s : 1f;
        }

        /// <summary>
        /// 外と内の光の帯を作る。各帯は3重の輪:
        /// リングの太さの真ん中（リングの下に隠れる重なり。多角形のリングの辺との隙間を埋める）→ 輪郭 → 光の端。
        /// </summary>
        public static void BuildBand(Mesh mesh, RingShape s, float outerWidth, float innerWidth, float edgeInset, int segments)
        {
            mesh.Clear();
            var mid = (s.innerRadius + s.outerRadius) * 0.5f;
            var hasInner = innerWidth > 0f;
            var rings = hasInner ? 6 : 3;
            var count = segments + 1;
            var verts = new Vector3[rings * count];
            var uvs = new Vector2[rings * count];

            var outerW = Mathf.Max(outerWidth, 1e-5f);
            var innerW = Mathf.Max(innerWidth, 1e-5f);
            var radii = new float[6];
            var dist = new float[6];
            var side = new float[6];

            // 外の輪郭。ずらす分だけ内へ（円盤のように太さのない形でも、重なりの輪が輪郭より外に出ないようにする）。
            var outerEdge = Mathf.Max(0f, s.outerRadius - edgeInset);
            var overlap = Mathf.Min(mid, outerEdge);
            radii[0] = overlap; dist[0] = (overlap - outerEdge) / outerW; side[0] = 0f;
            radii[1] = outerEdge; dist[1] = 0f; side[1] = 0f;
            radii[2] = outerEdge + outerWidth; dist[2] = 1f; side[2] = 0f;
            radii[3] = mid; dist[3] = (s.innerRadius - mid) / innerW; side[3] = 1f;
            radii[4] = s.innerRadius; dist[4] = 0f; side[4] = 1f;
            radii[5] = Mathf.Max(0f, s.innerRadius - innerWidth); dist[5] = 1f; side[5] = 1f;

            for (var ring = 0; ring < rings; ring++)
            {
                for (var i = 0; i < count; i++)
                {
                    var a = i * Mathf.PI * 2f / segments;
                    var p = Vector3.zero;
                    p[s.axisU] = s.center.x + Mathf.Cos(a) * radii[ring];
                    p[s.axisV] = s.center.y + Mathf.Sin(a) * radii[ring];
                    p[s.normalAxis] = s.backHeight;
                    verts[ring * count + i] = p;
                    uvs[ring * count + i] = new Vector2(dist[ring], side[ring]);
                }
            }

            var strips = hasInner ? 4 : 2;
            var tris = new int[strips * segments * 6];
            var t = 0;
            for (var band = 0; band < (hasInner ? 2 : 1); band++)
            {
                for (var step = 0; step < 2; step++)
                {
                    var r0 = band * 3 + step;
                    var r1 = r0 + 1;
                    for (var i = 0; i < segments; i++)
                    {
                        var a0 = r0 * count + i;
                        var a1 = a0 + 1;
                        var b0 = r1 * count + i;
                        var b1 = b0 + 1;
                        tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                        tris[t++] = a1; tris[t++] = b0; tris[t++] = b1;
                    }
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}
