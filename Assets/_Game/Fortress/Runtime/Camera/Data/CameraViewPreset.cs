using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 4人分＋全体視点の「視点セット」。地形案や演出シーン(通常時・ボス戦など)ごとにアセットとして複数保存しておき、
    /// <see cref="FortressCameraRig"/> に割り当てる。ゲーム中の切り替えもなめらかに補間できる。
    /// 砲台配置プリセット(FortressLayoutConfig)とは独立しているので、同じ地形で視点だけ差し替える比較もできる。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Camera View Preset", fileName = "New CameraViewPreset")]
    public sealed class CameraViewPreset : ScriptableObject
    {
        [Tooltip("プリセットの表示名（例: 通常 / 自分の砲台が下 / 寄り気味）。")]
        public string presetName = "New Camera Preset";

        [TextArea(1, 3)]
        [Tooltip("メモ。どんな意図の視点かを書いておくと比較しやすい。")]
        public string memo;

        [Tooltip("観戦・未接続時などプレイヤーが決まっていないときの視点。")]
        public CameraViewSettings overview = CameraViewSettings.Default;

        [Tooltip("プレイヤー0-3それぞれの視点。")]
        public CameraViewSettings[] players = CreateDefaultPlayers();

        [Header("絵を立たせる(ビルボード)")]
        [Tooltip("斜め見下ろしの視点で、地面に寝ている平らな絵をカメラに向けて立たせる。OFFなら今まで通りの平らな絵。")]
        public BillboardSettings billboard = new BillboardSettings();

        [Header("共通")]
        [Tooltip("これより近いものは映らない。")]
        [Min(0.01f)]
        public float nearClip = 0.3f;

        [Tooltip("これより遠いものは映らない。")]
        [Min(0.02f)]
        public float farClip = 1000f;

        public string DisplayName => string.IsNullOrWhiteSpace(presetName) ? name : presetName;

        /// <summary>指定した視点番号の視点を返す。プレイヤー分が足りないときは全体視点で代用する。</summary>
        public CameraViewSettings GetView(int viewer)
        {
            if (ViewerIndex.IsPlayer(viewer) && players != null && viewer < players.Length)
            {
                return players[viewer];
            }

            return overview;
        }

        /// <summary>指定した視点番号の視点を書き換える（Undo・SetDirtyは呼び出し側で行う）。</summary>
        public void SetView(int viewer, CameraViewSettings view)
        {
            view = view.Sanitized();

            if (!ViewerIndex.IsPlayer(viewer))
            {
                overview = view;
                return;
            }

            EnsurePlayerSlots();
            players[viewer] = view;
        }

        /// <summary>プレイヤー分の配列を4人分に揃える。足りない分は全体視点で埋める。</summary>
        public void EnsurePlayerSlots()
        {
            if (players != null && players.Length == ViewerIndex.PlayerCount)
            {
                return;
            }

            var resized = new CameraViewSettings[ViewerIndex.PlayerCount];
            for (var i = 0; i < resized.Length; i++)
            {
                resized[i] = players != null && i < players.Length ? players[i] : overview;
            }

            players = resized;
        }

        private void OnValidate()
        {
            EnsurePlayerSlots();
            farClip = Mathf.Max(farClip, nearClip + 0.01f);
        }

        private static CameraViewSettings[] CreateDefaultPlayers()
        {
            var result = new CameraViewSettings[ViewerIndex.PlayerCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = CameraViewSettings.Default;
            }

            return result;
        }
    }
}
