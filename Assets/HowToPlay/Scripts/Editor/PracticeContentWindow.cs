using UnityEditor;
using UnityEngine;

namespace MS2026.HowToPlay.EditorTools
{
    /// <summary>
    /// 研修（練習の画面）の絵と文字を差し替える画面（2026-11-19）。Tools › あそびかた › 研修の絵と文字（差し替え）。
    /// 中身は2つのファイル（どちらも Assets/HowToPlay/Resources）:
    /// ・PracticeArtLibrary … 絵（入れた絵が使われる。空の欄は今までの絵）
    /// ・PracticeTextLibrary … 文字（最初は今の文字が入っている。書き換えるとそのまま画面に出る）
    /// どこに出る絵・文字かを横に書いておき、絵は小さい見本を出す。元に戻す（Ctrl+Z）も効く。
    /// </summary>
    public sealed class PracticeContentWindow : EditorWindow
    {
        private const string ArtPath = "Assets/HowToPlay/Resources/PracticeArtLibrary.asset";
        private const string TextPath = "Assets/HowToPlay/Resources/PracticeTextLibrary.asset";

        private static readonly string[] Tabs = { "絵", "キャラの絵", "セリフ", "画面の文字" };
        private static readonly string[] CharacterIds = { "alpha", "beta", "delta", "gamma" };
        private static readonly string[] CharacterNames = { "赤星レイザ（赤）", "青波雫（青）", "パチ（黄）", "緑谷ミラ（緑）" };
        private static readonly string[] TaskNames =
        {
            "研修1 ① 握る", "研修1 ② 握り続けて倒す", "研修1 ③ 離す", "研修2 ① オーバーヒートまで握る", "研修2 ② 離して冷ます", "研修3 ① 泡を割る", "研修3 ② スキルを使う"
        };

        private PracticeArtLibrary _art;
        private PracticeTextLibrary _text;
        private SerializedObject _artSo;
        private SerializedObject _textSo;
        private int _tab;
        private int _character;
        private bool _showCommon;
        private Vector2 _scroll;
        private GUIStyle _area;
        private GUIStyle _help;

        [MenuItem("Tools/あそびかた/研修の絵と文字（差し替え）")]
        public static void Open()
        {
            var window = GetWindow<PracticeContentWindow>("研修の絵と文字");
            window.minSize = new Vector2(520f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            Load();
        }

        private void Load()
        {
            _art = AssetDatabase.LoadAssetAtPath<PracticeArtLibrary>(ArtPath);
            _text = AssetDatabase.LoadAssetAtPath<PracticeTextLibrary>(TextPath);
            _artSo = _art != null ? new SerializedObject(_art) : null;
            _textSo = _text != null ? new SerializedObject(_text) : null;
        }

        private void OnGUI()
        {
            _area ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _help ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            if (_art == null || _text == null || _artSo == null || _textSo == null)
            {
                Load();
            }

            if (_art == null || _text == null)
            {
                EditorGUILayout.HelpBox("絵か文字のファイルが見つかりません。\n" + ArtPath + "\n" + TextPath, MessageType.Error);
                return;
            }

            _artSo.Update();
            _textSo.Update();

            EditorGUILayout.HelpBox(
                "研修（新人研修）の画面の絵と文字をここで差し替えます。\n" +
                "・絵: 欄に絵を入れるとその絵になる。空にすると今までの絵に戻る。\n" +
                "・文字: 書き換えるとそのまま画面に出る。【】で囲んだ所はキャラの色。{0} {1} には数や名前が入る（消さないこと）。\n" +
                "・試すとき: 再生してから Tools › あそびかた › 練習の画面を試す。",
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("絵のファイルを選ぶ"))
                {
                    Select(_art);
                }

                if (GUILayout.Button("文字のファイルを選ぶ"))
                {
                    Select(_text);
                }
            }

            EditorGUILayout.Space(4f);
            _tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(26f));
            if (_tab == 1 || _tab == 2)
            {
                _character = GUILayout.Toolbar(_character, CharacterNames);
            }

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawArt(); break;
                case 1: DrawCharacterArt(); break;
                case 2: DrawLines(); break;
                default: DrawScreenTexts(); break;
            }

            EditorGUILayout.EndScrollView();

