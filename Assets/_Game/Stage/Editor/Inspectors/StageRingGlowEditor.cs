using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 「リングの輪郭を光らせる」（StageRingGlow）を日本語の項目名で並べる。
    /// stage_frame の色を1か所で変えられるよう、リングの線の色（マテリアル）と、まわりの光の色を上下に並べる。
    /// </summary>
    [CustomEditor(typeof(StageRingGlow))]
    public sealed class StageRingGlowEditor : Editor
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const string EmissionKeyword = "_EMISSION";

        public override VisualElement CreateInspectorGUI()
        {
            var root = StageInspectorUi.Root();

            root.Add(StudioUi.Section("光らせている物そのものの色（stage_frame・floor1 など）"));
            root.Add(BuildRingLine());

            root.Add(StudioUi.Section("まわりの光（外へ広がる光）"));
            StageInspectorUi.AddFields(root, serializedObject,
                ("color", "光の色"),
                ("intensity", "光の強さ（0で消える）"),
                ("outerWidth", "外へ広がる幅"),
                ("softness", "消え方（小さいほどぼんやり遠くまで）"));

            var more = StageInspectorUi.Folded("くわしく（内側の光・脈打つ動き・対象）", "RingGlow.More");
            StageInspectorUi.AddFields(more, serializedObject,
                ("innerWidth", "内へ広がる幅（0で外側だけ光る）"),
                ("innerStrength", "内側の強さ（外側を1として）"),
                ("pulse", "脈打つ量（0で動かない）"),
                ("pulsePeriod", "脈打つ間隔（秒）"),
                ("edgeInset", "光の始まりを内へずらす（ふちとすき間が見えるとき）"),
                ("target", "光らせるリング（空なら自動で探す）"));
            root.Add(more);

            root.Bind(serializedObject);
            return root;
        }

        /// <summary>リングのマテリアルの「基本の色」「自己発光の色」を直接いじる欄。</summary>
        private VisualElement BuildRingLine()
        {
            var box = new VisualElement();
            var material = FindRingMaterial();
            if (material == null || !material.HasProperty(BaseColorId) || !material.HasProperty(EmissionColorId))
            {
                box.Add(StudioUi.Note("マテリアルが見つからないので、ここでは色を変えられません。", NoteKind.Warn));
                return box;
            }

            var baseField = new ColorField("基本の色") { showAlpha = false, value = material.GetColor(BaseColorId), tooltip = "光らせている物（リングの板・円盤）そのものの色。" };
            baseField.AddToClassList(BaseField<Color>.alignedFieldUssClassName);
            baseField.RegisterValueChangedCallback(e => SetColor(material, BaseColorId, e.newValue));

            var emissionField = new ColorField("光る色（黒で光らない）") { hdr = true, showAlpha = false, value = material.GetColor(EmissionColorId), tooltip = "ライトに関係なく光る色。明るくするほど光って見える。" };
            emissionField.AddToClassList(BaseField<Color>.alignedFieldUssClassName);
            emissionField.RegisterValueChangedCallback(e => SetColor(material, EmissionColorId, e.newValue));

            box.Add(baseField);
            box.Add(emissionField);
            box.Add(StudioUi.Note($"マテリアル「{material.name}」を直接変えます。同じマテリアルを使うほかの物も同じ色になります。"));

            // 元に戻す（Ctrl+Z）やマテリアル側で変えたときも、欄の色を合わせる。
            box.schedule.Execute(() =>
            {
                if (material == null)
                {
                    return;
                }

                baseField.SetValueWithoutNotify(material.GetColor(BaseColorId));
                emissionField.SetValueWithoutNotify(material.GetColor(EmissionColorId));
            }).Every(300);

            return box;
        }

        private Material FindRingMaterial()
        {
            var glow = (StageRingGlow)target;
            var filter = glow != null ? glow.ResolveRingMesh() : null;
            return filter != null && filter.TryGetComponent<MeshRenderer>(out var renderer) ? renderer.sharedMaterial : null;
        }

        private static void SetColor(Material material, int id, Color value)
        {
            Undo.RecordObject(material, "光らせている物の色");
            material.SetColor(id, value);

            if (id == EmissionColorId)
            {
                // 黒なら光らせる処理ごと切る（少し軽くなる）。
                var glows = value.maxColorComponent > 0f;
                if (glows)
                {
                    material.EnableKeyword(EmissionKeyword);
                }
                else
                {
                    material.DisableKeyword(EmissionKeyword);
                }

                material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
        }
    }
}
