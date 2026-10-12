using System;
using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// UIスタジオ。画面（ロビー・HUD…）をレイヤーで重ね、部品を Photoshop のように整理し、
    /// 動き・画面切り替え・ゲームの値とのつなぎを、プログラムなしで作って試す。
    /// 見た目の骨組みは StudioKit（ステージ背景スタジオと共通）。ページは独立したクラスで、ここは並べるだけ。
    /// </summary>
    public sealed class UiStudioWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.25;

        private UiStudioContext _context;
        private StudioShell _shell;
        private UiStudioHeader _header;
        private double _nextTick;
        private readonly List<IStudioPage> _pages = new List<IStudioPage>();
        private Type _restorePage;

        [MenuItem("Tools/UIスタジオ/UIスタジオを開く", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<UiStudioWindow>();
            window.titleContent = new GUIContent("UIスタジオ");
            window.minSize = new Vector2(780, 560);
            window.Show();
        }

        public static void OpenPage<T>() where T : IStudioPage
        {
            Open();
            GetWindow<UiStudioWindow>()._shell?.ShowPage<T>();
        }

        public void CreateGUI()
        {
            _context?.Dispose();
            _context = new UiStudioContext();
            _context.Tick();

            _shell = new StudioShell("U", "UIスタジオ", "画面を重ねて、動かして、ゲームの値とつなぐ", "マウスを項目に乗せると、ここに説明が出ます。");
            _header = new UiStudioHeader(_context, () => _shell.ShowPage<UiPlayChangesPage>());
            _shell.HeaderSlot.Add(_header);

            _pages.Clear();
            AddPage(new UiStartPage(_context, _shell));
            AddPage(new UiScreensPage(_context));
            AddPage(new UiLayersPage(_context));
            AddPage(new UiMotionPage(_context));
            AddPage(new UiTransitionsPage(_context));
            AddPage(new UiValuesPage(_context));
            AddPage(new UiPlayChangesPage(_context));
            AddPage(new UiCheckPage(_context));
            ShowRestoredPage();

            rootVisualElement.Clear();
            rootVisualElement.Add(_shell);
            _nextTick = 0;
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // OnDisable は Play の前後やレイアウトの切り替えで一時的に隠れたときにも呼ばれるので、
        // ここでは試している動きを止めるだけにし、状態を捨てるのは本当に閉じたとき（OnDestroy）だけにする。
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UiMotionPreview.Stop();
        }

        // Play の始め・終わりには、ページが覚えている物（Play 中だけの画面・部品）が消えたり入れ替わったりする。
        // 古い物を触り続けないよう、開いているページはそのままに、中身をすべて作り直す。
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode && state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            _restorePage = _shell?.Current?.GetType();
            var selectedId = _context?.SelectedScreen != null ? _context.SelectedScreen.screenId : null;
            var selectedWasAsset = _context?.SelectedScreen != null && EditorUtility.IsPersistent(_context.SelectedScreen);
            var selectedAsset = selectedWasAsset ? _context.SelectedScreen : null;
            CreateGUI();
            if (selectedAsset != null)
            {
                _context.SelectScreen(selectedAsset);
            }
            else if (!string.IsNullOrEmpty(selectedId))
            {
                _context.SelectScreenById(selectedId);
            }
        }

        private void AddPage(IStudioPage page)
        {
            _pages.Add(page);
            _shell.AddPage(page);
        }

        private void ShowRestoredPage()
        {
            var type = _restorePage;
            _restorePage = null;
            foreach (var page in _pages)
            {
                if (type != null && page.GetType() == type)
                {
                    _shell.Show(page);
                    return;
                }
            }

            _shell.Show(_pages[0]);
        }

        private void OnDestroy()
        {
            _context?.Dispose();
            _context = null;
        }

        // 定期更新は Unity がウィンドウに1秒10回送る OnInspectorUpdate で行う（UI Toolkit の予約実行は、
        // Play の前後やレイアウトの切り替えで止まり、古い表示が残ることがあるため）。
        private void OnInspectorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextTick)
            {
                return;
            }

            _nextTick = EditorApplication.timeSinceStartup + RefreshSeconds;
            OnTick();
        }

        // ウィンドウに戻ってきたら、待たずにすぐ読み直す。
        private void OnFocus()
        {
            _nextTick = 0;
        }

        private void OnTick()
        {
            if (_context == null)
            {
                // 何かの理由で状態が無くなっていたら、画面ごと作り直す（止まったままにしない）。
                CreateGUI();
                return;
            }

            _context.Tick();
            _header.Refresh();
            _shell.Tick();
        }
    }
}
