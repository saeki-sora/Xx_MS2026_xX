using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MS2026.Stage
{
    /// <summary>画面と画像の縦横比が違うときの合わせ方。</summary>
    public enum StageScreenBackgroundFit
    {
        /// <summary>画面を余りなく埋め、はみ出た分を切る（絵はゆがまない）。</summary>
        [InspectorName("切る（余りなく埋める・おすすめ）")]
        Cover,

        /// <summary>画面に合わせて伸ばす（全部見えるが、比が違うと絵がゆがむ）。</summary>
        [InspectorName("伸ばす（全部見せる・絵がゆがむ）")]
        Stretch,
    }

    /// <summary>
    /// 1枚の画像を、ゲームのカメラの画面いっぱいに「背景」として貼る。
    /// カメラが動いても揺れてもズームしても、いつも画面ぴったり（余りなし）。床・障害物・敵などはすべてこの上に描かれる。
    /// ・ステージのシーンに置くだけで動く（カメラは Game シーンの FortressCameraRig を自動で探す）。
    /// ・描くのはゲームのカメラだけ（UI・カットイン用のカメラ、オーバーレイのカメラには描かない）。
    /// ・床の板が画面を覆っていると隠れてしまうので、床の見た目は消しておくこと。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/Stage/画面いっぱいの背景")]
    public sealed class StageScreenBackground : MonoBehaviour
    {
        [Tooltip("背景にする画像。")]
        public Texture texture;

        [Tooltip("画像に掛ける色。白でそのまま。暗くしたいときは灰色に。")]
        public Color tint = Color.white;

        [Tooltip("画面と画像の縦横比が違うときの合わせ方。\n切る: 余りなく埋めて、はみ出た分を切る（おすすめ）\n伸ばす: 全部見せるが、絵がゆがむ")]
        public StageScreenBackgroundFit fit = StageScreenBackgroundFit.Cover;

        [Tooltip("シーンビューにも出す（編集中の確認用）。")]
        public bool showInSceneView;

        [SerializeField, HideInInspector]
        private Shader shader;

        private const string ShaderName = "MS2026/Stage/ScreenBackground";
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");

        private GameObject _drawer;
        private MeshRenderer _renderer;
        private Material _material;
        private Mesh _mesh;

        private void OnEnable()
        {
            if (!CreateDrawer())
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            DestroyDrawer();
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (_renderer == null)
            {
                return;
            }

            var draw = texture != null && ShouldDrawFor(cam);
            _renderer.enabled = draw;
            if (!draw)
            {
                return;
            }

            _material.SetTexture(BaseMapId, texture);
            _material.SetColor(BaseColorId, tint);
            _material.SetVector(UvRectId, ComputeUvRect(cam.aspect, (float)texture.width / Mathf.Max(1, texture.height), fit));
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private bool ShouldDrawFor(Camera cam)
        {
            if (cam == null)
            {
                return false;
            }

            if (cam.cameraType == CameraType.SceneView)
            {
                return showInSceneView;
            }

            if (cam.cameraType != CameraType.Game || cam.targetTexture != null)
            {
                return false;
            }

            // オーバーレイのカメラに描くと、下のカメラの絵を全部隠してしまう。
            if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType != CameraRenderType.Base)
            {
                return false;
            }

            return cam == ResolveCamera();
        }

        private static Camera ResolveCamera()
        {
            var rig = FortressCameraRig.Active;
            return rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
        }

        /// <summary>
        /// 画面の縦横比と画像の縦横比から、画像のどの範囲を使うか（拡大 xy・ずらし zw）を出す。
        /// 「切る」は中央をそろえて、はみ出た側を左右または上下で同じだけ切る。
        /// </summary>
        public static Vector4 ComputeUvRect(float screenAspect, float textureAspect, StageScreenBackgroundFit fit)
        {
            if (fit == StageScreenBackgroundFit.Stretch || screenAspect <= 0f || textureAspect <= 0f)
            {
                return new Vector4(1f, 1f, 0f, 0f);
            }

            if (screenAspect > textureAspect)
            {
                // 画面のほうが横長 → 横は全部、上下を切る。
                var scaleY = textureAspect / screenAspect;
                return new Vector4(1f, scaleY, 0f, (1f - scaleY) * 0.5f);
            }

            // 画面のほうが縦長 → 縦は全部、左右を切る。
            var scaleX = screenAspect / textureAspect;
            return new Vector4(scaleX, 1f, (1f - scaleX) * 0.5f, 0f);
        }

        private bool CreateDrawer()
        {
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }

            if (shader == null)
            {
                Debug.LogWarning($"[StageScreenBackground] シェーダー {ShaderName} が見つからないので背景を出せません。", this);
                return false;
            }

            _material = new Material(shader) { name = "StageScreenBackground (自動)", hideFlags = HideFlags.HideAndDontSave };
            _mesh = BuildMesh();

            _drawer = new GameObject("[自動] 画面いっぱいの背景") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            _drawer.transform.SetParent(transform, false);
            _drawer.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = _drawer.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _renderer.allowOcclusionWhenDynamic = false;
            _renderer.enabled = false;
            return true;
        }

        private void DestroyDrawer()
        {
            DestroySafe(_drawer);
            DestroySafe(_material);
            DestroySafe(_mesh);
            _drawer = null;
            _renderer = null;
            _material = null;
            _mesh = null;
        }

        /// <summary>
        /// 位置はシェーダーが無視する（UV から画面全体の四角を作る）。
        /// どこにカメラがあっても切り捨てられないよう、範囲（Bounds）だけ大きくしておく。
        /// </summary>
        private static Mesh BuildMesh()
        {
            var mesh = new Mesh { name = "StageScreenBackground Quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f) });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
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
}
