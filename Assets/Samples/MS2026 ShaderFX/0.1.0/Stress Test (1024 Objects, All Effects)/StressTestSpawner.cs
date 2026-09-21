using UnityEditor;
using UnityEngine;
using MS2026.ShaderFX;
using MS2026.ShaderFX.Modules;

namespace MS2026.ShaderFX.Samples.StressTest
{
    // フェーズ5の負荷テストサンプル。1024体のオブジェクトへ、4種のオブジェクト系エフェクトを
    // 均等に振り分けて配置し(=マテリアルキャッシュのキーが複数パターン同時に存在する状況を再現)、
    // さらに画面系エフェクトも1つ有効化した状態で、SRP Batcherと全体のフレームタイムを検証できます。
    public static class StressTestSpawner
    {
        private const int GridSize = 32; // 32 x 32 = 1024 objects
        private const float Spacing = 1.5f;

        [MenuItem("Tools/シェーダーFX/サンプル/全エフェクト負荷テストを生成（1024個）")]
        private static void Spawn()
        {
            var shader = Shader.Find("ShaderFX/Uber");
            if (shader == null)
            {
                Debug.LogError("[ShaderFX StressTest] ShaderFX/Uber シェーダーが見つかりません。");
                return;
            }

            var root = new GameObject("ShaderFX_StressTest");
            Undo.RegisterCreatedObjectUndo(root, "Spawn ShaderFX Stress Test");

            var profiles = BuildObjectProfiles();
            var baseMaterial = new Material(shader) { name = "ShaderFX_StressTest_Base" };

            for (int x = 0; x < GridSize; x++)
            {
                for (int z = 0; z < GridSize; z++)
                {
                    int index = x * GridSize + z;

                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(root.transform);
                    cube.transform.localPosition = new Vector3(x * Spacing, 0f, z * Spacing);
                    cube.GetComponent<Renderer>().sharedMaterial = baseMaterial;

                    var target = cube.AddComponent<EffectTarget>();
                    target.Profile = profiles[index % profiles.Length];
                }
            }

            // Also exercise one 画面系 (screen-space) effect for the duration of this test.
            // No Renderer needed here — EffectDirector aggregates screen-space modules from every
            // registered EffectTarget regardless of whether that target has anything to draw.
            var screenCarrier = new GameObject("ShaderFX_StressTest_ScreenCarrier");
            screenCarrier.transform.SetParent(root.transform);
            var screenProfile = ScriptableObject.CreateInstance<EffectProfile>();
            screenProfile.name = "StressTest_ScreenProfile";
            screenProfile.modules.Add(new PosterizeModule { levels = 6 });
            var screenTarget = screenCarrier.AddComponent<EffectTarget>();
            screenTarget.Profile = screenProfile;

            Selection.activeObject = root;
            Debug.Log($"[ShaderFX StressTest] {GridSize * GridSize} 個のオブジェクトを生成しました" +
                "(RimLight/Dissolve/HitFlash/EmissionControlを均等に配分 + Posterizeを画面全体に適用)。" +
                " Play モードに入って Window > Analysis > Frame Debugger や Profiler で計測してください。" +
                " 終わったら ShaderFX_StressTest を Hierarchy から削除してください。");
        }

        private static EffectProfile[] BuildObjectProfiles()
        {
            var rim = ScriptableObject.CreateInstance<EffectProfile>();
            rim.name = "StressTest_RimLight";
            rim.modules.Add(new RimLightModule());

            var dissolve = ScriptableObject.CreateInstance<EffectProfile>();
            dissolve.name = "StressTest_Dissolve";
            dissolve.modules.Add(new DissolveModule { amount = 0.3f });

            var hitFlash = ScriptableObject.CreateInstance<EffectProfile>();
            hitFlash.name = "StressTest_HitFlash";
            hitFlash.modules.Add(new HitFlashModule { amount = 0.4f });

            var emission = ScriptableObject.CreateInstance<EffectProfile>();
            emission.name = "StressTest_Emission";
            emission.modules.Add(new EmissionControlModule { intensity = 2f });

            return new[] { rim, dissolve, hitFlash, emission };
        }
    }
}
