using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>役割ごとの色・アイコン・説明（一覧・シーンビュー・選択ボタンで共通に使う）。</summary>
    public static class StageRoleStyle
    {
        public static readonly StagePropRole[] All =
        {
            StagePropRole.Floor,
            StagePropRole.Wall,
            StagePropRole.DestructibleWall,
            StagePropRole.SlowZone,
            StagePropRole.Decoration
        };

        public static Color Color(StagePropRole role) => role switch
        {
            StagePropRole.Floor => new Color(0.55f, 0.6f, 0.68f),
            StagePropRole.Wall => new Color(1f, 0.48f, 0.24f),
            StagePropRole.DestructibleWall => new Color(0.88f, 0.36f, 1f),
            StagePropRole.SlowZone => new Color(0.31f, 0.76f, 0.97f),
            _ => new Color(0.62f, 0.64f, 0.68f)
        };

        public static string Icon(StagePropRole role) => role switch
        {
            StagePropRole.Floor => "▭",
            StagePropRole.Wall => "■",
            StagePropRole.DestructibleWall => "◩",
            StagePropRole.SlowZone => "≈",
            _ => "✦"
        };

        public static string Explain(StagePropRole role) => role switch
        {
            StagePropRole.Floor => "床・地面。敵はこの上を歩きます。手前に来ても透けず、敵の影も作りません。",
            StagePropRole.Wall => "壁。敵は形に沿って、さらさらと避けて流れます。レーザーは止まります（設定で素通りも可）。",
            StagePropRole.DestructibleWall => "壊せる壁。レーザーで耐久を削ると溶けるように消え、敵が通れるようになります。時間で再生もできます。",
            StagePropRole.SlowZone => "遅くなる地帯（水たまり・泡など）。敵は通れますが遅くなり、なるべく避けます。レーザーは素通り。",
            _ => "飾り。敵もレーザーも素通りします（遠くの小物、空中の物など）。"
        };
    }
}
