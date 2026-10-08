using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ステージ背景スタジオ。3Dの背景（シャンプー・歯ブラシ…）を取り込み、敵が避けて流れる「通れない範囲」を作り、
    /// 透け・敵の影・熱・揺れ・壊れ方・光・カメラの見え方をまとめて調整する。
    /// 画面の骨組みは StudioKit（UIスタジオと共通）。各ページは独立したクラスで、ここは並べるだけ。
    /// </summary>
    public sealed class StageStudioWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.25;

        private StageStudioContext _context;
        private StudioShell _shell;
        private StageStudioHeader _header;
        private double _nextTick;

        [MenuItem("Tools/ステージ背景/ステージ背景スタジオを開く", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<StageStudioWindow>();
            window.titleContent = new GUIContent("ステージ背景スタジオ");
            window.minSize = new Vector2(760, 540);
            window.Show();
        }

        /// <summary>開いて、指定のページを表示する（他のツールや点検の「直す」から使う）。</summary>
        public static void OpenPage<T>() where T : IStudioPage
        {
            Open();
            GetWindow<StageStudioWindow>()._shell?.ShowPage<T>();
        }

        public void CreateGUI()
        {
            _context?.Dispose();
            _context = new StageStudioContext();
            _context.Tick();
            StageSceneOverlay.Context = _context; // 次の定期更新を待たずに、シーンビューの表示・配置ツールへ渡す

            _shell = new StudioShell("S", "ステージ背景スタジオ", "3Dの背景を置いて、敵の流れ方と見え方を整える",
                "マウスを項目に乗せると、ここに説明が出ます。");
            _header = new StageStudioHeader(_context, () => _shell.InvalidateAll());
            _shell.HeaderSlot.Add(_header);

            _shell.AddPage(new StageStartPage(_context, _shell));
            _shell.AddPage(new StageImportPage(_context));
            _shell.AddPage(new StageLayoutPage(_context));
            _shell.AddPage(new StagePropsPage(_context));
            _shell.AddPage(new StageViewPage(_context));
            _shell.AddPage(new StageLightPage(_context));
            _shell.AddPage(new StageCheckPage(_context));
            _shell.ShowPage<StageStartPage>();

            rootVisualElement.Clear();
            rootVisualElement.Add(_shell);
            _nextTick = 0;
        }

        private void OnEnable()
        {
            StageSceneOverlay.Context = _context;
        }

        // OnDisable は「閉じた」ときだけでなく、Play の前後やレイアウトの切り替え（Game ビューの最大化など）で
        // 一時的に隠れたときにも呼ばれる。ここで状態を捨てると、画面が作り直されないまま更新が止まるので、
        // 状態を捨てるのは本当に閉じたとき（OnDestroy）だけにする。
        private void OnDisable()
        {
            StageSceneOverlay.Context = null;
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
            StageSceneOverlay.Context = _context;
            _header.Refresh();
            _shell.Tick();
        }
    }
}
