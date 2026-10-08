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
            ("透け（手前の物が砲台・コアを隠したとき）", new[]
            {
                ("turretHoleRadius", "砲台のまわりの透ける丸の半径"),
                ("coreHoleRadius", "コアのまわりの透ける丸の半径"),
                ("localTurretHoleScale", "自分の砲台の丸の倍率"),
                ("holeSoftness", "丸のふちのぼかし"),
                ("focusHeight", "透かす目印の高さ"),
                ("wholeFadeSpeed", "「全体を薄く」の速さ")
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
