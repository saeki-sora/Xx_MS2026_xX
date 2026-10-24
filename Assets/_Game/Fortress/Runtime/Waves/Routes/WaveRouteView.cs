using System.Collections.Generic;
using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 次のウェーブの敵のルートを、地面に赤い線で描く（2026-10-24。アークナイツの発表のような見せ方）。
    /// ・発表の間: 赤い線（敵の多い道ほど太い）＋ 線の上をコアへ流れる白い矢印 ＋ 湧く場所の「！」の印（脈打つ）
    /// ・ウェーブが始まったら 2 秒かけて消える。ほかの段階では出さない
    /// 線は地面（XY平面）に描くので、視点が回っているPCでも正しい場所に出る。各PCが自分で描く（通信なし）。
    /// 起動時に自動でお知らせを受け取り、必要になったらステージのシーン（アクティブなシーン）に「[Wave] Routes」を作る。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class WaveRouteView : MonoBehaviour
    {
        private const float ZOffset = -0.03f;
        private const float ArrowSpacing = 1.6f;
        private const float ArrowSpeed = 3.2f;
        private const float ShowSeconds = 0.3f;
        private const float FadeSeconds = 2f;
        private const int RenderQueue = 2980; // 群衆の敵（2990）より下
        private const float RebuildInterval = 1f; // 発表の間に道が変わる（橋が崩れてコアが落ちる等）ことがあるので、ときどき引き直す

        private static readonly Color LineColor = new Color(1f, 0.16f, 0.22f, 0.62f);
        private static readonly Color EdgeColor = new Color(0.25f, 0f, 0.04f, 0.45f);
        private static readonly Color ArrowColor = new Color(1f, 0.97f, 0.97f, 0.95f);
        private static readonly Color MarkerColor = new Color(1f, 0.22f, 0.28f, 0.95f);

        private static WaveRouteView _instance;
        private static Material _material;
        private static Sprite _arrowSprite;
        private static Sprite _markerSprite;
        private static Sprite _ringSprite;
        private static Sprite _bangSprite;

        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly List<LineRenderer> _edges = new List<LineRenderer>();
        private readonly List<SpriteRenderer> _arrows = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _rings = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _bangs = new List<SpriteRenderer>();
        private float _alpha;
        private float _target;
        private float _speed;
        private float _time;
        private float _nextRebuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            FortressWaves.PhaseChanged += OnPhaseChanged;
        }

        private static void OnPhaseChanged(WavePhase previous, WavePhase next)
        {
            var plan = FortressWaves.Plan;
            if (next == WavePhase.Announce && plan != null && plan.showRoutes)
            {
                WaveRoutes.Build(plan, FortressWaves.WaveIndex, FindDirector(plan));
                Ensure().Show();
            }
            else if (next == WavePhase.Combat)
            {
                if (_instance != null)
                {
                    _instance.FadeOut();
                }
            }
            else
            {
                if (_instance != null)
                {
                    _instance.HideNow();
                }

                WaveRoutes.Clear();
            }
        }

        private static EnemySpawnDirector FindDirector(WavePlan plan)
        {
            foreach (var director in FindObjectsByType<EnemySpawnDirector>(FindObjectsSortMode.None))
            {
                if (director.wavePlan == plan)
                {
                    return director;
                }
            }

            return null;
        }

        private static WaveRouteView Ensure()
        {
            if (_instance != null)
            {
                return _instance;
            }

            // 普通のオブジェクトとして、今のシーン（ステージ）に作る（Play を止めれば消える。DontSave にはしない）。
            var go = new GameObject("[Wave] Routes");
            go.AddComponent<BillboardIgnore>();
            _instance = go.AddComponent<WaveRouteView>();
            return _instance;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Show()
        {
            gameObject.SetActive(true);
            _target = 1f;
            _speed = 1f / ShowSeconds;
            _time = 0f;
            _nextRebuild = RebuildInterval;
            Rebuild();
        }

        private void FadeOut()
        {
            _target = 0f;
            _speed = 1f / FadeSeconds;
        }

        private void HideNow()
        {
            _alpha = _target = 0f;
            WaveRoutes.Alpha = 0f;
            gameObject.SetActive(false);
        }

        private void Rebuild()
        {
            var routes = WaveRoutes.Routes;
            Grow(_edges, routes.Count, "Edge");
            Grow(_lines, routes.Count, "Line");
            for (var i = 0; i < _lines.Count; i++)
            {
                var used = i < routes.Count;
                _lines[i].enabled = _edges[i].enabled = used;
                if (!used)
                {
                    continue;
                }

                var route = routes[i];
                SetPoints(_edges[i], route.points);
                SetPoints(_lines[i], route.points);
                _edges[i].widthMultiplier = route.width + 0.14f;
                _lines[i].widthMultiplier = route.width;
            }

            var arrowCount = 0;
            foreach (var route in routes)
            {
                arrowCount += Mathf.CeilToInt(route.length / ArrowSpacing) + 1;
            }

            GrowSprites(_arrows, arrowCount, "Arrow", ArrowSprite);
            var spawns = WaveRoutes.Spawns;
            GrowSprites(_rings, spawns.Count, "Ring", RingSprite);
            GrowSprites(_markers, spawns.Count, "Marker", MarkerSprite);
            GrowSprites(_bangs, spawns.Count, "Bang", BangSprite);
            for (var i = 0; i < _markers.Count; i++)
            {
                var used = i < spawns.Count;
                _markers[i].enabled = _rings[i].enabled = _bangs[i].enabled = used;
                if (used)
                {
                    var position = new Vector3(spawns[i].Position.x, spawns[i].Position.y, ZOffset * 2f);
                    _markers[i].transform.position = position;
                    _rings[i].transform.position = position;
                    _bangs[i].transform.position = position;
                }
            }
        }

        private void LateUpdate()
        {
            _alpha = Mathf.MoveTowards(_alpha, _target, _speed * Time.deltaTime);
            WaveRoutes.Alpha = _alpha;
            if (_alpha <= 0f && _target <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            _time += Time.deltaTime;
            if (_target > 0f && _time >= _nextRebuild && FortressWaves.Phase == WavePhase.Announce && FortressWaves.Plan != null)
            {
                _nextRebuild = _time + RebuildInterval;
                WaveRoutes.Build(FortressWaves.Plan, FortressWaves.WaveIndex, FindDirector(FortressWaves.Plan));
                Rebuild();
            }

            var routes = WaveRoutes.Routes;
            for (var i = 0; i < routes.Count && i < _lines.Count; i++)
            {
                _lines[i].startColor = _lines[i].endColor = WithAlpha(LineColor, _alpha);
                _edges[i].startColor = _edges[i].endColor = WithAlpha(EdgeColor, _alpha);
            }

            UpdateArrows(routes);
            UpdateMarkers();
        }

        // 線の上を、矢印がコアへ向かって流れる（始まりと終わりの近くは薄く）。
        private void UpdateArrows(IReadOnlyList<WaveRoute> routes)
        {
            var index = 0;
            var offset = Mathf.Repeat(_time * ArrowSpeed, ArrowSpacing);
            foreach (var route in routes)
            {
                for (var d = offset; d < route.length && index < _arrows.Count; d += ArrowSpacing)
                {
                    var arrow = _arrows[index++];
                    var point = WaveRouteShapes.PointAt(route.points, d, out var direction);
                    arrow.enabled = true;
                    arrow.transform.position = new Vector3(point.x, point.y, ZOffset * 1.5f);
                    arrow.transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
                    arrow.transform.localScale = Vector3.one * (route.width * 1.25f);
                    var ends = Mathf.Clamp01(d / 1.5f) * Mathf.Clamp01((route.length - d) / 1.5f);
                    arrow.color = WithAlpha(ArrowColor, _alpha * ends);
                }
            }

            for (; index < _arrows.Count; index++)
            {
                _arrows[index].enabled = false;
            }
        }

        // 湧く場所の「！」: 画面の上向きに立て（視点が回っていても読める）、輪が広がって脈打つ。
        private void UpdateMarkers()
        {
            var up = (Vector3)EnemyScreenAlign.ScreenUp;
            var pulse = Mathf.Repeat(_time * 1.2f, 1f);
            for (var i = 0; i < _markers.Count; i++)
            {
                if (!_markers[i].enabled)
                {
                    continue;
                }

                var rotation = Quaternion.LookRotation(Vector3.forward, up);
                var scale = Vector3.one * (1.6f + 0.12f * Mathf.Sin(_time * 8f));
                _markers[i].transform.rotation = _bangs[i].transform.rotation = rotation;
                _markers[i].transform.localScale = _bangs[i].transform.localScale = scale;
                _markers[i].color = WithAlpha(MarkerColor, _alpha);
                _bangs[i].color = WithAlpha(Color.white, _alpha);
                _rings[i].transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 3.4f, pulse);
                _rings[i].color = WithAlpha(MarkerColor, _alpha * (1f - pulse));
            }
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

        private static void SetPoints(LineRenderer line, List<Vector2> points)
        {
            line.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, ZOffset));
            }
        }

        private void Grow(List<LineRenderer> list, int count, string name)
        {
            while (list.Count < count)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.alignment = LineAlignment.TransformZ; // 地面（XY平面）に寝かせる
                line.numCornerVertices = 4;
                line.numCapVertices = 4;
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = SharedMaterial;
                line.sortingOrder = name == "Edge" ? 0 : 1;
                list.Add(line);
            }
        }

        private void GrowSprites(List<SpriteRenderer> list, int count, string name, Sprite sprite)
        {
            while (list.Count < count)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = SharedMaterial;
                renderer.sortingOrder = name == "Bang" ? 5 : name == "Marker" ? 4 : name == "Arrow" ? 2 : 3;
                renderer.enabled = false;
                list.Add(renderer);
            }
        }

        // ───────── 材質・絵（コードで作る。差し替えの予定がないので画像ファイルにしない） ─────────

        private static Material SharedMaterial
        {
            get
            {
                if (_material == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = RenderQueue };
                }

                return _material;
            }
        }

        private static Sprite ArrowSprite => _arrowSprite != null ? _arrowSprite : _arrowSprite = MakeSprite(64, Chevron);

        private static Sprite MarkerSprite => _markerSprite != null ? _markerSprite : _markerSprite = MakeSprite(96, Exclamation);

        private static Sprite BangSprite => _bangSprite != null ? _bangSprite : _bangSprite = MakeSprite(96, Bang);

        private static Sprite RingSprite => _ringSprite != null ? _ringSprite : _ringSprite = MakeSprite(96, Ring);

        private static Sprite MakeSprite(int size, System.Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // 0〜1 の座標（中心 0.5）で、少しぼかした形の濃さ。
                    var a = 0f;
                    for (var s = 0; s < 4; s++)
                    {
                        a += alphaAt((x + 0.25f + 0.5f * (s & 1)) / size, (y + 0.25f + 0.5f * (s >> 1)) / size);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a / 4f) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // 上向きの「＾」（太い2本の斜め線）。
        private static float Chevron(float u, float v)
        {
            var x = Mathf.Abs(u - 0.5f);
            var center = 0.72f - x * 1.1f;
            return Mathf.Abs(v - center) < 0.12f && x < 0.42f ? 1f : 0f;
        }

        // 赤い丸（「！」の所は抜いて、上に白い「！」を重ねる）。
        private static float Exclamation(float u, float v)
        {
            var dx = u - 0.5f;
            var dy = v - 0.5f;
            return dx * dx + dy * dy > 0.22f || Bang(u, v) > 0f ? 0f : 1f;
        }

        private static float Bang(float u, float v)
        {
            var dx = u - 0.5f;
            var bar = Mathf.Abs(dx) < 0.07f && v > 0.38f && v < 0.82f;
            var dot = dx * dx + (v - 0.24f) * (v - 0.24f) < 0.0064f;
            return bar || dot ? 1f : 0f;
        }

        private static float Ring(float u, float v)
        {
            var r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
            return r < 0.47f && r > 0.4f ? 1f : 0f;
        }
    }
}
