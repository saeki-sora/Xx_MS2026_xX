using System.Linq;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MS2026.CharacterSelect.EditorTools
{
    /// <summary>
    /// メニュー「Tools/キャラ選択/見た目と数値の調整」: キャラ選択の見た目と数値を1か所で変える画面。
    /// ・キャラ: 名前・色・立ち絵・ボタン・欄の土台・スキルの動画・スキル・能力（キャラのファイルを直接書き換える）
    /// ・画像の差し替え: 仮の絵を、ボタンで選んだ画像ファイルで上書き（使っている所は全部その絵になる）／仮の絵に戻す
    /// ・数値: よく触る数値を、4つの区画・4つの丸の分などまとめて変える（開いているキャラ選択のシーンの物を書き換える）
    /// </summary>
    public sealed class CharacterSelectTuningWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "キャラ", "画像の差し替え", "数値" };

        private int _tab;
        private Vector2 _scroll;

        [MenuItem("Tools/キャラ選択/見た目と数値の調整", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<CharacterSelectTuningWindow>();
            window.titleContent = new GUIContent("キャラ選択の調整");
            window.minSize = new Vector2(460f, 420f);
        }

        private void OnGUI()
        {
            _tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(28f));
            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0:
                    DrawCharacters();
                    break;
                case 1:
                    DrawImages();
                    break;
                default:
                    DrawNumbers();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ───────── キャラ ─────────

        private void DrawCharacters()
        {
            EditorGUILayout.HelpBox("キャラのファイルを直接書き換えます（保存は自動）。並べ替え・外すときは「キャラの一覧を選ぶ」から。", MessageType.Info);
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(CharacterSelectSceneBuilder.RosterPath);
            if (roster == null)
            {
                EditorGUILayout.HelpBox("キャラの一覧がまだありません。先にシーンを組み立ててください。", MessageType.Warning);
                if (GUILayout.Button("シーンを組み立てる"))
                {
                    CharacterSelectSceneBuilder.BuildInteractive();
                }

                return;
            }

            for (var i = 0; i < roster.Count; i++)
            {
                var definition = roster.Get(i);
                if (definition == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                DrawThumbnail(definition.sprite != null ? definition.sprite.texture : null, definition.spriteTint, 56f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"{i + 1}. {definition.DisplayName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(definition), EditorStyles.miniLabel);
                if (GUILayout.Button("ファイルを選ぶ", GUILayout.Width(110f)))
                {
                    Ping(definition);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                var serialized = new SerializedObject(definition);
                serialized.Update();
                Field(serialized, "displayName", "名前");
                Field(serialized, "spriteTint", "色（立ち絵にかける色。仮のキューブはこの色）");
                Field(serialized, "sprite", "立ち絵（2Dの絵）");
                Field(serialized, "icon", "ミニキャラの顔（今の画面では使っていない）");
                Field(serialized, "buttonOn", "下のボタン（カーソルが乗ったとき）");
                Field(serialized, "buttonOff", "下のボタン（ふだん）");
                Field(serialized, "rowBase", "ほかの人の欄の土台（ミニキャラ入り）");
                Field(serialized, "mainBase", "自分の区画の土台（キャラの色にするときだけ）");
                Field(serialized, "skillVideo", "スキルの動画（空なら「準備中」）");
                Field(serialized, "skillName", "スキル名");
                Field(serialized, "skillDescription", "スキルの説明");
                Field(serialized, "stats", "能力の棒グラフ");
                Field(serialized, "spriteScale", "立ち絵の大きさ");
                Field(serialized, "spriteOffset", "立ち絵の位置のずれ");
                serialized.ApplyModifiedProperties();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ キャラを増やす（最後のキャラを複製）", GUILayout.Height(24f)))
            {
                AddCharacter(roster);
            }

            if (GUILayout.Button("キャラの一覧を選ぶ", GUILayout.Height(24f)))
            {
                Ping(roster);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void AddCharacter(CharacterRoster roster)
        {
            var last = roster.Count > 0 ? roster.Get(roster.Count - 1) : null;
            if (last == null)
            {
                return;
            }

            var path = StudioAssets.UniquePath(CharacterSelectSceneBuilder.CharactersFolder, "Chara_New.asset");
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(last), path))
            {
                return;
            }

            var copy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            copy.id = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            EditorUtility.SetDirty(copy);
            var serialized = new SerializedObject(roster);
            var list = serialized.FindProperty("characters");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = copy;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        // ───────── 画像の差し替え ─────────

        private void DrawImages()
        {
            EditorGUILayout.HelpBox("「画像を選んで差し替える」で選んだ画像（PNG / JPG）が、そのファイルに上書きされます。使っている所は全部その絵になります（再生し直すと反映）。\n" +
                                    "白い絵は、画面の側でプレイヤーやキャラの色をかけて使っています。本番の絵に色が付いているときは、その色の上にさらに色がかかります。", MessageType.Info);
            foreach (var entry in CharacterSelectArt.Entries)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                DrawThumbnail(AssetDatabase.LoadAssetAtPath<Texture2D>(entry.path), Color.white, 72f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(entry.label, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(entry.usage, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"{entry.path}（仮の絵は {entry.width}×{entry.height}）", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("画像を選んで差し替える"))
                {
                    var source = EditorUtility.OpenFilePanel($"「{entry.label}」にする画像", "", "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(source) && !CharacterSelectArt.ReplaceWithFile(entry, source))
                    {
                        EditorUtility.DisplayDialog("キャラ選択", "画像を読み込めませんでした（PNG か JPG を選んでください）。", "OK");
                    }
                }

                if (GUILayout.Button("仮の絵に戻す", GUILayout.Width(100f)) &&
                    EditorUtility.DisplayDialog("キャラ選択", $"「{entry.label}」を仮の絵に戻しますか？（今の絵は消えます）", "戻す", "やめる"))
                {
                    CharacterSelectArt.RestorePlaceholder(entry);
                }

                if (GUILayout.Button("場所を見る", GUILayout.Width(80f)))
                {
                    Ping(AssetDatabase.LoadAssetAtPath<Object>(entry.path));
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.HelpBox("歯磨き粉の絵（ロビーから入るときの幕・キャラの塗り替え）は、タイトルのオープニングと同じ絵（Title_PasteCurtain）を使っています。\n" +
                                    "キャラ選択だけ別の絵にしたいときは「数値」タブの「歯磨き粉の絵」に別の絵を入れてください（ここで上書きするとタイトルも変わるため）。", MessageType.None);
        }

        // ───────── 数値 ─────────

        private void DrawNumbers()
        {
            var controllers = FindAll<CharacterSelectController>();
            if (controllers.Length == 0)
            {
                EditorGUILayout.HelpBox("キャラ選択のシーンが開いていません。シーンの数値は、シーンを開いてから変えます。", MessageType.Warning);
                if (GUILayout.Button("キャラ選択のシーンを開く"))
                {
                    CharacterSelectSceneBuilder.OpenScene();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("3つの欄・キャラのボタンなどは、まとめて同じ値になります。変えたらシーンを保存してください。\n" +
                                        "シーンを組み立て直すときは「数値を引き継いで作り直す」を選ぶと、ここで変えた値が残ります。再生中に変えた値は、止めると元に戻ります。", MessageType.Info);
                Section("ルール", controllers,
                    ("allowSameCharacter", "同じキャラを選べる"),
                    ("longPressSeconds", "長押しで READY になる秒数"),
                    ("longPressCancels", "READY の後の長押しで取り消せる"),
                    ("timeLimitSeconds", "制限時間（秒・0 で無し）"),
                    ("finishedHoldSeconds", "GO! を見せる秒数"),
                    ("key", "仮のキー（ネット対戦）"),
                    ("offlineKeys", "1台で試すときの 1P〜4P のキー"),
                    ("ignoreKeyboardGrip", "Q などの握力の代わりを無視する"));
                Section("キャラの切り替え（歯磨き粉で塗る）", FindAll<CharacterSwapFx>(),
                    ("clipToCharacter", "キャラの上だけに塗る（オフ＝道すじのとおりに外まで）"),
                    ("fillGaps", "すき間なく塗りつぶす（線を自動で太く）"),
                    ("lean", "ジグザグの傾き（度）"),
                    ("legs", "線の本数（多いほど密）"),
                    ("size", "ジグザグ全体の大きさ"),
                    ("paintSeconds", "塗り終わるまでの秒数"),
                    ("holdSeconds", "塗ったままの秒数"),
                    ("revealStyle", "消え方"),
                    ("revealSeconds", "消えるまでの秒数"),
                    ("jitter", "毎回の形のばらつき"),
                    ("shineSeconds", "キラーンの秒数"));
                Section("歯磨き粉の線", FindAll<PasteStroke>(),
                    ("width", "線の太さ"),
                    ("texture", "歯磨き粉の絵"),
                    ("textureU", "しまの太さ（絵の横の範囲。狭いほど太い）"));
                Section("自分の区画（READY! など）", FindAll<MainPlayerPanel>(),
                    ("baseColorMode", "土台の色の決め方"),
                    ("baseBySeat", "席ごとの土台の絵（1P〜4P）"),
                    ("stampSlamSeconds", "READY! を叩きつける秒数"),
                    ("stampSlamScale", "叩きつける前の大きさ"),
                    ("blockedAlpha", "選べないキャラの立ち絵の濃さ"),
                    ("bobPixels", "立ち絵のふわふわの幅"),
                    ("bobSpeed", "立ち絵のふわふわの速さ"));
                Section("スキル動画のモニター", FindAll<SkillMonitor>(),
                    ("openSeconds", "決定して開く秒数"),
                    ("closeSeconds", "取り消して閉じる秒数"),
                    ("openAngle", "開ききる角度"),
                    ("placeholderFormat", "動画が無いときの文字（{0} = スキル名）"));
                Section("ほかの人の欄", FindAll<OtherPlayerRow>(),
                    ("selectingColor", "「えらび中…」の色"),
                    ("inactiveAlpha", "参加していない人の欄の濃さ"));
                Section("キャラのボタン・札・矢印", FindAll<CharacterButtonRow>(),
                    ("badgeOffset", "札の位置（ボタンの左上から）"),
                    ("badgeSpacing", "札を横に並べる間隔"),
                    ("arrowOffset", "矢印の位置（ボタンの右から）"),
                    ("arrowBobPixels", "矢印の揺れ幅"),
                    ("followSpeed", "札・矢印が移る速さ"),
                    ("lockedTint", "ほかの人が決めたキャラのボタンの色"));
                Section("キャラのボタン1つ", FindAll<CharacterButton>(),
                    ("offSize", "ふだんの大きさ"),
                    ("onSize", "カーソルが乗ったときの大きさ"),
                    ("resizeSpeed", "大きさが替わる速さ"));
                Section("決定のゲージ", FindAll<PushGauge>(),
                    ("waitingPulseSpeed", "「他の社員を待ってね…」の明滅の速さ"));
                Section("登場・ほかの人の欄・GO!", FindAll<CharacterSelectView>(),
                    ("entranceSeconds", "画面の部分が滑り込む秒数"),
                    ("entranceStagger", "部分どうしの出るずれ（秒）"),
                    ("entranceDistance", "左右から滑り込む距離（0 で動かない）"),
                    ("rowsStartAt", "モニターがどれだけ開いたら欄を出すか"),
                    ("rowSeconds", "欄が滑り込む秒数"),
                    ("rowStagger", "欄どうしの出るずれ（秒）"),
                    ("rowSlideDistance", "欄が滑り込む距離"),
                    ("fadeOutSeconds", "1台で試すときのフェードの秒数"));
                if (GUILayout.Button("シーンを保存", GUILayout.Height(26f)))
                {
                    EditorSceneManager.SaveOpenScenes();
                }
            }

            EditorGUILayout.Space(8f);
            var curtain = AssetDatabase.LoadAssetAtPath<PasteCurtainSettings>(CharacterSelectSceneBuilder.CurtainSettingsPath);
            if (curtain != null)
            {
                Section("歯磨き粉の幕（ロビーから入るとき・GO! のあと。設定ファイル）", new Object[] { curtain },
                    ("useForGameStart", "GO! のあとゲームへ進むときも幕を出す"),
                    ("dropSeconds", "覆いきるまでの秒数"),
                    ("holdSeconds", "覆ったままの最短の秒数"),
                    ("revealSeconds", "流れ落ちるまでの秒数"),
                    ("columns", "縦の列の数"),
                    ("unevenness", "垂れる先端のでこぼこ"),
                    ("texture", "幕の絵"),
                    ("playOnStandalone", "このシーンだけで再生したときも幕を出す"));
            }
        }

        private static void Section(string title, Object[] targets, params (string property, string label)[] fields)
        {
            if (targets == null || targets.Length == 0)
            {
                return;
            }

            var key = "MS2026.CharaSelect.Tuning." + title;
            var open = SessionState.GetBool(key, true);
            var count = targets.Length > 1 ? $"（{targets.Length} 個をまとめて）" : string.Empty;
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, title + count);
            SessionState.SetBool(key, open);
            if (open)
            {
                var serialized = new SerializedObject(targets);
                serialized.Update();
                EditorGUI.indentLevel++;
                foreach (var (property, label) in fields)
                {
                    Field(serialized, property, label);
                }

                EditorGUI.indentLevel--;
                serialized.ApplyModifiedProperties();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void Field(SerializedObject serialized, string property, string label)
        {
            var found = serialized.FindProperty(property);
            if (found != null)
            {
                EditorGUILayout.PropertyField(found, new GUIContent(label, found.tooltip), true);
            }
        }

        private static Object[] FindAll<T>() where T : Object
        {
            return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Object>().ToArray();
        }

        private static void DrawThumbnail(Texture texture, Color tint, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.38f, 0.5f));
            if (texture != null)
            {
                var previous = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                GUI.color = previous;
            }
        }

        private static void Ping(Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
