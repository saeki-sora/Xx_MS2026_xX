using MS2026.Fortress.Billboards;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「絵を立たせる(ビルボード)」の設定。視点プリセットごとに持つので、プリセットを切り替えればいつでも平らな絵に戻せる。
    /// 斜め見下ろし用のプリセットを、今のプリセットを複製してワンクリックで作ることもできる(元のプリセットは残る)。
    /// </summary>
    public sealed class CameraBillboardPanel
    {
        private const float ObliqueTiltDegrees = 45f;

        private static readonly string[] AnchorLabels = { "足元を地面に固定", "絵の中心を軸に" };

        private static readonly (BillboardTargets target, string label, string tooltip)[] TargetToggles =
        {
            (BillboardTargets.Enemies, "敵（群衆）", "群衆の敵と、個別に置いた敵。"),
            (BillboardTargets.Core, "コア", "中央のコアクリスタル。"),
            (BillboardTargets.TurretBody, "砲台の本体", "砲身・照準線・レーザーは向きが正確に分かるよう地面に寝たままです。"),
            (BillboardTargets.Destructibles, "破壊可能物・スマッシュボール", "HPバーなど子の絵も含みます。")
        };

        private SerializedObject _serialized;

        public void Draw(CameraTabContext context)
        {
            if (!CameraGui.Section("billboard", "絵を立たせる（ビルボード）"))
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "斜め見下ろしの視点で、地面に寝ている平らな絵をカメラに向けて立たせます。設定は視点プリセットごとなので、" +
                "真上からのプリセットに切り替えればいつでも今まで通りの平らな絵に戻せます。4人の視点の向きが違っても、各画面でそれぞれ正面を向きます。",
                MessageType.None);

            var preset = context.Preset;
            if (_serialized == null || _serialized.targetObject != preset)
            {
                _serialized = new SerializedObject(preset);
            }

            _serialized.Update();
            var billboard = _serialized.FindProperty(nameof(CameraViewPreset.billboard));

            EditorGUI.BeginChangeCheck();
            DrawSettings(billboard);
            var changed = EditorGUI.EndChangeCheck();
            _serialized.ApplyModifiedProperties();

            if (changed && context.Billboards != null)
            {
                context.Billboards.RequestRescan();
            }

            DrawStatus(context);
            DrawObliquePresetButton(context);
        }

        private static void DrawSettings(SerializedProperty billboard)
        {
            var enabled = billboard.FindPropertyRelative(nameof(BillboardSettings.enabled));
            enabled.boolValue = EditorGUILayout.ToggleLeft("このプリセットで絵を立たせる", enabled.boolValue, EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                var stand = billboard.FindPropertyRelative(nameof(BillboardSettings.standAmount));
                EditorGUILayout.PropertyField(stand, new GUIContent("立ち上がり具合", stand.tooltip));

                var anchor = billboard.FindPropertyRelative(nameof(BillboardSettings.anchor));
                anchor.enumValueIndex = EditorGUILayout.Popup(new GUIContent("立たせる軸", anchor.tooltip), anchor.enumValueIndex, AnchorLabels);

                var cutoff = billboard.FindPropertyRelative(nameof(BillboardSettings.alphaCutoff));
                EditorGUILayout.PropertyField(cutoff, new GUIContent("縁の切り落とし", cutoff.tooltip));

                DrawTargets(billboard.FindPropertyRelative(nameof(BillboardSettings.targets)));
            }
        }

        private static void DrawTargets(SerializedProperty targets)
        {
            EditorGUILayout.LabelField("立たせる物", EditorStyles.miniBoldLabel);
            var value = (BillboardTargets)targets.intValue;

            foreach (var (target, label, tooltip) in TargetToggles)
            {
                var on = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), (value & target) == target);
                value = on ? value | target : value & ~target;
            }

            targets.intValue = (int)value;
            EditorGUILayout.LabelField(
                "個別に立たせたい物には BillboardSprite、寝かせたままにしたい物には BillboardIgnore コンポーネントを付けてください。",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawStatus(CameraTabContext context)
        {
            var preset = context.Preset;
            if (!preset.billboard.enabled)
            {
                return;
            }

            if (context.Billboards == null)
            {
                EditorGUILayout.HelpBox("適用する係(BillboardDirector)がシーンにありません。上の「足りない部品を追加」で追加できます。", MessageType.Warning);
            }

            if (!HasAnyTilt(preset))
            {
                EditorGUILayout.HelpBox(
                    "どの視点も真上から見ているので、立たせても見た目は変わりません。視点の「見下ろしの傾き」を付けると立ち上がって見えます。",
                    MessageType.Info);
            }

            if (Application.isPlaying)
            {
                var count = context.Billboards != null ? context.Billboards.Controller.ActiveRendererCount : 0;
                EditorGUILayout.LabelField($"立たせている絵: {count}枚（群衆の敵は別途シェーダーで立たせています）", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Play外では上の4人の画面プレビューで確認できます（Gameビューへの反映はPlay中のみ）。", EditorStyles.miniLabel);
            }
        }

        private static void DrawObliquePresetButton(CameraTabContext context)
        {
            if (!GUILayout.Button(new GUIContent("斜め見下ろし用のプリセットを作る（今のプリセットを複製）",
                    $"今のプリセットを複製し、全員を透視投影・傾き{ObliqueTiltDegrees:0}°にして絵を立たせます。元のプリセットはそのまま残るので、いつでも切り替えて戻せます。")))
            {
                return;
            }

            var source = context.Preset;
            var oblique = CameraPresetEditing.CreatePresetAsset(source.DisplayName + " (斜め見下ろし)", source);
            oblique.billboard.enabled = true;
            CameraPresetEditing.ModifyAll(oblique, "Make Oblique Preset", (_, view) =>
            {
                view = view.WithProjection(CameraProjection.Perspective);
                view.tiltDegrees = ObliqueTiltDegrees;
                return view;
            });
            AssetDatabase.SaveAssetIfDirty(oblique);

            if (Application.isPlaying)
            {
                context.Rig.SetPreset(oblique, 0.8f);
                return;
            }

            Undo.RecordObject(context.Rig, "Assign Camera Preset");
            context.Rig.preset = oblique;
            EditorUtility.SetDirty(context.Rig);
        }

        private static bool HasAnyTilt(CameraViewPreset preset)
        {
            foreach (var viewer in ViewerIndex.All)
            {
                if (preset.GetView(viewer).tiltDegrees > 0.5f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
