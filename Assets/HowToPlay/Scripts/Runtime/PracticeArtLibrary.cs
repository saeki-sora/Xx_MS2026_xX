using System;
using System.Collections.Generic;
using MS2026.CharacterSelect;
using UnityEngine;
using UnityEngine.Serialization;

namespace MS2026.HowToPlay
{
    /// <summary>キャラ1人ぶんの、練習の画面の絵の差し替え（2026-11-19）。空の欄は、キャラ選択のキャラの絵のまま。</summary>
    [Serializable]
    public sealed class PracticePortrait
    {
        [Tooltip("キャラ選択のキャラの id（alpha = 赤星レイザ・beta = 青波雫・delta = パチ・gamma = 緑谷ミラ）。")]
        public string characterId = "alpha";

        [Tooltip("左下の顔の窓に出す絵。空なら、キャラ選択の立ち絵を「練習の画面の設定」の切り抜きで出す。")]
        public Sprite image;

        [Tooltip("上の絵のどこを出すか。x,y = 真ん中（絵の幅・高さに対する割合、y は下から）、w = 幅（絵の幅に対する割合。1 = 絵の横幅いっぱい）。" +
                 "高さは窓の形（横 450 × 縦 340）に合わせて決まる（h は使わない）。顔だけの絵なら (0.5, 0.5, 1) のままでよい。")]
        public Rect crop = new Rect(0.5f, 0.5f, 1f, 0f);

        [Tooltip("右下「みんなの研修」のアイコン（ふだん）。空なら、キャラ選択のボタンの絵（OFF）。")]
        public Sprite iconOff;

        [Tooltip("右下「みんなの研修」のアイコン（握っている間・修了したとき）。空なら、キャラ選択のボタンの絵（ON）。")]
        public Sprite iconOn;

        [Tooltip("絵で描いたフィールド（練習のシーンを使わないとき）の砲台の絵。空なら、キャラ選択の小さいキャラの絵。")]
        public Sprite pod;
    }

    /// <summary>
    /// 練習の画面（新人研修）で使う絵の置き場（Resources/PracticeArtLibrary・2026-11-16、すべて差し替え可 2026-11-19）。
    /// ここに入れた絵が一番に使われる。空の欄は「練習の画面の設定」（<see cref="HowToPlaySettings.screen"/>）の絵、
    /// キャラの絵はキャラ選択の絵のまま。絵を替えたいときは、このファイルの欄に入れるだけでよい（コードは触らない）。
    /// ※ フィールドの本物の砲台・敵（群衆）・泡・スキルは本番のゲームの物（練習のシーン）なので、ここでは替えない。
    ///   練習の敵（1人ずつ別の敵）の絵だけは、下の「練習の敵」で替えられる。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/あそびかた/練習の画面の絵", fileName = "PracticeArtLibrary")]
    public sealed class PracticeArtLibrary : ScriptableObject
    {
        public const string ResourcePath = "PracticeArtLibrary";

        [Header("画面")]
        [Tooltip("画面いっぱいの背景。")]
        public Texture background;

        [Tooltip("左上のロゴ。")]
        public Texture logo;

        [Tooltip("網点（顔の窓の下・背景の飾り）。")]
        public Texture halftone;

        [Tooltip("しま模様（背景の飾り）。")]
        public Texture stripes;

        [Tooltip("きらきら（飾り）。")]
        public Texture sparkle;

        [Tooltip("矢印（飾り）。")]
        public Texture arrow;

        [Tooltip("できたときのハンコの輪（研修の札・右の欄）。")]
        public Texture stamp;

        [Tooltip("1P〜4P の札（上から順）。")]
        public Texture[] seatBadges = new Texture[4];

        [Header("キャラ")]
        [Tooltip("キャラの一覧（キャラ選択と同じ物）。誰もいない席に置くダミーのキャラ（砲台の絵・右下のアイコン）を、この並びで選ぶ。")]
        public CharacterRoster roster;

        [Header("文字")]
        [Tooltip("練習の画面の文字のフォント（Zen Maru Gothic Bold・SIL Open Font License。Art/Fonts に OFL.txt）。空なら Unity の標準のフォント。")]
        public Font font;

        [Header("画面の枠と欄（絵で差し替え・2026-11-19。入れた所はコードで描かずに、この絵を出す。空ならコードで描く）")]
        [Tooltip("フィールドの枠（真ん中が透明な絵。「ためしてみてね」の札も絵の中）。フィールドの真ん中に、絵の大きさのまま出す。")]
        public Texture fieldFrame;

