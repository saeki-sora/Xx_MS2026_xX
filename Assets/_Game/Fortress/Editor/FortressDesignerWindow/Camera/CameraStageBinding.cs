using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// カメラタブが「ステージごとの視点」を扱うための窓口。ステージの仕組み（MS2026.Stages.Editor）が起動時に
    /// <see cref="CameraStageBinding.Current"/> へ登録する（要塞デザイナーはステージの仕組みを直接知らない）。
    /// ステージを分けた後は、カメラリグは共通の Game にだけあり、視点はステージのシーンの「配置の目印」（StageLayout.cameraPreset）が持つ。
    /// </summary>
    public interface ICameraStageBinding
    {
        /// <summary>そのシーンがステージのシーンか（カメラリグを置いてはいけないシーン）。</summary>
        bool IsStageScene(Scene scene);

        /// <summary>開いているステージ（2 つ以上ならアクティブなシーンの物）。開いていなければ null。</summary>
        CameraStageInfo FindOpenStage();

        /// <summary>Game だけで Play したときに読み込まれるステージ（説明用。分からなければ null）。</summary>
        CameraStageInfo FindStageForPlay();

        /// <summary>ステージの視点を preset にする（ステージのシーンに保存される。Game のリグには見せるだけで保存しない）。</summary>
        void AssignPreset(CameraStageInfo stage, CameraViewPreset preset);

        /// <summary>preset を使っている、ほかのステージの表示名（stage が null なら全部のステージから探す。ファイルに保存された中身で調べる）。</summary>
        IReadOnlyList<string> OtherStagesUsing(CameraStageInfo stage, CameraViewPreset preset);

        /// <summary>共通のシーン（Game）を重ねて開く（ステージはアクティブのまま）。</summary>
        void OpenCommonScene();

        /// <summary>そのステージを Game と一緒に開く（編集用）。</summary>
        void OpenWithCommonScene(CameraStageInfo stage);
    }

    /// <summary>ステージ 1 つ分の、カメラに関わる情報。</summary>
    public sealed class CameraStageInfo
    {
        /// <summary>ルーレットなどに出る名前（例: お風呂場）。</summary>
        public string DisplayName;

        /// <summary>シーンの名前（例: Stage2_Bath）。</summary>
        public string SceneName;

        /// <summary>シーンのファイル。</summary>
        public string ScenePath;

        /// <summary>このステージの視点（空なら Game のカメラの視点のまま）。開いていないステージでは、シーンが参照している物からの推定。</summary>
        public CameraViewPreset Preset;

        /// <summary>視点を持っている部品（開いているステージの StageLayout。開いていなければ null）。</summary>
        public Object Owner;

        public string Label => string.IsNullOrEmpty(DisplayName) || DisplayName == SceneName ? $"「{SceneName}」" : $"「{DisplayName}」（{SceneName}）";
    }

    public static class CameraStageBinding
    {
        /// <summary>ステージの仕組みが登録する窓口（ステージの仕組みが無いプロジェクトでは null）。</summary>
        public static ICameraStageBinding Current { get; set; }

        /// <summary>そのリグがステージのシーンに置かれているか（置いてはいけない）。</summary>
        public static bool IsInStageScene(Component component)
        {
            return Current != null && component != null && Current.IsStageScene(component.gameObject.scene);
        }
    }
}
