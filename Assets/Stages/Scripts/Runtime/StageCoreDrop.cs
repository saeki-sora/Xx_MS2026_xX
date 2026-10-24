using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// ステージの仕掛け「橋が壊れたらコアが落ちる」（ステージ2「お風呂場」）。コアが乗っている橋（壊れる障害物）が壊れたら、
    /// Game のコアを「落ちる先」の目印まで、速くなりながら落として少し弾ませる。着地したら敵の通り道を計算し直す。
    /// 橋が壊れたことは今の壊れる障害物の通信で全員に届き、各PCで <see cref="DestructibleObstacle.Destroyed"/> が鳴るので、
    /// この部品は通信をしない（各PCが自分でコアを動かす。敵の行き先は毎フレームのコアの位置を使うので、そのまま付いてくる）。
    /// 途中から入ったPC（参加してすぐ橋が壊れた状態が届く）では、アニメなしで落ちた位置に置く。
    /// ステージの中身（`[Stage] Content`）の下に置く（Game がそろって中身が動き出してから働くように）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/ステージ/橋が壊れたらコアが落ちる")]
    public sealed class StageCoreDrop : MonoBehaviour
    {
        [Tooltip("コアが乗っている橋（壊れる障害物）。これが壊れたらコアが落ちる。")]
        public DestructibleObstacle bridge;

        [Tooltip("コアが落ちる先の目印（位置だけ使う）。")]
        public Transform dropTarget;

        [Tooltip("ウェーブの計画があるとき、この番号のウェーブの発表が始まった時点でまだ橋が残っていたら、自動で崩す（0 なら崩さない）。崩すのは Host（オフラインでは自分）。")]
        [Min(0)]
        public int collapseAtWave = 3;

        [Header("落ち方")]
        [Tooltip("落ち始めてから着地するまでの秒数。")]
        [Min(0.05f)]
        public float fallSeconds = 0.6f;

        [Tooltip("着地してから弾み終わるまでの秒数。0 なら弾まない。")]
        [Min(0f)]
        public float bounceSeconds = 0.25f;

        [Tooltip("着地で弾む高さ（m。画面の上方向）。")]
        [Min(0f)]
        public float bounceHeight = 0.6f;

        [Tooltip("ステージが始まってからこの秒数以内に橋が壊れたと届いたら、途中参加とみなしてアニメなしで落ちた位置に置く。")]
        [Min(0f)]
        public float joinSnapSeconds = 2f;

        [Header("演出（D-Drive の番号札。空なら鳴らさない。各PCで鳴らす）")]
        [Tooltip("落ち始めた瞬間。位置はコア。")]
        public FortressEffect onFallStart = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("着地した瞬間。位置は落ちた先。")]
        public FortressEffect onLand = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("着地した瞬間の画面の揺れ。揺らさないなら「揺れの大きさ」を 0 に。")]
        public SimpleShakeSettings landShake = new SimpleShakeSettings { amplitude = 0.35f, rollAmplitude = 0.6f, frequency = 18f, duration = 0.35f };

        private CoreCrystalController _core;
        private bool _dropped;
        private bool _animating;
        private bool _landEffectsPlayed;
        private float _elapsed;
        private Vector3 _from;
        private float _startTime;

        /// <summary>橋が壊れてコアが落ちた（落ちている途中も含む）か。</summary>
        public bool IsDropped => _dropped;

        private void OnEnable()
        {
            _startTime = Time.time;
            if (bridge != null)
            {
                bridge.Destroyed += OnBridgeDestroyed;
            }

            FortressWaves.WaveAnnounced += OnWaveAnnounced;
        }

        private void OnDisable()
        {
            if (bridge != null)
            {
                bridge.Destroyed -= OnBridgeDestroyed;
            }

            FortressWaves.WaveAnnounced -= OnWaveAnnounced;
        }

        // 決めたウェーブ（既定 WAVE 3）の発表が始まったとき、橋が残っていたら崩す（壊れたことは今の通信で全員に届く）。
        private void OnWaveAnnounced(int waveIndex)
        {
            if (collapseAtWave <= 0 || waveIndex + 1 < collapseAtWave || bridge == null || bridge.IsDestroyed ||
                !MS2026.Fortress.Net.FortressNet.HasSimulationAuthority)
            {
                return;
            }

            bridge.DestroyNow();
        }

        private void Start()
        {
            // 中身が動き出す前に橋がもう壊れていた（途中参加など）。
            if (bridge != null && bridge.IsDestroyed && !_dropped)
            {
                Drop(false);
            }
        }

        private void OnBridgeDestroyed(DestructibleObstacle obstacle)
        {
            if (!_dropped)
            {
                Drop(Time.time - _startTime > joinSnapSeconds);
            }
        }

        private void Drop(bool animate)
        {
            _core = FindFirstObjectByType<CoreCrystalController>();
            if (_core == null || dropTarget == null)
            {
                return;
            }

            _dropped = true;
            _from = _core.transform.position;
            if (!animate)
            {
                Land(false);
                return;
            }

            _animating = true;
            _landEffectsPlayed = false;
            _elapsed = 0f;
            FortressEffectPlayer.PlayOnce(onFallStart, _from, Quaternion.identity);
        }

        private void Update()
        {
            if (!_animating || _core == null)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var target = Target();
            if (_elapsed < fallSeconds)
            {
                // 重力のように、だんだん速く落ちる。
                var t = _elapsed / fallSeconds;
                _core.transform.position = Vector3.LerpUnclamped(_from, target, t * t);
                return;
            }

            // 着地の瞬間（弾み始め）に演出を 1 回だけ鳴らす。
            PlayLandEffects(target);
            var bounce = _elapsed - fallSeconds;
            if (bounce < bounceSeconds && bounceHeight > 0f)
            {
                _core.transform.position = target + Vector3.up * (bounceHeight * Mathf.Sin(Mathf.PI * bounce / bounceSeconds));
                return;
            }

            _animating = false;
            Land(false);
        }

        private void LateUpdate()
        {
            // 落ちた後は、ほかの物（ステージの目印の当てはめ直しなど）が元の位置へ戻しても、落ちた先に置き直す。
            if (_dropped && !_animating && _core != null)
            {
                var target = Target();
                if (((Vector2)_core.transform.position - (Vector2)target).sqrMagnitude > 1e-6f)
                {
                    _core.transform.position = target;
                    MarkNavigationDirty();
                }
            }
        }

        private void Land(bool playEffects)
        {
            var target = Target();
            _core.transform.position = target;
            if (playEffects)
            {
                PlayLandEffects(target);
            }

            MarkNavigationDirty();
        }

        private void PlayLandEffects(Vector3 position)
        {
            if (_landEffectsPlayed)
            {
                return;
            }

            _landEffectsPlayed = true;
            FortressEffectPlayer.PlayOnce(onLand, position, Quaternion.identity);
            var rig = FortressCameraRig.Active;
            if (rig != null && landShake != null && landShake.amplitude > 0f)
            {
                rig.AddModifier(new SimpleShakeModifier(landShake, 1f));
            }
        }

        /// <summary>落ちる先（Z はコアの今の高さのまま）。</summary>
        private Vector3 Target()
        {
            var p = dropTarget.position;
            return new Vector3(p.x, p.y, _core.transform.position.z);
        }

        private static void MarkNavigationDirty()
        {
            // コアの位置が変わったので、敵の通り道を作り直す。
            if (EnemyNavigation.Current is NavigationField field)
            {
                field.MarkDirty();
            }
        }

        private void OnDrawGizmos()
        {
            if (dropTarget == null)
            {
                return;
            }

            Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.9f);
            Gizmos.DrawWireSphere(dropTarget.position, 1.2f);
            if (bridge != null)
            {
                Gizmos.DrawLine(bridge.transform.position, dropTarget.position);
            }
        }
    }
}