        [Tooltip("右下「準備中」の欄の絵（見出しも絵の中）。右上の四角の所に「0/4」を白い文字で出す。")]
        public Texture rosterPanel;

        [Tooltip("右上の研修の欄の絵（研修1〜3の順。研修の名前・「練習」・▼ も絵の中）。空の研修はコードで描く。")]
        public Texture[] lessonPanels = new Texture[3];

        [Tooltip("課題のカードの絵（握る・握り続ける・離す・オーバーヒート・冷ます・泡・スキル の順。番号・やること・説明も絵の中）。" +
                 "左寄りの灰色の四角の所に手の絵を重ねる。空の課題はコードで描く。")]
        public Texture[] taskCards = new Texture[7];

        [Tooltip("研修の名前が入っていない欄の絵（「練習」と ▼ 1つは絵の中）。上の「研修の欄」が空の研修で使い、研修の名前はコードで書く（▼ が足りないときはコードで足す）。" +
                 "研修1の欄の絵から、研修の名前と2つ目の ▼ を消して作った（2026-11-20）。")]
        public Texture lessonPanelBlank;

        [Tooltip("やること・説明が入っていないカードの絵（1・2・3 番目。番号と灰色の四角は絵の中）。上の「課題のカード」が空の課題で使い、やること・説明はコードで書く。" +
                 "研修1のカードの絵から文字を消して作った（2026-11-20）。")]
        public Texture[] taskCardBlanks = new Texture[3];

        [Tooltip("今の課題のカードに重ねる光るふち（真ん中が透明な絵）。")]
        public Texture taskCardCurrent;

        [Header("いまの研修（右上の欄）")]
        [Tooltip("開いた手（左半分）と握った手（右半分）が並んだ絵。")]
        public Texture hand;

        [Tooltip("泡（スマッシュボール）の絵（研修3の手の代わりの絵）。")]
        public Texture bubble;

        [Header("フィールドの札")]
        [Tooltip("ばい菌の説明の札の絵（研修1で出す）。")]
        public Texture enemy;

        [Header("絵で描いたフィールド（練習のシーンを使わないときだけ）")]
        [Tooltip("フィールドの背景（どのステージでもこの絵にする）。空なら、ステージごとに撮った絵。")]
        public Texture fieldPlate;

        [Tooltip("真ん中のコアの絵。空なら、撮ったコアの絵。")]
        public Texture core;

        [Tooltip("ビームの模様。空なら「あそびかたの設定」のビームの絵。")]
        public Texture beam;

        [Header("練習の敵（1人ずつ別の敵）")]
        [Tooltip("練習の敵のスプライトシート（横 = コマ、縦 = 向き。上から 右, 右上, 上, 左上, 左, 左下, 下, 右下）。空なら、本番の敵の絵。")]
        public Texture2D enemySheet;

        [Tooltip("上のシートの横のコマ数（0 = 本番の敵と同じ）。")]
        [Min(0)]
        public int enemySheetFrames;

        [Tooltip("上のシートの縦の向きの数（1 / 2 / 4 / 8。0 = 本番の敵と同じ）。")]
        [Min(0)]
        public int enemySheetDirections;

        [Header("キャラごとの絵（顔の窓・みんなの研修のアイコン・砲台）")]
        [Tooltip("絵を入れた欄だけ、その絵に替わる。空の欄はキャラ選択の絵のまま。")]
        [FormerlySerializedAs("portraits")]
        public PracticePortrait[] characters =
        {
            new PracticePortrait { characterId = "alpha" },
            new PracticePortrait { characterId = "beta" },
            new PracticePortrait { characterId = "delta" },
            new PracticePortrait { characterId = "gamma" }
        };

