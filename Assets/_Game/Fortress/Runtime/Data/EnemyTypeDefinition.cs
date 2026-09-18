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

        [Header("見た目（後から本番アセットに差し替え可能）")]
        [Tooltip("スポーン時に生成する見た目のプレファブ。未設定の場合はプレースホルダーの円形を使う。")]
        public GameObject visualPrefab;

        [Tooltip("visualPrefab未設定時に使うプレースホルダーの色。")]
        public Color placeholderColor = Color.magenta;
    }
}
