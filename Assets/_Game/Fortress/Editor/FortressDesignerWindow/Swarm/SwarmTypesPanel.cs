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
            ("placeholderColor", "仮絵の色"),
            ("introText", "紹介文（NEW ENEMY・30字まで・空=出さない）"),
            ("introIcon", "紹介カードの絵（空=群衆の絵）")
        };

        private static readonly (string path, string label)[] SwarmFields =
        {
            ("swarm.radius", "体の大きさ（押し合いの半径）"),
            ("swarm.hitRadius", "レーザーが当たる半径（0=体の大きさと同じ）"),
            ("swarm.mass", "重さ"),
            ("swarm.acceleration", "加速の速さ"),
            ("swarm.speedVariance", "速度のばらつき"),
            ("swarm.spriteSize", "画面上の大きさ"),
            ("swarm.spriteSheet", "スプライトシート（空=仮絵）"),
            ("swarm.frameCount", "アニメのコマ数（横）"),
            ("swarm.directionCount", "向きの数（縦: 1/2/4/8）"),
            ("swarm.animationFps", "アニメ速度(コマ/秒)")
        };

        private static readonly (string path, string label)[] BombFields =
        {
            ("bomb.explosionRadius", "爆発の半径"),
            ("bomb.enemyDamage", "敵へのダメージ（巻き込まれた爆弾も爆発）"),
            ("bomb.obstacleDamage", "破壊可能物へのダメージ（0=壊さない）"),
            ("bomb.coreDamage", "コアへのダメージ（近くで倒したとき・0=なし）"),
            ("bomb.chainDelaySeconds", "連鎖の間隔（秒）"),
            ("bomb.explodeAtCore", "コアに着いたら自爆する"),
            ("bomb.onExplode", "爆発の演出"),
            ("bomb.showBlastRing", "爆発の範囲を輪で見せる（仮の見た目）"),
            ("bomb.blastRingColor", "輪の色"),
            ("bomb.shockwave", "衝撃波で床を波打たせる"),
            ("bomb.shockwaveStrength", "衝撃波の強さ（m）"),
            ("bomb.shockwaveSeconds", "衝撃波の秒数"),
            ("bomb.flash", "爆発の瞬間に閃光を出す"),
            ("bomb.flashColor", "閃光の色"),
            ("bomb.flashSize", "閃光の大きさ（爆発の半径の何倍）"),
            ("bomb.flashSeconds", "閃光の秒数"),
            ("bomb.blinkColor", "点滅の色"),
            ("bomb.blinkStrength", "点滅の強さ"),
            ("bomb.blinkPerSecond", "点滅の速さ（回/秒・遠いとき）"),
            ("bomb.blinkSpeedUpNearCore", "コアに近いほど速くなる強さ"),
            ("bomb.blinkSpeedUpDistance", "速くなり始める距離"),
            ("bomb.blinkSwell", "点滅でふくらむ割合")
        };

        private static readonly (string path, string label)[] SpiralFields =
        {
            ("spiral.radius", "輪の半径"),
            ("spiral.turnsPerSecond", "回る速さ（周/秒）"),
            ("spiral.mixDirections", "右回り・左回りをばらばらにする")
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
                    DrawBomb(serialized);
                    DrawSpiral(serialized, definition);
                }
                else
                {
                    EditorGUILayout.HelpBox("Actorモードは1体ごとにGameObjectを使います。数百体を超える場合はSwarmを使ってください。", MessageType.Info);
                }
            }

            serialized.ApplyModifiedProperties();
        }

        private static void DrawBomb(SerializedObject serialized)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("爆弾（倒すと爆発する）", EditorStyles.miniBoldLabel);
            var toggle = serialized.FindProperty("bomb.explodeOnDeath");
            if (toggle == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(toggle, new GUIContent("倒すと爆発する", toggle.tooltip));
            if (!toggle.boolValue)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "点滅しながら近づき、倒された場所で爆発して近くの敵・破壊可能物・コアを巻き込みます。巻き込まれた爆弾も爆発するので、群れの中で連鎖します。" +
                "巻き込んで倒した敵は、爆弾を倒した人の撃破になります。",
                MessageType.None);
            using (new EditorGUI.IndentLevelScope())
            {
                DrawFields(serialized, BombFields);
            }
        }

        private static void DrawSpiral(SerializedObject serialized, EnemyTypeDefinition definition)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("螺旋（くるくる回りながら近づく）", EditorStyles.miniBoldLabel);
            var toggle = serialized.FindProperty("spiral.enabled");
            if (toggle == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(toggle, new GUIContent("螺旋で進む", toggle.tooltip));
            if (!toggle.boolValue)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawFields(serialized, SpiralFields);
            }

            // 値を変えた直後でも合うよう、反映してから読む。
            serialized.ApplyModifiedProperties();
            var spiral = definition.spiral;
            var lap = definition.moveSpeed / spiral.turnsPerSecond;
            var shape = spiral.MakesLoops(definition.moveSpeed)
                ? "輪を描きながら近づきます。"
                : "回る速さが近づく速さ（移動速度）より遅いので、輪にならず波打つだけです。輪にしたいときは、半径か回る速さを上げるか、移動速度を下げてください。";
            EditorGUILayout.HelpBox(
                $"近づく速さ {definition.moveSpeed:0.##} m/秒（移動速度）＋ 回る速さ {spiral.CircleSpeed:0.##} m/秒。1 周するごとに {lap:0.##} m 近づきます。{shape}",
                MessageType.None);
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
