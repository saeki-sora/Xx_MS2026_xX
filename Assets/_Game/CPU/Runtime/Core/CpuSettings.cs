using UnityEngine;

namespace MS2026.Cpu
{
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

        [Header("上手さ（仮。段6 で強さごとに分ける）")]
        [Tooltip("今はどの強さでもこの値を使う（ふつうの仮の値）。")]
        public CpuSkillValues skill = CpuSkillValues.Default;

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
