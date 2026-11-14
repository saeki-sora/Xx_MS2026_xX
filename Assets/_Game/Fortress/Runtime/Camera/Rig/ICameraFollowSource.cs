namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】ずっと続くカメラの追従（WASD で動く砲台を追う）。
    /// 一時演出（<see cref="ICameraViewModifier"/>）と違い、SnapToTarget や ClearModifiers で消えない。
    /// リグは「補間後の視点」にこれを重ねてから、一時演出（揺れ・ズーム）を重ねる。
    /// </summary>
    public interface ICameraFollowSource
    {
        /// <summary>viewer（<see cref="ViewerIndex"/>）の視点 view に追従を重ねて返す。deltaTime が 0 なら今の追従のまま重ねる。</summary>
        CameraViewSettings Apply(int viewer, CameraViewSettings view, float deltaTime);
    }
}
