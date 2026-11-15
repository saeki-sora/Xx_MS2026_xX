using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>結果の表（CSV）をいつ書くか。</summary>
    public enum CpuCsvMode
    {
        [InspectorName("書かない")]
        Off = 0,

        [InspectorName("自動プレイのときだけ")]
        AutoplayOnly = 1,

        [InspectorName("CPU がいる試合はいつも")]
        Always = 2
    }

    /// <summary>
    /// CPU の設定（Resources/CpuSettings。無ければ初期値で動く）。メニュー「Tools/CPU/CPU の設定を開く」で作る・開く。
    /// 起動オプション（<see cref="CpuLaunchOptions"/>）があれば、そちらが優先。
    /// </summary>
    [CreateAssetMenu(fileName = "CpuSettings", menuName = "MS2026/CPU/CPU の設定")]
    public sealed class CpuSettings : ScriptableObject
    {
        public const string ResourcePath = "CpuSettings";

        [Header("CPU を入れる席")]
        [Tooltip("OFF にすると CPU を一切使わない（起動オプションの -cpu も効かない）。")]
        public bool cpuEnabled = true;

        [Tooltip("通信していないとき（ステージのシーンだけで実行したときなど）、自分以外の席を CPU にする。\n" +
                 "OFF にすると今まで通り、ほかの席もキーボードで動かせる。")]
        public bool fillOtherSeatsOffline = true;

        [Header("強さ")]
        [Tooltip("CPU の強さ（起動オプション -cpu-level で変えられる）。ゲームの画面では選ばない。")]
        public CpuLevel level = CpuLevel.Normal;

        [Tooltip("0 なら毎回ばらばらの動き。0 以外なら毎回同じ動き（自動プレイで同じ試合を再現する用。起動オプション -cpu-seed）。")]
        public int randomSeed;

        [Header("頭と目")]
        [Tooltip("CPU が「次に何をするか」を考え直す間隔（秒）。短いほど素早く切り替わるが重くなる。")]
        [Range(0.05f, 0.5f)]
        public float thinkInterval = 0.15f;

        [Tooltip("CPU の目が敵の様子を見直す間隔（秒、実時間）。CPU 全員で1回だけ群衆の位置を写す。短いほど正確だが重くなる。")]
        [Range(0.05f, 0.5f)]
        public float senseInterval = 0.1f;

        [Header("上手さ（4つの目安。強さはこの間の区間を受け持つ）")]
        [Tooltip("やさしい = 初めて遊ぶ人 → 少し慣れた人、ふつう = 少し慣れた人 → 上手い人、つよい = 上手い人 → とても上手い人。\n" +
                 "区間の中のどこになるかは、分野ごとの慣れ（試合の始めは下の値。ウェーブごとに学んで上がる）で決まる。")]
        public CpuSkillProfile profile = new CpuSkillProfile();

        [Tooltip("試合の始めの慣れ（0〜1。0 = チュートリアルを終えたばかり）。試しに上限の強さを見たいときに 1。")]
        [Range(0f, 1f)]
        public float startFamiliarity;

        [Header("自動プレイ・結果の表（開発用）")]
        [Tooltip("結果の表（CSV）をいつ書くか。置き場所はエディタならプロジェクトの Logs/CPU/、ビルドなら persistentDataPath/CPU/。")]
        public CpuCsvMode csvMode = CpuCsvMode.AutoplayOnly;

        [Tooltip("メニュー「Tools/CPU/自動プレイで Play する」で回す試合の数（.exe では起動オプション -autoplay N）。")]
        [Min(1)]
        public int autoplayMatches = 5;

        [Tooltip("自動プレイの早送りの倍率（.exe では -timescale）。群衆は1フレームに進める時間に上限があるので、" +
                 "上げすぎると敵だけ遅くなる（60 FPS なら 2 倍くらいまでが目安）。")]
        [Range(1f, 4f)]
        public float autoplayTimeScale = 2f;

        [Tooltip("試合が終わってから次の試合を始めるまでの秒数（実時間。リザルトを少し見る）。")]
        [Range(0f, 30f)]
        public float autoplayWaitSeconds = 3f;

        [Tooltip("自動プレイで、1試合がこの分数（ゲームの時間）を過ぎても終わらなければ打ち切って次へ。")]
        [Range(1f, 60f)]
        public float autoplayMaxMinutes = 20f;

        [Header("開発用")]
        [Tooltip("CPU の席が変わったとき、Console（ビルドでは Player.log）に出す。")]
        public bool logSeatChanges = true;

        private static CpuSettings _fallback;

        /// <summary>Resources の設定を読む（無ければ初期値）。</summary>
        public static CpuSettings Load()
        {
            var settings = Resources.Load<CpuSettings>(ResourcePath);
            if (settings != null)
            {
                return settings;
            }

            if (_fallback == null)
            {
                _fallback = CreateInstance<CpuSettings>();
                _fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _fallback;
        }
    }
}