            _artSo.ApplyModifiedProperties();
            _textSo.ApplyModifiedProperties();
        }

        private static void Select(Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // ───────── 絵 ─────────

        private void DrawArt()
        {
            Section("文字のフォント");
            EditorGUILayout.PropertyField(_artSo.FindProperty("font"), new GUIContent("フォント", "練習の画面の文字すべて（今は Zen Maru Gothic Bold）。空なら Unity の標準のフォント。"));

            Section("画面の枠と欄（入れた所は、コードで描かずにこの絵を出す）");
            Picture(_artSo.FindProperty("fieldFrame"), typeof(Texture), "フィールドの枠", "真ん中が透明な絵。「ためしてみてね」の札も絵の中。空ならコードで描く枠と札。");
            Picture(_artSo.FindProperty("rosterPanel"), typeof(Texture), "右下「準備中」の欄", "見出しも絵の中。右上の四角（絵の 470〜560, 16〜60）に「0/4」を白い文字で出す。");
            var panels = _artSo.FindProperty("lessonPanels");
            for (var i = 0; i < 3; i++)
            {
                Picture(Element(panels, i), typeof(Texture), $"右上の欄（研修{i + 1}）", "研修の名前・「練習」・▼ も絵の中。空ならコードで描く。");
            }

            var cards = _artSo.FindProperty("taskCards");
            for (var i = 0; i < TaskNames.Length; i++)
            {
                Picture(Element(cards, i), typeof(Texture), $"課題のカード（{TaskNames[i]}）", "番号・やること・説明も絵の中。左寄りの灰色の四角に手の絵を重ねる。空ならコードで描く。");
            }

            Picture(_artSo.FindProperty("taskCardCurrent"), typeof(Texture), "今の課題に重ねる光るふち", "真ん中が透明な絵（カードと同じ大きさ）。");
            Picture(_artSo.FindProperty("lessonPanelBlank"), typeof(Texture), "研修の名前なしの欄", "研修ごとの欄の絵が無い研修で使う（「練習」と ▼ 1つは絵の中。研修の名前はコードで書く）。");
            var blanks = _artSo.FindProperty("taskCardBlanks");
            for (var i = 0; i < 3; i++)
            {
                Picture(Element(blanks, i), typeof(Texture), $"文字なしのカード（{i + 1} 番目）", "課題のカードの絵が無い課題で使う（番号と灰色の四角は絵の中。やること・説明はコードで書く）。");
            }

            Section("画面");
            Picture(_artSo.FindProperty("background"), typeof(Texture), "背景", "画面いっぱいの背景。");
            Picture(_artSo.FindProperty("halftone"), typeof(Texture), "網点", "背景の飾り（右下・左上）。");
            Picture(_artSo.FindProperty("stamp"), typeof(Texture), "ハンコの輪", "できた課題に押すハンコ。");
            var badges = _artSo.FindProperty("seatBadges");
            for (var i = 0; i < 4; i++)
            {
                if (badges.arraySize <= i)
                {
                    badges.arraySize = 4;
                }

                Picture(badges.GetArrayElementAtIndex(i), typeof(Texture), $"{i + 1}P の札", "右下「みんなの研修」のアイコンの左上。");
            }

            Section("右上「いまの研修」の欄");
            Picture(_artSo.FindProperty("hand"), typeof(Texture), "手の絵", "左半分 = 開いた手、右半分 = 握った手 が並んだ1枚の絵。");
            Picture(_artSo.FindProperty("bubble"), typeof(Texture), "泡の絵", "研修3「泡を割る」の行の絵。");

            Section("フィールドの札");
            Picture(_artSo.FindProperty("enemy"), typeof(Texture), "ばい菌の絵", "研修1で出す「ばい菌（敵）」の説明の札。");

            Section("練習の敵（1人ずつ別の敵）");
            Picture(_artSo.FindProperty("enemySheet"), typeof(Texture2D), "敵のスプライトシート",
                "横 = コマ、縦 = 向き（上から 右, 右上, 上, 左上, 左, 左下, 下, 右下）。空なら本番の敵の絵。");
            EditorGUILayout.PropertyField(_artSo.FindProperty("enemySheetFrames"), new GUIContent("横のコマ数（0 = 本番と同じ）"));
            EditorGUILayout.PropertyField(_artSo.FindProperty("enemySheetDirections"), new GUIContent("縦の向きの数（0 = 本番と同じ）"));

            Section("絵で描いたフィールド（練習のシーンを使わないときだけ）");
            Picture(_artSo.FindProperty("fieldPlate"), typeof(Texture), "フィールドの背景", "空なら、ステージごとに撮った絵。");
            Picture(_artSo.FindProperty("core"), typeof(Texture), "真ん中のコア", "空なら、撮ったコアの絵。");
            Picture(_artSo.FindProperty("beam"), typeof(Texture), "ビームの模様", "空なら、あそびかたの設定のビームの絵。");
        }

        private void DrawCharacterArt()
        {
            var art = FindCharacter(_artSo.FindProperty("characters"));
            var text = FindCharacter(_textSo.FindProperty("characters"));
            if (art == null)
            {
                EditorGUILayout.HelpBox("このキャラの欄が絵のファイルにありません。", MessageType.Warning);
                return;
            }

            Section("左下の顔の窓");
            Picture(art.FindPropertyRelative("image"), typeof(Sprite), "顔の窓の絵", "空なら、キャラ選択の立ち絵を下の「立ち絵の切り抜き」で出す。");
            EditorGUILayout.PropertyField(art.FindPropertyRelative("crop"), new GUIContent("上の絵の切り抜き",
                "x,y = 真ん中（絵の幅・高さに対する割合、y は下から）、w = 幅（1 = 絵の横幅いっぱい）。h は使わない。顔だけの絵なら (0.5, 0.5, 1)。"));
            if (text != null)
            {
                EditorGUILayout.PropertyField(text.FindPropertyRelative("portraitCrop"), new GUIContent("立ち絵の切り抜き（絵を入れないとき）",
                    "キャラ選択の立ち絵のどこを出すか。x,y = 真ん中、w = 幅（立ち絵の横幅に対する割合）。"));
            }

            Section("右下「みんなの研修」のアイコン");
            Picture(art.FindPropertyRelative("iconOff"), typeof(Sprite), "ふだん", "空なら、キャラ選択のボタンの絵（OFF）。");
            Picture(art.FindPropertyRelative("iconOn"), typeof(Sprite), "握っている間・修了したとき", "空なら、キャラ選択のボタンの絵（ON）。");

            Section("絵で描いたフィールドの砲台（練習のシーンを使わないときだけ）");
            Picture(art.FindPropertyRelative("pod"), typeof(Sprite), "砲台の絵", "空なら、キャラ選択の小さいキャラの絵。");
        }

        // ───────── セリフ ─────────

        private void DrawLines()
        {
            var look = FindCharacter(_textSo.FindProperty("characters"));
            if (look == null)
            {
                EditorGUILayout.HelpBox("このキャラのセリフが文字のファイルにありません。", MessageType.Warning);
                return;
            }

            Section("名前");
            Line(look.FindPropertyRelative("displayName"), "名前", "顔の窓の名札・みんなの研修。空ならキャラ選択の名前。", false);

            Section("研修のセリフ（その課題に来たとき）");
            Line(look.FindPropertyRelative("introLine"), "最初のひとこと", "研修が始まってすぐ（数秒）。");
            var steps = look.FindPropertyRelative("stepLines");
            for (var i = 0; i < TaskNames.Length; i++)
            {
                Line(Element(steps, i), TaskNames[i], StepWhere(i));
            }

            Section("研修1: ばい菌を倒したとき");
            var kills = look.FindPropertyRelative("killLines");
            Line(Element(kills, 0), "1体目", "{0} = 倒した数、{1} = あと何体。");
            Line(Element(kills, 1), "2体目（3体目以降も）", "{0} = 倒した数、{1} = あと何体。");

            Section("そのほか");
            Line(look.FindPropertyRelative("overheatLine"), "オーバーヒートしたとき", "すぐ割り込んで出る。");
            StringList(look.FindPropertyRelative("skillLines"), "スキルを出したときの掛け声", "いくつか書くと、その中から出る。");
            Line(look.FindPropertyRelative("afterSkillLine"), "スキルを使い終わったあと", "研修修了までの数秒。");
            Line(look.FindPropertyRelative("doneLine"), "自分が研修を全部終えたとき", "ほかの人を待つ間。");
            Line(look.FindPropertyRelative("allDoneLine"), "全員そろったとき", "本番へ進む直前。");

            EditorGUILayout.Space(8f);
            _showCommon = EditorGUILayout.Foldout(_showCommon, "共通のセリフ（キャラのセリフが空のときに使う）", true);
            if (!_showCommon)
            {
                return;
            }

            Line(_textSo.FindProperty("introLine"), "最初のひとこと", string.Empty);
            var common = _textSo.FindProperty("stepLines");
            for (var i = 0; i < TaskNames.Length; i++)
            {
                Line(Element(common, i), TaskNames[i], string.Empty);
            }

            Line(Element(_textSo.FindProperty("killLines"), 0), "ばい菌を倒したとき", "{0} = 倒した数、{1} = あと何体。");
            Line(_textSo.FindProperty("overheatLine"), "オーバーヒートしたとき", string.Empty);
            Line(_textSo.FindProperty("afterSkillLine"), "スキルを使い終わったあと", string.Empty);
            Line(_textSo.FindProperty("doneLine"), "自分が研修を全部終えたとき", string.Empty);
            Line(_textSo.FindProperty("allDoneLine"), "全員そろったとき", string.Empty);
        }

        private static string StepWhere(int step)
        {
            switch (step)
            {
                case 1: return "研修1の「握り続けて倒す」。";
                case 3: return "研修2の始まり（オーバーヒートまで握ってみよう）。";
                case 5: return "研修3の始まり（泡を壊すとスキル）。";
                case 6: return "スキルが使えるようになったとき（キャラごとのスキルの使い方）。";
                default: return string.Empty;
            }
        }

        // ───────── 画面の文字 ─────────

        private void DrawScreenTexts()
        {
            Section("研修の名前");
            var titles = _textSo.FindProperty("lessonTitles");
            for (var i = 0; i < 3; i++)
            {
                Line(Element(titles, i), $"研修{i + 1}", "右上の欄の見出し・フィールド左上の札。", false);
            }

            Section("課題（右上の欄）");
            var labels = _textSo.FindProperty("taskLabels");
            var hints = _textSo.FindProperty("taskHints");
            var ui = _textSo.FindProperty("ui");
            var captions = ui.FindPropertyRelative("captions");
            for (var i = 0; i < TaskNames.Length; i++)
            {
                EditorGUILayout.LabelField(TaskNames[i], EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    Line(Element(labels, i), "やること", i == 1 ? "{0} = 倒す数。" : string.Empty, false);
                    Line(Element(hints, i), "小さい説明", string.Empty, false);
                    Line(Element(captions, i), "手の絵の下の文字", string.Empty, false);
                }
            }

            Section("いろいろな文字");
            Line(_textSo.FindProperty("waitingText"), "ほかの人を待つとき", string.Empty, false);
            Line(_textSo.FindProperty("allDoneText"), "全員そろったとき", string.Empty, false);
            Line(_textSo.FindProperty("localMark"), "自分の印", "みんなの研修の自分のアイコンの上。", false);
            Line(_textSo.FindProperty("startText"), "本番へ進むとき", string.Empty, false);

            // そのほかの画面の文字（見出しと説明は、文字のファイルに書いたものをそのまま出す）。
            var it = ui.Copy();
            var end = ui.GetEndProperty();
            if (!it.NextVisible(true))
            {
                return;
            }

            while (!SerializedProperty.EqualContents(it, end))
            {
                if (it.name != "captions")
                {
                    EditorGUILayout.PropertyField(it, true);
                }

                if (!it.NextVisible(false))
                {
                    break;
                }
            }
        }

        // ───────── 部品 ─────────

        private SerializedProperty FindCharacter(SerializedProperty list)
        {
            if (list == null || !list.isArray)
            {
                return null;
            }

            var id = CharacterIds[Mathf.Clamp(_character, 0, CharacterIds.Length - 1)];
            for (var i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                var idProp = item.FindPropertyRelative("characterId");
                if (idProp != null && idProp.stringValue == id)
                {
                    return item;
                }
            }

            return null;
        }

        private static SerializedProperty Element(SerializedProperty array, int index)
        {
            if (array == null || !array.isArray)
            {
                return null;
            }

            if (array.arraySize <= index)
            {
                array.arraySize = index + 1;
            }

            return array.GetArrayElementAtIndex(index);
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(2f);
        }

        /// <summary>絵の欄（小さい見本つき）。</summary>
        private void Picture(SerializedProperty prop, System.Type type, string label, string where)
        {
            if (prop == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.ObjectField(prop.objectReferenceValue, type, false, GUILayout.Width(64f), GUILayout.Height(64f));
                if (EditorGUI.EndChangeCheck())
                {
                    prop.objectReferenceValue = value;
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(where, _help);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "（空 = 今までの絵）", _help);
                        using (new EditorGUI.DisabledScope(prop.objectReferenceValue == null))
                        {
                            if (GUILayout.Button("空にする", GUILayout.Width(70f)))
                            {
                                prop.objectReferenceValue = null;
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space(2f);
        }

        /// <summary>文字の欄（見出し・どこに出るか・書く所）。</summary>
        private void Line(SerializedProperty prop, string label, string where, bool multiLine = true)
        {
            if (prop == null || prop.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(where))
            {
                EditorGUILayout.LabelField(where, _help);
            }

            EditorGUI.BeginChangeCheck();
            var value = multiLine
                ? EditorGUILayout.TextArea(prop.stringValue, _area, GUILayout.MinHeight(38f))
                : EditorGUILayout.TextField(prop.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                prop.stringValue = value;
            }

            EditorGUILayout.Space(2f);
        }

        /// <summary>いくつでも書ける文字の欄（＋・－で増やす・減らす）。</summary>
        private void StringList(SerializedProperty list, string label, string where)
        {
            if (list == null || !list.isArray)
            {
                return;
            }

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(where, _help);
            for (var i = 0; i < list.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var item = list.GetArrayElementAtIndex(i);
                    EditorGUI.BeginChangeCheck();
                    var value = EditorGUILayout.TextField(item.stringValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        item.stringValue = value;
                    }

                    if (GUILayout.Button("－", GUILayout.Width(24f)))
                    {
                        list.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("＋ 増やす", GUILayout.Width(90f)))
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = string.Empty;
            }

            EditorGUILayout.Space(2f);
        }
    }
}
