using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>フロー・フィールド計算に渡す、敵の「経路の性格」。</summary>
    public struct NavigationProfileSettings
    {
        public int hardInflateCells;
        public int softClearanceCells;
        public float softClearancePenalty;

        public static NavigationProfileSettings Default => new NavigationProfileSettings
        {
            hardInflateCells = 0,
            softClearanceCells = 1,
            softClearancePenalty = 3f
        };
    }

    /// <summary>
    /// 敵の種類ごとの経路の性格（体の大きさ・障害物との距離の取り方）。
    /// 大型の敵は「膨らませるセル数」を増やして細い隙間を通れなくする、といった調整をアセット1つで行える。
    /// EnemyTypeDefinitionから割り当てる。未設定ならNavigationFieldの既定プロファイルが使われる。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Navigation Profile", fileName = "New NavigationProfile")]
    public sealed class NavigationProfile : ScriptableObject
    {
        [Header("体の大きさ（通れない隙間）")]
        [Tooltip("障害物をこのセル数だけ膨らませて、通行不可にする。大型の敵ほど大きくすると、狭い隙間を通れなくなる。")]
        [Range(0, 6)]
        public int hardInflateCells;

        [Header("障害物との距離の取り方（見た目の滑らかさ）")]
        [Tooltip("障害物のこのセル数以内を「通りにくい」扱いにして、角に張り付かず余裕を持って迂回させる。")]
        [Range(0, 6)]
        public int softClearanceCells = 1;

        [Tooltip("障害物に接している位置の追加コスト。大きいほど障害物から離れた経路を選ぶ。")]
        [Min(0f)]
        public float softClearancePenalty = 3f;

        public NavigationProfileSettings ToSettings()
        {
            return new NavigationProfileSettings
            {
                hardInflateCells = hardInflateCells,
                softClearanceCells = softClearanceCells,
                softClearancePenalty = softClearancePenalty
            };
        }

        /// <summary>アセット未指定時のフォールバック用。保存されない実行時インスタンス。</summary>
        public static NavigationProfile CreateRuntimeDefault()
        {
            var profile = CreateInstance<NavigationProfile>();
            profile.name = "Runtime Default Profile";
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }
    }
}
