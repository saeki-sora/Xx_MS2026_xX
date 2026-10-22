using System;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Stages
{
    /// <summary>
    /// ステージの一覧とルーレットの設定（1つだけ。Resources/StageCatalog に置く）。
    /// ・ロビー経由の本番: キャラ選択の後、ホストがこの一覧からランダムに選び、ルーレットで全員に見せてから「ステージ → Game」の順に読み込む。
    /// ・Game だけで Play／テスト用の bat: <see cref="defaultStageIndex"/>（または起動引数 -fortress-stage）のステージを Game が自分で読み込む。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/ステージ/ステージの一覧", fileName = "StageCatalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        /// <summary>Resources の中の場所（拡張子なし）。</summary>
        public const string ResourcePath = "StageCatalog";

        [Tooltip("共通のゲームのシーン（砲台・コア・通信・カメラ・UI）の名前。ステージはこのシーンに重ねて読み込む。")]
        public string commonSceneName = "Game";

        [Tooltip("ステージの一覧（上から 1, 2, 3…）。")]
        public StageDefinition[] stages = Array.Empty<StageDefinition>();

        [Tooltip("Game だけで Play したとき・テスト用の bat で起動したときに読み込むステージ（上の一覧の番号、0 が一番上）。起動引数 -fortress-stage <id・シーン名・番号> があればそちらを使う。")]
        [Min(0)]
        public int defaultStageIndex;

        [Tooltip("ON なら、ルーレットで前回と同じステージは選ばない（ルーレットに出るステージが 2 つ以上のとき）。")]
        public bool avoidRepeat = true;

        [Tooltip("ステージ抽選のルーレットの設定。")]
        public StageRouletteSettings roulette = new StageRouletteSettings();

        private static StageCatalog _cached;

        /// <summary>Resources/StageCatalog を読む（無ければ null。ステージの仕組みを使わない＝今までどおり）。</summary>
        public static StageCatalog Load()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<StageCatalog>(ResourcePath);
            }

            return _cached;
        }

        public int Count => stages != null ? stages.Length : 0;

        public StageDefinition Get(int index)
        {
            return stages != null && index >= 0 && index < stages.Length ? stages[index] : null;
        }

        /// <summary>そのシーン名のステージの番号（無ければ -1）。</summary>
        public int IndexOfScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || stages == null)
            {
                return -1;
            }

            for (var i = 0; i < stages.Length; i++)
            {
                if (stages[i] != null && stages[i].sceneName == sceneName)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool IsStageScene(string sceneName) => IndexOfScene(sceneName) >= 0;

        /// <summary>ステージの名前・シーン名・番号（1 から）のどれかで探す（起動引数用）。見つからなければ -1。</summary>
        public int Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || stages == null)
            {
                return -1;
            }

            key = key.Trim();
            for (var i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                if (stage == null)
                {
                    continue;
                }

                if (string.Equals(stage.stageId, key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stage.sceneName, key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return int.TryParse(key, out var number) && number >= 1 && number <= stages.Length ? number - 1 : -1;
        }

        /// <summary>そのステージを今読み込めるか（一覧にあり、シーンがビルドに入っている）。</summary>
        public bool IsPlayable(int index)
        {
            var stage = Get(index);
            return stage != null && !string.IsNullOrEmpty(stage.sceneName) && Application.CanStreamedLevelBeLoaded(stage.sceneName);
        }

        /// <summary>ルーレットに出すステージか（読み込めて、「ルーレットに出す」が ON）。</summary>
        public bool IsInRoulette(int index) => IsPlayable(index) && Get(index).includeInRoulette;

        /// <summary>ルーレットに出るステージの数。</summary>
        public int RouletteCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Count; i++)
                {
                    if (IsInRoulette(i))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>ルーレットで選ぶ（ランダム）。選べるステージが無ければ -1。previous は前回のステージ（無ければ -1）。</summary>
        public int PickRandom(int previous)
        {
            var eligible = new bool[Count];
            for (var i = 0; i < eligible.Length; i++)
            {
                eligible[i] = IsInRoulette(i);
            }

            return PickIndex(eligible, previous, avoidRepeat, UnityEngine.Random.value);
        }

        /// <summary>
        /// 選べる物（eligible）の中から 1 つ選ぶ（random01 は 0〜1 の乱数）。avoidRepeat なら、ほかに選べる物があるとき previous を外す。
        /// Unity に頼らない計算なのでテストできる。選べる物が無ければ -1。
        /// </summary>
        public static int PickIndex(bool[] eligible, int previous, bool avoidRepeat, float random01)
        {
            if (eligible == null)
            {
                return -1;
            }

            var count = 0;
            for (var i = 0; i < eligible.Length; i++)
            {
                if (eligible[i])
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return -1;
            }

            var skip = avoidRepeat && count > 1 && previous >= 0 && previous < eligible.Length && eligible[previous];
            var choices = skip ? count - 1 : count;
            var pick = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(random01) * choices), 0, choices - 1);
            for (var i = 0; i < eligible.Length; i++)
            {
                if (!eligible[i] || (skip && i == previous))
                {
                    continue;
                }

                if (pick == 0)
                {
                    return i;
                }

                pick--;
            }

            return -1;
        }

        /// <summary>今読み込まれているステージのシーン（無ければ default）。</summary>
        public Scene FindLoadedStageScene()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && IsStageScene(scene.name))
                {
                    return scene;
                }
            }

            return default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _cached = null;
        }
    }

    /// <summary>ステージ抽選のルーレット（キャラ選択の後に全員の画面に出る）の設定。</summary>
    [Serializable]
    public sealed class StageRouletteSettings
    {
        [Tooltip("OFF にすると、ルーレットを見せずに選んだステージへすぐ進む（選び方はランダムのまま）。")]
        public bool enabled = true;

        [Tooltip("カードが流れ始めてから止まるまでの秒数。")]
        [Min(0.5f)]
        public float spinSeconds = 3.6f;

        [Tooltip("止まってから、ゲームへ進むまでの秒数（決まったステージを見せる時間）。")]
        [Min(0f)]
        public float holdSeconds = 1.8f;

        [Tooltip("止まるまでに少なくとも何周するか（カードが少なくても、たくさん流れて見えるように）。")]
        [Min(1)]
        public int minLaps = 4;

        [Tooltip("止まる直前に「行き過ぎて戻る」大きさ（カード何枚ぶん。0 でぴたっと止まる）。")]
        [Range(0f, 0.6f)]
        public float overshootCards = 0.18f;

        [Tooltip("上の大きな見出し。")]
        public string title = "STAGE ROULETTE";

        [Tooltip("見出しの下の一言。")]
        public string caption = "ステージ抽選中…";

        [Tooltip("止まったときに出す言葉。")]
        public string decidedText = "決定！";

        [Tooltip("全体の色（光・枠）。")]
        public Color accent = new Color(0.35f, 0.95f, 1f);

        [Tooltip("止まったときの光の色。")]
        public Color flashColor = new Color(1f, 0.95f, 0.55f);

        [Tooltip("カードの枠の絵（空なら角丸の枠をコードで描く）。")]
        public Sprite cardFrame;

        [Tooltip("後ろの模様（斜めのしま。空なら無し）。流れて見える。")]
        public Texture stripes;

        [Tooltip("描く順番（画面の UI の中で）。キャラ選択の歯磨き粉の幕（950）より小さくすること。")]
        public int sortingOrder = 900;

        [Tooltip("ルーレットが回り始めたとき。")]
        public FortressEffect onStart = new FortressEffect();

        [Tooltip("カードが真ん中を通るたび（カチッ）。")]
        public FortressEffect onTick = new FortressEffect();

        [Tooltip("止まったとき（ドン）。")]
        public FortressEffect onStop = new FortressEffect();

        /// <summary>回り始めてからゲームへ進むまでの秒数。</summary>
        public float TotalSeconds => spinSeconds + holdSeconds;
    }
}
