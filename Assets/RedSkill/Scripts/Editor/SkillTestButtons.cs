using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>スキル欄のテスト用ボタン（スキル欄・配る係・スキルの設定ファイルのインスペクターで共通に使う）。</summary>
    internal static class SkillTestButtons
    {
        public static string StateLabel(PlayerSkillSlot slot)
        {
            if (slot == null)
            {
                return "（なし）";
            }

            switch (slot.State)
            {
                case PlayerSkillState.NoSkill:
                    return "スキルなし";
                case PlayerSkillState.Empty:
                    return "まだ使えない";
                case PlayerSkillState.Ready:
                    return slot.IsWaitingForRelease ? "使える（今のレーザーを離したら次で発動）" : "使える（次に撃つとスキル）";
                case PlayerSkillState.Active:
                    return "スキルを撃っている";
                default:
                    return slot.State.ToString();
            }
        }

        /// <summary>1台ぶんの状態とテスト用ボタンを1行で描く。</summary>
        public static void DrawRow(PlayerSkillSlot slot)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var skillName = slot.Skill != null ? slot.Skill.DisplayName : "スキルなし";
                EditorGUILayout.LabelField($"P{slot.PlayerIndex + 1}  {skillName}", StateLabel(slot));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || slot.Skill == null))
                {
                    if (GUILayout.Button(new GUIContent("使えるようにする", "スマッシュボールを割ったときと同じ状態にする。次にレーザーを撃つとスキルになる。")))
                    {
                        slot.GrantCharge();
                    }

                    if (GUILayout.Button(new GUIContent("今すぐ撃つ", "握っていなくても、使える状態にしてすぐスキルを撃つ。")))
                    {
                        slot.GrantCharge();
                        slot.ActivateNow();
                    }

                    if (GUILayout.Button(new GUIContent("止める", "撃っている途中のスキルをすぐ止める／使える状態を取り消す。")))
                    {
                        slot.CancelActive();
                        slot.ClearCharge();
                    }
                }
            }
        }

        public static void DrawPlayModeNote()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("テスト用のボタンは Play 中に使えます。Play 中はエディタだけ、キーボードの 1〜4 で P1〜P4 のスキルを使える状態にできます（Q/W/O/P で握る）。", MessageType.Info);
            }
        }
    }

    [CustomEditor(typeof(PlayerSkillSlot))]
    internal sealed class PlayerSkillSlotEditor : Editor
    {
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            SkillInspectorGUI.DrawAll(serializedObject);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("テスト", EditorStyles.boldLabel);
            SkillTestButtons.DrawPlayModeNote();
            SkillTestButtons.DrawRow((PlayerSkillSlot)target);
        }
    }

    [CustomEditor(typeof(PlayerSkillInstaller))]
    internal sealed class PlayerSkillInstallerEditor : Editor
    {
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "シーンの全ての砲台にスキルを配ります。この部品（またはプレハブ [RedSkill] Skill Installer）を置くだけで、別のシーンでもスキルが使えます。\n" +
                "配る順番: キャラ選択の結果＋対応表 → プレイヤーごとのスキル → 全員に配るスキル。",
                MessageType.None);
            SkillInspectorGUI.DrawAll(serializedObject);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("テスト（各砲台の状態）", EditorStyles.boldLabel);
            SkillTestButtons.DrawPlayModeNote();
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var slots = PlayerSkillSlot.All;
            if (slots.Count == 0)
            {
                EditorGUILayout.HelpBox("スキル欄の付いた砲台がありません。シーンに LaserTurret があるか確認してください。", MessageType.Warning);
                return;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    SkillTestButtons.DrawRow(slots[i]);
                }
            }
        }
    }

    [CustomEditor(typeof(CharacterSkillTable))]
    internal sealed class CharacterSkillTableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "キャラ選択で選ばれたキャラ → 配るスキル の対応表です。キャラを増やしたら行を足してください。\n" +
                "使うには、シーンのスキルを配る係（[RedSkill] Skill Installer）の「キャラとスキルの対応表」にこのファイルを入れます（プレハブには入れてあります）。",
                MessageType.None);
            SkillInspectorGUI.DrawAll(serializedObject);
        }
    }

    [CustomEditor(typeof(CharacterSkill), true)]
    internal sealed class CharacterSkillEditor : Editor
    {
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "このファイルの値は Play 中に変えてもすぐ反映され、Play をやめても残ります（設定ファイルなので）。\n" +
                "各グループの ▶ を開くと細かい値があります。項目にマウスを乗せると説明が出ます。",
                MessageType.None);
            SkillInspectorGUI.DrawAll(serializedObject);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("テスト（このスキルを持っている砲台）", EditorStyles.boldLabel);
            SkillTestButtons.DrawPlayModeNote();
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var found = false;
            var slots = PlayerSkillSlot.All;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Skill == target)
                {
                    SkillTestButtons.DrawRow(slots[i]);
                    found = true;
                }
            }

            if (!found)
            {
                EditorGUILayout.HelpBox("このスキルを持っている砲台がありません。", MessageType.Info);
            }
        }
    }
}
