using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>【実験】砲台の調子。</summary>
    public enum TurretCondition : byte
    {
        /// <summary>ふつう。菌に触れると汚れが溜まる。</summary>
        Normal = 0,

        /// <summary>倒れている。撃てない・回らない・動けない。菌は素通りする。</summary>
        Downed = 1,

        /// <summary>復活した直後。少しの間、菌にやられない。</summary>
        Protected = 2
    }

    /// <summary>1人分の調子（Host・オフラインが計算し、Client は届いた値を入れる）。</summary>
    internal sealed class TurretDownState
    {
        public TurretCondition Condition;

        /// <summary>汚れ（0〜1、1 で倒れる）。</summary>
        public float Dirt01;

        /// <summary>倒れている間の復活の進み具合（0〜1、1 で復活）。</summary>
        public float Revive01;

        public float ProtectLeft;
        public bool LastGripping;

        /// <summary>体力（0〜1）= 1 − 汚れ。</summary>
        public float Health01 => Condition == TurretCondition.Downed ? 0f : 1f - Dirt01;

        public void Reset()
        {
            Condition = TurretCondition.Normal;
            Dirt01 = 0f;
            Revive01 = 0f;
            ProtectLeft = 0f;
            LastGripping = false;
        }
    }

    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】菌にやられて倒れる・復活する仕組み（見た目は <see cref="TurretDownView"/>、
    /// 倒れた人の画面の案内は <see cref="TurretDownOverlay"/>）。
    /// ・Host・オフライン（<see cref="TickAuthority"/>）が4人分を計算する。菌の位置・ビーム・全員の握力がそろっているのは Host だから。
    /// ・Client は Host から届いた調子を <see cref="ApplyRemote"/> で入れるだけ。
    /// ・倒れる: 菌に触れている間、重なっている菌の量に応じて汚れが溜まり、1 で倒れる（体力 = 1 − 汚れ）。
    /// ・回復: 時間では戻らない。倒れていない人に味方のビームが当たっている間だけ、汚れが落ちる（体力が戻る）。
    /// ・復活: 味方のビームが自分の上を通っている間 ＋ 握るたび（押した瞬間だけ数えるので、長押しは1回）に進み、1 で復活。
    /// ・復活の直後は <see cref="TurretMoveSettings.protectSeconds"/> 秒やられない。
    /// </summary>
    internal sealed class TurretDownSystem
    {
        public const int PlayerCount = 4;

        public readonly TurretDownState[] States = new TurretDownState[PlayerCount];

        /// <summary>菌との重なりの合計（Host・オフラインが群衆の計算の合間に入れる）。</summary>
        public readonly float[] Contact = new float[PlayerCount];

        private readonly TurretCondition[] _shownCondition = new TurretCondition[PlayerCount];

        public TurretDownSystem()
        {
            for (var p = 0; p < PlayerCount; p++)
            {
                States[p] = new TurretDownState();
            }
        }

        public bool IsDowned(int player) => player >= 0 && player < PlayerCount && States[player].Condition == TurretCondition.Downed;

        public void Reset(LaserTurret[] turrets)
        {
            for (var p = 0; p < PlayerCount; p++)
            {
                States[p].Reset();
                Contact[p] = 0f;
                _shownCondition[p] = TurretCondition.Normal;
                if (turrets[p] != null)
                {
                    turrets[p].ForceIdle = false;
                }
            }
        }

        /// <summary>Host・オフライン: 4人分の調子を進める。</summary>
        /// <param name="paused">落下演出・席替えの間（汚れが溜まらない）。</param>
        /// <param name="reviveAll">歯みがきタイム（ウェーブの合間）の間。倒れている人は全員その場で復活する。</param>
        public void TickAuthority(float dt, LaserTurret[] turrets, TurretMoveSettings settings, bool paused, bool reviveAll)
        {
            for (var p = 0; p < PlayerCount; p++)
            {
                var turret = turrets[p];
                var state = States[p];
                if (turret == null)
                {
                    continue;
                }

                var gripping = ReadGripping(turret);
                var pressed = gripping && !state.LastGripping;
                state.LastGripping = gripping;

                if (!settings.downEnabled)
                {
                    state.Reset();
                    state.LastGripping = gripping;
                }
                else if (state.Condition == TurretCondition.Downed && reviveAll)
                {
                    // 歯みがきタイムに入った。倒れている人は全員復活（復活の直後と同じく少しの間やられない）。
                    state.Condition = TurretCondition.Protected;
                    state.ProtectLeft = settings.protectSeconds;
                    state.Dirt01 = 0f;
                    state.Revive01 = 0f;
                }
                else if (state.Condition == TurretCondition.Downed)
                {
                    if (pressed)
                    {
                        state.Revive01 += 1f / Mathf.Max(1, settings.strugglePresses);
                    }

                    var beams = CountAllyBeams(p, turret, settings);
                    state.Revive01 += beams * dt / Mathf.Max(0.05f, settings.reviveBeamSeconds);
                    if (state.Revive01 >= 1f)
                    {
                        state.Condition = TurretCondition.Protected;
                        state.ProtectLeft = settings.protectSeconds;
                        state.Dirt01 = 0f;
                        state.Revive01 = 0f;
                    }
                }
                else
                {
                    if (state.Condition == TurretCondition.Protected)
                    {
                        state.ProtectLeft -= dt;
                        if (state.ProtectLeft <= 0f)
                        {
                            state.Condition = TurretCondition.Normal;
                        }
                    }

                    var contact = state.Condition == TurretCondition.Normal && !paused ? Contact[p] : 0f;
                    if (contact > 1e-4f)
                    {
                        state.Dirt01 += Mathf.Min(contact * settings.dirtPerOverlap, settings.dirtMaxPerSecond) * dt;
                    }

                    // 時間では回復しない。味方のビームを当ててもらった分だけ汚れが落ちる（体力が戻る）。
                    var healers = state.Dirt01 > 0f ? CountAllyBeams(p, turret, settings) : 0;
                    if (healers > 0)
                    {
                        state.Dirt01 = Mathf.Max(0f, state.Dirt01 - healers * settings.allyBeamHealPerSecond * dt);
                    }

                    if (state.Dirt01 >= 1f)
                    {
                        state.Dirt01 = 1f;
                        state.Revive01 = 0f;
                        state.Condition = TurretCondition.Downed;
                    }
                }

                turret.ForceIdle = state.Condition == TurretCondition.Downed;
            }
        }

        /// <summary>Client: Host から届いた調子を入れる。</summary>
        public void ApplyRemote(int player, TurretCondition condition, float dirt01, float revive01)
        {
            if (player < 0 || player >= PlayerCount)
            {
                return;
            }

            var state = States[player];
            state.Condition = condition;
            state.Dirt01 = dirt01;
            state.Revive01 = revive01;
        }

        /// <summary>
        /// 全PC: 調子が変わった瞬間に演出を鳴らす（Host でも Client でも、自分の画面で見えた変化で鳴らすので通信しない）。
        /// </summary>
        public void PlayTransitionEffects(LaserTurret[] turrets, TurretMoveSettings settings)
        {
            for (var p = 0; p < PlayerCount; p++)
            {
                var now = States[p].Condition;
                var before = _shownCondition[p];
                _shownCondition[p] = now;
                if (now == before || turrets[p] == null)
                {
                    continue;
                }

                var position = turrets[p].transform.position;
                if (now == TurretCondition.Downed)
                {
                    FortressEffectPlayer.PlayOnce(settings.onDowned, position, Quaternion.identity, p);
                }
                else if (before == TurretCondition.Downed)
                {
                    FortressEffectPlayer.PlayOnce(settings.onRevived, position, Quaternion.identity, p);
                }
            }
        }

        // その人の握力（Host では TurretNetworkHub が他人の分を Client から届いた値で返す。オフラインはセンサー p）。
        private static bool ReadGripping(LaserTurret turret)
        {
            if (turret.GripSourceOverride != null)
            {
                turret.GripSourceOverride.GetGrip(turret.playerIndex, out var isGripping, out _);
                return isGripping;
            }

            return global::MS2026.GripInputBridge.GripInputBridge.Provider.IsGripping(turret.playerIndex);
        }

        // 倒れている砲台 downed の上を、いま通っている味方のビームの数。ビームは歯や壁で止まった所まで（その先の仲間は助けられない）。
        private int CountAllyBeams(int downedPlayer, LaserTurret downed, TurretMoveSettings settings)
        {
            var count = 0;
            Vector2 target = downed.transform.position;
            var beams = LaserBeamVisual.Active;
            for (var i = 0; i < beams.Count; i++)
            {
                var beam = beams[i];
                var shooter = beam != null ? beam.Turret : null;
                if (shooter == null || shooter == downed || !shooter.IsFiring || shooter.tuning == null)
                {
                    continue;
                }

                var shooterIndex = shooter.playerIndex;
                if (shooterIndex == downedPlayer || IsDowned(shooterIndex))
                {
                    continue;
                }

                Vector2 origin = shooter.MuzzlePosition;
                var end = beam.HasImpact ? (Vector2)beam.ImpactPoint : origin + shooter.AimDirection * shooter.tuning.range;
                var reach = settings.bodyRadius + shooter.CurrentThicknessMeters * 0.5f;
                if (DistanceToSegment(target, origin, end) <= reach)
                {
                    count++;
                }
            }

            return count;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            var t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
            return (a + ab * t - p).magnitude;
        }
    }
}
