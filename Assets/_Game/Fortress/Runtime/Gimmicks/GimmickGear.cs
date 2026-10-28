using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>歯車の回り方。</summary>
    public enum GearMotion
    {
        /// <summary>止まっては回る（一定の間隔で、決まった角度だけ回る）。道を開けたり閉じたりする歯車。</summary>
        [InspectorName("止まっては回る")]
        Step = 0,

        /// <summary>ずっと同じ速さで回る。</summary>
        [InspectorName("ずっと回る")]
        Continuous = 1,

        /// <summary>ほかの歯車にかみ合って、反対向きに回る（歯の数の比で速さが変わる）。</summary>
        [InspectorName("ほかの歯車にかみ合う")]
        Follow = 2,

        /// <summary>回らない（置いておくだけ）。</summary>
        [InspectorName("回らない")]
        Still = 3
    }

    /// <summary>
    /// 歯車の仕掛け（2026-10-29、ステージ3「歯車ステージ」から）。子の <see cref="rotor"/>（回る部分）を Z 軸で回す。
    /// 回る部分の中に「腕」（<see cref="GimmickGearArm"/>: 経路の壁）を入れると、回るたびに敵の通り道が変わる。
    /// 敵は今までどおり経路（NavigationField）に従うだけで、敵の仕組みは変えていない。
    /// 歯車の位置・最初の向きは、この物（根元）を動かす・回すだけ。回る部分は時刻 0 で回転 0。
    /// 設定・プレビュー・点検は 要塞デザイナー › 仕掛け タブ。
    /// </summary>
    [AddComponentMenu("Fortress/仕掛け/歯車")]
    [DisallowMultipleComponent]
    public sealed class GimmickGear : GimmickBehaviour
    {
        public const string RotorName = "Rotor";
        private const int MaxFollowDepth = 8;

        [Tooltip("回る部分。この中の物（腕の壁・歯車の見た目）が一緒に回る。空なら子の「Rotor」を使う。")]
        public Transform rotor;

        [Header("回り方")]
        [Tooltip("止まっては回る / ずっと回る / ほかの歯車にかみ合う / 回らない")]
        public GearMotion motion = GearMotion.Step;

        [Tooltip("ON: 時計回り（画面で見て右回り）。OFF: 反時計回り。「かみ合う」のときは使わない（相手と反対向きになる）。")]
        public bool clockwise = true;

        [Tooltip("最初に回り始める時刻（秒。試合の始めが 0）。「ずっと回る」なら回り始める時刻。")]
        [Min(0f)]
        public float firstTurnAt = 6f;

        [Tooltip("1回に回る角度（度）。棒の形の腕なら 90 で「通れる ↔ ふさぐ」が入れ替わる。")]
        public float stepDegrees = 90f;

        [Tooltip("1回回るのにかける秒数。")]
        [Min(GimmickMath.MinSeconds)]
        public float turnSeconds = 1.5f;

        [Tooltip("回り終わってから、次に回り始めるまで止まっている秒数。")]
        [Min(0f)]
        public float waitSeconds = 8f;

        [Tooltip("「ずっと回る」ときの速さ（度/秒）。")]
        public float degreesPerSecond = 15f;

        [Tooltip("「かみ合う」ときの相手の歯車。相手と反対向きに、歯の数の比で回る。")]
        public GimmickGear driver;

        [Header("形（エディタの表示・かみ合わせの比に使う。当たり判定は腕の方）")]
        [Tooltip("歯の数。かみ合う歯車の回る速さの比に使う。")]
        [Min(3)]
        public int teeth = 16;

        [Tooltip("歯車の半径（m）。シーンの表示と、腕を足すときの長さの目安。")]
        [Min(0.1f)]
        public float radius = 2f;

        [Header("演出（全員のPCが自分で鳴らす）")]
        [Tooltip("回り始めたとき（ガコン、など）。")]
        public FortressEffect onTurnStart = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("回り終わって止まったとき。")]
        public FortressEffect onTurnEnd = new FortressEffect { tintWithPlayerColor = false };

        private float _appliedAngle = float.NaN;

        protected override FortressEffect StartEffect => onTurnStart;

        protected override FortressEffect StopEffect => onTurnEnd;

        /// <summary>回る部分（空なら子の Rotor）。</summary>
        public Transform Rotor
        {
            get
            {
                if (rotor == null)
                {
                    rotor = transform.Find(RotorName);
                }

                return rotor;
            }
        }

        /// <summary>今当てはめている角度（度）。</summary>
        public float CurrentAngle => float.IsNaN(_appliedAngle) ? 0f : _appliedAngle;

        /// <summary>時刻 t の回る部分の角度（度。反時計回りが +）。</summary>
        public float AngleAt(float t) => AngleAt(t, 0);

        private float AngleAt(float t, int depth)
        {
            switch (motion)
            {
                case GearMotion.Step:
                    return Sign * GimmickMath.StepAngle(t, firstTurnAt, waitSeconds, turnSeconds, stepDegrees, out _, out _);
                case GearMotion.Continuous:
                    return Sign * GimmickMath.ContinuousAngle(t, firstTurnAt, degreesPerSecond);
                case GearMotion.Follow:
                    var source = FollowSource(depth);
                    return source != null ? GimmickMath.FollowAngle(source.AngleAt(t, depth + 1), source.teeth, teeth) : 0f;
                default:
                    return 0f;
            }
        }

        private float Sign => clockwise ? -1f : 1f;

        private GimmickGear FollowSource(int depth)
        {
            return driver != null && driver != this && depth < MaxFollowDepth ? driver : null;
        }

        public override bool IsMovingAt(float t) => IsMovingAt(t, 0);

        private bool IsMovingAt(float t, int depth)
        {
            switch (motion)
            {
                case GearMotion.Step:
                    GimmickMath.StepAngle(t, firstTurnAt, waitSeconds, turnSeconds, stepDegrees, out var turning, out _);
                    return turning;
                case GearMotion.Continuous:
                    return t > firstTurnAt && Mathf.Abs(degreesPerSecond) > 0f;
                case GearMotion.Follow:
                    var source = FollowSource(depth);
                    return source != null && source.IsMovingAt(t, depth + 1);
                default:
                    return false;
            }
        }

        public override bool ApplyAt(float t)
        {
            var target = Rotor;
            var angle = AngleAt(t);
            if (target != null && (!Application.isPlaying || float.IsNaN(_appliedAngle) || Mathf.Abs(GimmickMath.DeltaAngle(_appliedAngle, angle)) > 0.001f))
            {
                target.localRotation = Quaternion.Euler(0f, 0f, angle);
                _appliedAngle = angle;
                NotifyNavigation();
            }

            return IsMovingAt(t);
        }

        public override void CollectIntervals(float horizon, List<(float start, float end)> result)
        {
            CollectIntervals(horizon, result, 0);
        }

        private void CollectIntervals(float horizon, List<(float start, float end)> result, int depth)
        {
            switch (motion)
            {
                case GearMotion.Step:
                    GimmickMath.StepIntervals(firstTurnAt, waitSeconds, turnSeconds, horizon, result);
                    break;
                case GearMotion.Continuous:
                    if (firstTurnAt < horizon && Mathf.Abs(degreesPerSecond) > 0f)
                    {
                        result.Add((firstTurnAt, horizon));
                    }

                    break;
                case GearMotion.Follow:
                    FollowSource(depth)?.CollectIntervals(horizon, result, depth + 1);
                    break;
            }
        }

        public override string Describe()
        {
            var dir = clockwise ? "右回り" : "左回り";
            switch (motion)
            {
                case GearMotion.Step:
                    return $"{firstTurnAt:0.#}秒から、{turnSeconds + waitSeconds:0.#}秒ごとに {stepDegrees:0.#}° {dir}（{turnSeconds:0.#}秒かけて）";
                case GearMotion.Continuous:
                    return $"{firstTurnAt:0.#}秒から、ずっと {dir}（{Mathf.Abs(degreesPerSecond):0.#}°/秒）";
                case GearMotion.Follow:
                    return driver != null ? $"「{driver.name}」にかみ合って回る（歯 {driver.teeth}:{teeth}）" : "かみ合う相手が空です";
                default:
                    return "回らない";
            }
        }

        /// <summary>回る部分の中の腕（壁）。</summary>
        public void CollectArms(List<GimmickGearArm> result)
        {
            result.Clear();
            var target = Rotor;
            if (target != null)
            {
                target.GetComponentsInChildren(true, result);
            }
        }

        private void Reset()
        {
            if (Rotor == null)
            {
                var go = new GameObject(RotorName);
                go.transform.SetParent(transform, false);
                rotor = go.transform;
            }
        }

        private void OnValidate()
        {
            _appliedAngle = float.NaN;
        }
    }
}
