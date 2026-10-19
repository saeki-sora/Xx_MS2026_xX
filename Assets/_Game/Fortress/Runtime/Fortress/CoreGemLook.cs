using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアクリスタルを宝石のように見せる（設定は <see cref="CoreCrystalController.gemLook"/>）。<see cref="CoreCrystalController"/> が起動時に自動で付ける。
    /// コアの絵の上に「同じ絵・同じ色の層」を1つ上の並び順で重ね、Resources/Fortress/CoreGem.shader で描く:
    /// 後ろが屈折して透ける・中心から脈打つ光・ふちの光・HP が減るとひびが広がり脈が速く赤くなる・ダメージの瞬間に白く光る。
    /// 元の絵（色・汚れ CoreDirtVisual・絵を立たせるビルボード）には触れない。層は元の絵と同じ計算で立つので、ぴったり重なる。
    /// HP は Client にも届くので、全員の画面で同じように見える（通信は増やさない）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreCrystalController))]
    public sealed class CoreGemLook : MonoBehaviour
    {
        private const string ShaderResourcePath = "Fortress/CoreGem";

        private static readonly int BoundsId = Shader.PropertyToID("_BillboardBounds");
        private static readonly int GlowColorId = Shader.PropertyToID("_GemGlowColor");
        private static readonly int HurtColorId = Shader.PropertyToID("_GemHurtColor");
        private static readonly int CrackColorId = Shader.PropertyToID("_GemCrackColor");
        private static readonly int LookId = Shader.PropertyToID("_GemLook");
        private static readonly int DetailId = Shader.PropertyToID("_GemDetail");
        private static readonly int StateId = Shader.PropertyToID("_GemState");
        private static readonly int PulseId = Shader.PropertyToID("_GemPulse");

        private static Material _material;

        private CoreCrystalController _core;
        private SpriteRenderer _source;
        private SpriteRenderer _overlay;
        private MaterialPropertyBlock _block;
        private float _lastHealth = -1f;
        private float _lastHitTime = float.NegativeInfinity;

        private void Awake()
        {
            _core = GetComponent<CoreCrystalController>();
        }

        private void Start()
        {
            _lastHealth = _core.CurrentHealth;
        }

        private void OnEnable()
        {
            _core.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            _core.OnHealthChanged -= HandleHealthChanged;
            if (_overlay != null)
            {
                _overlay.enabled = false;
            }
        }

        /// <summary>要塞デザイナーの「試す」ボタン用。ダメージを受けた瞬間の白い光だけを出す。</summary>
        public void PlayHit()
        {
            _lastHitTime = Time.time;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastHealth >= 0f && current < _lastHealth - 0.0001f)
            {
                _lastHitTime = Time.time;
            }

            _lastHealth = current;
        }

        private void LateUpdate()
        {
            var settings = _core.gemLook;
            _source ??= FindSource();
            if (settings == null || !settings.enabled || _source == null || !_source.enabled || !_source.gameObject.activeInHierarchy || _source.sprite == null)
            {
                if (_overlay != null)
                {
                    _overlay.enabled = false;
                }

                return;
            }

            if (!EnsureOverlay())
            {
                return;
            }

            _overlay.sprite = _source.sprite;
            _overlay.flipX = _source.flipX;
            _overlay.flipY = _source.flipY;
            _overlay.drawMode = _source.drawMode;
            if (_source.drawMode != SpriteDrawMode.Simple)
            {
                _overlay.size = _source.size;
            }

            // 宝石の色＝コアの絵の色（HP で汚れていく色）。
            _overlay.color = _source.color;
            _overlay.sortingLayerID = _source.sortingLayerID;
            _overlay.sortingOrder = _source.sortingOrder + 1;
            _overlay.enabled = true;

            var health01 = CoreDangerLook.PreviewHealth01 >= 0f
                ? Mathf.Clamp01(CoreDangerLook.PreviewHealth01)
                : _core.maxHealth > 0f ? Mathf.Clamp01(_core.CurrentHealth / _core.maxHealth) : 1f;

            _overlay.GetPropertyBlock(_block);
            _block.SetVector(BoundsId, LocalBounds(_overlay));
            _block.SetColor(GlowColorId, settings.glowColor);
            _block.SetColor(HurtColorId, settings.hurtColor);
            _block.SetColor(CrackColorId, settings.crackColor);
            _block.SetVector(LookId, new Vector4(settings.opacity, settings.refraction, settings.glowStrength, settings.rimStrength));
            _block.SetVector(DetailId, new Vector4(settings.highlight, settings.caustic, settings.crackWidth, 0f));
            _block.SetVector(StateId, new Vector4(
                health01,
                CoreGemMath.Damage(health01),
                CoreGemMath.CrackAmount(health01, settings.crackStartHealth01),
                CoreGemMath.HitFlash(Time.time - _lastHitTime, settings.hitFlashSeconds)));
            _block.SetVector(PulseId, new Vector4(settings.calmPulsePerSecond, settings.hurtPulsePerSecond, 0f, 0f));
            _overlay.SetPropertyBlock(_block);
        }

        private SpriteRenderer FindSource()
        {
            return TryGetComponent<SpriteRenderer>(out var own) ? own : GetComponentInChildren<SpriteRenderer>();
        }

        private bool EnsureOverlay()
        {
            if (_overlay != null)
            {
                return true;
            }

            if (_material == null)
            {
                var shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                {
                    return false;
                }

                _material = new Material(shader) { name = "CoreGem (runtime)", hideFlags = HideFlags.DontSave };
            }

            // 元の絵と同じ場所・同じ立ち方にするため、元の絵の子に置く（Play中だけの物。ビルボードの差し替え対象からは外す）。
            var go = new GameObject("[CoreGem]");
            go.transform.SetParent(_source.transform, false);
            go.AddComponent<BillboardIgnore>();
            _overlay = go.AddComponent<SpriteRenderer>();
            _overlay.sharedMaterial = _material;
            _block = new MaterialPropertyBlock();
            return true;
        }

        // 反転前の、ローカル座標での絵の外形（BillboardMaterialSwapper と同じ考え方）。
        private static Vector4 LocalBounds(SpriteRenderer renderer)
        {
            if (renderer.drawMode == SpriteDrawMode.Simple)
            {
                var bounds = renderer.sprite.bounds;
                return new Vector4(bounds.center.x, bounds.center.y, bounds.extents.x, bounds.extents.y);
            }

            var size = renderer.size;
            var pivot = renderer.sprite.pivot / renderer.sprite.pixelsPerUnit;
            var spriteSize = renderer.sprite.rect.size / renderer.sprite.pixelsPerUnit;
            var normalizedPivot = new Vector2(pivot.x / Mathf.Max(spriteSize.x, 1e-4f), pivot.y / Mathf.Max(spriteSize.y, 1e-4f));
            var center = new Vector2((0.5f - normalizedPivot.x) * size.x, (0.5f - normalizedPivot.y) * size.y);
            return new Vector4(center.x, center.y, size.x * 0.5f, size.y * 0.5f);
        }
    }
}
