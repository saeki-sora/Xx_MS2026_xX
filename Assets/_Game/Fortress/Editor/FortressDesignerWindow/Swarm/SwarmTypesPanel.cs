using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>敵の種類ごとの設定（体力・速度・大きさ・重さ・スプライトシートなど）の編集。</summary>
    public sealed class SwarmTypesPanel
    {
        private static readonly (string path, string label)[] CommonFields =
        {
            ("displayName", "表示名"),
            ("simulationMode", "動かし方（Swarm=群衆 / Actor=個別）"),
            ("maxHealth", "体力"),
            ("moveSpeed", "移動速度"),
            ("damageToCore", "コアに与えるダメージ"),
            ("navigationProfile", "経路プロファイル"),
            ("placeholderColor", "仮絵の色")
        };

        private static readonly (string path, string label)[] SwarmFields =
        {
            ("swarm.radius", "体の大きさ（当たり半径）"),
            ("swarm.mass", "重さ"),
            ("swarm.acceleration", "加速の速さ"),
            ("swarm.speedVariance", "速度のばらつき"),
            ("swarm.spriteSize", "画面上の大きさ"),
            ("swarm.spriteSheet", "スプライトシート（空=仮絵）"),
            ("swarm.frameCount", "アニメのコマ数（横）"),
            ("swarm.directionCount", "向きの数（縦: 1/2/4/8）"),
            ("swarm.animationFps", "アニメ速度(コマ/秒)")
        };

        private readonly Dictionary<EnemyTypeDefinition, SerializedObject> _serialized =
            new Dictionary<EnemyTypeDefinition, SerializedObject>();

        private readonly Dictionary<EnemyTypeDefinition, bool> _foldouts = new Dictionary<EnemyTypeDefinition, bool>();
        private bool _foldout = true;

        public void Draw()
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, "敵の種類");
            if (_foldout)
            {
                DrawContents();
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawContents()
        {
            EditorGUILayout.HelpBox(
                "スプライトシートは、横=アニメのコマ（左→右）、縦=向き（上から 右, 右上, 上, 左上, 左, 左下, 下, 右下）の格子で作ります。" +
                "8方向×10コマなら、横10分割・縦8分割。空のままなら自動生成の仮絵で動きます。",
                MessageType.None);

            foreach (var definition in SwarmTools.FindAllEnemyTypes())
            {
                DrawType(definition);
            }

            if (GUILayout.Button("＋ 新しい敵の種類を作成"))
            {
                Selection.activeObject = SwarmTools.CreateEnemyTypeAsset();
            }
        }

        private void DrawType(EnemyTypeDefinition definition)
        {
            if (!_foldouts.TryGetValue(definition, out var open))
            {
                open = false;
            }

            var title = $"{definition.displayName}  ({definition.name})";
            open = EditorGUILayout.Foldout(open, title, true);
            _foldouts[definition] = open;
            if (!open)
            {
                return;
            }

            if (!_serialized.TryGetValue(definition, out var serialized) || serialized.targetObject == null)
            {
                serialized = new SerializedObject(definition);
                _serialized[definition] = serialized;
            }

            serialized.Update();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawFields(serialized, CommonFields);

                if (definition.simulationMode == EnemySimulationMode.Swarm)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("群衆の設定", EditorStyles.miniBoldLabel);
                    DrawFields(serialized, SwarmFields);
                }
                else
                {
                    EditorGUILayout.HelpBox("Actorモードは1体ごとにGameObjectを使います。数百体を超える場合はSwarmを使ってください。", MessageType.Info);
                }
            }

            serialized.ApplyModifiedProperties();
        }

        private static void DrawFields(SerializedObject serialized, (string path, string label)[] fields)
        {
            foreach (var field in fields)
            {
                var property = serialized.FindProperty(field.path);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(field.label, property.tooltip));
                }
            }
        }
    }
}
