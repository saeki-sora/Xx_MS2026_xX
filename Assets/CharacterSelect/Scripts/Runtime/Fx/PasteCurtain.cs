using System;
using System.Collections.Generic;
using MS2026.UI.Game;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面全体を歯磨き粉で上から覆って、次の画面へ切り替える幕（タイトルのオープニングの「どろーっ」と同じ動き）。
    /// ・幕の絵を縦の列に分け、列ごとに少しずつ遅れて上から垂れてくる（先端がでこぼこ）
    /// ・覆いきったら次の画面の読み込みを始め、読み込みが終わるまで覆ったまま
    /// ・そのまま下へ流れ落ちて、次の画面が見える
    /// ロビー（<see cref="GameSession"/>）がキャラ選択に入るとき、キャラ選択の「GO!」からゲームへ進むときに使う
    /// （起動時に <see cref="GameSession.CharacterSelectCurtain"/> と <see cref="GameSession.GameStartCurtain"/> に登録する）。
    /// シーンをまたいで残る（ロビー → キャラ選択の間も消えない）。設定は Resources の <see cref="PasteCurtainSettings"/>。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PasteCurtain : MonoBehaviour
    {
        private enum Stage
        {
            Idle,
            Covering,
            Holding,
            Revealing
        }

        private const float HeightRatio = 1.35f;

        private static PasteCurtain _instance;
        private static PasteCurtainSettings _loadedSettings;

        private readonly List<RawImage> _columns = new List<RawImage>();
        private readonly List<float> _lags = new List<float>();
        private PasteCurtainSettings _settings;
        private RectTransform _area;
        private Stage _stage;
        private float _t;
        private float _held;
        private Action _onCovered;
        private Func<bool> _isReady;

        /// <summary>幕が動いている（覆っている・流れ落ちている）。</summary>
        public static bool IsPlaying => _instance != null && _instance._stage != Stage.Idle;

        /// <summary>画面が幕で隠れている（垂れてくる途中・覆ったまま）。流れ落ち始めたら false。</summary>
        public static bool IsCovering => _instance != null && (_instance._stage == Stage.Covering || _instance._stage == Stage.Holding);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            // ロビーがキャラ選択に入るとき・キャラ選択からゲームへ進むとき、この幕を使ってもらう。
            GameSession.CharacterSelectCurtain = Play;
            GameSession.GameStartCurtain = TryPlayGameStart;
        }

        /// <summary>「GO!」のあとゲームへ進むときも、この幕で切り替えるか（設定の useForGameStart）。</summary>
        public static bool UseForGameStart => Settings.useForGameStart;

        /// <summary>「GO!」のあとゲームへ進むときの幕。設定で OFF なら何もせず false（ロビーのいつもの幕になる）。</summary>
        public static bool TryPlayGameStart(Action onCovered, Func<bool> isReady)
        {
            if (!UseForGameStart)
            {
                return false;
            }

            Play(onCovered, isReady);
            return true;
        }

        private static PasteCurtainSettings Settings
        {
            get
            {
                if (_loadedSettings == null)
                {
                    _loadedSettings = Resources.Load<PasteCurtainSettings>(PasteCurtainSettings.ResourcePath);
                    if (_loadedSettings == null)
                    {
                        _loadedSettings = ScriptableObject.CreateInstance<PasteCurtainSettings>();
                    }
                }

                return _loadedSettings;
            }
        }

        /// <summary>上から覆う → onCovered → isReady が true になるまで覆ったまま → 流れ落ちる。</summary>
        public static void Play(Action onCovered, Func<bool> isReady)
        {
            Ensure().Begin(onCovered, isReady, false);
        }

        /// <summary>キャラ選択のシーンだけで再生したとき、覆った状態から流れ落ちる（設定で OFF にできる）。</summary>
        public static void RevealForStandalone()
        {
            var curtain = Ensure();
            if (curtain._settings.playOnStandalone && !IsPlaying)
            {
                curtain.Begin(null, null, true);
            }
        }

        private static PasteCurtain Ensure()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var settings = Settings;
            var go = new GameObject("[Paste Curtain]");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = settings.sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var area = new GameObject("Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(go.transform, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            _instance = go.AddComponent<PasteCurtain>();
            _instance._settings = settings;
            _instance._area = area;
            _instance.BuildColumns();
            return _instance;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void BuildColumns()
        {
            var count = Mathf.Max(1, _settings.columns);
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject($"Column {i}", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(_area, false);
                rect.anchorMin = new Vector2((float)i / count, 1f);
                rect.anchorMax = new Vector2((float)(i + 1) / count, 1f);
                rect.pivot = new Vector2(0.5f, 0f);
                var image = go.AddComponent<RawImage>();
                image.texture = _settings.texture;
                image.color = _settings.color;
                image.raycastTarget = false;
                image.uvRect = new Rect((float)i / count, 0f, 1f / count, 1f);
                image.enabled = false;
                _columns.Add(image);
                _lags.Add(0f);
            }
        }

        private void Begin(Action onCovered, Func<bool> isReady, bool startCovered)
        {
            // 覆っている途中にもう一度呼ばれたら、今の幕をそのまま使う（覆いきったら両方の合図を出す）。
            if (_stage == Stage.Covering)
            {
                _onCovered += onCovered;
                _isReady = isReady;
                return;
            }

            if (_stage == Stage.Holding && !startCovered)
            {
                onCovered?.Invoke();
                _isReady = isReady;
                return;
            }

            // 隣どうしは似た遅れにして、先端がなだらかに波打つようにする（毎回少し違う形）。
            var seed = UnityEngine.Random.Range(0f, 100f);
            for (var i = 0; i < _lags.Count; i++)
            {
                var n = Mathf.PerlinNoise(i * 0.35f, seed);
                _lags[i] = Mathf.Clamp01((n - 0.2f) / 0.6f);
            }

            _onCovered = onCovered;
            _isReady = isReady;
            _held = 0f;
            if (startCovered)
            {
                _t = 1f;
                _stage = Stage.Holding;
                Apply(1f);
                return;
            }

            _t = 0f;
            _stage = Stage.Covering;
            CharacterSelectEffects.Play(_settings.onCover, Vector3.zero, -1);
            Apply(0f);
        }

        private void Update()
        {
            if (_stage == Stage.Idle)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            switch (_stage)
            {
                case Stage.Covering:
                    _t = Mathf.Min(1f, _t + dt / _settings.dropSeconds);
                    Apply(_t);
                    if (_t >= 1f)
                    {
                        _stage = Stage.Holding;
                        _held = 0f;
                        var covered = _onCovered;
                        _onCovered = null;
                        covered?.Invoke();
                    }

                    break;
                case Stage.Holding:
                    _held += dt;
                    Apply(1f);
                    if (_held >= _settings.holdSeconds && (_isReady == null || _isReady()))
                    {
                        _stage = Stage.Revealing;
                        _t = 0f;
                        CharacterSelectEffects.Play(_settings.onReveal, Vector3.zero, -1);
                    }

                    break;
                case Stage.Revealing:
                    _t = Mathf.Min(1f, _t + dt / _settings.revealSeconds);
                    Apply(1f + _t);
                    if (_t >= 1f)
                    {
                        _stage = Stage.Idle;
                        _isReady = null;
                        foreach (var column in _columns)
                        {
                            column.enabled = false;
                        }
                    }

                    break;
            }
        }

        // cover: 0 = 画面の上に隠れている、1 = 画面を覆っている、2 = 画面の下へ流れ落ちた（タイトルのオープニングと同じ計算）。
        private void Apply(float cover)
        {
            var screenHeight = _area.rect.height > 1f ? _area.rect.height : 1080f;
            var height = screenHeight * HeightRatio;
            // 覆いきったとき、絵の下のふち（垂れた部分）が画面の下からはみ出す量。
            var coveredDrop = screenHeight + (height - screenHeight) * 0.6f;
            var spread = Mathf.Clamp(_settings.unevenness, 0f, 0.9f);
            for (var i = 0; i < _columns.Count; i++)
            {
                float drop;
                if (cover <= 1f)
                {
                    var local = Mathf.Clamp01((cover - _lags[i] * spread) / (1f - spread));
                    drop = coveredDrop * (0.5f - 0.5f * Mathf.Cos(local * Mathf.PI));
                }
                else
                {
                    var local = Mathf.Clamp01((cover - 1f - _lags[i] * spread) / (1f - spread));
                    drop = coveredDrop + (screenHeight + height) * local * local * local;
                }

                var column = _columns[i];
                // 横は列の幅＋1px（すき間ができないように）。
                column.rectTransform.sizeDelta = new Vector2(1f, height);
                column.rectTransform.anchoredPosition = new Vector2(0f, -drop);
                column.enabled = drop > 0.5f && drop < coveredDrop + screenHeight + height;
            }
        }
    }
}
