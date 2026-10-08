namespace MS2026.Stage
{
    /// <summary>背景オブジェクトがゲームの中で果たす役割。役割ごとに、敵の通路・レーザーへの影響が決まる。</summary>
    public enum StagePropRole
    {
        /// <summary>飾り。敵もレーザーも素通りする（遠くの小物・空中の物など）。</summary>
        Decoration,

        /// <summary>床・地面。敵はこの上を歩く。手前に来ても透けず、敵の影（シルエット）も作らない。</summary>
        Floor,

        /// <summary>壁。敵は形に沿って避けて流れる。レーザーは止まる（設定で素通りにもできる）。</summary>
        Wall,

        /// <summary>壊せる壁。レーザーで耐久を削ると壊れて通れるようになる（要塞デザイナーの破壊可能物と同じ仕組み）。</summary>
        DestructibleWall,

        /// <summary>遅くなる地帯（水たまり・泡など）。敵は通れるが遅くなり、なるべく避ける。レーザーは素通り。</summary>
        SlowZone
    }

    public static class StagePropRoleExtensions
    {
        /// <summary>この役割が「通れない範囲・遅くなる範囲」の輪郭を必要とするか。</summary>
        public static bool NeedsFootprint(this StagePropRole role) =>
            role == StagePropRole.Wall || role == StagePropRole.DestructibleWall || role == StagePropRole.SlowZone;

        /// <summary>プランナー向けの日本語名。</summary>
        public static string DisplayName(this StagePropRole role) => role switch
        {
            StagePropRole.Decoration => "飾り",
            StagePropRole.Floor => "床",
            StagePropRole.Wall => "壁",
            StagePropRole.DestructibleWall => "壊せる壁",
            StagePropRole.SlowZone => "遅くなる地帯",
            _ => role.ToString()
        };
    }
}
