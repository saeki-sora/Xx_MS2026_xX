using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>群衆全体の設定（流体らしさ・押し合い・コア到達・描画）の編集とプリセット。</summary>
    public sealed class SwarmSettingsPanel
    {
        private static readonly (string title, (string path, string label)[] fields)[] Sections =
        {
            ("容量", new[]
            {
                ("maxEnemies", "最大同時敵数（再Playで反映）"),
                ("maxFrameDelta", "1フレームの最大時間(秒)")
            }),
            ("移動と密度（渋滞のしやすさ）", new[]
            {
                ("densityRadius", "混み具合を数える範囲"),
                ("densityReferenceCount", "「密集」とみなす数"),
                ("densitySlowdown", "密集時の減速の強さ"),
                ("minSpeedFactor", "密集時の最低速度の割合"),
                ("momentumTransfer", "押されて流れる度合い"),
                ("turnSharpness", "向きが変わる鋭さ")
            }),
            ("押し合い（重なりの押し戻し）", new[]
            {
                ("separationIterations", "押し戻しの反復回数"),
                ("separationStiffness", "押し戻しの強さ"),
                ("personalSpace", "敵同士が保つ距離の倍率"),
                ("maxCorrectionRatio", "1回の最大移動量（半径比）"),
                ("wallStiffness", "壁からの押し出しの強さ"),
                ("maxNeighborsChecked", "1体が調べる周囲の敵の上限（0=無制限）")
            }),
            ("コア到達", new[]
            {
                ("arrivalMode", "到達した敵の扱い"),
                ("arrivalRadius", "到達判定の距離"),
                ("lingerDamagePerSecond", "張り付き時の秒間ダメージ/体")
            }),
            ("湧き", new[]
            {
                ("spawnJitter", "湧き位置のばらつき半径")
            }),
            ("描画", new[]
            {
                ("ySort", "手前の敵を上に描く（Yソート）"),
                ("renderQueue", "描画順（レンダーキュー）"),
                ("hitFlashDuration", "被弾フラッシュの長さ(秒)")
            }),
            ("範囲", new[]
            {
                ("fallbackAreaSize", "NavigationField無し時の範囲")
            })
        };

        private SerializedObject _serialized;
        private bool _foldout = true;

        public void Draw(SwarmSystem system)
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, "群衆の設定（流体らしさ）");
            if (_foldout)
            {
                DrawContents(system);
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawContents(SwarmSystem system)
        {
            if (system.settings == null)
            {
                EditorGUILayout.HelpBox("設定アセットが未割り当てです（既定値で動作中）。", MessageType.Warning);
                if (GUILayout.Button("設定アセットを作成して割り当てる"))
                {
                    Undo.RecordObject(system, "Assign Swarm Settings");
                    system.settings = SwarmTools.CreateSettingsAsset();
                    EditorUtility.SetDirty(system);
                }

                return;
            }

            DrawPresets(system.settings);

            if (_serialized == null || _serialized.targetObject != system.settings)
            {
                _serialized = new SerializedObject(system.settings);
            }

            _serialized.Update();
            foreach (var section in Sections)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(section.title, EditorStyles.miniBoldLabel);
                foreach (var field in section.fields)
                {
                    var property = _serialized.FindProperty(field.path);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, new GUIContent(field.label, property.tooltip));
                    }
                }
            }

            _serialized.ApplyModifiedProperties();
        }

        private static void DrawPresets(SwarmSettings settings)
        {
            EditorGUILayout.HelpBox(
                "プリセットで「流体らしさ」の基本を選び、下の数値で微調整します。Play中でも即座に反映されます。",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                PresetButton(settings, "標準", SwarmPreset.Standard, "バランスの取れた押し合い。");
                PresetButton(settings, "どろどろ", SwarmPreset.Viscous, "粘り気があり、密集すると強く減速する。");
                PresetButton(settings, "さらさら", SwarmPreset.Fluid, "押されると流れるように動く。詰まりにくい。");
                PresetButton(settings, "ぎゅうぎゅう", SwarmPreset.Packed, "密度が高く、詰まって溜まりやすい。");
            }
        }

        private static void PresetButton(SwarmSettings settings, string label, SwarmPreset preset, string tooltip)
        {
            if (!GUILayout.Button(new GUIContent(label, tooltip)))
            {
                return;
            }

            Undo.RecordObject(settings, "Apply Swarm Preset");
            settings.ApplyPreset(preset);
            EditorUtility.SetDirty(settings);
        }
    }
}
