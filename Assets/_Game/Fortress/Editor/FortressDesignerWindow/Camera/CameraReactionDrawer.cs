using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>出来事1種類ぶんの演出設定(<see cref="CameraReaction"/>)を、日本語の項目名で描く。</summary>
    public static class CameraReactionDrawer
    {
        private const string AssetBrowserMenu = "Tools/D-Drive/Asset Browser";
        private const string ShakeEditorMenu = "Tools/D-Drive/Editors/Shake / Haptics";

        public static void Draw(SerializedProperty reaction, CameraFeedbackEvent eventType)
        {
            var enabled = reaction.FindPropertyRelative(nameof(CameraReaction.enabled));

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    enabled.boolValue = EditorGUILayout.ToggleLeft(CameraFeedbackLabels.Event(eventType), enabled.boolValue, EditorStyles.boldLabel);
                }

                EditorGUILayout.LabelField(CameraFeedbackLabels.EventDescription(eventType), EditorStyles.miniLabel);

                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    DrawAudience(reaction);
                    DrawTiming(reaction);
                    DrawShake(reaction.FindPropertyRelative(nameof(CameraReaction.shake)));
                    DrawZoom(reaction.FindPropertyRelative(nameof(CameraReaction.zoom)));
                }
            }
        }

        private static void DrawAudience(SerializedProperty reaction)
        {
            var audience = reaction.FindPropertyRelative(nameof(CameraReaction.audience));
            audience.enumValueIndex = EditorGUILayout.Popup(new GUIContent("鳴らす画面", audience.tooltip), audience.enumValueIndex, CameraFeedbackLabels.Audiences);

            if (audience.enumValueIndex == (int)CameraFeedbackAudience.Everyone)
            {
                Field(reaction, nameof(CameraReaction.othersStrength), "関係者以外の強さ");
            }
        }

        private static void DrawTiming(SerializedProperty reaction)
        {
            Field(reaction, nameof(CameraReaction.cooldownSeconds), "連続で鳴らさない秒数");
            var fullAmount = Field(reaction, nameof(CameraReaction.fullStrengthAmount), "強さ100%になる大きさ");
            if (fullAmount.floatValue > 0f)
            {
                Field(reaction, nameof(CameraReaction.minStrength), "最低の強さ");
            }
        }

        private static void DrawShake(SerializedProperty shake)
        {
            EditorGUILayout.Space(2);
            var enabled = shake.FindPropertyRelative(nameof(CameraShakeReaction.enabled));
            enabled.boolValue = EditorGUILayout.ToggleLeft("揺れ", enabled.boolValue, EditorStyles.miniBoldLabel);
            if (!enabled.boolValue)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var backend = shake.FindPropertyRelative(nameof(CameraShakeReaction.backend));
                backend.enumValueIndex = EditorGUILayout.Popup(new GUIContent("揺らし方", backend.tooltip), backend.enumValueIndex, CameraFeedbackLabels.ShakeBackends);
                Field(shake, nameof(CameraShakeReaction.strength), "強さの倍率");

                if (backend.enumValueIndex == (int)CameraShakeBackend.DDrive)
                {
                    DrawDDriveShake(shake);
                }

                var simple = shake.FindPropertyRelative(nameof(CameraShakeReaction.simple));
                EditorGUILayout.LabelField(backend.enumValueIndex == (int)CameraShakeBackend.DDrive ? "簡易揺れ（D-Driveが使えないときの代用）" : "簡易揺れ", EditorStyles.miniLabel);
                Field(simple, nameof(SimpleShakeSettings.amplitude), "揺れ幅");
                Field(simple, nameof(SimpleShakeSettings.rollAmplitude), "回転の揺れ幅(度)");
                Field(simple, nameof(SimpleShakeSettings.frequency), "細かさ(Hz)");
                Field(simple, nameof(SimpleShakeSettings.duration), "長さ(秒)");
            }
        }

        private static void DrawDDriveShake(SerializedProperty shake)
        {
            var shakeId = shake.FindPropertyRelative(nameof(CameraShakeReaction.ddriveShake));
            EditorGUILayout.PropertyField(shakeId, new GUIContent("揺れアセット", shakeId.tooltip));

            var value = shakeId.FindPropertyRelative("value");
            if (value != null && value.ulongValue == 0)
            {
                EditorGUILayout.HelpBox("揺れアセットが未設定です（未設定の間は下の簡易揺れで代用します）。Asset Browser で Shake を作成して選んでください。", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button("Asset Browser を開く"))
                {
                    EditorApplication.ExecuteMenuItem(AssetBrowserMenu);
                }

                if (GUILayout.Button("揺れエディタを開く"))
                {
                    EditorApplication.ExecuteMenuItem(ShakeEditorMenu);
                }
            }
        }

        private static void DrawZoom(SerializedProperty zoom)
        {
            EditorGUILayout.Space(2);
            var enabled = zoom.FindPropertyRelative(nameof(CameraZoomReaction.enabled));
            enabled.boolValue = EditorGUILayout.ToggleLeft("寄り（一時的なズーム）", enabled.boolValue, EditorStyles.miniBoldLabel);
            if (!enabled.boolValue)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var punch = zoom.FindPropertyRelative(nameof(CameraZoomReaction.punch));
                Field(punch, nameof(ZoomPunchSettings.zoomAmount), "寄る量(負で引き)");
                Field(punch, nameof(ZoomPunchSettings.focusTowardSource), "出来事の場所へ寄せる");
                Field(punch, nameof(ZoomPunchSettings.attackSeconds), "寄り切るまで(秒)");
                Field(punch, nameof(ZoomPunchSettings.holdSeconds), "止まる時間(秒)");
                Field(punch, nameof(ZoomPunchSettings.releaseSeconds), "戻るまで(秒)");
            }
        }

        private static SerializedProperty Field(SerializedProperty parent, string name, string label)
        {
            var property = parent.FindPropertyRelative(name);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
            }

            return property;
        }
    }
}
