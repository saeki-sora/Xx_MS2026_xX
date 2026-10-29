using UnityEditor;
using UnityEngine;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>MS2026/3DObject/Toon の材質の画面。項目を見出しごとにまとめ、重さに関わる所に説明を付ける。</summary>
    public sealed class ObjectToonShaderGUI : ShaderGUI
    {
        private sealed class Section
        {
            public readonly string title;
            public readonly string help;
            public readonly string[] properties;

            public Section(string title, string help, params string[] properties)
            {
                this.title = title;
                this.help = help;
                this.properties = properties;
            }
        }

        private static readonly Section[] Sections =
        {
            new Section("基本の色", null,
                "_BaseMap", "_BaseColor", "_AlphaClip", "_Cutoff", "_Cull"),
            new Section("影（明暗の境目）", "「明暗の境目の位置」を上げると影の面が広がり、「ぼかし」を0に近づけるほどパキッと分かれます。",
                "_ShadowColor", "_CelMidPoint", "_CelSoftness", "_ReceiveShadowStrength", "_ReceiveShadowOffset"),
            new Section("太陽の光", "ステージの太陽はとても暗い設定なので、「太陽の強さを無視する」を1にすると明るい面がはっきり出ます。",
                "_DirectStrength", "_IgnoreLightIntensity", "_LightColorInfluence",
                "_AdditionalLightsOn", "_AdditionalLightStrength", "_AdditionalLightCelBlend"),
            new Section("まわりの光", "影の面の明るさに効きます。「平らにする」を1にすると向きによる陰影がなくなり、アニメ塗りに近づきます。",
                "_IndirectMinColor", "_IndirectStrength", "_IndirectFlatten", "_LightAddBlend"),
            new Section("ハイライト", null,
                "_SpecularOn", "_SpecularColor", "_SpecularSize", "_SpecularSoftness"),
            new Section("ふちの光", null,
                "_RimOn", "_RimColor", "_RimWidth", "_RimSoftness", "_RimLitSideOnly"),
            new Section("鏡・つや", "鏡や金属の部分用です（真っ白になる所に使う）。映り込みは画面の上下で3色に塗り分けるだけなので軽いです。「まわりの景色を混ぜる」だけテクスチャを1回読みます。この欄は「共通の設定」からは配られず、材質ごとの値です。",
                "_ReflectOn", "_ReflectStrength", "_ReflectSkyColor", "_ReflectHorizonColor", "_ReflectGroundColor",
                "_ReflectSteps", "_ReflectFresnel", "_ReflectEnvBlend",
                "_StreakOn", "_StreakColor", "_StreakAngle", "_StreakWidth", "_StreakGap", "_StreakOffset", "_StreakSoftness", "_StreakScreenSpace"),
            new Section("輪郭線", "輪郭線を出すと、その物の描画が1回増えます（光の計算はしないので軽めです）。角ばった物で線が割れるときは「押し出す向き」を1に寄せてください。",
                "_OutlineOn", "_OutlineWidth", "_OutlineColor", "_OutlineBaseColorMix", "_OutlineDirection", "_OutlineZOffset"),
            new Section("自己発光・仕上げ", null,
                "_EmissionColor", "_BeamLightStrength", "_Saturation", "_Brightness"),
            new Section("ステージ用", "元の材質から写した値です。床は「透け穴をあけない」をオンにします。",
                "_StageNoHoles", "_StencilRef"),
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var profile = ObjectToonMaterials.LoadProfile();
            var material = materialEditor.target as Material;
            DrawRoleNote(profile, material);

            EditorGUI.BeginChangeCheck();
            foreach (var section in Sections)
            {
                if (!Foldout(section.title))
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                if (!string.IsNullOrEmpty(section.help))
                {
                    EditorGUILayout.HelpBox(section.help, MessageType.None);
                }

                foreach (var name in section.properties)
                {
                    var property = FindProperty(name, properties, false);
                    if (property != null)
                    {
                        materialEditor.ShaderProperty(property, property.displayName);
                    }
                }

                if (section.properties[0] == "_ReflectOn" && GUILayout.Button("鏡のおすすめの値にする"))
                {
                    foreach (var target in materialEditor.targets)
                    {
                        Undo.RecordObject(target, "鏡のおすすめの値");
                        ObjectToonMaterials.ApplyMirrorPreset((Material)target);
                    }

                    GUI.changed = true;
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(4);
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var target in materialEditor.targets)
                {
                    ObjectToonMaterials.SyncOutlinePass(target as Material);
                }

                ObjectToonMaterials.PushIfCommon(profile, material);
                ObjectToonMaterials.RepaintViews();
            }
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ObjectToonMaterials.SyncOutlinePass(material);
        }

        private static void DrawRoleNote(ObjectToonProfile profile, Material material)
        {
            if (profile == null || material == null)
            {
                return;
            }

            if (material == profile.commonSettings)
            {
                EditorGUILayout.HelpBox("これは「共通の設定」です。ここで変えた値（色・テクスチャ・ステージ用の値以外）は、「共通に合わせる」がオンの材質すべてにすぐ配られます。", MessageType.Info);
                return;
            }

            if (profile.IsOverrideMaterial(material))
            {
                EditorGUILayout.HelpBox("これは1つの物だけに使う材質です（共通の設定は配られません）。", MessageType.Info);
                return;
            }

            var entry = profile.FindByToon(material);
            if (entry != null && entry.useCommon)
            {
                EditorGUILayout.HelpBox("この材質は「共通の設定」に合わせています。基本の色・鏡・ステージ用の値以外を変えても、共通の設定を変えたときに上書きされます。", MessageType.Warning);
                if (GUILayout.Button("この材質だけ個別に調整する（共通に合わせるのをやめる）"))
                {
                    Undo.RecordObject(profile, "共通に合わせるのをやめる");
                    entry.useCommon = false;
                    EditorUtility.SetDirty(profile);
                }
            }
        }

        private static bool Foldout(string title)
        {
            var key = "MS2026.ObjectToon.GUI." + title;
            bool open = EditorPrefs.GetBool(key, true);
            bool next = EditorGUILayout.BeginFoldoutHeaderGroup(open, title);
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (next != open)
            {
                EditorPrefs.SetBool(key, next);
            }

            return next;
        }
    }
}
