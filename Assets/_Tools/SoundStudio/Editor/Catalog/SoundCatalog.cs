using System.Collections.Generic;
using System.Text.RegularExpressions;
using static MS2026.SoundStudio.SoundPageId;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 名前の表。プロジェクトの音の欄（FortressEffect と BGM）を、どのページのどの小見出しに、何という名前で出すかを決める。
    /// 欄が増えたら、ここに1行足すだけで並ぶ（表にない欄は「その他」に、欄の説明を名前にして出る）。
    /// 「その人の事」（Personal）は、鳴らす所が持ち主の番号を渡している欄（2026-11-22 にコードで確かめた）。
    /// </summary>
    public static class SoundCatalog
    {
        private const string Title = "MS2026.Title.";
        private const string Chara = "MS2026.CharacterSelect.";
        private const string HowTo = "MS2026.HowToPlay.";
        private const string Stages = "MS2026.Stages.";
        private const string Fort = "MS2026.Fortress.";
        private const string Skills = "MS2026.Skills.";
        private const string Session = "MS2026.UI.Game.GameSession";

        private static readonly SoundCatalogEntry[] EntryTable =
        {
            // タイトル
            E(Title + "TitleOpening", "onPinch", SoundPageId.Title, "オープニング", "チューブをつまむ"),
            E(Title + "TitleOpening", "onEject", SoundPageId.Title, "オープニング", "歯みがき粉が飛び出す"),
            E(Title + "TitleOpening", "onCovered", SoundPageId.Title, "オープニング", "画面が覆われた"),
            E(Title + "TitleElementMotion", "onLand", SoundPageId.Title, "オープニング", "文字・部品が着地した"),
            P(Title + "TitleScreenController", "onGripBegin", SoundPageId.Title, "スタート", "握りはじめた（スタートの前）"),
            E(Title + "TitleScreenController", "onStart", SoundPageId.Title, "スタート", "スタートを押した"),
            Bgm(Title + "TitleScreenController", "bgm", SoundPageId.Title, "タイトルの BGM"),

            // ロビー
            E(Session, "onPlayerJoined", Lobby, "人の出入り", "人が入った"),
            E(Session, "onPlayerLeft", Lobby, "人の出入り", "人が抜けた"),
            E(Session, "onReadyChanged", Lobby, "準備", "準備OKが変わった"),
            E(Session, "onProblem", Lobby, "準備", "うまくいかなかった（お知らせ）"),
            Bgm(Session, "lobbyBgm", Lobby, "ロビーの BGM"),

            // キャラ選択
            P(Chara + "CharacterSelectController", "onMove", CharacterSelect, "カーソル", "カーソルを動かした"),
            P(Chara + "CharacterSelectController", "onConfirm", CharacterSelect, "カーソル", "キャラを決めた"),
            P(Chara + "CharacterSelectController", "onCancel", CharacterSelect, "カーソル", "決めたのを取り消した"),
            P(Chara + "CharacterSelectController", "onRejected", CharacterSelect, "カーソル", "選べなかった（取られている）"),
            V(Chara + "CharacterSelectController", "characterVoices", CharacterSelect, "カーソル", "キャラの声", true),
            E(Chara + "CharacterSelectController", "onAllDecided", CharacterSelect, "みんな", "全員が決めた"),
            Bgm(Chara + "CharacterSelectController", "bgm", CharacterSelect, "キャラ選択・遊び方の BGM"),
            E(Chara + "PasteCurtainSettings", "onCover", CharacterSelect, "歯みがき粉の幕", "幕が閉じる"),
            E(Chara + "PasteCurtainSettings", "onReveal", CharacterSelect, "歯みがき粉の幕", "幕が開く"),

            // 遊び方
            E(HowTo + "HowToPlaySettings", "onStepDone", HowToPlay, "握る練習", "練習の1段ができた"),
            E(HowTo + "HowToPlaySettings", "onLocalDone", HowToPlay, "握る練習", "自分の練習が全部できた"),
            E(HowTo + "HowToPlaySettings", "onOverheat", HowToPlay, "握る練習", "練習でオーバーヒートした"),
            E(HowTo + "HowToPlaySettings", "onFinished", HowToPlay, "握る練習", "練習の時間が終わった"),
            E(HowTo + "HowToPlaySettings", "onTipShow", HowToPlay, "読み込み中", "歯みがきの豆知識が出た"),

            // ステージ抽選
            E(Stages + "StageRouletteSettings", "onStart", Roulette, "ルーレット", "回りはじめた"),
            E(Stages + "StageRouletteSettings", "onTick", Roulette, "ルーレット", "1つ進んだ（カチカチ）"),
            E(Stages + "StageRouletteSettings", "onStop", Roulette, "ルーレット", "止まった"),

            // ステージ登場
            E(Stages + "StageCoreDrop", "onFallStart", StageIntro, "コア", "コアが落ちはじめた"),
            E(Stages + "StageCoreDrop", "onLand", StageIntro, "コア", "コアが着地した"),
            P(Stages + "StageDropKindSettings", "onLand", StageIntro, "砲台・物", "砲台・物が着地した"),
            E(Stages + "StageDropIntroSettings", "onFinished", StageIntro, "砲台・物", "全部そろった"),

            // 試合 › 試合の進行
            E(Session, "onCountdownTick", SoundPageId.Match, "試合の進行", "始まる前のカウントダウン"),
            E(Session, "onMatchStart", SoundPageId.Match, "試合の進行", "試合スタート"),
            E(Fort + "WavePlan", "onOpening", SoundPageId.Match, "試合の進行", "最初のあいさつ"),
            E(Fort + "WavePlan", "onAnnounce", SoundPageId.Match, "試合の進行", "次のウェーブのお知らせ"),
            E(Fort + "WavePlan", "onCountdownTick", SoundPageId.Match, "試合の進行", "ウェーブ前のカウントダウン"),
            E(Fort + "WavePlan", "onWaveStart", SoundPageId.Match, "試合の進行", "ウェーブ開始"),
            E(Fort + "WavePlan", "onTimeWarning", SoundPageId.Match, "試合の進行", "残り時間が少ない（10秒前から1秒ごと）"),
            E(Fort + "WavePlan", "onWaveClear", SoundPageId.Match, "試合の進行", "ウェーブクリア"),
            E(Fort + "WavePlan", "onFinalAnnounce", SoundPageId.Match, "試合の進行", "最後のウェーブのお知らせ"),
            E("MS2026.UI.Game.WaveNewEnemyCards", "onCardsShown", SoundPageId.Match, "試合の進行", "新しい敵の紹介カードが出た"),
            Bgm(Fort + "FortressBgmPlayer", "bgm", SoundPageId.Match, "試合の BGM"),
            Bgm(Fort + "FortressBgmPlayer", "extraBgms", SoundPageId.Match, "試合の BGM（切り替え用）"),
            Bgm("MS2026.Stage.StageSet", "bgm", SoundPageId.Match, "ステージの BGM"),

            // 試合 › レーザー
            P(Fort + "LaserEffectSettings", "muzzle", SoundPageId.Match, "レーザー", "撃っている（銃口）"),
            P(Fort + "LaserEffectSettings", "impact", SoundPageId.Match, "レーザー", "当たっている所"),
            P(Fort + "LaserEffectSettings", "swarmHit", SoundPageId.Match, "レーザー", "敵に当たった"),
            P(Fort + "LaserEffectSettings", "steam", SoundPageId.Match, "レーザー", "冷ましている（湯気）"),
            P(Fort + "LaserEffectSettings", "onChargeStart", SoundPageId.Match, "レーザー", "握りはじめ（溜め）"),
            P(Fort + "LaserEffectSettings", "onOverheat", SoundPageId.Match, "レーザー", "オーバーヒートした"),
            P(Fort + "LaserEffectSettings", "onRecovered", SoundPageId.Match, "レーザー", "オーバーヒートから戻った"),
            P(Fort + "LaserEffectSettings", "onHeatWarning", SoundPageId.Match, "レーザー", "熱が危ない（もうすぐオーバーヒート）"),
            P(Fort + "LaserEffectSettings", "onDryFire", SoundPageId.Match, "レーザー", "オーバーヒート中に握った（空撃ち）"),
            P(Fort + "LaserEffectSettings", "heatLoop", SoundPageId.Match, "レーザー", "撃っている間の熱の音（熱いほど高く・ループの音を選ぶ）"),

            // 試合 › 敵・コア
            E(Fort + "WavePlan", "onEnemiesAppear", SoundPageId.Match, "敵・コア", "敵が画面に出てくる"),
            P("MS2026.ObjectToon.KillFx.KillFxSettings", "onKill", SoundPageId.Match, "敵・コア", "敵をやっつけた"),
            E(Fort + "FoamPopSettings", "sound", SoundPageId.Match, "敵・コア", "泡がはじけた"),
            E(Fort + "FoamPopSettings", "soundMany", SoundPageId.Match, "敵・コア", "泡がたくさんはじけた"),
            E(Fort + "CoreEffectSettings", "onDamaged", SoundPageId.Match, "敵・コア", "コアがやられた"),
            E(Fort + "CoreEffectSettings", "onDestroyed", SoundPageId.Match, "敵・コア", "コアがこわれた"),
            E(Fort + "CoreEffectSettings", "onPinch", SoundPageId.Match, "敵・コア", "コアがピンチ（HPが少ない）"),
            E(Fort + "CoreEffectSettings", "onHealed", SoundPageId.Match, "敵・コア", "コアが回復した"),
            P("MS2026.UI.Game.FortressUiValues", "onComboStep", SoundPageId.Match, "敵・コア", "コンボ（10・50・100…）"),
            P(Fort + "EnemyBombSettings", "onExplode", SoundPageId.Match, "敵・コア", "爆弾の敵が爆発した"),
            P(Fort + "EnemyShieldSettings", "onFocusChanged", SoundPageId.Match, "敵・コア", "盾の敵がねらいを変えた"),
            P(Fort + "EnemyShieldSettings", "onBreak", SoundPageId.Match, "敵・コア", "盾がこわれた"),

            // 試合 › 壊せる物
            P(Fort + "DestructibleFeedbackSettings", "onHit", SoundPageId.Match, "壊せる物", "当たった"),
            P(Fort + "DestructibleFeedbackSettings", "onStageChanged", SoundPageId.Match, "壊せる物", "ひびが進んだ"),
            P(Fort + "DestructibleFeedbackSettings", "onDestroyed", SoundPageId.Match, "壊せる物", "こわれた"),
            P(Fort + "DestructibleFeedbackSettings", "onRegenerated", SoundPageId.Match, "壊せる物", "元に戻った"),
            P(Fort + "SmashBallSettings", "onBroken", SoundPageId.Match, "壊せる物", "スマッシュボールをこわした"),

            // 試合 › スキル
            E(Skills + "SkillCutInSounds", "open", SoundPageId.Match, "スキル › カットイン", "カットインが出る"),
            E(Skills + "SkillCutInSounds", "close", SoundPageId.Match, "スキル › カットイン", "カットインが消える"),
            V(Skills + "SkillCutInEntry", "voice", SoundPageId.Match, "スキル › カットイン", "カットインの声", false),
            P(Skills + "Red.RedLaserSounds", "onReady", SoundPageId.Match, "スキル › 赤", "スキルが使えるようになった"),
            P(Skills + "Red.RedLaserSounds", "onWindup", SoundPageId.Match, "スキル › 赤", "溜め"),
            P(Skills + "Red.RedLaserSounds", "onFire", SoundPageId.Match, "スキル › 赤", "極太レーザー発射"),
            P(Skills + "Red.RedLaserSounds", "whileFiring", SoundPageId.Match, "スキル › 赤", "撃っている間"),
            P(Skills + "Red.RedLaserSounds", "onImpact", SoundPageId.Match, "スキル › 赤", "当たった所"),
            P(Skills + "Red.RedLaserSounds", "onEnd", SoundPageId.Match, "スキル › 赤", "終わり"),
            P(Skills + "Blue.BlueSkillSounds", "onReady", SoundPageId.Match, "スキル › 青", "スキルが使えるようになった"),
            P(Skills + "Blue.BlueSkillSounds", "onWindup", SoundPageId.Match, "スキル › 青", "溜め"),
            P(Skills + "Blue.BlueSkillSounds", "onThrow", SoundPageId.Match, "スキル › 青", "ボトルを投げた"),
            P(Skills + "Blue.BlueSkillSounds", "whileFlying", SoundPageId.Match, "スキル › 青", "飛んでいる間"),
            P(Skills + "Blue.BlueSkillSounds", "onBurst", SoundPageId.Match, "スキル › 青", "うずが生まれた"),
            P(Skills + "Blue.BlueSkillSounds", "whileVortex", SoundPageId.Match, "スキル › 青", "うずの間"),
            P(Skills + "Blue.BlueSkillSounds", "onTick", SoundPageId.Match, "スキル › 青", "うずが吸い込む（くり返し）"),
            P(Skills + "Blue.BlueSkillSounds", "onEnd", SoundPageId.Match, "スキル › 青", "終わり"),
            P(Skills + "Green.GreenGuardianSounds", "onReady", SoundPageId.Match, "スキル › 緑", "スキルが使えるようになった"),
            P(Skills + "Green.GreenGuardianSounds", "onTubePuff", SoundPageId.Match, "スキル › 緑", "チューブを押した"),
            P(Skills + "Green.GreenGuardianSounds", "onPillar", SoundPageId.Match, "スキル › 緑", "光の柱"),
            P(Skills + "Green.GreenGuardianSounds", "onLand", SoundPageId.Match, "スキル › 緑", "ミントが降り立った"),
            V(Skills + "Green.GreenGuardianSounds", "landVoice", SoundPageId.Match, "スキル › 緑", "降り立ったときの声", true),
            P(Skills + "Green.GreenGuardianSounds", "whileGuarding", SoundPageId.Match, "スキル › 緑", "守っている間"),
            P(Skills + "Green.GreenGuardianSounds", "onLeafHit", SoundPageId.Match, "スキル › 緑", "葉っぱが当たった"),
            P(Skills + "Green.GreenGuardianSounds", "onSuck", SoundPageId.Match, "スキル › 緑", "吸い込まれて戻る"),
            P(Skills + "Green.GreenGuardianSounds", "onEnd", SoundPageId.Match, "スキル › 緑", "終わり"),
            P(Skills + "Yellow.YellowArrowSounds", "onReady", SoundPageId.Match, "スキル › 黄", "スキルが使えるようになった"),
            P(Skills + "Yellow.YellowArrowSounds", "onLaunch", SoundPageId.Match, "スキル › 黄", "矢を放った"),

            // 試合 › 副砲
            P(Skills + "SubGuns.ChainLightningSubGun", "zapEffect", SoundPageId.Match, "副砲", "稲妻チェイン"),
            P(Skills + "SubGuns.HomingBubbleSubGun", "launchEffect", SoundPageId.Match, "副砲", "追尾バブルを撃った"),
            P(Skills + "SubGuns.HomingBubbleSubGun", "popEffect", SoundPageId.Match, "副砲", "バブルがはじけた"),
            P(Skills + "SubGuns.SeedSubGun", "sproutEffect", SoundPageId.Match, "副砲", "種が芽を出した"),

            // 試合 › 席替え・歯みがき
            P(Fort + "WaveSeatRotation", "onAnticipate", SoundPageId.Match, "席替え・歯みがき", "席替えの前ぶれ"),
            P(Fort + "WaveSeatRotation", "onJump", SoundPageId.Match, "席替え・歯みがき", "砲台がジャンプした"),
            P(Fort + "WaveSeatRotation", "onLand", SoundPageId.Match, "席替え・歯みがき", "砲台が着地した"),
            E(Fort + "WaveSeatRotation", "onSparkle", SoundPageId.Match, "席替え・歯みがき", "着地のきらめき"),
            E(Fort + "WaveSeatRotation", "onGather", SoundPageId.Match, "席替え・歯みがき", "みんなが集まった"),
            P(Fort + "WaveSeatRotation", "onOk", SoundPageId.Match, "席替え・歯みがき", "新しい席でOK"),
            E(Fort + "BrushingEvent", "onStart", SoundPageId.Match, "席替え・歯みがき", "歯みがきタイム開始"),
            P(Fort + "BrushingEvent", "onPulse", SoundPageId.Match, "席替え・歯みがき", "握って磨いた"),
            E(Fort + "BrushingEvent", "onSync", SoundPageId.Match, "席替え・歯みがき", "みんなのリズムがそろった"),
            E(Fort + "BrushingEvent", "onResult", SoundPageId.Match, "席替え・歯みがき", "歯みがきの結果"),

            // ボス
            E(Fort + "BossDefinition", "onWarning", Boss, "登場", "ボスが来る（警告）"),
            Bgm(Fort + "BossDefinition", "bossBgm", Boss, "ボス戦の BGM"),
            E(Fort + "BossDefinition", "onEnter", Boss, "登場", "ボスが出てきた"),
            E(Fort + "BossDefinition", "onCannonFire", Boss, "ボスの攻撃", "ボスが撃った"),
            E(Fort + "BossDefinition", "onVentSpawn", Boss, "ボスの攻撃", "ボスが敵を出した"),
            E(Fort + "BossDefinition", "onBulletBreak", Boss, "ボスの攻撃", "ボスの弾をこわした"),
            E(Fort + "BossDefinition", "onBulletHitCore", Boss, "ボスの攻撃", "ボスの弾がコアに当たった"),
            P(Fort + "BossDefinition", "onPartBreak", Boss, "やっつける", "ボスの部位をこわした"),
            E(Fort + "BossDefinition", "onReachCore", Boss, "やっつける", "ボスがコアに届いた"),
            E(Fort + "BossDefinition", "onDefeat", Boss, "やっつける", "ボスをやっつけた"),

            // 結果
            E(Fort + "FortressMatchRules", "onClear", Result, "試合の終わり", "クリア"),
            E(Fort + "FortressMatchRules", "onGameOver", Result, "試合の終わり", "ゲームオーバー"),
            E(Fort + "FortressMatchRules", "onResultShown", Result, "結果画面", "結果画面が出る"),
            E(Fort + "FortressMatchRules", "onCountUp", Result, "結果画面", "数字が増えていく"),
            E(Fort + "FortressMatchRules", "onMaxCombo", Result, "結果画面", "最大コンボの発表"),
            E(Fort + "FortressMatchRules", "onReturnToTitle", Result, "結果画面", "タイトルへ戻る"),
            Bgm(Fort + "FortressMatchRules", "clearBgm", Result, "クリアの BGM"),
            Bgm(Fort + "FortressMatchRules", "gameOverBgm", Result, "ゲームオーバーの BGM"),
        };

        private static readonly SoundCandidate[] CandidateTable =
        {
        };

        private static readonly Dictionary<string, SoundCatalogEntry> ByKey = BuildIndex();

        private static readonly Regex ArraySuffix = new Regex(@"\.Array\.data\[\d+\]$");

        public static IReadOnlyList<SoundCatalogEntry> Entries => EntryTable;

        /// <summary>まだ欄がない候補（2段目でゲームに足したら、ここから消す）。</summary>
        public static IReadOnlyList<SoundCandidate> Candidates => CandidateTable;

        /// <summary>欄を書いている型と欄の名前から、表の行を探す。無ければ null（その他に出す）。</summary>
        public static SoundCatalogEntry Find(string declaringType, string fieldName)
        {
            return declaringType != null && fieldName != null && ByKey.TryGetValue(declaringType + "|" + fieldName, out var entry) ? entry : null;
        }

        /// <summary>欄の道筋（SerializedProperty.propertyPath）の最後の欄の名前。配列の中なら配列の欄の名前。</summary>
        public static string FieldNameOf(string propertyPath)
        {
            var path = ArraySuffix.Replace(propertyPath ?? string.Empty, string.Empty);
            var dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(dot + 1) : path;
        }

        private static Dictionary<string, SoundCatalogEntry> BuildIndex()
        {
            var map = new Dictionary<string, SoundCatalogEntry>();
            for (var i = 0; i < EntryTable.Length; i++)
            {
                EntryTable[i].Order = i;
                map[EntryTable[i].DeclaringType + "|" + EntryTable[i].FieldName] = EntryTable[i];
            }

            return map;
        }

        // 「この仕組みを使っているか」のスイッチ（同じ設定の中の bool の欄）。OFF の物の音の欄は「使っていない」として薄く出す。
        private static readonly Dictionary<string, string> ActiveSwitches = new Dictionary<string, string>
        {
            { Fort + "EnemyBombSettings", "explodeOnDeath" },
            { Fort + "EnemyShieldSettings", "hasShield" },
            { Skills + "SkillCutInEntry", "enabled" },
        };

        /// <summary>この型の音の欄が使われているかを決める bool の欄の名前（無ければ null = いつも使っている）。</summary>
        public static string ActiveSwitchOf(string declaringType)
        {
            return declaringType != null && ActiveSwitches.TryGetValue(declaringType, out var name) ? name : null;
        }

        // みんなの事
        private static SoundCatalogEntry E(string type, string field, SoundPageId page, string group, string label)
            => new SoundCatalogEntry(type, field, page, group, label, personal: false);

        // その人の事
        private static SoundCatalogEntry P(string type, string field, SoundPageId page, string group, string label)
            => new SoundCatalogEntry(type, field, page, group, label, personal: true);

        // 声
        private static SoundCatalogEntry V(string type, string field, SoundPageId page, string group, string label, bool personal)
            => new SoundCatalogEntry(type, field, page, group, label, personal, voice: true);

        private static SoundCatalogEntry Bgm(string type, string field, SoundPageId page, string label)
            => new SoundCatalogEntry(type, field, page, "BGM", label, personal: false, isBgm: true);
    }
}
