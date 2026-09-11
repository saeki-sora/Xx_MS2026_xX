namespace MS2026.SpriteAnim
{
    /// <summary>
    /// 「あるフレームから次のフレームへ進める」というループ方式ごとの純粋な計算ロジック。
    /// ランタイム再生(SpriteAnimator)とエディタのプレビュー再生の両方から呼ばれることで、
    /// 見え方の挙動が常に一致することを保証する（プレビューだけ動きが違う、という事故を防ぐため）。
    /// </summary>
    public static class SpriteAnimationPlayback
    {
        public struct StepResult
        {
            public int frameIndex;
            public bool forward;

            /// <summary>この1ステップでループの先頭（Loop）または端の折り返し（PingPong）が発生したか。</summary>
            public bool looped;

            /// <summary>Once モードで最終フレームに到達し、再生が終了したか。</summary>
            public bool finished;
        }

        public static StepResult Advance(SpriteLoopMode mode, int frameCount, int frameIndex, bool forward)
        {
            var result = new StepResult { frameIndex = frameIndex, forward = forward };
            if (frameCount <= 1) return result;

            switch (mode)
            {
                case SpriteLoopMode.Once:
                    if (frameIndex < frameCount - 1) result.frameIndex = frameIndex + 1;
                    else result.finished = true;
                    break;

                case SpriteLoopMode.ClampForever:
                    if (frameIndex < frameCount - 1) result.frameIndex = frameIndex + 1;
                    break;

                case SpriteLoopMode.Loop:
                    result.frameIndex = frameIndex + 1;
                    if (result.frameIndex >= frameCount)
                    {
                        result.frameIndex = 0;
                        result.looped = true;
                    }
                    break;

                case SpriteLoopMode.PingPong:
                    if (forward)
                    {
                        int next = frameIndex + 1;
                        if (next >= frameCount - 1)
                        {
                            result.frameIndex = frameCount - 1;
                            result.forward = false;
                        }
                        else
                        {
                            result.frameIndex = next;
                        }
                    }
                    else
                    {
                        int next = frameIndex - 1;
                        if (next <= 0)
                        {
                            result.frameIndex = 0;
                            result.forward = true;
                            result.looped = true;
                        }
                        else
                        {
                            result.frameIndex = next;
                        }
                    }
                    break;
            }

            return result;
        }
    }
}
