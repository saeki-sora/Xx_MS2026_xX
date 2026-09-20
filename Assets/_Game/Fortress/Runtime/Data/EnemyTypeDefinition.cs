using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵1種類分のパラメータ。種類を増やしたい場合はこのアセットを複製するだけでよい。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Enemy Type Definition", fileName = "New EnemyType")]
    public sealed class EnemyTypeDefinition : ScriptableObject
    {
        [Tooltip("ツール上に表示する名前。")]
        public string displayName = "Enemy";

        [Min(1f)]
        public float maxHealth = 10f;

        [Min(0f)]
        public float moveSpeed = 2f;

        [Tooltip("コアクリスタルに到達した際に与えるダメージ。")]
        [Min(0f)]
        public float damageToCore = 1f;

        [Header("移動・経路")]
        [Tooltip("経路の性格（体の大きさ・障害物との距離の取り方）。未設定ならNavigationFieldの既定を使う。")]
        public NavigationProfile navigationProfile;

        [Tooltip("進行方向の切り替わりの鋭さ。大きいほど角ばった動き、小さいほど滑らかに曲がる。")]
        [Min(0.1f)]
        public float turnSharpness = 8f;

        [Header("見た目（後から本番アセットに差し替え可能）")]
        [Tooltip("スポーン時に生成する見た目のプレファブ。未設定の場合はプレースホルダーの円形を使う。")]
        public GameObject visualPrefab;

        [Tooltip("visualPrefab未設定時に使うプレースホルダーの色。")]
        public Color placeholderColor = Color.magenta;
    }
}
