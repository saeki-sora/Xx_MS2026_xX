namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 一時的なカメラ演出(ズーム・寄り・簡易揺れ等)の共通インターフェース。
    /// ベースの視点を受け取って書き換えるだけなので、演出を増やすときはこれを実装して
    /// <see cref="FortressCameraRig.AddModifier"/> に渡せばよい（リグ本体は変更不要）。
    /// </summary>
    public interface ICameraViewModifier
    {
        /// <summary>視点に演出を重ねる。演出が終わったらfalseを返す（リグが自動で取り除く）。</summary>
        bool Apply(ref CameraViewSettings view, float deltaTime);
    }
}
