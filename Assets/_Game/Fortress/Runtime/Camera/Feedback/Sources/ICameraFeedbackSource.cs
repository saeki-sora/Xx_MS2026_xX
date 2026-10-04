using System;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// ゲーム内の出来事を拾って演出の依頼(<see cref="CameraFeedbackRequest"/>)に変換する係。
    /// 出来事の種類を増やすときは、これを実装して <see cref="CameraFeedbackDirector.AddSource"/> で登録する。
    /// </summary>
    public interface ICameraFeedbackSource
    {
        /// <summary>出来事の購読を始める。起きたら raise を呼ぶ。</summary>
        void Enable(Action<CameraFeedbackRequest> raise);

        /// <summary>購読をやめる。Enableと対で必ず呼ばれる。</summary>
        void Disable();
    }
}
