using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>スマッシュボールの全設定項目を分かりやすいセクションに分けて描く。タブとInspectorの両方で共用する。</summary>
    public static class SmashBallSettingsDrawer
    {
        private readonly struct Field
        {
            public readonly string Path;
            public readonly string Label;
            public readonly string ShownWhen;
            public readonly bool ShownWhenInverted;

            public Field(string path, string label, string shownWhen = null, bool shownWhenInverted = false)
            {
                Path = path;
                Label = label;
                ShownWhen = shownWhen;
                ShownWhenInverted = shownWhenInverted;
            }
        }

        private sealed class Section
        {
            public string Title;
            public bool OpenByDefault;
            public Field[] Fields;
        }

        private const string S = "settings.";

        private static readonly Section[] Sections =
        {
            new Section
            {
                Title = "基本", OpenByDefault = true,
                Fields = new[]
                {
                    new Field(S + "label", "名札（履歴・デバッグ表示用）")
                }
            },
            new Section
            {
                Title = "浮遊（画面内をふわふわ移動）", OpenByDefault = true,
                Fields = new[]
                {
                    new Field(S + "floating.enabled", "画面内を無軌道に浮遊する（OFFなら置いた場所から動かない）"),
                    new Field(S + "floating.driftSpeed", "漂う速さ", S + "floating.enabled"),
                    new Field(S + "floating.directionChangeInterval", "方向転換の間隔(秒)の範囲", S + "floating.enabled"),
                    new Field(S + "floating.turnSharpness", "方向転換の滑らかさ", S + "floating.enabled"),
                    new Field(S + "floating.bobAmplitude", "上下に揺れる幅", S + "floating.enabled"),
                    new Field(S + "floating.bobFrequency", "上下に揺れる速さ", S + "floating.enabled"),
                    new Field(S + "floating.obstacleLayerMask", "避ける対象のレイヤー", S + "floating.enabled"),
                    new Field(S + "floating.avoidanceRadius", "避ける判定の半径(0=自動)", S + "floating.enabled"),
                    new Field(S + "floating.useNavigationFieldBounds", "経路フィールドの範囲内を漂う", S + "floating.enabled"),
                    new Field(S + "floating.fallbackAreaCenter", "漂う範囲の中心", S + "floating.useNavigationFieldBounds", shownWhenInverted: true),
                    new Field(S + "floating.fallbackAreaSize", "漂う範囲の大きさ", S + "floating.useNavigationFieldBounds", shownWhenInverted: true)
                }
            },
            new Section
            {
                Title = "割れたときの演出", OpenByDefault = true,
                Fields = new[]
                {
                    new Field(S + "onBroken", "演出（通常の破壊演出に追加で発生）"),
                    new Field(S + "effectLifetimeSeconds", "演出Prefabを消すまでの時間(秒)")
                }
            },
            new Section
            {
                Title = "履歴", OpenByDefault = false,
                Fields = new[]
                {
                    new Field(S + "recordHistory", "誰が割ったかを履歴に記録する")
                }
            },
            new Section
            {
                Title = "保護", OpenByDefault = false,
                Fields = new[]
                {
                    new Field(S + "regenGraceInvulnerableSeconds", "再生直後の追加無敵時間(秒)")
                }
            },
            new Section
            {
                Title = "同時出現の抑制（ローテーション）", OpenByDefault = false,
                Fields = new[]
                {
                    new Field(S + "exclusiveRotation", "同じグループで同時に1つだけ出現させる"),
                    new Field(S + "rotationGroupId", "グループ名（空=既定グループ）", S + "exclusiveRotation"),
                    new Field(S + "rotationDelaySeconds", "割れてから次が現れるまでの間隔(秒)", S + "exclusiveRotation")
                }
            }
        };

        public static void Draw(SerializedObject serialized)
        {
            serialized.Update();

            foreach (var section in Sections)
            {
                var key = "Fortress.SmashBall.Section." + section.Title;
                var open = SessionState.GetBool(key, section.OpenByDefault);
                open = EditorGUILayout.Foldout(open, section.Title, true, EditorStyles.foldoutHeader);
                SessionState.SetBool(key, open);
                if (!open)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                foreach (var field in section.Fields)
                {
                    DrawField(serialized, field);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            serialized.ApplyModifiedProperties();
        }

        private static void DrawField(SerializedObject serialized, Field field)
        {
            if (field.ShownWhen != null)
            {
                var gate = serialized.FindProperty(field.ShownWhen);
                if (gate != null && !gate.hasMultipleDifferentValues && gate.boolValue == field.ShownWhenInverted)
                {
                    return;
                }
            }

            var property = serialized.FindProperty(field.Path);
            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(field.Label, property.tooltip), true);
        }
    }
}