        /// <summary>
        /// 誰もいない席に置くダミーのキャラの id（席 0〜3。人がいる席は null）。2026-11-23。
        /// 人がいる席のキャラ（キャラ選択の結果。通っていなければ一覧のその席の番目）は使わず、残りを一覧の上から順に、空いている席へ配る。
        /// 練習のシーンの砲台（ダミーの絵）と、画面右下のアイコンの両方がこれを使うので、必ず同じキャラになる。
        /// </summary>
        public static string[] DummyCharacterIds(Func<int, bool> present)
        {
            var ids = new string[4];
            var lib = Load();
            var roster = lib != null ? lib.roster : null;
            if (roster == null || present == null)
            {
                return ids;
            }

            var used = new HashSet<string>();
            for (var p = 0; p < ids.Length; p++)
            {
                if (!present(p))
                {
                    continue;
                }

                var chosen = CharacterSelectionResult.HasResult ? CharacterSelectionResult.Get(p) : roster.Get(p);
                if (chosen != null && !string.IsNullOrEmpty(chosen.id))
                {
                    used.Add(chosen.id);
                }
            }

            for (var p = 0; p < ids.Length; p++)
            {
                if (present(p))
                {
                    continue;
                }

                for (var i = 0; i < roster.Count; i++)
                {
                    var c = roster.Get(i);
                    if (c != null && !string.IsNullOrEmpty(c.id) && !used.Contains(c.id))
                    {
                        ids[p] = c.id;
                        used.Add(c.id);
                        break;
                    }
                }
            }

            return ids;
        }

        /// <summary>キャラの一覧から id のキャラを探す（無ければ null）。</summary>
        public static CharacterDefinition FindRosterCharacter(string id)
        {
            var lib = Load();
            if (lib == null || lib.roster == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            var index = lib.roster.IndexOf(id);
            return index >= 0 ? lib.roster.Get(index) : null;
        }

        /// <summary>配列の i 番目（無ければ null）。</summary>
        public static Texture At(Texture[] list, int i) => list != null && i >= 0 && i < list.Length ? list[i] : null;

        private static PracticeArtLibrary _loaded;

        public static PracticeArtLibrary Load()
        {
            if (_loaded == null)
            {
                _loaded = Resources.Load<PracticeArtLibrary>(ResourcePath);
            }

            return _loaded;
        }

        /// <summary>そのキャラの欄（無ければ null）。</summary>
        public PracticePortrait FindCharacter(string characterId)
        {
            if (characters == null || string.IsNullOrEmpty(characterId))
            {
                return null;
            }

            foreach (var c in characters)
            {
                if (c != null && c.characterId == characterId)
                {
                    return c;
                }
            }

            return null;
        }

        /// <summary>
        /// 設定の写しを作り、絵をここの絵で置き換える（ここが空の欄は設定の絵のまま。設定のファイルそのものは変えない）。
        /// </summary>
        public static PracticeScreenSettings Fill(PracticeScreenSettings source)
        {
            var copy = (source ?? new PracticeScreenSettings()).Copy();
            var lib = Load();
            if (lib == null)
            {
                return copy;
            }

            copy.background = Pick(lib.background, copy.background);
            copy.logo = Pick(lib.logo, copy.logo);
            copy.halftone = Pick(lib.halftone, copy.halftone);
            copy.stripes = Pick(lib.stripes, copy.stripes);
            copy.sparkle = Pick(lib.sparkle, copy.sparkle);
            copy.arrow = Pick(lib.arrow, copy.arrow);
            copy.stamp = Pick(lib.stamp, copy.stamp);
            copy.hand = Pick(lib.hand, copy.hand);
            copy.bubble = Pick(lib.bubble, copy.bubble);
            copy.enemy = Pick(lib.enemy, copy.enemy);
            copy.core = Pick(lib.core, copy.core);

            var badges = new Texture[4];
            for (var i = 0; i < badges.Length; i++)
            {
                var own = copy.seatBadges != null && i < copy.seatBadges.Length ? copy.seatBadges[i] : null;
                var mine = lib.seatBadges != null && i < lib.seatBadges.Length ? lib.seatBadges[i] : null;
                badges[i] = Pick(mine, own);
            }

            copy.seatBadges = badges;

            if (lib.fieldPlate != null)
            {
                // どのステージでも、この背景にする（大きさの値は設定のまま）。
                var plates = copy.stagePlates != null && copy.stagePlates.Length > 0 ? copy.stagePlates : new[] { new PracticeStagePlate() };
                var replaced = new PracticeStagePlate[plates.Length];
                for (var i = 0; i < plates.Length; i++)
                {
                    var p = plates[i] ?? new PracticeStagePlate();
                    replaced[i] = new PracticeStagePlate { sceneName = p.sceneName, plate = lib.fieldPlate, plateWorldSize = p.plateWorldSize, turretRadius = p.turretRadius };
                }

                copy.stagePlates = replaced;
            }

            return copy;
        }

        private static Texture Pick(Texture first, Texture second) => first != null ? first : second;
    }
}
