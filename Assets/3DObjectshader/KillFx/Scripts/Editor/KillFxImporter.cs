using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MS2026.ObjectToon.KillFx.EditorTools
{
    /// <summary>
    /// 連番の PNG（手描き風エフェクトの素材。zip のままでも、展開したフォルダでもよい）から、
    /// コマを1枚に並べた画像・材質・パーティクルの Prefab を作る。
    /// ・画像は git に入れないフォルダ（ThirdPartyLocal）に作る。素材の利用規約で再配布が禁止されていて、リポジトリが公開されているため。
    /// ・画像の GUID は名前から決まるので、どのPCで作っても同じになる（材質・Prefab は git に入れて全員で同じものを使える）。
    ///   画像を作っていないPCでは、材質の絵の初期値（透明）になり、エフェクトは見えないだけでエラーにはならない。
    /// </summary>
    public static class KillFxImporter
    {
        public const string Root = "Assets/3DObjectshader/KillFx";
        public const string LocalFolder = Root + "/ThirdPartyLocal";
        public const string EffectFolder = Root + "/Effects";
        public const string ShaderName = "MS2026/3DObject/AnimeFx";

        public sealed class Sequence
        {
            public string source;     // zip かフォルダのパス
            public string location;   // その中のフォルダ
            public List<string> frames = new List<string>();
            public string name;
            public bool selected;

            public string Label => name + "（" + frames.Count + "コマ）";
        }

        public sealed class BuildOptions
        {
            public int frameSize = 256;
            public float fps = 12f;
            public float size = 2.5f;
            public bool glow = true;
            public bool randomRotation = true;
        }

        // ───────── 探す ─────────

        /// <summary>zip / フォルダの中から、PNG が3枚以上並んだフォルダ（= 1つのエフェクト）を探す。</summary>
        public static List<Sequence> Scan(IEnumerable<string> sources)
        {
            var result = new List<Sequence>();
            foreach (var source in sources)
            {
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }

                if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ScanZip(source, result);
                }
                else if (Directory.Exists(source))
                {
                    ScanFolder(source, result);
                }
            }

            MakeNamesUnique(result);
            return result;
        }

        private static void ScanZip(string zipPath, List<Sequence> result)
        {
            using (var stream = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var groups = new Dictionary<string, List<string>>();
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || entry.Length == 0)
                    {
                        continue;
                    }

                    var folder = Path.GetDirectoryName(entry.FullName.Replace('\\', '/'))?.Replace('\\', '/') ?? "";
                    if (!groups.TryGetValue(folder, out var list))
                    {
                        list = new List<string>();
                        groups.Add(folder, list);
                    }

                    list.Add(entry.FullName);
                }

                foreach (var pair in groups)
                {
                    AddSequence(result, zipPath, pair.Key, pair.Value);
                }
            }
        }

        private static void ScanFolder(string folder, List<Sequence> result)
        {
            foreach (var dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).Prepend(folder))
            {
                var files = Directory.GetFiles(dir, "*.png").ToList();
                var relative = dir.Length > folder.Length ? dir.Substring(folder.Length + 1).Replace('\\', '/') : "";
                AddSequence(result, folder, relative, files);
            }
        }

        private static void AddSequence(List<Sequence> result, string source, string location, List<string> frames)
        {
            if (frames.Count < 3)
            {
                return;
            }

            frames.Sort(string.CompareOrdinal);
            result.Add(new Sequence { source = source, location = location, frames = frames, name = MakeName(location) });
        }

        // 名前は、フォルダの道すじの最後のほうの英数字の部分から作る（日本語の部分は zip で文字化けすることがあるので使わない）。
        private static string MakeName(string location)
        {
            var parts = location.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.All(c => c < 128))
                .Select(p => Sanitize(p))
                .Where(p => p.Length > 0)
                .ToList();
            if (parts.Count == 0)
            {
                return "effect";
            }

            var leaf = parts[parts.Count - 1];
            var picked = new List<string> { leaf };
            // 「02_Glow」「Presets_01」「Anti-aliasing_on」のような版の名前を前に付けて、同じ絵の色違いと区別する。
            for (int i = parts.Count - 2; i >= 0 && picked.Count < 3; i--)
            {
                var part = parts[i];
                if (leaf.StartsWith(part, StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("footage", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Presets", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                picked.Insert(0, part);
                break;
            }

            return string.Join("_", picked);
        }

        private static void MakeNamesUnique(List<Sequence> list)
        {
            var used = new Dictionary<string, int>();
            foreach (var item in list)
            {
                if (used.TryGetValue(item.name, out var count))
                {
                    used[item.name] = count + 1;
                    item.name += "_" + (count + 1);
                }
                else
                {
                    used.Add(item.name, 1);
                }
            }
        }

        /// <summary>倒したときに合いそうなもの（衝撃・閃光・火花・光）を選ぶ。色付きの版（Glow / Color）を優先。</summary>
        public static void SelectRecommended(List<Sequence> list)
        {
            string[] good = { "impact", "flash", "spark", "light", "flare" };
            foreach (var item in list)
            {
                var lower = (item.location + "/" + item.name).ToLowerInvariant();
                bool kind = good.Any(lower.Contains);
                // 色付きの「02_Glow」か、版の分かれていない素の連番（色分け用の _a / _b や、ギザギザの版は除く）。
                bool plain = !lower.Contains("presets") && !lower.EndsWith("_a") && !lower.EndsWith("_b") && !lower.Contains("anti-aliasing_off");
                item.selected = kind && (lower.Contains("02_glow") || plain);
            }
        }

        // ───────── 作る ─────────

        /// <summary>選んだエフェクトの画像・材質・Prefab を作る。作った Prefab の数を返す。</summary>
        public static int Build(List<Sequence> sequences, BuildOptions options, Action<string, float> progress)
        {
            EnsureFolder(LocalFolder);
            EnsureFolder(EffectFolder);
            int built = 0;
            var shader = Shader.Find(ShaderName);
            try
            {
                var selected = sequences.Where(s => s.selected).ToList();
                for (int i = 0; i < selected.Count; i++)
                {
                    var sequence = selected[i];
                    progress?.Invoke(sequence.Label, i / (float)Mathf.Max(1, selected.Count));
                    var texture = BuildSheet(sequence, options, out int columns, out int rows);
                    if (texture == null)
                    {
                        continue;
                    }

                    var material = BuildMaterial(sequence.name, texture, shader, options);
                    BuildPrefab(sequence.name, material, sequence.frames.Count, columns, rows, options);
                    built++;
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return built;
        }

        private static Texture2D BuildSheet(Sequence sequence, BuildOptions options, out int columns, out int rows)
        {
            int count = sequence.frames.Count;
            int frame = options.frameSize;
            columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            rows = Mathf.CeilToInt(count / (float)columns);

            var sheet = new Texture2D(columns * frame, rows * frame, TextureFormat.RGBA32, false);
            sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
            var target = RenderTexture.GetTemporary(frame, frame, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            var zipStream = Directory.Exists(sequence.source) ? null : File.OpenRead(sequence.source);
            var zip = zipStream != null ? new ZipArchive(zipStream, ZipArchiveMode.Read) : null;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    // ミップマップ付きで読み込み、縮めるときに細かい線がちらつかないようにする。
                    if (!source.LoadImage(ReadFrame(sequence, zip, i), false))
                    {
                        Debug.LogWarning("[倒したときの演出] 読めない画像がありました: " + sequence.frames[i]);
                        continue;
                    }

                    source.filterMode = FilterMode.Trilinear;
                    Graphics.Blit(source, target);
                    RenderTexture.active = target;
                    int x = i % columns * frame;
                    int y = (rows - 1 - i / columns) * frame; // コマは左上から右へ、上から下へ並べる（パーティクルの並び順）
                    sheet.ReadPixels(new Rect(0, 0, frame, frame), x, y, false);
                }

                sheet.Apply(false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(source);
                zip?.Dispose();
                zipStream?.Dispose();
            }

            var path = LocalFolder + "/" + sequence.name + ".png";
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);

            // GUID を名前から決める（どのPCで作っても同じ GUID になり、git に入れた材質から見つかる）。
            var metaPath = path + ".meta";
            if (!File.Exists(metaPath))
            {
                File.WriteAllText(metaPath, "fileFormatVersion: 2\nguid: " + StableGuid(sequence.name) + "\n");
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static byte[] ReadFrame(Sequence sequence, ZipArchive zip, int index)
        {
            var name = sequence.frames[index];
            if (zip == null)
            {
                return File.ReadAllBytes(name);
            }

            using (var entry = zip.GetEntry(name).Open())
            using (var memory = new MemoryStream())
            {
                entry.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static Material BuildMaterial(string name, Texture2D texture, Shader shader, BuildOptions options)
        {
            EnsureFolder(EffectFolder + "/" + name);
            var path = EffectFolder + "/" + name + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(options.glow ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPrefab(string name, Material material, int frames, int columns, int rows, BuildOptions options)
        {
            var path = EffectFolder + "/" + name + "/" + name + ".prefab";
            var go = new GameObject(name);
            try
            {
                var system = go.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                float life = frames / Mathf.Max(1f, options.fps);

                var main = system.main;
                main.duration = life;
                main.loop = false;
                main.startLifetime = life;
                main.startSpeed = 0f;
                main.startSize = options.size;
                main.startRotation = options.randomRotation ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.playOnAwake = true;
                main.maxParticles = 4;

                var emission = system.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

                var shape = system.shape;
                shape.enabled = false;

                var sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = columns;
                sheet.numTilesY = rows;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.cycleCount = 1;
                // 並べたコマの数ちょうどで止める（余りのマスは見せない）。
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, (frames - 0.01f) / (columns * rows)));

                var color = system.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                color.color = gradient;

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 100; // 2Dの絵（順番0）の後ろに隠れないように（ほかのエフェクトと同じ）
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ───────── 一覧 ─────────

        /// <summary>作ったエフェクトの Prefab と、このPCに画像があるか。</summary>
        public static List<(GameObject prefab, bool hasTexture)> ListBuilt()
        {
            var result = new List<(GameObject, bool)>();
            if (!AssetDatabase.IsValidFolder(EffectFolder))
            {
                return result;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EffectFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var renderer = prefab != null ? prefab.GetComponent<ParticleSystemRenderer>() : null;
                bool hasTexture = renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.GetTexture("_MainTex") != null;
                result.Add((prefab, hasTexture));
            }

            return result;
        }

        // ───────── 小さな道具 ─────────

        public static string StableGuid(string name)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("MS2026.ObjectToon.KillFx/" + name));
                var builder = new StringBuilder(32);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static string Sanitize(string name)
        {
            var builder = new StringBuilder();
            foreach (var c in name)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return builder.ToString().Trim('_');
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
