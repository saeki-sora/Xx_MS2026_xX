using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// CPU 調整スタジオ（メニュー「Tools/CPU/CPU 調整スタジオを開く」）。ページは3つ: 強さ／試合を回す／頭の中。
    /// 中身は CPU の設定（Resources/CpuSettings）を直すだけで、独自の保存の形は持たない。
    /// </summary>
    internal sealed class CpuStudioWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.25;

        private CpuStudioContext _context;
        private StudioShell _shell;
        private double _nextTick;

        [MenuItem("Tools/CPU/CPU 調整スタジオを開く", priority = -10)]
        public static void Open()
        {
            var window = GetWindow<CpuStudioWindow>();
            window.titleContent = new GUIContent("CPU 調整スタジオ");
            window.minSize = new Vector2(640, 480);
            window.Show();
        }

        public void CreateGUI()
        {
            _context?.Dispose();
            _context = new CpuStudioContext();
            _context.Ensure();

            _shell = new StudioShell("C", "CPU 調整スタジオ", "CPU の強さを決めて、試合を回して比べる", "マウスを項目に乗せると、ここに説明が出ます。");
            _shell.AddPage(new CpuStrengthPage(_context));
            _shell.AddPage(new CpuRunPage(_context));
            _shell.AddPage(new CpuLivePage());
            _shell.ShowPage<CpuStrengthPage>();

            rootVisualElement.Clear();
            rootVisualElement.Add(_shell);
            _nextTick = 0;
        }

        private void OnDestroy()
        {
            _context?.Dispose();
            _context = null;
        }

        private void Update()
        {
            if (_shell == null || EditorApplication.timeSinceStartup < _nextTick)
            {
                return;
            }

            _nextTick = EditorApplication.timeSinceStartup + RefreshSeconds;
            _shell.Tick();
        }
    }
}
