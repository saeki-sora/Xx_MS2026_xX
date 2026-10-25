using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// ステージ1つ分の情報（名前・中身のシーン・ルーレットのカードの見た目）。
    /// 中身（障害物・背景・敵・砲台とコアの位置）は <see cref="sceneName"/> のシーンにあり、共通のゲームのシーン（Game）に重ねて読み込む。
    /// ステージの一覧は <see cref="StageCatalog"/>。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/ステージ/ステージ", fileName = "Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        [Tooltip("プログラムから探すときの名前（起動引数 -fortress-stage でも使える）。あとから変えないこと。")]
        public string stageId = "stage1";

        [Tooltip("ルーレットのカードなど、画面に出すステージ名。空ならファイル名。")]
        public string displayName = "ステージ1";

        [Tooltip("ルーレットのカードに小さく出す説明（1行くらい）。")]
        public string subtitle = "";

        [Tooltip("ステージの中身のシーンの名前（File › Build Profiles のシーン一覧に入っていること）。")]
        public string sceneName = "Stage1";

        [Tooltip("ルーレットのカードの絵（空ならテーマ色の板に番号を大きく出す）。")]
        public Sprite thumbnail;

        [Tooltip("ルーレットのカードの色。")]
        public Color themeColor = new Color(0.35f, 0.8f, 1f);

        [Tooltip("OFF にすると、ルーレットに出さない（作りかけのステージなど）。Game だけで遊ぶときに直接選ぶことはできる。")]
        public bool includeInRoulette = true;

        [Tooltip("OFF にすると、このステージでは試合の始まりの演出（障害物・コア・プレイヤーが上から落ちてくる）をしない。演出の設定は Resources の StageDropIntroSettings。")]
        public bool playDropIntro = true;

        /// <summary>画面に出す名前（空ならファイル名）。</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}
