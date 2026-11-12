using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Stage;
using MS2026.Stages;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.PlayerFloatShadow
{
    /// <summary>
    /// プレイヤー（砲台）が床から浮いて見えるように、体から離れた影を出す（見た目だけ）。
    /// ・ステージが当てはめられたら（<see cref="StageLayout.Applied"/>）、そのステージのシーンに係のオブジェクトを自動で作る。シーンの編集は要らず、ステージと一緒に消える
    /// ・足もとの丸い影（<see cref="GroundContactShadow"/>）は濃さを 0 にして隠し、代わりに太陽と反対側へずらした影を出す
    /// ・影は床の上の絵だけで、砲台そのものは動かさないので、当たり判定や照準は変わらない。通信もしない
    /// </summary>
    public sealed class PlayerFloatShadowRunner : MonoBehaviour
    {
        private const int ShadowOrder = -200;
        private const float SearchSeconds = 1f;
        private const float DefaultBodySize = 2.6f;

        private sealed class Entry
        {
            public LaserTurret Turret;
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public GroundContactShadow Original;
            public float OriginalDarkness;
            public float BodySize;
            public float Phase;
        }

        private static PlayerFloatShadowRunner _current;
        private static Sprite _sprite;
        private static Material _material;

        private readonly List<Entry> _entries = new List<Entry>();
        private PlayerFloatShadowSettings _settings;
        private float _searchTimer;
        private float _appear;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            StageLayout.Applied -= HandleApplied;
            _current = null;
            _material = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            StageLayout.Applied -= HandleApplied;
            StageLayout.Applied += HandleApplied;
            if (StageLayout.Active != null && StageLayout.Active.IsApplied)
            {
                HandleApplied(StageLayout.Active);
            }
        }

        private static void HandleApplied(StageLayout layout)
        {
            if (_current != null || layout == null || !Application.isPlaying)
            {
                return;
            }

            var go = new GameObject("[Stage] PlayerFloatShadow");
            SceneManager.MoveGameObjectToScene(go, layout.gameObject.scene);
            _current = go.AddComponent<PlayerFloatShadowRunner>();
            _current._settings = PlayerFloatShadowSettings.Load();
        }

        private void OnDestroy()
        {
            RestoreOriginals();
            if (_current == this)
            {
                _current = null;
            }
        }

        private void LateUpdate()
        {
            if (!_settings.enabled)
            {
                RestoreOriginals();
                SetShadowsVisible(false);
                return;
            }

            _searchTimer -= Time.unscaledDeltaTime;
            if (_searchTimer <= 0f)
            {
                _searchTimer = SearchSeconds;
                FindTurrets();
            }

            // 開始演出の間は物が空から落ちてくるので、影を出さない。
            if (StageDropIntro.IsHolding)
            {
                _appear = 0f;
                SetShadowsVisible(false);
                return;
            }

            _appear = Mathf.Min(1f, _appear + Time.deltaTime / Mathf.Max(0.01f, _settings.appearSeconds));
            var light = LightForward();
            for (var i = 0; i < _entries.Count; i++)
            {
                UpdateEntry(_entries[i], light);
            }
        }

        private void FindTurrets()
        {
            var turrets = FindObjectsByType<LaserTurret>(FindObjectsSortMode.None);
            foreach (var turret in turrets)
            {
                var entry = Find(turret);
                if (entry == null)
                {
                    entry = new Entry
                    {
                        Turret = turret,
                        Body = turret.GetComponent<SpriteRenderer>(),
                        Phase = _entries.Count * 0.27f,
                        Shadow = CreateShadow()
                    };
                    _entries.Add(entry);
                }

                // 足もとの影は砲台が起動時に付けるので、見つかるまで探す。
                if (entry.Original == null && turret.TryGetComponent(out entry.Original))
                {
                    entry.OriginalDarkness = entry.Original.darkness;
                }

                if (entry.Original != null)
                {
                    entry.Original.darkness = 0f;
                }
            }
        }

        private Entry Find(LaserTurret turret)
        {
            foreach (var entry in _entries)
            {
                if (entry.Turret == turret)
                {
                    return entry;
                }
            }

            return null;
        }

        private void UpdateEntry(Entry entry, Vector3 light)
        {
            var shadow = entry.Shadow;
            if (entry.Turret == null || !entry.Turret.isActiveAndEnabled || (entry.Body != null && !entry.Body.enabled))
            {
                shadow.enabled = false;
                return;
            }

            var s = _settings;
            if (entry.BodySize <= 0f)
            {
                entry.BodySize = entry.Body != null ? Mathf.Max(entry.Body.bounds.size.x, entry.Body.bounds.size.y) : DefaultBodySize;
            }

            var height = PlayerFloatShadowMath.Height(Time.time, s.hoverHeight, s.bobHeight, s.bobSeconds, entry.Phase);
            var low = Mathf.Max(0f, s.hoverHeight - s.bobHeight);
            var range = s.bobHeight * 2f;
            var high = range > 0.0001f ? (height - low) / range : 0f;
            var offset = PlayerFloatShadowMath.Offset(light, height, s.fallbackDirection, s.minimumOffset, s.maximumOffset);

            var position = entry.Turret.transform.position;
            shadow.transform.SetPositionAndRotation(new Vector3(position.x + offset.x, position.y + offset.y, -0.005f), Quaternion.identity);
            shadow.transform.localScale = Vector3.one * (entry.BodySize * s.size * PlayerFloatShadowMath.Spread(high, s.spreadWhenHigh));
            shadow.color = new Color(0f, 0f, 0f, s.darkness * PlayerFloatShadowMath.Fade(high, s.fadeWhenHigh) * _appear);
            shadow.enabled = true;
        }

        // 影を出す光の向き。太陽を使わないときは真上からの光にして、設定の向きへ出す。
        private Vector3 LightForward()
        {
            if (_settings.followSun && StageRoot.Active != null && StageRoot.Active.sun != null)
            {
                return StageRoot.Active.sun.transform.forward;
            }

            return Vector3.forward;
        }

        private void SetShadowsVisible(bool visible)
        {
            foreach (var entry in _entries)
            {
                if (entry.Shadow != null)
                {
                    entry.Shadow.enabled = visible;
                }
            }
        }

        // 設定を OFF にしたときと、ステージが消えるときに、足もとの影を元に戻す。
        private void RestoreOriginals()
        {
            foreach (var entry in _entries)
            {
                if (entry.Original != null)
                {
                    entry.Original.darkness = entry.OriginalDarkness;
                }

                entry.Original = null;
            }
        }

        private SpriteRenderer CreateShadow()
        {
            var go = new GameObject("Float Shadow");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = SharedMaterial;
            renderer.sprite = SoftCircle;
            renderer.sortingOrder = ShadowOrder;
            renderer.enabled = false;
            return renderer;
        }

        private static Material SharedMaterial
        {
            get
            {
                if (_material == null)
                {
                    _material = new Material(Shader.Find("Sprites/Default")) { name = "PlayerFloatShadow (runtime)", hideFlags = HideFlags.DontSave };
                }

                return _material;
            }
        }

        // 中が濃く、ふちがふんわり消える丸（1×1メートル）。
        private static Sprite SoftCircle
        {
            get
            {
                if (_sprite != null)
                {
                    return _sprite;
                }

                const int Size = 96;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = "PlayerFloatShadow (runtime)",
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
                        var t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.55f);
                        var alpha = 1f - t * t * (3f - 2f * t);
                        pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
                _sprite.name = texture.name;
                _sprite.hideFlags = HideFlags.DontSave;
                return _sprite;
            }
        }
    }
}
