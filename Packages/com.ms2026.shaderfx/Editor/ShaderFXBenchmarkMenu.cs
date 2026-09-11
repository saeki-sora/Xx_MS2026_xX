using UnityEditor;
using UnityEngine;

namespace MS2026.ShaderFX.Editor
{
    public static class ShaderFXBenchmarkMenu
    {
        private const int GridSize = 32;
        private const float Spacing = 1.5f;

        [MenuItem("Tools/ShaderFX/Spawn SRP Batcher Benchmark (1024 Cubes)")]
        private static void SpawnBenchmark()
        {
            var profile = Selection.activeObject as EffectProfile;
            if (profile == null)
            {
                Debug.LogWarning("[ShaderFX Benchmark] Project ウィンドウで EffectProfile アセットを選択してから実行してください。");
                return;
            }

            var shader = Shader.Find("ShaderFX/Uber");
            if (shader == null)
            {
                Debug.LogError("[ShaderFX Benchmark] ShaderFX/Uber シェーダーが見つかりません。");
                return;
            }

            var root = new GameObject("ShaderFX_Benchmark");
            Undo.RegisterCreatedObjectUndo(root, "Spawn ShaderFX Benchmark");

            var baseMaterial = new Material(shader) { name = "ShaderFX_BenchmarkBase" };

            for (int x = 0; x < GridSize; x++)
            {
                for (int z = 0; z < GridSize; z++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(root.transform);
                    cube.transform.localPosition = new Vector3(x * Spacing, 0f, z * Spacing);
                    cube.GetComponent<Renderer>().sharedMaterial = baseMaterial;

                    var effectTarget = cube.AddComponent<EffectTarget>();
                    var so = new SerializedObject(effectTarget);
                    so.FindProperty("profile").objectReferenceValue = profile;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Selection.activeObject = root;
            Debug.Log($"[ShaderFX Benchmark] {GridSize * GridSize} 個のオブジェクトを生成しました。" +
                " Play モードに入り、Window > Analysis > Frame Debugger で SRP Batcher のバッチ数を確認してください" +
                "(全オブジェクトが同一 Profile なら、ほぼ1バッチにまとまります)。");
        }
    }
}
