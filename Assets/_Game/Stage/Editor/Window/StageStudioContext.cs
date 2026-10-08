using System;
using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ステージ背景スタジオの各ページが共有する「今の状態」: シーンの置き場所・今のステージ・背景オブジェクトの一覧・選択・点検結果。
    /// 選択はUnityのヒエラルキーの選択と連動する（どちらで選んでも同じ物が選ばれる）。
    /// </summary>
    public sealed class StageStudioContext
    {
        private const float CheckInterval = 1.5f;
        private const string PrefShowFootprints = "MS2026.StageStudio.ShowFootprints";
        private const string PrefShowGaps = "MS2026.StageStudio.ShowGaps";

        private readonly List<StageProp> _props = new List<StageProp>();
        private readonly List<StageProp> _selection = new List<StageProp>();
        private bool _propsDirty = true;
        private double _nextCheck;

        public StageStudioContext()
        {
            EditorApplication.hierarchyChanged += MarkPropsDirty;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Selection.selectionChanged += SyncFromUnitySelection;
            SyncFromUnitySelection();
        }

        public event Action SelectionChanged;

        public StageRoot Root { get; private set; }

        public StageSet Stage => Root != null ? Root.current : null;

        public IReadOnlyList<StageProp> Props
        {
            get
            {
                if (_propsDirty)
                {
                    _propsDirty = false;
                    _props.Clear();
                    _props.AddRange(StageSceneService.CollectProps(Root));
                }

                return _props;
            }
        }

        public IReadOnlyList<StageProp> SelectedProps => _selection;

        public StageProp Primary => _selection.Count > 0 ? _selection[0] : null;

        public IReadOnlyList<StudioIssue> Issues { get; private set; } = Array.Empty<StudioIssue>();

        public bool ShowFootprints
        {
            get => EditorPrefs.GetBool(PrefShowFootprints, true);
            set { EditorPrefs.SetBool(PrefShowFootprints, value); SceneView.RepaintAll(); }
        }

        public bool ShowGaps
        {
            get => EditorPrefs.GetBool(PrefShowGaps, true);
            set { EditorPrefs.SetBool(PrefShowGaps, value); SceneView.RepaintAll(); }
        }

        public void Dispose()
        {
            EditorApplication.hierarchyChanged -= MarkPropsDirty;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Selection.selectionChanged -= SyncFromUnitySelection;
        }

        // Play の始まりと終わりでシーンの物はすべて作り直されるので、待たずに読み直す。
        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode || change == PlayModeStateChange.EnteredPlayMode)
            {
                RecheckNow();
                SyncFromUnitySelection();
            }
        }

        /// <summary>シーンの状態を読み直す（ウィンドウが定期的に呼ぶ）。点検は少し間隔をあけて行う。</summary>
        public void Tick()
        {
            // Unity の != は中の番号（インスタンスID）しか比べない。Play を終えてシーンが読み直されると、
            // 新しい [Stage] が消えた古い物と同じ番号になることがあり、!= では「変わっていない」と判定されて
            // 消えた古い物を持ち続けてしまう（「置き場所がありません」が出たままになる）。なので参照そのものを比べる。
            var root = StageSceneService.FindRoot();
            if (!ReferenceEquals(root, Root))
            {
                Root = root;
                _propsDirty = true;
                _nextCheck = 0;
            }

            if (EditorApplication.timeSinceStartup >= _nextCheck)
            {
                _nextCheck = EditorApplication.timeSinceStartup + CheckInterval;
                Issues = StageChecks.Run(this);
            }
        }

        public void MarkPropsDirty()
        {
            _propsDirty = true;
            _nextCheck = 0;
        }

        /// <summary>シーンの読み直しと点検をすぐにやり直す（直すボタンやステージを置いた後など）。</summary>
        public void RecheckNow()
        {
            _propsDirty = true;
            _nextCheck = 0;
            Tick();
        }

        public void Select(StageProp prop, bool additive = false)
        {
            if (!additive)
            {
                _selection.Clear();
            }

            if (prop != null && !_selection.Contains(prop))
            {
                _selection.Add(prop);
            }

            var objects = new List<UnityEngine.Object>();
            foreach (var p in _selection)
            {
                objects.Add(p.gameObject);
            }

            Selection.objects = objects.ToArray();
            SelectionChanged?.Invoke();
        }

        /// <summary>シーンビューでその物を画面の真ん中に映す。</summary>
        public static void Frame(Component target)
        {
            if (target == null || SceneView.lastActiveSceneView == null)
            {
                return;
            }

            Selection.activeGameObject = target.gameObject;
            SceneView.lastActiveSceneView.FrameSelected();
        }

        private void SyncFromUnitySelection()
        {
            _selection.Clear();
            foreach (var go in Selection.gameObjects)
            {
                var prop = go.GetComponentInParent<StageProp>(true);
                if (prop != null && !_selection.Contains(prop))
                {
                    _selection.Add(prop);
                }
            }

            SelectionChanged?.Invoke();
        }
    }
}
