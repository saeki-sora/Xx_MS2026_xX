using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>1つの出来事に対する演出。エフェクトのPrefabと効果音を差し替えられる。</summary>
    [Serializable]
    public sealed class DestructibleEffect
    {
        [Tooltip("その瞬間に生成するPrefab（パーティクル・破片・アニメ付きスプライトなど）。")]
        public GameObject prefab;

        [Tooltip("その瞬間に鳴らす効果音。")]
        public AudioClip sound;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("音程の基準。")]
        [Range(0.5f, 2f)]
        public float pitch = 1f;

        [Tooltip("鳴らすたびに音程をランダムにずらす幅。0でずらさない。")]
        [Range(0f, 0.5f)]
        public float pitchVariance = 0.05f;
    }

    /// <summary>被ダメージ・破壊・再生などの演出の設定。</summary>
    [Serializable]
    public sealed class DestructibleFeedbackSettings
    {
        [Tooltip("ダメージを受けている間の演出。")]
        public DestructibleEffect onHit = new DestructibleEffect();

        [Tooltip("onHitを繰り返し発生させる最短の間隔(秒)。レーザーは毎フレーム当たるので、間引かないと鳴りっぱなしになる。")]
        [Min(0.02f)]
        public float hitInterval = 0.15f;

        [Tooltip("見た目の段階が進んだ（ひびが入った等）瞬間の演出。")]
        public DestructibleEffect onStageChanged = new DestructibleEffect();

        [Tooltip("破壊された瞬間の演出。")]
        public DestructibleEffect onDestroyed = new DestructibleEffect();

        [Tooltip("再生した瞬間の演出。")]
        public DestructibleEffect onRegenerated = new DestructibleEffect();

        [Tooltip("生成したエフェクトPrefabを自動で消すまでの時間(秒)。")]
        [Min(0.1f)]
        public float effectLifetimeSeconds = 3f;

        [Header("仮の破片（素材が無い間の代わり）")]
        [Tooltip("破壊時の演出Prefabが空のとき、四角い仮の破片を飛び散らせる。")]
        public bool placeholderDebris = true;

        [Range(0, 40)]
        public int debrisCount = 10;

        public Color debrisColor = new Color(0.72f, 0.52f, 0.34f);
    }
}
