using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>視点プリセット(4人分の視点セット)の切り替え・作成・複製・名前やメモの編集。</summary>
    public sealed class CameraPresetPanel
    {
        private const float SwitchBlendSecondsDefault = 0.6f;

        private float _switchBlendSeconds = SwitchBlendSecondsDefault;
        private List<CameraViewPreset> _projectPresets;
        private double _nextScanTime;

        public void Draw(CameraTabContext context)
        {
            if (!CameraGui.Section("preset", "視点プリセット（4人分の視点セット）"))
            {
                return;
            }

            var rig = context.Rig;
            var preset = context.Preset;

            EditorGUI.BeginChangeCheck();
            var picked = (CameraViewPreset)EditorGUILayout.ObjectField("使用中のプリセット", preset, typeof(CameraViewPreset), false);
            if (EditorGUI.EndChangeCheck() && picked != null)
            {
                Use(rig, picked, 0f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新規作成"))
                {
                    Use(rig, CameraPresetEditing.CreatePresetAsset("CameraViewPreset_New"), 0f);
                }

                if (GUILayout.Button(new GUIContent("複製して編集", "今のプリセットを複製し、複製の方をリグに割り当てます。元は残るので比較できます。")))
                {
                    Use(rig, CameraPresetEditing.CreatePresetAsset(preset.DisplayName + " (コピー)", preset), 0f);
                }

                if (GUILayout.Button("アセットを表示"))
                {
                    EditorGUIUtility.PingObject(preset);
                }
            }

            DrawPresetFields(preset);
            DrawProjectPresets(rig, preset);
        }

        private static void DrawPresetFields(CameraViewPreset preset)
        {
            EditorGUI.BeginChangeCheck();
            var presetName = EditorGUILayout.TextField("表示名", preset.presetName);
            var memo = EditorGUILayout.TextField(new GUIContent("メモ", "どんな意図の視点かを書いておくと比較しやすくなります。"), preset.memo);
            var nearClip = EditorGUILayout.FloatField(new GUIContent("描画する最短距離(Near)", "これより近い物は映りません。"), preset.nearClip);
            var farClip = EditorGUILayout.FloatField(new GUIContent("描画する最長距離(Far)", "これより遠い物は映りません。"), preset.farClip);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(preset, "Edit Camera Preset");
                preset.presetName = presetName;
                preset.memo = memo;
                preset.nearClip = Mathf.Max(0.01f, nearClip);
                preset.farClip = Mathf.Max(preset.nearClip + 0.01f, farClip);
                EditorUtility.SetDirty(preset);
            }
        }

        private void DrawProjectPresets(FortressCameraRig rig, CameraViewPreset current)
        {
            var presets = GetProjectPresets();
            if (presets.Count <= 1)
            {
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("プロジェクト内のプリセット", EditorStyles.miniBoldLabel);

            if (Application.isPlaying)
            {
                _switchBlendSeconds = EditorGUILayout.Slider(
                    new GUIContent("切り替えの補間秒数", "Play中に切り替えると、この秒数でなめらかに遷移します(ウェーブ切替時などの演出確認用)。"),
                    _switchBlendSeconds, 0f, 3f);
            }

            foreach (var preset in presets)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var isCurrent = preset == current;
                    EditorGUILayout.LabelField((isCurrent ? "▶ " : "   ") + preset.DisplayName, isCurrent ? EditorStyles.boldLabel : EditorStyles.label);

                    using (new EditorGUI.DisabledScope(isCurrent))
                    {
                        if (GUILayout.Button(Application.isPlaying ? "なめらかに切替" : "使う", GUILayout.Width(110)))
                        {
                            Use(rig, preset, Application.isPlaying ? _switchBlendSeconds : 0f);
                        }
                    }
                }
            }
        }

        private List<CameraViewPreset> GetProjectPresets()
        {
            if (_projectPresets != null && EditorApplication.timeSinceStartup < _nextScanTime)
            {
                return _projectPresets;
            }

            _projectPresets = new List<CameraViewPreset>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CameraViewPreset)))
            {
                var preset = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset != null)
                {
                    _projectPresets.Add(preset);
                }
            }

            _nextScanTime = EditorApplication.timeSinceStartup + 2.0;
            return _projectPresets;
        }

        private void Use(FortressCameraRig rig, CameraViewPreset preset, float blendSeconds)
        {
            _projectPresets = null;
            if (rig == null || preset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                rig.SetPreset(preset, blendSeconds);
                return;
            }

            Undo.RecordObject(rig, "Assign Camera Preset");
            rig.preset = preset;
            EditorUtility.SetDirty(rig);
        }
    }
}
