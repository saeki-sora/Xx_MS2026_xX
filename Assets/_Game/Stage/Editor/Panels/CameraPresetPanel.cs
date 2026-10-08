using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// カメラの見え方プリセットをカードで並べ、押すだけで切り替える（ステージにも記録する）。
    /// 「P1の目で見る」でシーンビューをそのプレイヤーの目線に合わせられる。
    /// </summary>
    public sealed class CameraPresetPanel : VisualElement
    {
        private readonly StageStudioContext _context;
        private readonly VisualElement _grid;
        private readonly List<(CameraViewPreset preset, VisualElement card)> _cards = new List<(CameraViewPreset, VisualElement)>();
        private double _nextScan;

        public CameraPresetPanel(StageStudioContext context)
        {
            _context = context;
            _grid = StudioUi.Styled(new VisualElement(), "sk-grid");
            Add(_grid);

            var eyes = StudioUi.Row();
            eyes.Add(StudioUi.Styled(new Label("この目線でシーンを編集: "), "sk-muted"));
            foreach (var viewer in ViewerIndex.All)
            {
                var v = viewer;
                var button = StudioUi.Button(ViewerIndex.Label(v), () => LookFrom(v),
                    $"シーンビューを{ViewerIndex.LongLabel(v)}のゲームのカメラと同じ見え方に固定します（遠近・視野角まで同じ）。そのまま背景を動かせます。シーンビューを回す・ずらすと固定が外れます。", small: true);
                if (ViewerIndex.IsPlayer(v))
                {
                    button.style.color = ViewerIndex.Color(v);
                }

                eyes.Add(button);
            }

            Add(eyes);
        }

        public CameraViewPreset Current
        {
            get
            {
                var rig = Rig();
                return rig != null ? rig.preset : _context.Stage != null ? _context.Stage.cameraPreset : null;
            }
        }

        public void Refresh()
        {
            if (EditorApplication.timeSinceStartup >= _nextScan)
            {
                _nextScan = EditorApplication.timeSinceStartup + 3.0;
                RebuildCards();
            }

            var current = Current;
            foreach (var (preset, card) in _cards)
            {
                card.EnableInClassList("sk-card--selected", preset == current);
            }
        }

        private void RebuildCards()
        {
            _grid.Clear();
            _cards.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CameraViewPreset)))
            {
                var preset = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset == null)
                {
                    continue;
                }

                var p1 = preset.GetView(0);
                var body = $"{(p1.IsOrthographic ? "真上から（遠近なし）" : $"斜め {p1.tiltDegrees:0}°・遠近あり")}\n{(string.IsNullOrWhiteSpace(preset.memo) ? "" : preset.memo)}";
                var card = StudioUi.ClickCard(preset.DisplayName, body, () => Apply(preset),
                    "押すと、このカメラの見え方にします（ステージにも記録され、ステージを出すたびにこの見え方になります）。");
                _grid.Add(card);
                _cards.Add((preset, card));
            }

            if (_cards.Count == 0)
            {
                _grid.Add(StudioUi.Note("カメラの見え方プリセットがありません。要塞デザイナーの「カメラ」タブで作れます。", NoteKind.Warn));
            }
        }

        private void Apply(CameraViewPreset preset)
        {
            var stage = _context.Stage;
            if (stage != null)
            {
                Undo.RecordObject(stage, "カメラの見え方を変更");
                stage.cameraPreset = preset;
                EditorUtility.SetDirty(stage);
            }

            var rig = Rig();
            if (rig == null)
            {
                EditorUtility.DisplayDialog("カメラリグがありません",
                    "シーンに FortressCameraRig がありません。要塞デザイナーの「カメラ」タブで「カメラリグをセットアップ」を押してください。ステージには記録しました。", "OK");
                return;
            }

            if (Application.isPlaying)
            {
                rig.SetPreset(preset, rig.viewerSwitchBlendSeconds);
            }
            else
            {
                Undo.RecordObject(rig, "カメラの見え方を変更");
                rig.preset = preset;
                EditorUtility.SetDirty(rig);
                StageSceneService.MarkSceneDirty();
            }
        }

        private static void LookFrom(int viewer)
        {
            StagePlaceSettings.PreviewViewer = viewer;
            StageGameCamera.MarkDirty();
            StageSceneViewSync.Lock(viewer);
        }

        private static FortressCameraRig Rig() =>
            FortressCameraRig.Active != null ? FortressCameraRig.Active : Object.FindFirstObjectByType<FortressCameraRig>();
    }
}
