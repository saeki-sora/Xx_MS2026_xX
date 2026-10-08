using MS2026.Fortress.Cameras;
using UnityEngine;
using BgmId = DDrive.Foundation.Identity.AssetId<DDrive.Runtime.Audio.BgmMarker>;

namespace MS2026.Stage
{
    /// <summary>
    /// ステージ1つ分（洗面所・台所…）のまとめ。背景オブジェクトの入ったPrefabと、カメラ・光・反応の設定・BGMをセットにする。
    /// ステージ背景スタジオで「シーンに出す」と、Prefabがシーンの StageRoot の下に置かれ、カメラと光がこの設定になる。
    /// </summary>
    [CreateAssetMenu(menuName = "Stage/Stage Set", fileName = "StageSet")]
    public sealed class StageSet : ScriptableObject
    {
        [Tooltip("ツールやメニューに出す名前。")]
        public string displayName = "新しいステージ";

        [Tooltip("メモ（雰囲気・参考資料・担当者など）。")]
        [TextArea(2, 6)]
        public string memo;

        [Tooltip("ツールの一覧に出す絵（無ければ自動で撮った絵を使う）。")]
        public Texture2D thumbnail;

        [Tooltip("背景オブジェクトをまとめたPrefab。中身はステージ背景スタジオで編集する。")]
        public GameObject stagePrefab;

        [Tooltip("このステージで使うカメラの見え方（4人分の視点）。空ならシーンのカメラ設定のまま。")]
        public CameraViewPreset cameraPreset;

        [Tooltip("透け・敵の影・熱・揺れ・壊れ方の共通設定。空なら既定値。")]
        public StageLookProfile lookProfile;

        public StageLightingSettings lighting = new StageLightingSettings();

        [Tooltip("このステージが始まったときに流すBGM（D-Drive）。空なら何もしない。")]
        public BgmId bgm;

        public string Label => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}
