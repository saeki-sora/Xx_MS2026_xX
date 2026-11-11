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
            (FoamPopGroup, new[]
            {
                ("foamPop.enabled", "光る泡を出す"),
                ("foamPop.wrapSeconds", "① 包む泡のかたまりの秒数（0=出さない）"),
                ("foamPop.wrapSize", "① 包む泡のかたまりの大きさ（敵の何倍）"),
                ("foamPop.bubbleCount", "② 泡の数（まわりが空いているとき）"),
                ("foamPop.crowdedBubbleCount", "② 泡の数（たくさん倒れているとき）"),
                ("foamPop.crowdStart", "② 混んでいるとみなし始める数"),
                ("foamPop.crowdFull", "② とても混んでいるとみなす数"),
                ("foamPop.bubbleSize", "② 一番大きい泡の半径（m）"),
                ("foamPop.bubbleSpread", "② 泡が散らばる広さ（m）"),
                ("foamPop.bubbleSeconds", "② 泡が見えている秒数（1つめが縮み始めるまで）"),
                ("foamPop.bubbleStagger", "② 2つめ以降が遅れて縮む秒数"),
                ("foamPop.shrinkSeconds", "② 泡が縮んで光の点になる秒数"),
                ("foamPop.riseSpeed", "② 泡が浮く速さ"),
                ("foamPop.dotSeconds", "③ 光の点が消えるまでの秒数（0=出さない）"),
                ("foamPop.dotRiseSpeed", "③ 光の点が昇る速さ"),
                ("foamPop.mintColor", "色: ふちの内側の帯のミント色"),
                ("foamPop.playerColorMix", "色: ふちの外側の倒した人の色の帯の割合"),
                ("foamPop.glowWhiten", "色: 光を白に寄せる量"),
                ("foamPop.glowStrength", "色: 泡のまわりの光の強さ"),
                ("foamPop.bubbleFill", "色: 泡の中の白い塗りの濃さ"),
                ("foamPop.sound", "音: シュワッ（少し倒したとき）"),
                ("foamPop.soundMany", "音: シュワッ（たくさん倒したとき。空なら上と同じ）"),
                ("foamPop.manyKillsCount", "音: 「たくさん」とみなす数"),
                ("foamPop.soundGroupSeconds", "音: この秒数の間の撃破は1回にまとめる"),
                ("foamPop.maxNewPerFrame", "負荷: 1フレームに出す最大数"),
                ("foamPop.maxParts", "負荷: 同時に出す部品の最大数"),
                ("foamPop.lift", "床からの高さ")
            }),
            ("撃破の破裂（アニメ調。Play 中だけ。2026-11-09 から光る泡に替えて OFF）", new[]
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
            ("コアが壊れた瞬間のスローモーション（Play 中だけ）", new[]
            {
                ("coreBreakSlowMotion.enabled", "スローモーションにする"),
                ("coreBreakSlowMotion.slowScale", "ゆっくりの速さ（1=ふつう）"),
                ("coreBreakSlowMotion.holdSeconds", "ゆっくりのまま見せる秒数"),
                ("coreBreakSlowMotion.recoverSeconds", "ふつうの速さへ戻る秒数")
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
            ("敵の質感（縁・ぷるぷる・当たったときの潰れ）", new[]
            {
                ("enemyLook.enabled", "敵の質感を出す"),
                ("enemyLook.edgeDarken", "縁を濃くする量"),
                ("enemyLook.edgeColor", "縁の色"),
                ("enemyLook.edgeWidth", "縁の太さ"),
                ("enemyLook.jiggle", "ぷるぷる揺れる量"),
                ("enemyLook.jiggleFramesPerSecond", "揺れのコマ数（1秒あたり）"),
                ("enemyLook.hitSquash", "当たった瞬間に潰れる量")
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

        private const string FoamPopGroup = "倒したときの光る泡（Play 中だけ。下のボタンで試せる）";

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

                if (group == FoamPopGroup)
                {
                    root.Add(FoamPopPreviewButtons());
                }
            }

            root.Bind(serializedObject);
            root.TrackSerializedObjectValue(serializedObject, _ => ((StageLookProfile)target).ApplyGlobals());
            return root;
        }

        // Play 中に、コアの近くで敵を倒したことにして光る泡を出すボタン（1体・8体・30体）。
        private static VisualElement FoamPopPreviewButtons()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 4;
            row.style.marginBottom = 8;
            foreach (var count in new[] { 1, 8, 30 })
            {
                var n = count;
                var button = new Button(() => PreviewFoamPop(n)) { text = $"▶ {n}体倒した所を試す" };
                button.tooltip = "Play 中だけ。コアの右側で、1P が倒したことにして光る泡と効果音を出す（数字を変えたらすぐ押して見比べられる）。";
                row.Add(button);
            }

            return row;
        }

        private static void PreviewFoamPop(int count)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                UnityEngine.Debug.Log("[光る泡] 試すボタンは Play 中だけ使えます。");
                return;
            }

            var core = UnityEngine.Object.FindFirstObjectByType<MS2026.Fortress.CoreCrystalController>();
            var centre = core != null ? (UnityEngine.Vector2)core.transform.position : UnityEngine.Vector2.zero;
            MS2026.Fortress.FoamPopLook.Preview(centre + new UnityEngine.Vector2(3f, 0f), 0, count);
        }
    }
}
