using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>敵の動かし方。</summary>
    public enum EnemySimulationMode
    {
        /// <summary>配列で一括シミュレーション・一括描画する軽量な敵（数千体向け）。</summary>
        Swarm,

        /// <summary>1体ごとにGameObjectを持つ敵（ボスなどの特別な敵向け）。</summary>
        Actor
    }

    /// <summary>群衆(Swarm)として動かす場合の設定。</summary>
    [Serializable]
    public sealed class SwarmEnemySettings
    {
        [Tooltip("体の大きさ（当たり半径）。押し合いの基準になる。")]
        [Min(0.05f)]
        public float radius = 0.22f;

        [Tooltip("重さ。重い敵は軽い敵を押しのけて進む。")]
        [Min(0.1f)]
        public float mass = 1f;

        [Tooltip("加速の速さ。大きいほど機敏、小さいほど動き出し・止まりが緩やか。")]
        [Min(0.1f)]
        public float acceleration = 8f;

        [Tooltip("個体ごとの速度のばらつき(±割合)。0.15なら±15%。群れが自然にばらける。")]
        [Range(0f, 0.5f)]
        public float speedVariance = 0.15f;

        [Header("見た目")]
        [Tooltip("画面上の大きさ（ワールド単位）。")]
        [Min(0.1f)]
        public float spriteSize = 0.7f;

        [Tooltip("スプライトシート。横=アニメのコマ(左→右)、縦=向き(上→下に 右, 右上, 上, 左上, 左, 左下, 下, 右下)。" +
                 "未設定の場合は自動生成の仮絵を使う。")]
        public Texture2D spriteSheet;

        [Tooltip("アニメーションのコマ数（シートの横の分割数）。")]
        [Min(1)]
        public int frameCount = 10;

        [Tooltip("向きの数（シートの縦の分割数）。1 / 2 / 4 / 8 のいずれか。")]
        [Min(1)]
        public int directionCount = 8;

        [Tooltip("アニメーションの再生速度(コマ/秒)。実際の移動速度に応じて自動で伸縮する。")]
        [Min(1f)]
        public float animationFps = 12f;
    }

    /// <summary>
    /// 敵1種類分のパラメータ。種類を増やしたい場合はこのアセットを複製するだけでよい。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Enemy Type Definition", fileName = "New EnemyType")]
    public sealed class EnemyTypeDefinition : ScriptableObject
    {
        [Tooltip("ツール上に表示する名前。")]
        public string displayName = "Enemy";

        [Tooltip("Swarm=数千体向けの軽量な敵 / Actor=1体ごとにGameObjectを持つ特別な敵（ボスなど）。")]
        public EnemySimulationMode simulationMode = EnemySimulationMode.Swarm;

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

        [Tooltip("（Actorのみ）進行方向の切り替わりの鋭さ。大きいほど角ばった動き、小さいほど滑らかに曲がる。")]
        [Min(0.1f)]
        public float turnSharpness = 8f;

        [Header("群衆（Swarmモード）の設定")]
        public SwarmEnemySettings swarm = new SwarmEnemySettings();

        [Header("見た目（Actorモード・共通）")]
        [Tooltip("（Actorのみ）スポーン時に生成する見た目のプレファブ。未設定の場合はプレースホルダーの円形を使う。")]
        public GameObject visualPrefab;

        [Tooltip("プレースホルダー（仮絵）の色。Swarmの自動生成スプライトにも使われる。")]
        public Color placeholderColor = Color.magenta;
    }
}
