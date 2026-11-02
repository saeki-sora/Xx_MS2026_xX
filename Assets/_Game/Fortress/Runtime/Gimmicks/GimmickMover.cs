using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>動く障害物の、止まる所を回り終えた後。</summary>
    public enum MoverLoop
    {
        [InspectorName("最初に戻ってくり返す")]
        Loop = 0,

        [InspectorName("行ったり来たり")]
        PingPong = 1,

        [InspectorName("1回だけ（最後の所で止まる）")]
        Once = 2
    }

    /// <summary>動く障害物の止まる所1つ分。位置は出発点からのずれ（m）。</summary>
    [Serializable]
    public sealed class MoverStop
    {
        [Tooltip("出発点からのずれ（m。画面の右が +X、上が +Y。歯車の中では歯車から見た向きで、+X が歯車の 0° の向き＝棒の腕の長さの向き）。")]
        public Vector2 offset;

        [Tooltip("出発点の向きから回す角度（度。反時計回りが +）。")]
        public float angle;

        [Tooltip("前の所からここまで動くのにかける秒数（出発点なら、最後の所から戻ってくる秒数）。")]
        [Min(GimmickMath.MinSeconds)]
        public float moveSeconds = 1.5f;

        [Tooltip("ここに着いてから、次に動き出すまで止まっている秒数。")]
        [Min(0f)]
        public float waitSeconds = 5f;

        public MoverStop Clone() => (MoverStop)MemberwiseClone();
    }

    /// <summary>
    /// 動く障害物（2026-10-29、ステージ3「歯車ステージ」から）。今ある障害物（壁・地帯・壊せる物）に付けると、
    /// 決めた「止まる所」を順に動く。位置・向きは時刻（<see cref="GimmickClock"/>）だけで決まるので、全員のPCで同じ所にある。
    /// 動くと経路が自動で作り直され、敵は今までどおりそれに従う（敵の仕組みは変えていない）。
    /// 出発点（<see cref="homePosition"/>）は、エディタで物を動かすと自動で付いてくる（要塞デザイナー › 仕掛け タブ）。
    /// 歯車の回る部分の中に置くと（<see cref="InGear"/>。例: 歯車の門の棒を時間で横へずらす、2026-10-30）、位置・向きを回る部分から見た値で持つので、
    /// 歯車と一緒に回りながら動く。
    /// スマッシュボール（自分で浮いて動く）には付けない。
    /// </summary>
    [AddComponentMenu("Fortress/仕掛け/動く障害物")]
    [DisallowMultipleComponent]
    public sealed class GimmickMover : GimmickBehaviour
    {
        [Tooltip("出発点（シーンに置いた位置。歯車の中では回る部分から見た位置。エディタで物を動かすと自動で合わせる）。")]
        public Vector2 homePosition;

        [Tooltip("出発点の向き（度）。")]
        public float homeAngle;

        [HideInInspector]
        public bool homeCaptured;

        [Header("動き方")]
        [Tooltip("止まる所を回り終えた後: 最初に戻ってくり返す / 行ったり来たり / 1回だけ")]
        public MoverLoop loop = MoverLoop.PingPong;

        [Tooltip("最初に動き出す時刻（秒。試合の始めが 0）。")]
        [Min(0f)]
        public float firstMoveAt = 4f;

        [Tooltip("止まる所。1つ目は出発点（ずれ 0）。2つ目から順に動く。")]
        public List<MoverStop> stops = new List<MoverStop>
        {
            new MoverStop(),
            new MoverStop { offset = new Vector2(3f, 0f) }
        };

        [Header("演出（全員のPCが自分で鳴らす）")]
        [Tooltip("動き出したとき。")]
        public FortressEffect onMoveStart = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("着いて止まったとき。")]
        public FortressEffect onArrive = new FortressEffect { tintWithPlayerColor = false };

        private readonly List<GimmickMath.Stop> _stops = new List<GimmickMath.Stop>();
        private readonly List<GimmickMath.Leg> _legs = new List<GimmickMath.Leg>();
        private bool _built;
        private bool _hasApplied;
        private GimmickMath.MoverPose _applied;

        protected override FortressEffect StartEffect => onMoveStart;

        protected override FortressEffect StopEffect => onArrive;

        /// <summary>くり返すか（1回だけでなければ true）。</summary>
        public bool Repeats => loop != MoverLoop.Once;

        protected override void OnEnable()
        {
            if (Application.isPlaying && !homeCaptured)
            {
                CaptureHome();
            }

            base.OnEnable();
        }

        /// <summary>歯車の回る部分の中にあるか（中なら、位置・向きは回る部分から見た値＝ローカルで持つ）。</summary>
        public bool InGear => transform.parent != null && transform.parent.GetComponentInParent<GimmickGear>() != null;

        /// <summary>今の位置・向きを出発点にする。</summary>
        public void CaptureHome()
        {
            ReadPose(out homePosition, out homeAngle);
            homeCaptured = true;
            _hasApplied = false;
        }

        /// <summary>今の位置・向き（歯車の中ならローカル、外なら世界）。出発点と比べるときに使う。</summary>
        public void ReadPose(out Vector2 position, out float angle)
        {
            if (InGear)
            {
                position = transform.localPosition;
                angle = transform.localEulerAngles.z;
            }
            else
            {
                position = transform.position;
                angle = transform.eulerAngles.z;
            }
        }

        /// <summary>止まる所・動き方を変えた後に呼ぶ（次の計算で作り直す）。</summary>
        public void MarkChanged()
        {
            _built = false;
            _hasApplied = false;
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _stops.Clear();
            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i] ?? new MoverStop();
                var offset = i == 0 ? Vector2.zero : stop.offset;
                var angle = i == 0 ? 0f : stop.angle;
                _stops.Add(new GimmickMath.Stop(offset.x, offset.y, angle, stop.moveSeconds, stop.waitSeconds));
            }

            GimmickMath.BuildLegs(_stops, (int)loop, _legs);
            _built = Application.isPlaying; // 編集中は値がいつでも変わるので、毎回作り直す
        }

        /// <summary>時刻 t の様子（出発点からのずれ・角度）。</summary>
        public GimmickMath.MoverPose PoseAt(float t)
        {
            EnsureBuilt();
            return GimmickMath.EvaluateMover(t, firstMoveAt, _stops, _legs, Repeats);
        }

        /// <summary>止まる所 i の世界の位置（歯車の中なら、歯車の今の向きで）。</summary>
        public Vector3 StopWorldPosition(int i)
        {
            var offset = i > 0 && i < stops.Count && stops[i] != null ? stops[i].offset : Vector2.zero;
            var x = homePosition.x + offset.x;
            var y = homePosition.y + offset.y;
            return InGear
                ? transform.parent.TransformPoint(new Vector3(x, y, transform.localPosition.z))
                : new Vector3(x, y, transform.position.z);
        }

        /// <summary>止まる所 i の世界の向き（度）。</summary>
        public float StopWorldAngle(int i)
        {
            var angle = homeAngle + (i > 0 && i < stops.Count && stops[i] != null ? stops[i].angle : 0f);
            return InGear ? transform.parent.eulerAngles.z + angle : angle;
        }

        /// <summary>世界の位置を、出発点からのずれ（歯車の中なら回る部分から見たずれ）にする。</summary>
        public Vector2 WorldToOffset(Vector3 world)
        {
            var position = InGear ? (Vector2)transform.parent.InverseTransformPoint(world) : (Vector2)world;
            return position - homePosition;
        }

        public override bool IsMovingAt(float t) => PoseAt(t).Moving;

        public override bool ApplyAt(float t)
        {
            if (!homeCaptured)
            {
                CaptureHome();
            }

            var pose = PoseAt(t);
            // 編集中（プレビュー）は、手で動かした後でも必ず書く。
            var changed = !Application.isPlaying || !_hasApplied ||
                          Mathf.Abs(pose.X - _applied.X) > 1e-5f ||
                          Mathf.Abs(pose.Y - _applied.Y) > 1e-5f ||
                          Mathf.Abs(pose.Angle - _applied.Angle) > 1e-4f;
            if (changed)
            {
                var x = homePosition.x + pose.X;
                var y = homePosition.y + pose.Y;
                var rotation = Quaternion.Euler(0f, 0f, homeAngle + pose.Angle);
                if (InGear)
                {
                    transform.SetLocalPositionAndRotation(new Vector3(x, y, transform.localPosition.z), rotation);
                }
                else
                {
                    transform.SetPositionAndRotation(new Vector3(x, y, transform.position.z), rotation);
                }

                _applied = pose;
                _hasApplied = true;
                NotifyNavigation();
            }

            return pose.Moving;
        }

        public override void CollectIntervals(float horizon, List<(float start, float end)> result)
        {
            EnsureBuilt();
            GimmickMath.MoverIntervals(firstMoveAt, _legs, Repeats, horizon, result);
        }

        public override string Describe()
        {
            var count = Mathf.Max(0, stops.Count - 1);
            if (count == 0)
            {
                return "止まる所が出発点だけ（動きません）";
            }

            var mode = loop switch
            {
                MoverLoop.Loop => "くり返す",
                MoverLoop.PingPong => "行ったり来たり",
                _ => "1回だけ"
            };
            return $"{firstMoveAt:0.#}秒から、止まる所 {count} か所を{mode}";
        }

        private void OnValidate()
        {
            if (stops == null)
            {
                stops = new List<MoverStop>();
            }

            if (stops.Count == 0)
            {
                stops.Add(new MoverStop());
            }

            if (stops[0] != null)
            {
                stops[0].offset = Vector2.zero;
                stops[0].angle = 0f;
            }

            MarkChanged();
        }
    }
}
