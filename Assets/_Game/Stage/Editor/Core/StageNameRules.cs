using System.Text.RegularExpressions;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// モデルの中の物の名前から、役割と目印を推測する決まり（取り込み時の初期値。表で直せる）。
    /// ・MARK_Core / MARK_Turret1〜4（MARK_P1〜P4 も可）/ MARK_Spawn... … 目印（背景にはならない）
    /// ・floor, ground, plate, dish, base, 床, 地面, 皿, 台 … 床
    /// ・water, puddle, foam, bubble, soap, 水, 泡 … 遅くなる地帯
    /// ・deco, bg, far, sky, 飾り, 背景 … 飾り
    /// ・break, glass, crack, 壊, 割 … 壊せる壁
    /// ・それ以外 … 壁
    /// </summary>
    public static class StageNameRules
    {
        public const string MarkerPrefix = "MARK_";

        private static readonly Regex Floor = new Regex(@"floor|ground|plate|dish|base|床|地面|皿|台", RegexOptions.IgnoreCase);
        private static readonly Regex Slow = new Regex(@"water|puddle|foam|bubble|soap|水|泡", RegexOptions.IgnoreCase);
        private static readonly Regex Deco = new Regex(@"deco|^bg|_bg|far|sky|飾り|背景", RegexOptions.IgnoreCase);
        private static readonly Regex Breakable = new Regex(@"break|glass|crack|壊|割", RegexOptions.IgnoreCase);
        private static readonly Regex TurretMarker = new Regex(@"^(?:turret|p)(\d)$", RegexOptions.IgnoreCase);

        public static StagePropRole GuessRole(string name)
        {
            if (string.IsNullOrEmpty(name)) return StagePropRole.Wall;
            if (Floor.IsMatch(name)) return StagePropRole.Floor;
            if (Slow.IsMatch(name)) return StagePropRole.SlowZone;
            if (Breakable.IsMatch(name)) return StagePropRole.DestructibleWall;
            if (Deco.IsMatch(name)) return StagePropRole.Decoration;
            return StagePropRole.Wall;
        }

        /// <summary>目印なら種類と番号（砲台は0〜3）を返す。</summary>
        public static StageMarkerKind ParseMarker(string name, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(name) || !name.StartsWith(MarkerPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return StageMarkerKind.None;
            }

            var body = name.Substring(MarkerPrefix.Length);
            if (body.Equals("Core", System.StringComparison.OrdinalIgnoreCase))
            {
                return StageMarkerKind.Core;
            }

            var turret = TurretMarker.Match(body);
            if (turret.Success)
            {
                index = int.Parse(turret.Groups[1].Value) - 1;
                return index >= 0 && index < 4 ? StageMarkerKind.Turret : StageMarkerKind.None;
            }

            return body.StartsWith("Spawn", System.StringComparison.OrdinalIgnoreCase) ? StageMarkerKind.Spawn : StageMarkerKind.None;
        }
    }
}
