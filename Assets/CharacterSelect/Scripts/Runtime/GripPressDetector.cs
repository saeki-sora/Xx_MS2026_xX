namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 「短く握る」と「長く握る」を見分ける（握力センサー・仮のキーの両方に使う）。
    /// ・指定の秒数より前に離した → 短く（離した瞬間に1回）
    /// ・指定の秒数まで握り続けた → 長く（その瞬間に1回。離すまで次は出ない）
    /// 最初に握ったままだった場合（ロビーの「握って準備OK」の続きなど）は、一度離すまで何もしない。
    /// 連打で押しがつながって「長く」にならないよう、「このフレームの間に離した回数」も受け取る
    /// （1フレームの中で離して押し直しても、そこで1回の押しとして終わらせ、新しい押しを数え直す）。
    /// Unity に頼らないので、テストで中身を確かめられる。
    /// </summary>
    public sealed class GripPressDetector
    {
        public enum Result
        {
            None,
            Short,
            Long
        }

        private bool _down;
        private bool _longFired;
        private bool _waitRelease = true;
        private float _held;
        private float _longSeconds = 1f;

        /// <summary>今握っている秒数（長く握ったと判定した後・離しているときは 0）。</summary>
        public float HeldSeconds => _down && !_longFired ? _held : 0f;

        /// <summary>握っている途中か（「長く」になる前。長く握ったと判定した後は false）。</summary>
        public bool IsPressing => _down && !_longFired;

        /// <summary>長く握るまでの進み具合（0〜1）。</summary>
        public float Progress => _longSeconds > 0f ? Clamp01(HeldSeconds / _longSeconds) : 0f;

        /// <summary>毎フレーム呼ぶ。pressed = 今握っているか。</summary>
        public Result Tick(bool pressed, float deltaSeconds, float longSeconds) => Tick(pressed, 0, deltaSeconds, longSeconds);

        /// <summary>毎フレーム呼ぶ。pressed = 今握っているか、releasesThisFrame = 前のフレームからの間に離した回数。</summary>
        public Result Tick(bool pressed, int releasesThisFrame, float deltaSeconds, float longSeconds)
        {
            _longSeconds = longSeconds;
            if (_waitRelease)
            {
                if (pressed && releasesThisFrame == 0)
                {
                    return Result.None;
                }

                _waitRelease = false;
                if (pressed)
                {
                    // 離してすぐ押し直した: 新しい押しの始まり。
                    StartPress();
                }

                return Result.None;
            }

            if (_down && releasesThisFrame > 0)
            {
                // 押していた分は、このフレームの間に離された（すぐ押し直されていても、そこで1回の押しとして終わる）。
                var result = _longFired ? Result.None : Result.Short;
                _down = false;
                _longFired = false;
                _held = 0f;
                if (pressed)
                {
                    StartPress();
                }

                return result;
            }

            if (pressed)
            {
                if (!_down)
                {
                    StartPress();
                    return Result.None;
                }

                _held += deltaSeconds;
                if (!_longFired && _held >= longSeconds)
                {
                    _longFired = true;
                    return Result.Long;
                }

                return Result.None;
            }

            if (!_down)
            {
                return Result.None;
            }

            _down = false;
            _held = 0f;
            var wasLong = _longFired;
            _longFired = false;
            return wasLong ? Result.None : Result.Short;
        }

        /// <summary>途中の握りを忘れる。requireRelease なら、次は一度離してから数える。</summary>
        public void Reset(bool requireRelease = true)
        {
            _down = false;
            _longFired = false;
            _held = 0f;
            _waitRelease = requireRelease;
        }

        private void StartPress()
        {
            _down = true;
            _longFired = false;
            _held = 0f;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
