using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// サウンドスタジオ（2026-11-22）。遊ぶ流れの順にシーンを並べ、各タイミングの SE・声・BGM を入れる・試聴する・音量を合わせる。
    /// 画面の骨組みは StudioKit（ほかのスタジオと共通）。ページ・パネルは独立したクラスで、ここは並べるだけ。
    /// </summary>
    public sealed class SoundStudioWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.25;
        private const string StylePath = "Assets/_Tools/SoundStudio/Editor/SoundStudio.uss";

        private SoundStudioContext _context;
        private StudioShell _shell;
        private readonly Dictionary<SoundPageId, IStudioPage> _pages = new Dictionary<SoundPageId, IStudioPage>();
        private PlayedSlotIndex _played;
        private double _nextTick;
        private bool _scanPending;

        [MenuItem("Tools/サウンドスタジオ", priority = 31)]
        public static void Open()
        {
            var window = GetWindow<SoundStudioWindow>();
            window.titleContent = new GUIContent("サウンドスタジオ");
            window.minSize = new Vector2(760, 540);
            window.Show();
        }

        public void CreateGUI()
        {
            _context = new SoundStudioContext { Notify = message => ShowNotification(new GUIContent(message), 4) };
            _context.GoToRequested += slot =>
            {
                if (_pages.TryGetValue(slot.Page, out var page))
                {
                    _shell.Show(page);
                }
            };

            _shell = new StudioShell("音", "サウンドスタジオ", "場面ごとに、鳴らす音を入れて聞いて合わせる",
                "マウスを項目に乗せると、ここに説明が出ます。");
            var sheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(StylePath);
            if (sheet != null)
            {
                _shell.styleSheets.Add(sheet);
            }

            _shell.HeaderSlot.Add(new SoundHeader(_context));

            _pages.Clear();
            _pages[SoundPageId.Start] = new SoundStartPage(_context, id => _shell.Show(_pages[id]));
            foreach (SoundPageId id in System.Enum.GetValues(typeof(SoundPageId)))
            {
                if (id != SoundPageId.Start)
                {
                    _pages[id] = new SoundScenePage(_context, id);
                }
            }

            foreach (var page in _pages.Values)
            {
                _shell.AddPage(page);
            }

            // 流れの場面の後に「音のバランス」（うるさい所・大きさのばらつき）。
            _shell.AddPage(new SoundBalancePage(_context));

            _shell.Show(_pages[SoundPageId.Start]);
            rootVisualElement.Clear();
            rootVisualElement.Add(_shell);

            _played?.Dispose();
            _played = new PlayedSlotIndex(_context, _shell);
            _nextTick = 0;

            // 調べるのは画面ができた後（調べている途中で失敗しても、画面は残り、何度も調べ直さない）。
            ScheduleScan();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // Play 中に調べた結果は、開いていないシーンの欄が抜けていて、シーンの部品も Play 用の物を指している。
        // 止めたら調べ直す。
        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                ScheduleScan();
            }
        }

        // 調べ直しの予約。少し後（delayCall）か、窓の定期更新の早い方で1回だけ調べる
        // （Unity が裏に回っていると delayCall が遅れることがあるため、両方で拾う）。
        private void ScheduleScan()
        {
            _scanPending = true;
            EditorApplication.delayCall -= RunScan;
            EditorApplication.delayCall += RunScan;
        }

        private void RunScan()
        {
            if (this == null || _context == null || !_scanPending || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
            {
                return;
            }

            _scanPending = false;
            _context.TryRescan();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall -= RunScan;
            _played?.Dispose();
            _played = null;
            _context?.Preview.Dispose();
        }

        // 定期更新は OnInspectorUpdate で行う（ほかのスタジオと同じ理由: Play の前後でも止まらない）。
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

            if (_scanPending)
            {
                RunScan();
            }

            _shell.Tick();
            _played?.Tick();
        }
    }
}
