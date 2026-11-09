using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// スキル調整スタジオ（2026-11-09）。4人のキャラのスキル（赤・青・緑・黄）の「レベルデザイン」と「見た目」を1か所で調整する。
    /// 要塞デザイナーとは別のツール。画面の骨組みは StudioKit（ステージ背景スタジオ・UIスタジオと共通）。
    /// 左のナビで色を選び、ページの上の切り替えで欄を選ぶ。各ページ・パネルは独立したクラスで、ここは並べるだけ。
    /// </summary>
    public sealed class SkillStudioWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.25;

        private SkillStudioContext _context;
        private StudioShell _shell;
        private readonly Dictionary<SkillColor, SkillPage> _pages = new Dictionary<SkillColor, SkillPage>();
        private double _nextTick;

        [MenuItem("Tools/スキル調整スタジオを開く", priority = 30)]
        public static void Open()
        {
            var window = GetWindow<SkillStudioWindow>();
            window.titleContent = new GUIContent("スキル調整スタジオ");
            window.minSize = new Vector2(760, 540);
            window.Show();
        }

        /// <summary>開いて、指定の色のページを出す（他のツールから使う）。</summary>
        public static void OpenColor(SkillColor color)
        {
            Open();
            var window = GetWindow<SkillStudioWindow>();
            if (window._pages.TryGetValue(color, out var page))
            {
                window._shell.Show(page);
            }
        }

        public void CreateGUI()
        {
            _context = new SkillStudioContext();
            _shell = new StudioShell("技", "スキル調整スタジオ", "4人のスキルの強さと見た目を整える",
                "マウスを項目に乗せると、ここに説明が出ます。");
            _pages.Clear();
            foreach (var color in SkillColors.All)
            {
                _pages[color] = new SkillPage(_context, color);
            }

            var common = new SkillCommonPage(_context, _shell, color => _pages[color]);
            foreach (var color in SkillColors.All)
            {
                _shell.AddPage(_pages[color]);
            }

            _shell.AddPage(common);
            _shell.Show(_pages[SkillColor.Red]);

            rootVisualElement.Clear();
            rootVisualElement.Add(_shell);
            _nextTick = 0;
        }

        // 定期更新は OnInspectorUpdate で行う（ステージ背景スタジオと同じ理由: Play の前後でも止まらない）。
        private void OnInspectorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextTick)
            {
                return;
            }

            _nextTick = EditorApplication.timeSinceStartup + RefreshSeconds;
            if (_context == null || _shell == null)
            {
                CreateGUI();
                return;
            }

            _context.Tick();
            _shell.Tick();
        }

        private void OnFocus()
        {
            _nextTick = 0;
        }
    }
}
