using System;
using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// UIスタジオの各ページが共有する状態: UIの置き場所・画面の一覧・選んでいる画面・編集中の画面・選んでいる部品・サンプル値・点検結果。
    /// 「編集中の画面」は、Prefab の編集画面で開いている画面か、シーンで選んでいる画面。
    /// </summary>
    public sealed class UiStudioContext
    {
        private const string PrefSamples = "MS2026.UiStudio.ShowSamples";
        private const float CheckInterval = 1.5f;

        private readonly List<UiScreen> _screens = new List<UiScreen>();
        private double _nextScan;
        private double _nextCheck;
        private int _samplesVersion = -1;

        public UiStudioContext()
        {
            Selection.selectionChanged += OnSelectionChanged;
            PrefabStage.prefabStageOpened += OnStageChanged;
            PrefabStage.prefabStageClosing += OnStageChanged;
            EditorApplication.hierarchyChanged += MarkScreensDirty;
        }

        public event Action Changed;

        public UiRoot Root { get; private set; }

        /// <summary>画面の一覧（画面の一覧ファイルの Prefab ＋ シーンに置いた画面）。</summary>
        public IReadOnlyList<UiScreen> Screens => _screens;

        /// <summary>画面ページで選んでいる画面（Prefab かシーンの物）。</summary>
        public UiScreen SelectedScreen { get; private set; }

        /// <summary>いま部品を編集できる画面（Prefab の編集画面で開いている物か、シーンの物）。</summary>
        public UiScreen EditingScreen
        {
            get
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.prefabContentsRoot != null && stage.prefabContentsRoot.TryGetComponent<UiScreen>(out var staged))
                {
                    return staged;
                }

                var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<UiScreen>(true) : null;
                if (selected != null && !EditorUtility.IsPersistent(selected))
                {
                    return selected;
                }

                return SelectedScreen != null && !EditorUtility.IsPersistent(SelectedScreen) ? SelectedScreen : null;
            }
        }

        /// <summary>選んでいる部品（編集中の画面の中の物）。</summary>
        public RectTransform SelectedElement
        {
            get
            {
                var go = Selection.activeGameObject;
                var editing = EditingScreen;
                return go != null && editing != null && go.transform is RectTransform rect && go.transform.IsChildOf(editing.transform) ? rect : null;
            }
        }

        public IReadOnlyList<StudioIssue> Issues { get; private set; } = Array.Empty<StudioIssue>();

        public bool ShowSamples
        {
            get => EditorPrefs.GetBool(PrefSamples, true);
            set
            {
                EditorPrefs.SetBool(PrefSamples, value);
                _samplesVersion = -1;
                if (!value && !Application.isPlaying)
                {
                    UiValues.ClearValues();
                }
            }
        }

        public void Dispose()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            PrefabStage.prefabStageOpened -= OnStageChanged;
            PrefabStage.prefabStageClosing -= OnStageChanged;
            EditorApplication.hierarchyChanged -= MarkScreensDirty;
        }

        public void Tick()
        {
            Root = UiStudioSetup.FindRoot();
            var now = EditorApplication.timeSinceStartup;
            if (now >= _nextScan)
            {
                _nextScan = now + 2.0;
                ScanScreens();
            }

            if (now >= _nextCheck)
            {
                _nextCheck = now + CheckInterval;
                Issues = UiChecks.Run(this);
            }

            PublishSamples();
        }

        public void SelectScreen(UiScreen screen)
        {
            SelectedScreen = screen;
            Changed?.Invoke();
        }

        public void MarkScreensDirty()
        {
            _nextScan = 0;
            _nextCheck = 0;
        }

        public void RecheckNow()
        {
            _nextCheck = 0;
            Tick();
        }

        /// <summary>画面を編集できるようにする（Prefab なら Prefab の編集画面を開く、シーンの物なら選んで映す）。</summary>
        public static void OpenForEditing(UiScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(screen))
            {
                AssetDatabase.OpenAsset(screen.gameObject);
                return;
            }

            Selection.activeGameObject = screen.gameObject;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }

        /// <summary>編集中、サンプル値を値の掲示板に書いて、Play せずに見た目を確かめられるようにする。</summary>
        private void PublishSamples()
        {
            if (Application.isPlaying || !ShowSamples)
            {
                return;
            }

            var catalog = UiStudioSetup.ValueCatalog;
            var version = catalog != null ? catalog.GetHashCode() ^ EditorUtility.GetDirtyCount(catalog) : 0;
            if (version == _samplesVersion)
            {
                return;
            }

            _samplesVersion = version;
            if (catalog != null)
            {
                catalog.PublishSamples();
            }
            SceneView.RepaintAll();
        }

        private void ScanScreens()
        {
            _screens.Clear();
            var catalog = UiStudioSetup.Catalog;
            if (catalog != null)
            {
                foreach (var screen in catalog.screens)
                {
                    if (screen != null)
                    {
                        _screens.Add(screen);
                    }
                }
            }

            foreach (var screen in UnityEngine.Object.FindObjectsByType<UiScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!EditorUtility.IsPersistent(screen) && PrefabUtility.GetCorrespondingObjectFromSource(screen) == null)
                {
                    _screens.Add(screen);
                }
            }

            Changed?.Invoke();
        }

        private void OnSelectionChanged() => Changed?.Invoke();

        private void OnStageChanged(PrefabStage stage)
        {
            _nextCheck = 0;
            Changed?.Invoke();
        }
    }
}
