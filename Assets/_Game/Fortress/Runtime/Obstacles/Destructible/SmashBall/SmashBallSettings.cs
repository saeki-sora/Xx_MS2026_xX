using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>スマッシュボール1個分の設定。破壊可能物（DestructibleObstacle）に追加するコンポーネントが持つ。</summary>
    [Serializable]
    public sealed class SmashBallSettings
    {
        [Tooltip("履歴・デバッグ表示での名札。空ならオブジェクト名を使う。同じマップに複数置くときの目印。")]
        public string label = "";

        [Header("浮遊")]
        public SmashBallFloatSettings floating = new SmashBallFloatSettings();

        [Header("割れたときの演出（通常の破壊可能物の演出に追加で鳴る）")]
        [Tooltip("割った瞬間に生成するPrefab（ファンファーレ用の派手なエフェクトなど）。")]
        public DestructibleEffect onBroken = new DestructibleEffect();

        [Tooltip("演出Prefabを自動で消すまでの時間(秒)。")]
        [Min(0.1f)]
        public float effectLifetimeSeconds = 3f;

        [Header("履歴")]
        [Tooltip("誰がいつ割ったかを履歴（SmashBallHistory）に記録する。")]
        public bool recordHistory = true;

        [Header("保護")]
        [Tooltip("再生する設定の場合、再生した瞬間からこの秒数だけ追加で無敵にする（出現直後の連続破壊を防ぐ）。")]
        [Min(0f)]
        public float regenGraceInvulnerableSeconds = 1f;

        [Header("同時出現の抑制（ローテーション）")]
        [Tooltip("ONにすると、同じグループ名を持つスマッシュボールのうち1つだけが同時に出現し、" +
                 "それが割れたら少し間をおいて次の1つが現れる（スマブラのアイテムのような運用）。")]
        public bool exclusiveRotation;

        [Tooltip("ローテーションのグループ名。空なら「既定グループ」として、exclusiveRotationがONの物同士でまとまる。")]
        public string rotationGroupId = "";

        [Tooltip("割れてから、グループ内の次の1つが現れるまでの間隔(秒)。")]
        [Min(0f)]
        public float rotationDelaySeconds = 3f;
    }
}
