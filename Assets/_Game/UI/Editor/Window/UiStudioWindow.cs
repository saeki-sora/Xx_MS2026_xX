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
            _header = new UiStudioHeader(_context);
            _shell.HeaderSlot.Add(_header);

            _shell.AddPage(new UiStartPage(_context, _shell));
            _shell.AddPage(new UiScreensPage(_context));
            _shell.AddPage(new UiLayersPage(_context));
            _shell.AddPage(new UiMotionPage(_context));
            _shell.AddPage(new UiTransitionsPage(_context));
            _shell.AddPage(new UiValuesPage(_context));
            _shell.AddPage(new UiCheckPage(_context));
            _shell.ShowPage<UiStartPage>();

            rootVisualElement.Clear();
            rootVisualElement.Add(_shell);
            _nextTick = 0;
        }

        // OnDisable は Play の前後やレイアウトの切り替えで一時的に隠れたときにも呼ばれるので、
        // ここでは試している動きを止めるだけにし、状態を捨てるのは本当に閉じたとき（OnDestroy）だけにする。
        private void OnDisable()
        {
            UiMotionPreview.Stop();
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
