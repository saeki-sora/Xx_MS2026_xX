using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MS2026.UI
{
    /// <summary>
    /// UIの置き場所（シーンに1つ、「[UI]」）。レイヤー（段）ごとに Canvas を持ち、画面（UiScreen）を名前で開け閉めする。
    /// ・シーンに置いた画面と、一覧（UiScreenCatalog）のPrefabの画面のどちらも名前で開ける
    /// ・戻る（Esc / ゲームパッドのB）で一番手前の「戻るで閉じる」画面を閉じる
    /// ・幕（UiScreenTransition）を使った画面の切り替え・シーンの切り替え
    /// レイヤーの Canvas は編集中もシーンに置かれるので、画面をシーンで見ながら作れる。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    [AddComponentMenu("UI Studio/UIの置き場所 (UiRoot)")]
    public sealed class UiRoot : MonoBehaviour
    {
        public const string LayerPrefix = "[UI Layer] ";
        public const string OverlayName = "[Transition Overlay]";

        [Tooltip("レイヤー（段）の一覧。")]
        public UiLayerSettings layerSettings;

        [Tooltip("名前で開ける画面のPrefabの一覧（シーンに置いた画面は入れなくても開ける）。")]
        public UiScreenCatalog catalog;

        [Tooltip("ONなら、シーンを切り替えてもUIを残す（タイトル→ゲームの切り替えでも幕が途切れない）。")]
        public bool keepAcrossScenes = true;

        [Tooltip("ONなら、Esc キー・ゲームパッドのBで「戻る」。")]
        public bool backWithEscape = true;

        private readonly Dictionary<string, UiScreen> _screens = new Dictionary<string, UiScreen>();
        private readonly Dictionary<string, Canvas> _layerCanvases = new Dictionary<string, Canvas>();
        private readonly UiScreenStack _stack = new UiScreenStack();
        private UiTransitionOverlay _overlay;

        public static UiRoot Active { get; private set; }

        public IReadOnlyList<UiScreenStack.Item> OpenScreens => _stack.Open;

        public UiTransitionOverlay Overlay => _overlay;

        /// <summary>画面が開いた／閉じたとき（名前）。</summary>
        public event Action<string> ScreenOpened;
        public event Action<string> ScreenClosed;

        private void OnEnable()
        {
            if (Application.isPlaying && Active != null && Active != this)
            {
                // 別のシーンから持ち越したUIがすでにある（DontDestroyOnLoad）。こちらは使わない。
                Destroy(gameObject);
                return;
            }

            Active = this;
            EnsureLayers();
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (keepAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureEventSystem();
            RegisterSceneScreens();
            foreach (var screen in new List<UiScreen>(_screens.Values))
            {
                if (screen.openOnStart)
                {
                    Open(screen.screenId);
                }
                else if (screen.State == UiScreenState.Hidden)
                {
                    screen.SetVisibleImmediate(false);
                }
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !backWithEscape)
            {
                return;
            }

            var back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                       (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back && (_overlay == null || !_overlay.IsPlaying))
            {
                Back();
            }
        }

        // ───────── 画面を開く・閉じる ─────────

        /// <summary>名前で画面を開く（動きつき）。開けたら true。</summary>
        public bool Open(string screenId, Action onShown = null)
        {
            var screen = Resolve(screenId);
            if (screen == null)
            {
                Debug.LogWarning($"[UI] 画面「{screenId}」が見つかりません。UIスタジオの「画面」ページで名前を確認してください。");
                return false;
            }

            var layer = LayerOf(screen);
            var alreadyVisible = _stack.IsOpen(screen.screenId) && screen.IsVisible;
            foreach (var other in _stack.Push(screen.screenId, screen.layer, layer?.sortOrder ?? 0, layer?.oneAtATime ?? false, screen.closeOnBack))
            {
                CloseInternal(other, null);
            }

            screen.transform.SetAsLastSibling();
            if (alreadyVisible)
            {
                // もう開いている画面は手前に出すだけ（出る動きをやり直さない）。
                onShown?.Invoke();
                return true;
            }

            screen.Show(onShown);
            ScreenOpened?.Invoke(screen.screenId);
            return true;
        }

        public void Close(string screenId, Action onHidden = null)
        {
            if (_stack.Remove(screenId))
            {
                CloseInternal(screenId, onHidden);
            }
            else
            {
                onHidden?.Invoke();
            }
        }

        public void Toggle(string screenId)
        {
            if (IsOpen(screenId))
            {
                Close(screenId);
            }
            else
            {
                Open(screenId);
            }
        }

        public bool IsOpen(string screenId) => _stack.IsOpen(screenId);

        /// <summary>戻る: 一番手前の「戻るで閉じる」画面を閉じる。閉じる物が無ければ false。</summary>
        public bool Back()
        {
            var top = _stack.TopForBack();
            if (top == null)
            {
                return false;
            }

            Close(top);
            return true;
        }

        /// <summary>そのレイヤーの画面を全部閉じる。</summary>
        public void CloseLayer(string layerId)
        {
            foreach (var item in new List<UiScreenStack.Item>(_stack.Open))
            {
                if (item.Layer == layerId)
                {
                    Close(item.Id);
                }
            }
        }

        /// <summary>
        /// 幕を閉じ → 同じレイヤーの画面（と closeScreenId の画面）を閉じて目的の画面を出し → 幕を開ける。
        /// 例: ロビー（メニューの段）から HUD（HUDの段）へは closeScreenId にロビーを渡す。
        /// </summary>
        public void SwitchTo(string screenId, UiScreenTransition transition = null, Action onDone = null, string closeScreenId = null)
        {
            var screen = Resolve(screenId);
            if (screen == null)
            {
                Debug.LogWarning($"[UI] 画面「{screenId}」が見つかりません。");
                return;
            }

            EnsureOverlay().Play(transition, () =>
            {
                CloseImmediate(closeScreenId);
                CloseLayerImmediate(screen.layer);
                var layer = LayerOf(screen);
                _stack.Push(screen.screenId, screen.layer, layer?.sortOrder ?? 0, false, screen.closeOnBack);
                screen.transform.SetAsLastSibling();
                screen.SetVisibleImmediate(true);
                ScreenOpened?.Invoke(screen.screenId);
            }, null, onDone);
        }

        /// <summary>幕を閉じ → シーンを読み込み → 読み込み終わったら幕を開ける。</summary>
        public void LoadScene(string sceneName, UiScreenTransition transition = null, Action onDone = null)
        {
            AsyncOperation loading = null;
            EnsureOverlay().Play(transition, () =>
            {
                _stack.Clear();
                foreach (var screen in _screens.Values)
                {
                    if (screen != null)
                    {
                        screen.SetVisibleImmediate(false);
                    }
                }

                loading = SceneManager.LoadSceneAsync(sceneName);
            }, () => loading != null && loading.isDone, onDone);
        }

        /// <summary>幕だけを再生する（覆い終わったら onCovered、開き終わったら onDone）。試すときや、独自の入れ替えに使う。</summary>
        public void PlayTransition(UiScreenTransition transition, Action onCovered = null, Func<bool> isReady = null, Action onDone = null)
        {
            EnsureOverlay().Play(transition, onCovered, isReady, onDone);
        }

        /// <summary>名前の画面（無ければ一覧から作る）。</summary>
        public UiScreen Get(string screenId) => Resolve(screenId);

        // ───────── レイヤー ─────────

        /// <summary>レイヤーごとの Canvas をそろえる（無い物は作り、順番と大きさの設定を合わせる）。</summary>
        public void EnsureLayers()
        {
            _layerCanvases.Clear();
            if (layerSettings == null)
            {
                return;
            }

            foreach (var layer in layerSettings.layers)
            {
                if (layer == null || string.IsNullOrEmpty(layer.id))
                {
                    continue;
                }

                var canvas = FindOrCreateLayerCanvas(layer.id);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = layer.sortOrder;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = layerSettings.referenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = layerSettings.matchWidthOrHeight;
                _layerCanvases[layer.id] = canvas;
            }
        }

        public Canvas LayerCanvas(string layerId)
        {
            if (_layerCanvases.Count == 0)
            {
                EnsureLayers();
            }

            return layerId != null && _layerCanvases.TryGetValue(layerId, out var canvas) ? canvas : null;
        }

        public UiLayerSettings.Layer LayerOf(UiScreen screen) => layerSettings != null ? layerSettings.Find(screen.layer) : null;

        private Canvas FindOrCreateLayerCanvas(string id)
        {
            var child = transform.Find(LayerPrefix + id);
            if (child == null)
            {
                child = new GameObject(LayerPrefix + id, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).transform;
                child.SetParent(transform, false);
            }

            return child.GetComponent<Canvas>();
        }

        // ───────── 内部 ─────────

        private UiScreen Resolve(string screenId)
        {
            if (string.IsNullOrEmpty(screenId))
            {
                return null;
            }

            if (_screens.TryGetValue(screenId, out var screen) && screen != null)
            {
                return screen;
            }

            RegisterSceneScreens();
            if (_screens.TryGetValue(screenId, out screen) && screen != null)
            {
                return screen;
            }

            var prefab = catalog != null ? catalog.Find(screenId) : null;
            if (prefab == null)
            {
                return null;
            }

            var parent = LayerCanvas(prefab.layer);
            screen = Instantiate(prefab, parent != null ? parent.transform : transform);
            screen.name = prefab.name;
            screen.SetVisibleImmediate(false);
            _screens[screen.screenId] = screen;
            return screen;
        }

        private void RegisterSceneScreens()
        {
            foreach (var screen in GetComponentsInChildren<UiScreen>(true))
            {
                if (!string.IsNullOrEmpty(screen.screenId) && !_screens.ContainsKey(screen.screenId))
                {
                    _screens[screen.screenId] = screen;
                    var parent = LayerCanvas(screen.layer);
                    if (parent != null && screen.transform.parent != parent.transform)
                    {
                        screen.transform.SetParent(parent.transform, false);
                    }
                }
            }
        }

        private void CloseInternal(string screenId, Action onHidden)
        {
            if (_screens.TryGetValue(screenId, out var screen) && screen != null)
            {
                screen.Hide(onHidden);
            }
            else
            {
                onHidden?.Invoke();
            }

            ScreenClosed?.Invoke(screenId);
        }

        private void CloseImmediate(string screenId)
        {
            if (!string.IsNullOrEmpty(screenId) && _stack.Remove(screenId) && _screens.TryGetValue(screenId, out var screen) && screen != null)
            {
                screen.SetVisibleImmediate(false);
                ScreenClosed?.Invoke(screenId);
            }
        }

        private void CloseLayerImmediate(string layerId)
        {
            foreach (var item in new List<UiScreenStack.Item>(_stack.Open))
            {
                if (item.Layer == layerId && _screens.TryGetValue(item.Id, out var screen) && screen != null)
                {
                    _stack.Remove(item.Id);
                    screen.SetVisibleImmediate(false);
                    ScreenClosed?.Invoke(item.Id);
                }
            }
        }

        private UiTransitionOverlay EnsureOverlay()
        {
            if (_overlay != null)
            {
                return _overlay;
            }

            var canvas = LayerCanvas("Transition");
            var parent = canvas != null ? canvas.transform : transform;
            var existing = parent.Find(OverlayName);
            if (existing == null)
            {
                existing = new GameObject(OverlayName, typeof(RectTransform), typeof(RawImage), typeof(UiTransitionOverlay)).transform;
                existing.SetParent(parent, false);
                var rect = (RectTransform)existing;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }

            existing.SetAsLastSibling();
            _overlay = existing.GetComponent<UiTransitionOverlay>();
            return _overlay;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
