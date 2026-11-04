using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Fortress
{
    /// <summary>
    /// 空中をただよう環境の粒（データの粒・泡・湯気。見た目だけ）。設定は <see cref="Settings"/>（ステージの StageSet.ambientMotes を、
    /// ステージが開いたときに入れる）。Play 中だけ、シーンに保存しない係のオブジェクトを1つ作り、ゲームのカメラ（<see cref="FortressCameraRig"/>）に
    /// 映っている範囲の空中に粒を散らして描く（そのカメラの方を向く）。通信しない。
    /// </summary>
    public static class AmbientMotesLook
    {
        private static AmbientMoteSettings _settings;

        /// <summary>今の粒の設定。null なら出さない（ステージが無いシーン）。</summary>
        public static AmbientMoteSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                if (value != null && Application.isPlaying)
                {
                    AmbientMotesRunner.Ensure();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _settings = null;
        }
    }

    /// <summary><see cref="AmbientMotesLook"/> の粒を動かして描く係（Play 中だけ、シーンに保存しない隠しオブジェクト）。</summary>
    [AddComponentMenu("")]
    internal sealed class AmbientMotesRunner : MonoBehaviour
    {
        private const string ShaderName = "Hidden/MS2026/Fortress/AmbientMote";

        private struct Mote
        {
            public int Kind; // 0=データ, 1=泡, 2=湯気
            public Vector3 Position;
            public float Size;
            public float Age;
            public float Life;
            public float Phase;
        }

        private static AmbientMotesRunner _instance;

        private readonly List<Mote> _data = new List<Mote>();
        private readonly List<Mote> _bubbles = new List<Mote>();
        private readonly List<Mote> _steam = new List<Mote>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _indices = new List<int>();

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _material;
        private Texture2D _atlas;
        private bool _missingLogged;

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("[Look] Ambient Motes") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AmbientMotesRunner>();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (_atlas != null) Destroy(_atlas);
        }

        private void LateUpdate()
        {
            var settings = AmbientMotesLook.Settings;
            var rig = FortressCameraRig.Active;
            var camera = rig != null ? rig.targetCamera : null;
            var visible = settings != null && settings.enabled && camera != null && EnsureResources();
            if (_renderer != null)
            {
                _renderer.enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            var dt = Time.deltaTime;
            var up = ScreenUp(camera);
            Step(_data, settings.dataCount, 0, settings, camera, up, dt);
            Step(_bubbles, settings.bubbleCount, 1, settings, camera, up, dt);
            Step(_steam, settings.steamCount, 2, settings, camera, up, dt);
            BuildMesh(settings, camera);
        }

        private bool EnsureResources()
        {
            if (_material != null)
            {
                return true;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                if (!_missingLogged)
                {
                    _missingLogged = true;
                    Debug.LogWarning($"[AmbientMotes] シェーダー '{ShaderName}' が見つからないので、環境の粒は出しません。");
                }

                return false;
            }

            _atlas = AmbientMoteAtlas.Build();
            _material = new Material(shader) { name = "AmbientMotes", hideFlags = HideFlags.DontSave, mainTexture = _atlas };
            _mesh = new Mesh { name = "AmbientMotes", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            // 粒の位置はワールドの位置のまま入れるので、この係のオブジェクトは原点から動かさない。
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }

        private static void Step(List<Mote> motes, int count, int kind, AmbientMoteSettings s, Camera camera, Vector2 up, float dt)
        {
            while (motes.Count > count)
            {
                motes.RemoveAt(motes.Count - 1);
            }

            while (motes.Count < count)
            {
                // 最初に出すときは、寿命の途中から始めて、一斉に出たり消えたりしないようにする。
                var mote = Spawn(kind, s, camera);
                mote.Age = Random.value * mote.Life;
                motes.Add(mote);
            }

            var right = new Vector2(up.y, -up.x);
            for (var i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                m.Age += dt;
                if (m.Age >= m.Life)
                {
                    m = Spawn(kind, s, camera);
                }

                var speed = kind == 1 ? s.riseSpeed * 1.2f : kind == 2 ? s.riseSpeed * 0.5f : s.riseSpeed;
                var swayVelocity = Mathf.Cos(m.Age * 1.6f + m.Phase) * s.sway * 1.6f;
                var move = up * speed + right * swayVelocity;
                m.Position += new Vector3(move.x, move.y, 0f) * dt;
                if (kind == 2)
                {
                    m.Position.z -= 0.15f * dt; // 湯気はカメラの方へ立ちのぼる
                }

                motes[i] = m;
            }
        }

        private static Mote Spawn(int kind, AmbientMoteSettings s, Camera camera)
        {
            var height = Random.Range(Mathf.Min(s.height.x, s.height.y), Mathf.Max(s.height.x, s.height.y));
            var size = kind == 0 ? s.dataSize
                : kind == 1 ? Random.Range(s.bubbleSize.x, s.bubbleSize.y)
                : Random.Range(s.steamSize.x, s.steamSize.y);
            return new Mote
            {
                Kind = kind,
                Position = RandomPointInView(camera, height),
                Size = size,
                Age = 0f,
                Life = Mathf.Max(0.5f, Random.Range(s.lifeSeconds.x, s.lifeSeconds.y)),
                Phase = Random.value * 100f
            };
        }

        // カメラに映っている範囲（少し外まで）の、床から height だけ上（-Z）の面の上のどこか。
        private static Vector3 RandomPointInView(Camera camera, float height)
        {
            var plane = new Plane(Vector3.back, new Vector3(0f, 0f, -height));
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var hit = false;
            for (var i = 0; i < 4; i++)
            {
                var ray = camera.ViewportPointToRay(new Vector3(i % 2 == 0 ? -0.05f : 1.05f, i < 2 ? -0.05f : 1.05f, 0f));
                if (plane.Raycast(ray, out var distance))
                {
                    var p = ray.GetPoint(distance);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                    hit = true;
                }
            }

            if (!hit)
            {
                var c = camera.transform.position;
                min = new Vector2(c.x - 10f, c.y - 10f);
                max = new Vector2(c.x + 10f, c.y + 10f);
            }

            return new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), -height);
        }

        private void BuildMesh(AmbientMoteSettings s, Camera camera)
        {
            _vertices.Clear();
            _colors.Clear();
            _uvs.Clear();
            _indices.Clear();
            var right = camera.transform.right;
            var up = camera.transform.up;
            AddQuads(_steam, s.steamColor, right, up);
            AddQuads(_bubbles, s.bubbleColor, right, up);
            AddQuads(_data, s.dataColor, right, up);

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_indices, 0, false);
        }

        private void AddQuads(List<Mote> motes, Color color, Vector3 right, Vector3 up)
        {
            foreach (var m in motes)
            {
                var u = Mathf.Clamp01(m.Age / m.Life);
                var fade = Mathf.SmoothStep(0f, 1f, u / 0.15f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.25f);
                var size = m.Size;
                int tile;
                switch (m.Kind)
                {
                    case 0:
                        // データの粒: 0 と 1 がときどき入れ替わり、少しまたたく。
                        tile = ((int)(m.Age * 1.5f + m.Phase)) % 2 == 0 ? AmbientMoteAtlas.GlyphZero : AmbientMoteAtlas.GlyphOne;
                        fade *= 0.7f + 0.3f * Mathf.Sin(m.Age * 7f + m.Phase);
                        break;
                    case 1:
                        // 泡: 最後にふくらんで、はじけるように消える。
                        tile = AmbientMoteAtlas.Bubble;
                        size *= 1f + Mathf.Max(0f, u - 0.85f) * 3f;
                        break;
                    default:
                        // 湯気: だんだん広がる。
                        tile = AmbientMoteAtlas.Steam;
                        size *= 1f + u * 0.4f;
                        break;
                }

                var c = color;
                c.a *= fade;
                if (c.a <= 0.002f)
                {
                    continue;
                }

                var rect = AmbientMoteAtlas.TileRect(tile);
                var r = right * (size * 0.5f);
                var v = up * (size * 0.5f);
                var start = _vertices.Count;
                _vertices.Add(m.Position - r - v);
                _vertices.Add(m.Position + r - v);
                _vertices.Add(m.Position + r + v);
                _vertices.Add(m.Position - r + v);
                _uvs.Add(new Vector2(rect.x, rect.y));
                _uvs.Add(new Vector2(rect.x + rect.z, rect.y));
                _uvs.Add(new Vector2(rect.x + rect.z, rect.y + rect.w));
                _uvs.Add(new Vector2(rect.x, rect.y + rect.w));
                for (var k = 0; k < 4; k++)
                {
                    _colors.Add(c);
                }

                _indices.Add(start);
                _indices.Add(start + 2);
                _indices.Add(start + 1);
                _indices.Add(start);
                _indices.Add(start + 3);
                _indices.Add(start + 2);
            }
        }

        // 画面の上の向き（地面の上の向き）。カメラの上方向を床に倒したもの。
        private static Vector2 ScreenUp(Camera camera)
        {
            var up = camera.transform.up;
            var v = new Vector2(up.x, up.y);
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.up;
        }
    }
}
