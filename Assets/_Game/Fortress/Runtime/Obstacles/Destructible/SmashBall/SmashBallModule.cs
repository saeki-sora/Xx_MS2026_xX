using System.Collections;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 破壊可能物を「スマッシュボール」化する追加コンポーネント。壊れたら誰が壊したかを記録・通知するだけで、
    /// 何が起こるか（能力発動など）はこのクラスの外（<see cref="AnyBroken"/>を購読する側）が決める。
    /// DestructibleObstacle本体には手を加えず、そのイベントを購読して振る舞いを足すだけ（既存設計と同じやり方）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DestructibleObstacle))]
    [RequireComponent(typeof(SmashBallFloater))]
    public sealed class SmashBallModule : MonoBehaviour
    {
        public SmashBallSettings settings = new SmashBallSettings();

        /// <summary>このスマッシュボールが割れた（このインスタンス限定）。</summary>
        public event System.Action<SmashBallBreakInfo> Broken;

        /// <summary>シーン内のどのスマッシュボールが割れても通知される。将来の能力発動フックなどに使う想定。</summary>
        public static event System.Action<SmashBallBreakInfo> AnyBroken;

        public DestructibleObstacle Obstacle { get; private set; }

        /// <summary>ローテーション待機中（他のスマッシュボールが出現中なので自分は隠れている）か。</summary>
        public bool IsWaitingInRotation => settings.exclusiveRotation && !gameObject.activeSelf;

        private void OnEnable()
        {
            Obstacle = GetComponent<DestructibleObstacle>();
            if (Obstacle == null)
            {
                return;
            }

            Obstacle.Destroyed += OnDestroyed;
            Obstacle.Regenerated += OnRegenerated;

            if (settings.exclusiveRotation)
            {
                SmashBallRotationGroup.Register(this);
            }
        }

        private void Start()
        {
            if (settings.exclusiveRotation)
            {
                SmashBallRotationGroup.ResolveInitial(this);
            }
        }

        private void OnDisable()
        {
            if (Obstacle != null)
            {
                Obstacle.Destroyed -= OnDestroyed;
                Obstacle.Regenerated -= OnRegenerated;
            }

            SmashBallRotationGroup.Unregister(this);
        }

        private void OnDestroyed(DestructibleObstacle obstacle)
        {
            var info = new SmashBallBreakInfo(this, obstacle, obstacle.DestroyedBy, transform.position, Time.time);

            DestructibleEffectPlayer.Play(settings.onBroken, transform.position, settings.effectLifetimeSeconds);

            if (settings.recordHistory)
            {
                SmashBallHistory.Add(info);
            }

            Broken?.Invoke(info);
            AnyBroken?.Invoke(info);

            if (settings.exclusiveRotation && isActiveAndEnabled)
            {
                StartCoroutine(ActivateNextAfterDelay());
            }
        }

        private void OnRegenerated(DestructibleObstacle obstacle)
        {
            if (settings.regenGraceInvulnerableSeconds > 0f)
            {
                obstacle.SetInvulnerable(true);
                StartCoroutine(EndInvulnerabilityGrace(obstacle));
            }

            if (settings.exclusiveRotation)
            {
                SmashBallRotationGroup.Reconsider(this);
            }
        }

        private IEnumerator ActivateNextAfterDelay()
        {
            if (settings.rotationDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(settings.rotationDelaySeconds);
            }

            SmashBallRotationGroup.ActivateNext(this);
        }

        private IEnumerator EndInvulnerabilityGrace(DestructibleObstacle obstacle)
        {
            yield return new WaitForSeconds(settings.regenGraceInvulnerableSeconds);
            if (obstacle != null)
            {
                obstacle.Unlock();
            }
        }
    }
}
