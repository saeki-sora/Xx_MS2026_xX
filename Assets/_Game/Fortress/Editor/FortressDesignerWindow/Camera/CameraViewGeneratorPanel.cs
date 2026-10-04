using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 4人分の視点をワンクリックでまとめて作る(自分の砲台を下に・回転対称に複製・自動フィット・コピー)。
    /// どれも1回のUndoで戻せる。
    /// </summary>
    public sealed class CameraViewGeneratorPanel
    {
        private bool _fitIncludeSpawnPoints = true;
        private bool _fitIncludeNavigationArea;
        private float _fitPadding = 0.5f;

        public void Draw(CameraTabContext context)
        {
            if (!CameraGui.Section("generate", "4人分をまとめて作る", false))
            {
                return;
            }

            var preset = context.Preset;
            var points = context.Points;
            var selected = context.SelectedViewer;

            CameraToolState.SnapRollTo90 = EditorGUILayout.Toggle(
                new GUIContent("回転を90°単位にそろえる", "「砲台を下に」系の自動設定で、回転角を90°単位に丸めます。"), CameraToolState.SnapRollTo90);

            if (GUILayout.Button(new GUIContent("全員「自分の砲台が画面の下」になるよう回転",
                    "マップ中心から見て各自の砲台が手前(画面下)に来るよう、4人の回転を自動設定します。見る場所・ズームはそのままです。")))
            {
                var snap = CameraToolState.SnapRollTo90 ? 90f : 0f;
                CameraPresetEditing.ModifyPlayers(preset, "Rotate Views To Own Turret", (viewer, view) =>
                {
                    if (points.TryGetTurret(viewer, out var turret))
                    {
                        view.rollDegrees = CameraViewMath.RollToPlaceAtBottom(points.MapCenter, turret, snap);
                    }

                    return view;
                });
            }

            if (GUILayout.Button(new GUIContent($"{ViewerIndex.LongLabel(selected)}の視点を基準に、回転対称で4人分を作る",
                    "選択中の視点を、各プレイヤーの砲台の位置関係に合わせてマップ中心まわりに回して複製します。砲台が見つからない人は90°ずつ回します。")))
            {
                GenerateRotationalSymmetry(preset, points, selected);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent($"{ViewerIndex.LongLabel(selected)}の視点を全員にコピー")))
                {
                    var source = preset.GetView(selected);
                    CameraPresetEditing.ModifyAll(preset, "Copy View To All", (_, _) => source);
                }

                if (GUILayout.Button(new GUIContent("ズームだけ全員にそろえる", "選択中の視点の映る広さ・投影方式・距離・傾きを全員に適用します(見る場所と回転はそのまま)。")))
                {
                    var source = preset.GetView(selected);
                    CameraPresetEditing.ModifyAll(preset, "Match Zoom", (_, view) => MatchZoom(view, source));
                }
            }

            DrawFit(context);
        }

        private void DrawFit(CameraTabContext context)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("自動フィット（コアと砲台が安全域に収まるよう、中心とズームを自動調整）", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _fitIncludeSpawnPoints = EditorGUILayout.ToggleLeft("敵の出現地点も収める", _fitIncludeSpawnPoints);
                _fitIncludeNavigationArea = EditorGUILayout.ToggleLeft("経路フィールド全体も収める", _fitIncludeNavigationArea);
            }

            _fitPadding = EditorGUILayout.Slider(new GUIContent("余白", "収めた上で追加する余白(ワールド単位)。"), _fitPadding, 0f, 10f);

            var fitPoints = context.Points.GetFitPoints(_fitIncludeSpawnPoints, _fitIncludeNavigationArea);
            using (new EditorGUI.DisabledScope(fitPoints.Count == 0))
            using (new EditorGUILayout.HorizontalScope())
            {
                var aspect = context.Aspect;
                var safeRatio = context.Guides.safeAreaRatio;

                if (GUILayout.Button($"{ViewerIndex.LongLabel(context.SelectedViewer)}だけフィット"))
                {
                    var view = context.Preset.GetView(context.SelectedViewer);
                    CameraPresetEditing.SetView(context.Preset, context.SelectedViewer,
                        CameraViewMath.FitToPoints(view, aspect, fitPoints, safeRatio, _fitPadding), "Fit Camera View");
                }

                if (GUILayout.Button("全員フィット"))
                {
                    CameraPresetEditing.ModifyAll(context.Preset, "Fit All Camera Views",
                        (_, view) => CameraViewMath.FitToPoints(view, aspect, fitPoints, safeRatio, _fitPadding));
                }
            }

            if (fitPoints.Count == 0)
            {
                EditorGUILayout.HelpBox("シーンにコア・砲台が見つからないため、自動フィットできません。", MessageType.None);
            }
        }

        private static void GenerateRotationalSymmetry(CameraViewPreset preset, CameraScenePoints points, int sourceViewer)
        {
            var source = preset.GetView(sourceViewer);
            var pivot = points.MapCenter;
            // 全体視点を基準にしたときは「P1の向き」の視点として扱う。
            var reference = ViewerIndex.IsPlayer(sourceViewer) ? sourceViewer : 0;
            var sourceAngle = TurretAngle(points, reference, pivot, reference * 90f);

            CameraPresetEditing.ModifyPlayers(preset, "Generate Symmetric Views", (viewer, _) =>
            {
                var delta = TurretAngle(points, viewer, pivot, viewer * 90f) - sourceAngle;
                var view = source;
                view.center = CameraViewMath.RotateAround(source.center, pivot, delta);
                view.rollDegrees = source.rollDegrees + delta;
                return view;
            });
        }

        private static float TurretAngle(CameraScenePoints points, int viewer, Vector2 pivot, float fallback)
        {
            if (!points.TryGetTurret(viewer, out var turret) || (turret - pivot).sqrMagnitude < 1e-6f)
            {
                return fallback;
            }

            var direction = turret - pivot;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static CameraViewSettings MatchZoom(CameraViewSettings view, CameraViewSettings source)
        {
            view.projection = source.projection;
            view.orthographicSize = source.orthographicSize;
            view.fieldOfView = source.fieldOfView;
            view.distance = source.distance;
            view.tiltDegrees = source.tiltDegrees;
            return view;
        }
    }
}
