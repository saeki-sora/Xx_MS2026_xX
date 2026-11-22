using System.Collections.Generic;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>調整ウィンドウの見出し（やりたいことの名前）。下の2つは物ごとに調整しているときだけ出す。</summary>
    public enum ToonGroup
    {
        Shadow,
        Line,
        ColorLight,
        Shine,
        ThisObjectColor,
        Mirror,
    }

    /// <summary>項目の部品の種類。Auto はシェーダーの型（数値の範囲・色）から決める。</summary>
    public enum ToonKind
    {
        Auto,
        Toggle,
        TwoSided,
    }

    /// <summary>調整ウィンドウの1項目（シェーダーの値1つ）。</summary>
    public sealed class ToonItem
    {
        public readonly string Id;
        public readonly string Label;
        public readonly ToonGroup Group;
        public readonly bool Top;
        public readonly string Parent;
        public readonly string Tip;
        public readonly ToonKind Kind;
        public readonly string Keyword;

        public ToonItem(string id, string label, ToonGroup group, string tip, bool top = false, string parent = null,
            ToonKind kind = ToonKind.Auto, string keyword = null)
        {
            Id = id;
            Label = label;
            Group = group;
            Tip = tip;
            Top = top;
            Parent = parent;
            Kind = kind;
            Keyword = keyword;
        }

        /// <summary>物ごとに持つ値か（共通の設定からは配らない・変えた所の印を付けない）。</summary>
        public bool PerMaterial => ToonCatalog.IsPerMaterialGroup(Group);
    }

    /// <summary>
    /// 調整ウィンドウに出す項目の表。並びは「やりたいこと」の順（資料 T1）。
    /// よく触る8つ（Top）は折りたたまずにいつも出す（T2）。名前は細い窓に収まるよう短く、説明は下の帯に出す（T6）。
    /// </summary>
    public static class ToonCatalog
    {
        private const string Outline = "_OutlineOn";
        private const string Specular = "_SpecularOn";
        private const string Rim = "_RimOn";
        private const string Additional = "_AdditionalLightsOn";
        private const string Reflect = "_ReflectOn";
        private const string Streak = "_StreakOn";

        public static readonly IReadOnlyList<ToonItem> Items = new[]
        {
            // ── よく触る8つ（この順で上に並ぶ）
            new ToonItem("_ShadowColor", "影の色", ToonGroup.Shadow, "影になった面の色。物の色にかけ合わせます。青紫に寄せると涼しく、赤茶に寄せるとあたたかく見えます。", top: true),
            new ToonItem("_CelMidPoint", "影の広さ", ToonGroup.Shadow, "上げると影の面が広がり、下げると明るい面が広がります。", top: true),
            new ToonItem("_CelSoftness", "境目のぼかし", ToonGroup.Shadow, "明るい面と影の境目のぼかし。0に近いほどパキッと分かれて、アニメ塗りらしくなります。", top: true),
            new ToonItem("_IndirectStrength", "影の面の明るさ", ToonGroup.ColorLight, "影になった面の明るさ。下げると影が濃く見えます。", top: true),
            new ToonItem("_OutlineWidth", "線の太さ", ToonGroup.Line, "輪郭線の太さ。0にすると線が消えます。", top: true, parent: Outline),
            new ToonItem("_OutlineColor", "線の色", ToonGroup.Line, "輪郭線の色。「物の色を混ぜる」を上げると、物の色に寄ります。", top: true, parent: Outline),
            new ToonItem("_Saturation", "彩度", ToonGroup.ColorLight, "色の鮮やかさ。1が元のまま、0で白黒、2でとても鮮やか。", top: true),
            new ToonItem("_Brightness", "全体の明るさ", ToonGroup.ColorLight, "全部の色の明るさ。1が元のまま。", top: true),

            // ── 影
            new ToonItem("_ReceiveShadowStrength", "落ち影の濃さ", ToonGroup.Shadow, "ほかの物が落とす影の濃さ。"),
            new ToonItem("_ReceiveShadowOffset", "落ち影のずらし", ToonGroup.Shadow, "影にしま模様が出るときだけ、少し上げてください。"),

            // ── 輪郭線
            new ToonItem(Outline, "線を出す", ToonGroup.Line, "輪郭線を出すか。出すと物ごとに描画が1回増えます（軽め）。", kind: ToonKind.Toggle),
            new ToonItem("_OutlineBaseColorMix", "物の色を混ぜる", ToonGroup.Line, "線に物の色を混ぜる量。上げると線がなじみ、下げると線の色がそのまま出ます。", parent: Outline),
            new ToonItem("_OutlineDirection", "押し出す向き", ToonGroup.Line, "0=面の向き / 1=中心から外。角ばった物で線が割れるときは1に寄せます。", parent: Outline),
            new ToonItem("_OutlineZOffset", "線を奥へずらす", ToonGroup.Line, "線が物の手前にはみ出して見えるときに上げます。", parent: Outline),

            // ── 色と光
            new ToonItem("_DirectStrength", "明るい面の明るさ", ToonGroup.ColorLight, "太陽が当たった面の明るさ。"),
            new ToonItem("_IgnoreLightIntensity", "太陽の強さを無視", ToonGroup.ColorLight, "ステージの太陽はとても暗い設定なので、1にすると明るい面がはっきり出ます。"),
            new ToonItem("_LightColorInfluence", "太陽の色を乗せる", ToonGroup.ColorLight, "太陽の色を物に乗せる量。"),
            new ToonItem("_IndirectFlatten", "影の面を平らに", ToonGroup.ColorLight, "1にすると向きによる陰影がなくなり、アニメ塗りに近づきます。"),
            new ToonItem("_IndirectMinColor", "いちばん暗い色", ToonGroup.ColorLight, "影の面がこれより暗くならない色。"),
            new ToonItem("_LightAddBlend", "光の合わせ方", ToonGroup.ColorLight, "0=明るい方を使う / 1=足す。"),
            new ToonItem("_BeamLightStrength", "ビームに照らされる", ToonGroup.ColorLight, "撃っているビームに、線に沿って照らされる強さ。"),
            new ToonItem(Additional, "点の光を受ける", ToonGroup.ColorLight, "点光源などの追加の光を受けるか。", kind: ToonKind.Toggle),
            new ToonItem("_AdditionalLightStrength", "点の光の強さ", ToonGroup.ColorLight, "追加の光の強さ。", parent: Additional),
            new ToonItem("_AdditionalLightCelBlend", "点の光の段をなくす", ToonGroup.ColorLight, "1に近いほど、追加の光で明暗の段ができません。", parent: Additional),

            // ── ハイライト・ふちの光
            new ToonItem(Specular, "ハイライトを出す", ToonGroup.Shine, "つやの白い点を出すか。", kind: ToonKind.Toggle),
            new ToonItem("_SpecularColor", "ハイライトの色", ToonGroup.Shine, "ハイライトの色（明るさも込み）。", parent: Specular),
            new ToonItem("_SpecularSize", "ハイライトの大きさ", ToonGroup.Shine, "ハイライトの大きさ。", parent: Specular),
            new ToonItem("_SpecularSoftness", "ハイライトのぼかし", ToonGroup.Shine, "ハイライトのふちのぼかし。", parent: Specular),
            new ToonItem(Rim, "ふちの光を出す", ToonGroup.Shine, "物のふちを光らせて、背景から浮かせます。", kind: ToonKind.Toggle),
            new ToonItem("_RimColor", "ふちの光の色", ToonGroup.Shine, "ふちの光の色（明るさも込み）。", parent: Rim),
            new ToonItem("_RimWidth", "ふちの光の幅", ToonGroup.Shine, "ふちの光の幅。", parent: Rim),
            new ToonItem("_RimSoftness", "ふちの光のぼかし", ToonGroup.Shine, "ふちの光の内側のぼかし。", parent: Rim),
            new ToonItem("_RimLitSideOnly", "明るい側だけに", ToonGroup.Shine, "1に近いほど、太陽が当たっている側だけにふちの光が出ます。", parent: Rim),

            // ── この物の色（物ごとに調整しているときだけ）
            new ToonItem("_BaseColor", "基本の色", ToonGroup.ThisObjectColor, "この物の色（テクスチャにかけ合わせます）。最初は元の材質から写した色です。"),
            new ToonItem("_EmissionColor", "自己発光の色", ToonGroup.ThisObjectColor, "この物が自分で光る色。黒なら光りません。"),
            new ToonItem("_Cull", "両面を描く", ToonGroup.ThisObjectColor, "オンにすると裏側の面も描きます（薄い板などで穴が開いて見えるとき）。", kind: ToonKind.TwoSided),
            new ToonItem("_StageNoHoles", "透け穴をあけない", ToonGroup.ThisObjectColor, "床に使います。オンにすると、砲台やコアを隠しても穴があきません。", kind: ToonKind.Toggle, keyword: "_STAGE_NO_HOLES"),

            // ── 鏡・映り込み（物ごとに調整しているときだけ）
            new ToonItem(Reflect, "映り込みを出す", ToonGroup.Mirror, "鏡・金属用。真っ白に見える物に使います。", kind: ToonKind.Toggle),
            new ToonItem("_ReflectStrength", "映り込みの強さ", ToonGroup.Mirror, "映り込みの強さ。", parent: Reflect),
            new ToonItem("_ReflectSkyColor", "上の色", ToonGroup.Mirror, "映り込みの上の色。", parent: Reflect),
            new ToonItem("_ReflectHorizonColor", "真ん中の色", ToonGroup.Mirror, "映り込みの真ん中の色。", parent: Reflect),
            new ToonItem("_ReflectGroundColor", "下の色", ToonGroup.Mirror, "映り込みの下の色。", parent: Reflect),
            new ToonItem("_ReflectSteps", "色の段の数", ToonGroup.Mirror, "映り込みの色の段の数。0でなめらか。", parent: Reflect),
            new ToonItem("_ReflectFresnel", "ふちほど強く", ToonGroup.Mirror, "上げると、物のふちほど強く映ります。", parent: Reflect),
            new ToonItem("_ReflectEnvBlend", "景色を混ぜる", ToonGroup.Mirror, "まわりの景色を混ぜる量（テクスチャを1回読むので少し重くなります）。", parent: Reflect),
            new ToonItem(Streak, "光の筋を出す", ToonGroup.Mirror, "鏡に斜めの光の筋を出すか。", kind: ToonKind.Toggle),
            new ToonItem("_StreakColor", "筋の色", ToonGroup.Mirror, "光の筋の色（明るさも込み）。", parent: Streak),
            new ToonItem("_StreakAngle", "筋の角度", ToonGroup.Mirror, "光の筋の角度。", parent: Streak),
            new ToonItem("_StreakWidth", "筋の太さ", ToonGroup.Mirror, "光の筋の太さ。", parent: Streak),
            new ToonItem("_StreakGap", "2本目までの間", ToonGroup.Mirror, "2本目の筋までの間。", parent: Streak),
            new ToonItem("_StreakOffset", "筋の位置", ToonGroup.Mirror, "光の筋の位置。", parent: Streak),
            new ToonItem("_StreakSoftness", "筋のぼかし", ToonGroup.Mirror, "光の筋のぼかし。", parent: Streak),
            new ToonItem("_StreakScreenSpace", "筋を画面に固定", ToonGroup.Mirror, "オンで画面に固定、オフで物に貼り付きます。", parent: Streak, kind: ToonKind.Toggle),
        };

        public static readonly ToonGroup[] FoldGroups =
        {
            ToonGroup.Shadow, ToonGroup.Line, ToonGroup.ColorLight, ToonGroup.Shine, ToonGroup.ThisObjectColor, ToonGroup.Mirror,
        };

        public static string GroupLabel(ToonGroup group)
        {
            switch (group)
            {
                case ToonGroup.Shadow: return "影";
                case ToonGroup.Line: return "輪郭線";
                case ToonGroup.ColorLight: return "色と光";
                case ToonGroup.Shine: return "ハイライト・ふちの光";
                case ToonGroup.ThisObjectColor: return "この物の色";
                default: return "鏡・映り込み";
            }
        }

        public static bool IsPerMaterialGroup(ToonGroup group)
        {
            return group == ToonGroup.ThisObjectColor || group == ToonGroup.Mirror;
        }

        /// <summary>検索の言葉が、名前か見出しに含まれるか（空なら全部合う）。</summary>
        public static bool Matches(ToonItem item, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            query = query.Trim();
            return item.Label.Contains(query) || GroupLabel(item.Group).Contains(query);
        }
    }
}
