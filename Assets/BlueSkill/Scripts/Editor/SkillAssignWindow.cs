using System.Collections.Generic;
using MS2026.CharacterSelect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Skills.Blue.EditorTools
{
    /// <summary>
    /// メニュー「Tools › スキル › スキルの割り当て（プレイヤー・キャラ）」。色ごとのスキル（赤・青・…）を、
    /// ・今のテスト用に「P1〜P4 の席ごと」に配る（シーンのスキルを配る係の「プレイヤーごとのスキル」を書き換える）
    /// ・後で使う「キャラごと」の対応表（CharacterSkillTable）を、キャラの一覧から選ぶだけで埋める
    /// ・Play 中に「このキャラを選んだことにする」でキャラとの紐づけを試す
    /// をまとめて行う窓。プロジェクト内のスキルの設定ファイル（CharacterSkill）とキャラ（CharacterDefinition）を自動で探すので、
    /// 黄・緑のスキルを作れば何もしなくても選べるようになる。
    /// </summary>
    internal sealed class SkillAssignWindow : EditorWindow
    {
        private const int PlayerCount = PlayerSkillInstaller.PlayerCount;

        private static readonly string[] PlayerKeys = { "Q", "W", "O", "P" };

        private readonly List<CharacterSkill> _skills = new List<CharacterSkill>();
        private readonly List<CharacterDefinition> _characters = new List<CharacterDefinition>();
        private readonly CharacterDefinition[] _testCharacters = new CharacterDefinition[PlayerCount];
        private string[] _skillOptionsNone = new string[0];
        private string[] _characterOptions = new string[0];
        private PlayerSkillInstaller _installer;
        private Vector2 _scroll;
        private bool _reinstallInPlay = true;

        [MenuItem("Tools/スキル/スキルの割り当て（プレイヤー・キャラ）", false, 0)]
        private static void Open()
        {
            var window = GetWindow<SkillAssignWindow>();
            window.titleContent = new GUIContent("スキルの割り当て");
            window.minSize = new Vector2(380f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
            EditorApplication.hierarchyChanged += FindInstaller;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= FindInstaller;
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void OnProjectChange()
        {
            Refresh();
            Repaint();
        }

        private void Refresh()
        {
            LoadAssets(_skills, "t:CharacterSkill");
            LoadAssets(_characters, "t:CharacterDefinition");

            _skillOptionsNone = new string[_skills.Count + 1];
            _skillOptionsNone[0] = "（なし）";
            for (var i = 0; i < _skills.Count; i++)
            {
                _skillOptionsNone[i + 1] = SkillLabel(_skills[i]);
            }

            _characterOptions = new string[_characters.Count + 1];
            _characterOptions[0] = "（選ばない）";
            for (var i = 0; i < _characters.Count; i++)
            {
                _characterOptions[i + 1] = CharacterLabel(_characters[i]);
            }

            FindInstaller();
        }

        private void FindInstaller()
        {
            _installer = Object.FindAnyObjectByType<PlayerSkillInstaller>(FindObjectsInactive.Include);
            Repaint();
        }

        private static void LoadAssets<T>(List<T> list, string filter) where T : Object
        {
            list.Clear();
            var guids = AssetDatabase.FindAssets(filter);
            var paths = new List<string>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            paths.Sort(System.StringComparer.Ordinal);
            for (var i = 0; i < paths.Count; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
                if (asset != null && !list.Contains(asset))
                {
                    list.Add(asset);
                }
            }
        }

        private static string SkillLabel(CharacterSkill skill)
        {
            return skill.DisplayName == skill.name ? skill.name : skill.DisplayName + "（" + skill.name + "）";
        }

        private static string CharacterLabel(CharacterDefinition character)
        {
            return character.name + "（id: " + character.id + "）";
        }

        // ------------------------------------------------------------------

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "色ごとのスキル（赤・青…）を、キャラごと（②）と、キャラ選択を通らないときの席ごと（①）に配るための窓です。\n" +
                "キャラ選択を通ってきたら②の対応表だけを使い（対応表に無いキャラはスキルなし）、通っていなければ①を使います。\n" +
                "スキルの設定ファイルとキャラはプロジェクトから自動で探します（黄・緑を作れば自動で選べるようになります）。",
                MessageType.None);

            if (_installer == null)
            {
                DrawNoInstaller();
                EditorGUILayout.EndScrollView();
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("このシーンのスキルを配る係", _installer, typeof(PlayerSkillInstaller), true);
            }

            if (_skills.Count == 0)
            {
                EditorGUILayout.HelpBox("スキルの設定ファイル（CharacterSkill）がプロジェクトに見つかりません。", MessageType.Warning);
            }

            DrawPlayers();
            EditorGUILayout.Space(10f);
            DrawCharacters();
            EditorGUILayout.Space(10f);
            DrawPlayTest();
            EditorGUILayout.EndScrollView();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawNoInstaller()
        {
            EditorGUILayout.HelpBox("開いているシーンにスキルを配る係（PlayerSkillInstaller）がありません。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("スキルを配る係をシーンに置く"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlueSkillMenu.PrefabPath);
                    var instance = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : new GameObject("[Skill] Skill Installer", typeof(PlayerSkillInstaller));
                    Undo.RegisterCreatedObjectUndo(instance, "スキルを配る係を置く");
                    EditorSceneManager.MarkSceneDirty(instance.scene);
                    Selection.activeGameObject = instance;
                    FindInstaller();
                }
            }
        }

        // ------------------------------------------------------------------
        // プレイヤーごと
        // ------------------------------------------------------------------

        private void DrawPlayers()
        {
            EditorGUILayout.LabelField("① プレイヤーごと（キャラ選択を通らないとき）", EditorStyles.boldLabel);
            var so = new SerializedObject(_installer);
            var perPlayer = so.FindProperty("perPlayerSkills");
            if (perPlayer.arraySize != PlayerCount)
            {
                perPlayer.arraySize = PlayerCount;
            }

            EditorGUI.BeginChangeCheck();
            for (var i = 0; i < PlayerCount; i++)
            {
                var element = perPlayer.GetArrayElementAtIndex(i);
                var current = IndexOf(element.objectReferenceValue as CharacterSkill);
                var next = EditorGUILayout.Popup("P" + (i + 1) + "（" + PlayerKeys[i] + " で握る）", current, _skillOptionsNone);
                if (next != current)
                {
                    element.objectReferenceValue = SkillAt(next);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("席の色どおりに配る", "P1 赤・P2 青・P3 緑・P4 黄 の席に、その色に一番近いキャラのスキル（②の対応表）を入れる。")))
                {
                    for (var i = 0; i < PlayerCount; i++)
                    {
                        perPlayer.GetArrayElementAtIndex(i).objectReferenceValue = SkillForSeatColor(i);
                    }
                }

                if (GUILayout.Button(new GUIContent("席ごとの指定を全部消す", "P1〜P4 を「なし」（スキルなし）にする。")))
                {
                    for (var i = 0; i < PlayerCount; i++)
                    {
                        perPlayer.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    }
                }
            }

            var changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                so.ApplyModifiedProperties();
                if (!EditorApplication.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(_installer.gameObject.scene);
                }
                else if (_reinstallInPlay)
                {
                    _installer.InstallAll();
                }
            }

            if (EditorApplication.isPlaying)
            {
                _reinstallInPlay = EditorGUILayout.ToggleLeft("Play 中に変えたら、すぐ全砲台に配り直す", _reinstallInPlay);
                EditorGUILayout.HelpBox("Play 中に変えた割り当ては、Play を止めると元に戻ります（シーンの値なので）。残したいときは Play を止めてから変えて、シーンを保存してください。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("変えたらシーンを保存してください（Ctrl+S）。", MessageType.None);
            }
        }

        /// <summary>その席の色（P1 赤…）に一番近い色のキャラの、対応表のスキル。</summary>
        private CharacterSkill SkillForSeatColor(int seat)
        {
            var table = _installer != null ? _installer.characterTable : null;
            if (table == null)
            {
                return null;
            }

            var seatColor = MS2026.Fortress.FortressColors.SeatColor(seat);
            CharacterDefinition best = null;
            var bestDistance = float.MaxValue;
            foreach (var character in _characters)
            {
                var c = character.themeColor;
                var distance = (c.r - seatColor.r) * (c.r - seatColor.r) + (c.g - seatColor.g) * (c.g - seatColor.g) + (c.b - seatColor.b) * (c.b - seatColor.b);
                if (table.Find(character) != null && distance < bestDistance)
                {
                    best = character;
                    bestDistance = distance;
                }
            }

            return table.Find(best);
        }

        // ------------------------------------------------------------------
        // キャラごと
        // ------------------------------------------------------------------

        private void DrawCharacters()
        {
            EditorGUILayout.LabelField("② キャラごと（キャラ選択を通ってきたときに使う）", EditorStyles.boldLabel);
            var installerSo = new SerializedObject(_installer);
            var useSelection = installerSo.FindProperty("useCharacterSelection");
            var tableProperty = installerSo.FindProperty("characterTable");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(useSelection, new GUIContent("キャラ選択の結果を使う", "ON なら、キャラ選択で選ばれたキャラのスキルを下の対応表から探して配る（見つからなければスキルなし）。"));
            EditorGUILayout.PropertyField(tableProperty, new GUIContent("キャラとスキルの対応表"));
            if (EditorGUI.EndChangeCheck())
            {
                installerSo.ApplyModifiedProperties();
                if (!EditorApplication.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(_installer.gameObject.scene);
                }
            }

            var table = _installer.characterTable;
            if (table == null)
            {
                EditorGUILayout.HelpBox("対応表が入っていません。Assets/RedSkill/Data/CharacterSkillTable.asset（赤・青・…で共通）を入れてください。", MessageType.Warning);
                return;
            }

            if (_characters.Count == 0)
            {
                EditorGUILayout.HelpBox("キャラ（CharacterDefinition）がプロジェクトに見つかりません。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("対応表は全シーン共通のファイルです（" + AssetDatabase.GetAssetPath(table) + "）。変えるとすぐ保存され、チームのみんなに影響します。", MessageType.None);
            for (var i = 0; i < _characters.Count; i++)
            {
                var character = _characters[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    var swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                    EditorGUI.DrawRect(swatch, character.themeColor);
                    var current = IndexOf(table.Find(character));
                    var next = EditorGUILayout.Popup(CharacterLabel(character), current, _skillOptionsNone);
                    if (next != current)
                    {
                        SetTableSkill(table, character, SkillAt(next));
                    }
                }
            }
        }

        /// <summary>対応表のそのキャラの行を書き換える（無ければ足す、「なし」なら消す）。</summary>
        private static void SetTableSkill(CharacterSkillTable table, CharacterDefinition character, CharacterSkill skill)
        {
            var so = new SerializedObject(table);
            var entries = so.FindProperty("entries");
            var index = -1;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var entryCharacter = entry.FindPropertyRelative("character").objectReferenceValue as CharacterDefinition;
                var entryId = entry.FindPropertyRelative("characterId").stringValue;
                if (entryCharacter == character || (entryCharacter == null && !string.IsNullOrEmpty(entryId) && entryId == character.id))
                {
                    index = i;
                    break;
                }
            }

            if (skill == null)
            {
                if (index >= 0)
                {
                    entries.DeleteArrayElementAtIndex(index);
                }
            }
            else
            {
                if (index < 0)
                {
                    index = entries.arraySize;
                    entries.arraySize++;
                }

                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("character").objectReferenceValue = character;
                entry.FindPropertyRelative("characterId").stringValue = character.id;
                entry.FindPropertyRelative("skill").objectReferenceValue = skill;
            }

            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(table);
        }

        // ------------------------------------------------------------------
        // Play 中のテスト
        // ------------------------------------------------------------------

        private void DrawPlayTest()
        {
            EditorGUILayout.LabelField("③ Play 中に試す", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 中に、今の砲台ごとのスキルが並びます。キャラを選んだことにして、②の対応表どおりに配られるかも試せます。", MessageType.None);
                return;
            }

            var slots = PlayerSkillSlot.All;
            for (var p = 0; p < PlayerCount; p++)
            {
                var slot = PlayerSkillSlot.Find(p);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var skillName = slot != null && slot.Skill != null ? slot.Skill.DisplayName : "スキルなし";
                    EditorGUILayout.LabelField("P" + (p + 1), skillName + "  ／  " + StateText(slot));
                    using (new EditorGUI.DisabledScope(slot == null || slot.Skill == null))
                    {
                        if (GUILayout.Button("使えるようにする", GUILayout.Width(110f)))
                        {
                            slot.GrantCharge();
                        }
                    }
                }
            }

            if (slots.Count == 0)
            {
                EditorGUILayout.HelpBox("スキル欄の付いた砲台がありません。", MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("キャラを選んだことにする（キャラ選択の画面を通らずに試す）", EditorStyles.miniBoldLabel);
            for (var p = 0; p < PlayerCount; p++)
            {
                var current = _testCharacters[p] != null ? _characters.IndexOf(_testCharacters[p]) + 1 : 0;
                var next = EditorGUILayout.Popup("P" + (p + 1) + " のキャラ", current, _characterOptions);
                _testCharacters[p] = next > 0 && next - 1 < _characters.Count ? _characters[next - 1] : null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("このキャラで配り直す", "キャラ選択の結果をこの内容にして、全砲台にスキルを配り直す（②の対応表が使われる）。")))
                {
                    CharacterSelectionResult.Store(_testCharacters);
                    _installer.InstallAll();
                }

                if (GUILayout.Button(new GUIContent("キャラ選択の結果を消す", "キャラを選んでいない状態に戻して配り直す（①の指定が使われる）。")))
                {
                    CharacterSelectionResult.Clear();
                    _installer.InstallAll();
                }
            }

            EditorGUILayout.LabelField("今のキャラ選択の結果", CharacterSelectionResult.HasResult ? "あり" : "なし（キャラ選択を通っていない）");
        }

        private static string StateText(PlayerSkillSlot slot)
        {
            if (slot == null)
            {
                return "砲台なし";
            }

            switch (slot.State)
            {
                case PlayerSkillState.NoSkill:
                    return "スキルなし";
                case PlayerSkillState.Empty:
                    return "まだ使えない";
                case PlayerSkillState.Ready:
                    return slot.IsWaitingForRelease ? "使える（離したら次で発動）" : "使える";
                case PlayerSkillState.Active:
                    return "スキル中";
                default:
                    return slot.State.ToString();
            }
        }

        private int IndexOf(CharacterSkill skill)
        {
            return skill == null ? 0 : _skills.IndexOf(skill) + 1;
        }

        private CharacterSkill SkillAt(int popupIndex)
        {
            return popupIndex > 0 && popupIndex - 1 < _skills.Count ? _skills[popupIndex - 1] : null;
        }
    }
}
