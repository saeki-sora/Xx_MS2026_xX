using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ステージの仕掛け（<see cref="GimmickGear"/> 歯車・<see cref="GimmickMover"/> 動く障害物）の共通部分（2026-10-29）。
    /// ・形は <see cref="GimmickClock.Time"/> だけで決まる（<see cref="ApplyAt"/>）。全員のPCが自分で動かす（通信は時計だけ）
    /// ・中に経路の障害物（<see cref="NavigationObstacle"/>）があれば、動いている間は経路の再計算を頼む（敵の動き方そのものは変えない）
    /// ・動き出し・止まった瞬間に演出（<see cref="FortressEffect"/>）を鳴らす
    /// Play していないときは動かない（要塞デザイナーの「仕掛け」タブのプレビューが <see cref="ApplyAt"/> を呼ぶ）。
    /// </summary>
    public abstract class GimmickBehaviour : MonoBehaviour
    {
        [TextArea(1, 3)]
        [Tooltip("メモ（ゲームでは使わない）。レベルデザインの意図など。")]
        public string memo = "";

        private bool _wasMoving;
        private int _seenJump = -1;
        private readonly List<NavigationObstacle> _obstacleBuffer = new List<NavigationObstacle>();

        /// <summary>時刻 t の形にする。戻り値は「動いている途中か」。</summary>
        public abstract bool ApplyAt(float t);

        /// <summary>時刻 t に動いているか（形は変えない）。</summary>
        public abstract bool IsMovingAt(float t);

        /// <summary>0〜horizon 秒の間で動いている時間帯（エディタの時間割に使う）。</summary>
        public abstract void CollectIntervals(float horizon, List<(float start, float end)> result);

        /// <summary>動き方を一言で（エディタの一覧に使う）。</summary>
        public abstract string Describe();

        /// <summary>動き出したとき・止まったときの演出。</summary>
        protected abstract FortressEffect StartEffect { get; }

        protected abstract FortressEffect StopEffect { get; }

        /// <summary>経路に関係する物（<see cref="NavigationObstacle"/>）を中に持っているか。</summary>
        public bool AffectsNavigation
        {
            get
            {
                GetComponentsInChildren(true, _obstacleBuffer);
                var any = _obstacleBuffer.Count > 0;
                _obstacleBuffer.Clear();
                return any;
            }
        }

        protected virtual void OnEnable()
        {
            GimmickClock.Register(this);
            _seenJump = -1;
            if (Application.isPlaying)
            {
                // 途中から有効になった（途中参加・中身が後から有効になった）ときも、今の時刻の形から始める。
                _wasMoving = ApplyAt(GimmickClock.Time);
            }
        }

        protected virtual void OnDisable()
        {
            GimmickClock.Unregister(this);
        }

        protected virtual void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var moving = ApplyAt(GimmickClock.Time);
            if (_seenJump != GimmickClock.JumpCount)
            {
                // 時計が飛んだ（試合が始まった・Host に合わせた）ので、その瞬間の演出は鳴らさない。
                _seenJump = GimmickClock.JumpCount;
                _wasMoving = moving;
                return;
            }

            if (moving != _wasMoving)
            {
                var effect = moving ? StartEffect : StopEffect;
                if (effect != null && !effect.IsEmpty)
                {
                    FortressEffectPlayer.PlayOnce(effect, transform.position, Quaternion.identity);
                }
            }

            _wasMoving = moving;
        }

        /// <summary>形が変わったので、経路の再計算を頼む（中に障害物があるときだけ。経路の側で 0.1 秒に 1 回までにまとめられる）。</summary>
        protected void NotifyNavigation()
        {
            if (AffectsNavigation)
            {
                NavigationObstacle.NotifyChanged();
            }
        }
    }
}
