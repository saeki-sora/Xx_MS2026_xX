using System;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>練習の3つの課題（上から順に進む）。</summary>
    public enum PracticeStep : byte
    {
        /// <summary>①「握ってみる」の途中。</summary>
        Grip = 0,

        /// <summary>②「思いきり握る」の途中。</summary>
        Squeeze = 1,

        /// <summary>③「離して冷ます」の途中。</summary>
        Release = 2,

        /// <summary>全部できた。</summary>
        Done = 3
    }

    /// <summary><see cref="PracticeTrainer.Tick"/> で起きたこと。</summary>
    [Flags]
    public enum PracticeEvents
    {
        None = 0,

        /// <summary>課題が1つ進んだ。</summary>
        StepAdvanced = 1,

        /// <summary>オーバーヒートした。</summary>
        Overheated = 2
    }

    /// <summary>
    /// 自分の PC の練習を動かす係（握力 → 熱・オーバーヒート → 課題の進み）。Unity にも通信にも頼らないのでテストで確かめられる。
    /// 熱は本番の砲台（LaserTurret）と同じく「握っている間 握力² でたまり、離すと下がる。いっぱいになると数秒撃てない」。
    /// </summary>
    public sealed class PracticeTrainer
    {
        private float _silence;

        public PracticeStep Step { get; private set; }

        /// <summary>握力（0〜1）。</summary>
        public float Grip01 { get; private set; }

        /// <summary>握っているか（センサーの「握った」判定）。</summary>
        public bool Gripping { get; private set; }

        /// <summary>熱（0〜1。1 でオーバーヒート）。</summary>
        public float Heat01 { get; private set; }

        /// <summary>オーバーヒートで撃てない間。</summary>
        public bool Overheated { get; private set; }

        public bool IsDone => Step == PracticeStep.Done;

        /// <summary>ビームが出ているか。</summary>
        public bool Firing => Gripping && !Overheated;

        public void Reset()
        {
            Step = PracticeStep.Grip;
            Grip01 = 0f;
            Gripping = false;
            Heat01 = 0f;
            Overheated = false;
            _silence = 0f;
        }

        /// <summary>全部できたことにする（時間切れのとき）。熱やビームはそのまま動く。</summary>
        public void Complete() => Step = PracticeStep.Done;

        /// <summary>1フレーム進める。gripping = センサーの「握った」判定、grip = 握力（0〜1）。</summary>
        public PracticeEvents Tick(bool gripping, float grip, float dt, PracticeRules rules)
        {
            rules ??= new PracticeRules();
            dt = Mathf.Max(0f, dt);
            Grip01 = Mathf.Clamp01(grip);
            Gripping = gripping && Grip01 > 0f;

            var events = PracticeEvents.None;
            if (Overheated)
            {
                _silence -= dt;
                Heat01 = Mathf.Max(0f, Heat01 - rules.heatCoolingPerSecond * dt);
                if (_silence <= 0f)
                {
                    Overheated = false;
                }
            }
            else if (Gripping)
            {
                Heat01 += Grip01 * Grip01 * rules.heatGainPerSecond * dt;
                if (Heat01 >= 1f)
                {
                    Heat01 = 1f;
                    Overheated = true;
                    _silence = rules.overheatSilenceSeconds;
                    events |= PracticeEvents.Overheated;
                }
            }
            else
            {
                Heat01 = Mathf.Max(0f, Heat01 - rules.heatCoolingPerSecond * dt);
            }

            var before = Step;
            switch (Step)
            {
                case PracticeStep.Grip:
                    if (Gripping && Grip01 >= rules.firstGripThreshold)
                    {
                        Step = PracticeStep.Squeeze;
                    }

                    break;
                case PracticeStep.Squeeze:
                    if (Heat01 >= rules.squeezeHeat01 || Overheated)
                    {
                        Step = PracticeStep.Release;
                    }

                    break;
                case PracticeStep.Release:
                    if (!Gripping && Heat01 <= rules.cooledHeat01)
                    {
                        Step = PracticeStep.Done;
                    }

                    break;
            }

            if (Step != before)
            {
                events |= PracticeEvents.StepAdvanced;
            }

            return events;
        }
    }
}
