using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// SD の絵の読み込み・書き出し（<see cref="SdArtWindow"/> が使う）。
    /// 元の絵: Assets/RedSkill/Art/Source~/*.png（Unity に取り込まれないフォルダ。絵描きさんの絵を余白だけ切った物）。
    /// 書き出す絵: Assets/RedSkill/Art/同じ名前.png（上書き。.meta / GUID はそのままなので、砲台のキャラの絵の一覧はそのまま使える）。
    /// </summary>
    internal static class SdArtExporter
    {
        public const string ArtFolder = "Assets/RedSkill/Art";
        public const string SourceFolder = ArtFolder + "/Source~";
        public const string SettingsPath = "Assets/RedSkill/Data/SdArtSettings.asset";
        public const string TablePath = "Assets/RedSkill/Data/TurretPortraits.asset";

        /// <summary>元の絵のファイル名（名前順）。</summary>
        public static string[] SourceNames()
        {
            if (!Directory.Exists(SourceFolder))
            {
                return new string[0];
            }

            return Directory.GetFiles(SourceFolder, "*.png").Select(Path.GetFileName).OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
        }

        public static SdArtSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SdArtSettings>(SettingsPath);
            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<SdArtSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static SdImage LoadSource(string name)
        {
            var path = Path.Combine(SourceFolder, name);
            if (!File.Exists(path))
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    return null;
                }

                var pixels = texture.GetPixels32();
                var image = new SdImage(texture.width, texture.height);
                for (var i = 0; i < pixels.Length; i++)
                {
                    image.Px[i * 4] = pixels[i].r / 255f;
                    image.Px[i * 4 + 1] = pixels[i].g / 255f;
                    image.Px[i * 4 + 2] = pixels[i].b / 255f;
                    image.Px[i * 4 + 3] = pixels[i].a / 255f;
                }

                return image;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>絵を Texture2D にする（プレビュー用。使い終わったら DestroyImmediate する）。</summary>
        public static Texture2D ToTexture(SdImage image, FilterMode filter)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[image.Width * image.Height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(ToByte(image.Px[i * 4]), ToByte(image.Px[i * 4 + 1]), ToByte(image.Px[i * 4 + 2]), ToByte(image.Px[i * 4 + 3]));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>床の色の上に重ねた絵（プレビュー用。ゲームの小さな表示に近い見え方）。</summary>
        public static SdImage OnFloor(SdImage image, Color floor, int margin)
        {
            var result = new SdImage(image.Width + margin * 2, image.Height + margin * 2);
            for (var y = 0; y < result.Height; y++)
            {
                for (var x = 0; x < result.Width; x++)
                {
                    var o = (y * result.Width + x) * 4;
                    float r = floor.r, g = floor.g, b = floor.b;
                    int sx = x - margin, sy = y - margin;
                    if (sx >= 0 && sy >= 0 && sx < image.Width && sy < image.Height)
                    {
                        var i = (sy * image.Width + sx) * 4;
                        var a = image.Px[i + 3];
                        r = image.Px[i] * a + r * (1 - a);
                        g = image.Px[i + 1] * a + g * (1 - a);
                        b = image.Px[i + 2] * a + b * (1 - a);
                    }

                    result.Px[o] = r; result.Px[o + 1] = g; result.Px[o + 2] = b; result.Px[o + 3] = 1f;
                }
            }

            return result;
        }

        /// <summary>
        /// 元の絵を全部処理して書き出す。余白の割合が変わっていたら、砲台のキャラの絵の一覧の「絵の高さ」を同じ割合で直す
        /// （キャラ自体の画面での大きさは変わらない）。書き出した名前を返す。
        /// </summary>
        public static List<string> ExportAll(SdArtSettings settings)
        {
            var written = new List<string>();
            var parameters = settings.ToParams();
            var names = SourceNames();
            try
            {
                for (var i = 0; i < names.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("SD の絵を書き出す", names[i], (float)i / names.Length);
                    var source = LoadSource(names[i]);
                    if (source == null)
                    {
                        Debug.LogWarning($"[SD の絵] {names[i]} を読めませんでした。");
                        continue;
                    }

                    var processed = SdArtProcessor.Process(source, parameters);
                    var texture = ToTexture(processed, FilterMode.Bilinear);
                    var path = ArtFolder + "/" + names[i];
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                    written.Add(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (var path in written)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                EnsureImportSettings(path);
            }

            UpdateTableHeights(settings, SdArtProcessor.CanvasScale(parameters));
            Debug.Log($"[SD の絵] {written.Count} 枚を書き出しました（{string.Join(", ", written.Select(Path.GetFileName))}）。");
            return written;
        }

        // 小さく表示しても線がくっきり残る取り込み設定（新しく足した絵にも同じ設定を付ける）。
        private static void EnsureImportSettings(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            var changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                          || settings.spriteMeshType != SpriteMeshType.FullRect || !importer.mipmapEnabled
                          || importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter || importer.anisoLevel != 4
                          || importer.filterMode != FilterMode.Trilinear || !Mathf.Approximately(importer.mipMapBias, -0.3f)
                          || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize != 1024
                          || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp;
            if (!changed)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.anisoLevel = 4;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipMapBias = -0.3f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static void UpdateTableHeights(SdArtSettings settings, float newScale)
        {
            var oldScale = settings.exportedCanvasScale > 0f ? settings.exportedCanvasScale : newScale;
            if (!Mathf.Approximately(oldScale, newScale))
            {
                var table = AssetDatabase.LoadAssetAtPath<TurretPortraitTable>(TablePath);
                if (table != null && table.entries != null)
                {
                    Undo.RecordObject(table, "SD の絵の高さを合わせる");
                    foreach (var entry in table.entries)
                    {
                        if (entry != null)
                        {
                            entry.height = Mathf.Round(entry.height * newScale / oldScale * 1000f) / 1000f;
                        }
                    }

                    EditorUtility.SetDirty(table);
                    Debug.Log($"[SD の絵] 余白が変わったので、砲台のキャラの絵の一覧の「絵の高さ」を {newScale / oldScale:0.###} 倍にしました（キャラ自体の大きさは同じ）。");
                }
            }

            Undo.RecordObject(settings, "SD の絵を書き出す");
            settings.exportedCanvasScale = newScale;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}
