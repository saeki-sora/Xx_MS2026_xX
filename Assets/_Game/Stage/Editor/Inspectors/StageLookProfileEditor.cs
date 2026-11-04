using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>反応の共通設定（StageLookProfile）を日本語の項目名で並べる。スタジオの「見え方」ページとインスペクターで共通。</summary>
    [CustomEditor(typeof(StageLookProfile))]
    public sealed class StageLookProfileEditor : Editor
    {
        private static readonly (string group, (string path, string label)[] fields)[] Groups =
        {
            ("斜めの光の筋（Play 中のゲームの画面だけ）", new[]
            {
                ("lightRays.enabled", "光の筋を出す"),
                ("lightRays.color", "光の色"),
                ("lightRays.intensity", "明るさ"),
                ("lightRays.angle", "傾き（度）"),
                ("lightRays.density", "筋の多さ"),
                ("lightRays.sharpness", "筋のくっきりさ"),
                ("lightRays.reach", "光が届く長さ"),
                ("lightRays.driftSpeed", "横に流れる速さ"),
                ("lightRays.shimmer", "ゆらめき")
            }),

            ("透け（手前の物が砲台・コアを隠したとき）", new[]
            {
                ("turretHoleRadius", "砲台のまわりの透ける丸の半径"),
                ("coreHoleRadius", "コアのまわりの透ける丸の半径"),
                ("localTurretHoleScale", "自分の砲台の丸の倍率"),
                ("holeSoftness", "丸のふちのぼかし"),
                ("focusHeight", "透かす目印の高さ"),
                ("wholeFadeSpeed", "「全体を薄く」の速さ")
            }),
            ("撃破の破裂（アニメ調。Play 中だけ）", new[]
            {
                ("killBurst.enabled", "破裂を出す"),
                ("killBurst.size", "大きさ"),
                ("killBurst.lifeSeconds", "消えるまでの秒数"),
                ("killBurst.framesPerSecond", "コマ数（1秒あたり。少ないほどカクッと）"),
                ("killBurst.dropCount", "しずくの数"),
                ("killBurst.starBrightness", "白い星の明るさ"),
                ("killBurst.colorBrightness", "しずく・輪の明るさ"),
                ("killBurst.maxNewPerFrame", "1フレームに出す最大数"),
                ("killBurst.maxActive", "同時に出す最大数"),
                ("killBurst.lift", "床からの高さ")
            }),

            ("隠れた敵の影（シルエット）", new[]
            {
                ("showHiddenEnemies", "隠れた敵を影で見せる"),
                ("hiddenEnemyColor", "影の色（アルファ＝濃さ）")
            }),
            ("熱（レーザーが当たった所）", new[]
            {
                ("glowPerSecond", "光る速さ"),
                ("coolPerSecond", "冷める速さ"),
                ("scorchPerSecond", "焦げが溜まる速さ"),
                ("heatRadius", "光る範囲の半径"),
                ("heatRadiusPerThickness", "太さで広がる量"),
                ("heatHeightOffset", "光る点の高さのずらし"),
                ("glowColor", "熱い色"),
                ("hotColor", "一番熱い色"),
                ("scorchColor", "焦げ跡の色")
            }),
            ("揺れ（敵の群れに押されたとき）", new[]
            {
                ("pressureForFullTilt", "一番傾く敵の数"),
                ("maxTiltDegrees", "一番傾く角度"),
                ("squash", "縦に縮む量"),
                ("stiffness", "ばねの硬さ"),
                ("damping", "揺れの止まりやすさ"),
                ("pressureRange", "敵を数える範囲")
            }),
            ("壊せる壁", new[]
            {
                ("hitFlashColor", "被弾で光る色"),
                ("hitFlashSeconds", "被弾で光る長さ"),
                ("darkenAtZeroHealth", "耐久0での暗さ"),
                ("dissolveSeconds", "溶けて消える時間"),
                ("dissolveEdgeColor", "溶けるふちの色")
            }),
            ("画面のふちの靄（敵が出てくる所。Play 中のゲームの画面だけ）", new[]
            {
                ("edgeMist.enabled", "靄を出す"),
                ("edgeMist.start", "靄が始まる位置"),
                ("edgeMist.softness", "濃くなるまでの幅"),
                ("edgeMist.nearSideFade", "コアに近い側の端を弱める"),
                ("edgeMist.cornerRoundness", "靄の形の四角さ（小さいほど角が丸い）"),
                ("edgeMist.darkness", "靄の中の暗さ"),
                ("edgeMist.mistColor", "靄の色"),
                ("edgeMist.shadeTint", "靄の中の物に掛ける色"),
                ("edgeMist.edgeBandColor", "靄の内側のふちの色"),
                ("edgeMist.edgeBandStrength", "内側のふちの色の濃さ"),
                ("edgeMist.wobble", "ふちのゆらぎ"),
                ("edgeMist.wobbleScale", "ゆらぎの細かさ"),
                ("edgeMist.flowSpeed", "ゆらぎの流れる速さ"),
                ("edgeMist.keepLights", "光る物を明るいまま残す量"),
                ("edgeMist.keepThreshold", "光る物とみなす明るさ"),
                ("edgeMist.keepSoftness", "光る物とみなす幅"),
                ("edgeMist.beamGlow", "ビームが靄を照らす明るさ"),
                ("edgeMist.beamGlowRange", "ビームが靄を照らす範囲"),
                ("edgeMist.beamReveal", "光だまりで元の色を見せる量"),
                ("edgeMist.enemyRimColor", "靄の中の敵の縁の光の色"),
                ("edgeMist.enemyRimWidth", "縁の光の太さ")
            })
        };

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StudioShell.StylePath);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            foreach (var (group, fields) in Groups)
            {
                root.Add(StudioUi.Section(group));
                foreach (var (path, label) in fields)
                {
                    var property = serializedObject.FindProperty(path);
                    if (property != null)
                    {
                        root.Add(new PropertyField(property, label) { tooltip = property.tooltip });
                    }
                }
            }

            root.Bind(serializedObject);
            root.TrackSerializedObjectValue(serializedObject, _ => ((StageLookProfile)target).ApplyGlobals());
            return root;
        }
    }
}
