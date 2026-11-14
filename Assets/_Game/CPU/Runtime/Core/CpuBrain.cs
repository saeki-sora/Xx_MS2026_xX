using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 点数の比べ方（決まりだけ。テストできるように Unity に頼らない）。
    /// </summary>
    public static class CpuChoice
    {
        /// <summary>
        /// 一番点の高い番号を返す（全部 0 以下なら -1）。今の行動（current）には stickiness を足して比べる
        /// （ほとんど同じ点の行動どうしで、考え直すたびに行ったり来たりしないように）。同じ点なら先に登録した方。
        /// </summary>
        public static int Pick(IReadOnlyList<float> scores, int current, float stickiness)
        {
            var best = -1;
            var bestScore = 0f;
            for (var i = 0; i < scores.Count; i++)
            {
                var score = scores[i];
                if (!(score > 0f))
                {
                    continue; // 0 以下・NaN はやらない
                }

                if (i == current)
                {
                    score += Mathf.Max(0f, stickiness);
                }

                if (best < 0 || score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// CPU の頭。登録された行動に点を付けさせ（<see cref="ThinkInterval"/> 秒ごと）、一番高い行動を毎フレーム進める。
    /// どの行動もやらないときは手を離す。
    /// </summary>
    public sealed class CpuBrain
    {
        private readonly List<ICpuBehavior> _behaviors;
        private readonly List<float> _scores = new List<float>();
        private float _thinkTimer;
        private int _current = -1;

        public CpuBrain(List<ICpuBehavior> behaviors)
        {
            _behaviors = behaviors ?? new List<ICpuBehavior>();
            for (var i = 0; i < _behaviors.Count; i++)
            {
                _scores.Add(0f);
            }
        }

        /// <summary>考え直す間隔（秒）。</summary>
        public float ThinkInterval { get; set; } = 0.15f;

        /// <summary>今の行動に足す点（行ったり来たりしないため）。</summary>
        public float Stickiness { get; set; } = 0.1f;

        public IReadOnlyList<ICpuBehavior> Behaviors => _behaviors;

        /// <summary>最後に考えたときの点（頭の中の表示用。並びは <see cref="Behaviors"/> と同じ）。</summary>
        public IReadOnlyList<float> Scores => _scores;

        /// <summary>今やっている行動（何もしていなければ null）。</summary>
        public ICpuBehavior Current => _current >= 0 ? _behaviors[_current] : null;

        public void Tick(CpuContext context, float deltaTime)
        {
            _thinkTimer -= deltaTime;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = Mathf.Max(0.02f, ThinkInterval);
                Think(context);
            }

            var current = Current;
            if (current == null)
            {
                context.Hands.Release();
                return;
            }

            try
            {
                current.Tick(context, deltaTime);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>今の行動をやめる（CPU が止まるとき）。</summary>
        public void Stop(CpuContext context)
        {
            SwitchTo(context, -1);
            context.Hands.ReleaseNow();
        }

        private void Think(CpuContext context)
        {
            for (var i = 0; i < _behaviors.Count; i++)
            {
                float score;
                try
                {
                    score = _behaviors[i].Score(context);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    score = 0f;
                }

                _scores[i] = score;
            }

            SwitchTo(context, CpuChoice.Pick(_scores, _current, Stickiness));
        }

        private void SwitchTo(CpuContext context, int next)
        {
            if (next == _current)
            {
                return;
            }

            var before = Current;
            _current = next;
            try
            {
                before?.Exit(context);
                Current?.Enter(context);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
