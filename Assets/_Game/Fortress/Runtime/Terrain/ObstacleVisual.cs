using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// DestructibleObstacleの状態をSpriteRendererで可視化するデフォルト実装。本体はHPが減っても
    /// 縮まず、代わりにダメージが蓄積するほどひび割れの重ね表示が濃くなっていく。HPが尽きると
    /// ひびが限界に達したとして<see cref="ObstacleBreakEffect"/>で崩れ落ち、破線のゴースト表示に
    /// 切り替わって再生されるタイミングを見た目で予告する。
    /// 子のSpriteRendererはいずれも1x1単位のスプライトをスケール1のまま使う前提で、大きさは
    /// DestructibleObstacle.SetSize()が動かすtransform.localScale(親)まかせにしている。
    /// これにより、コリジョン(BoxCollider2D)と見た目が親子階層の仕組みだけで必ず同時に追従する。
    /// 本番の見た目（アニメーション/シェーダーFX等）に差し替える場合は、このコンポーネントを
    /// 外して同じ役割の別コンポーネントに置き換えればよい。
    /// </summary>
    [RequireComponent(typeof(DestructibleObstacle))]
    public sealed class ObstacleVisual : MonoBehaviour
    {
        [Tooltip("障害物本体の見た目。未設定の場合は自動で子オブジェクトとして生成する。")]
        public SpriteRenderer bodyRenderer;

        [Tooltip("ダメージに応じて濃くなるひび割れの重ね表示。未設定の場合は自動で子オブジェクトとして生成する。")]
        public SpriteRenderer crackRenderer;

        [Tooltip("破壊中に表示する再生待ちのゴースト表示。未設定の場合は自動で子オブジェクトとして生成する。")]
        public SpriteRenderer ghostRenderer;

        private DestructibleObstacle _obstacle;

        private void Awake()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
            EnsureRenderers();

            _obstacle.OnHealthChanged += HandleHealthChanged;
            _obstacle.OnStateChanged += HandleStateChanged;

            Refresh();
        }

        private void OnDestroy()
        {
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.OnHealthChanged -= HandleHealthChanged;
            _obstacle.OnStateChanged -= HandleStateChanged;
        }

        private void EnsureRenderers()
        {
            if (bodyRenderer == null)
            {
                var go = new GameObject("Body");
                go.transform.SetParent(transform, false);
                bodyRenderer = go.AddComponent<SpriteRenderer>();
                bodyRenderer.sprite = PlaceholderSpriteFactory.CreateSquareSprite();
            }

            if (crackRenderer == null)
            {
                var go = new GameObject("Cracks");
                go.transform.SetParent(transform, false);
                crackRenderer = go.AddComponent<SpriteRenderer>();
                crackRenderer.sprite = PlaceholderSpriteFactory.CreateCrackOverlaySprite();
                crackRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
                crackRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            if (ghostRenderer == null)
            {
                var go = new GameObject("Ghost");
                go.transform.SetParent(transform, false);
                ghostRenderer = go.AddComponent<SpriteRenderer>();
                ghostRenderer.sprite = PlaceholderSpriteFactory.CreateDashedSquareSprite();
                ghostRenderer.color = new Color(1f, 1f, 1f, 0.55f);
                ghostRenderer.sortingOrder = bodyRenderer.sortingOrder - 1;
            }

            // 破壊エフェクト用の破片スプライト/プールも配置時に先読みしておく。破壊された瞬間の初回生成コストを
            // 演出のタイミングからずらし、初めて壊れる瞬間にプチフリーズするのを防ぐ。
            PlaceholderSpriteFactory.CreateTriangleHalfSprite();
            ObstacleBreakEffect.Prewarm();
        }

        private void HandleHealthChanged(float current, float max) => Refresh();

        private void HandleStateChanged(ObstacleState state)
        {
            if (state == ObstacleState.Destroyed)
            {
                SpawnBreakEffect();
            }

            Refresh();
        }

        private void SpawnBreakEffect()
        {
            var tuning = _obstacle.tuning;
            var color = tuning != null ? tuning.damagedColor : Color.white;
            var lossyScale = transform.lossyScale;
            var size = new Vector2(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));

            var fragmentCount = tuning != null ? tuning.breakFragmentCount : 5;
            var duration = tuning != null ? tuning.breakEffectDuration : 0.6f;
            var launchSpeed = tuning != null ? tuning.breakEffectLaunchSpeed : 3f;
            var spinSpeed = tuning != null ? tuning.breakEffectSpinSpeedDegrees : 320f;
            var gravity = tuning != null ? tuning.breakEffectGravity : 6f;

            ObstacleBreakEffect.Spawn(transform.position, transform.rotation, size, color, fragmentCount, duration, launchSpeed, spinSpeed, gravity);
        }

        private void Refresh()
        {
            var tuning = _obstacle.tuning;
            var isDestroyed = _obstacle.State == ObstacleState.Destroyed;

            bodyRenderer.enabled = !isDestroyed;
            crackRenderer.enabled = !isDestroyed;
            ghostRenderer.enabled = isDestroyed;

            if (isDestroyed)
            {
                return;
            }

            bodyRenderer.color = tuning != null
                ? Color.Lerp(tuning.damagedColor, tuning.healthyColor, _obstacle.HealthRatio01)
                : Color.white;

            var crackAlpha = 0f;
            if (tuning != null && tuning.enableCrackVisual)
            {
                crackAlpha = Mathf.Clamp01(tuning.crackAlphaCurve.Evaluate(_obstacle.HealthRatio01));
            }

            var crackColor = crackRenderer.color;
            crackColor.a = crackAlpha;
            crackRenderer.color = crackColor;
        }
    }
}
