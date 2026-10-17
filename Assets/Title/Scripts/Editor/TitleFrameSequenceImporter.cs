using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Title.EditorTools
{
    /// <summary>
    /// メニュー「Tools/タイトル/歯磨き粉の連番を取り込む」。After Effects などで書き出した PNG 連番（背景は透明、全部同じ大きさ）を、
    /// オープニングの歯磨き粉（<see cref="TitleOpening.tubeSequence"/>）として使えるようにする。
    /// ・全部のコマで透明な周りを同じ枠で切り落として Assets/Title/Sprites/ToothpasteFrames/ に入れる（同じ名前は上書きなので、作り直しても参照は切れない）
    /// ・各コマの口（チューブの上端）の位置と、キャップが離れて飛んでいくコマを自動で求めて <see cref="TitleFrameSequence"/> にまとめる
    /// ・タイトルのシーンの歯磨き粉に入れる（書き出したときと同じ位置・大きさに置くか、今の位置・大きさのままにするかを選べる）
    /// AE の修正版が届いたら、もう一度このメニューで入れ直せばよい（1秒の枚数は前の設定を引き継ぐ）。
    /// </summary>
    internal static class TitleFrameSequenceImporter
    {
        private const string MenuPath = "Tools/タイトル/歯磨き粉の連番を取り込む";
        public const string FramesDir = "Assets/Title/Sprites/ToothpasteFrames";
        public const string SequencePath = "Assets/Title/Sprites/ToothpasteFrames.asset";
        private const string LastFolderKey = "MS2026.Title.ToothpasteFramesFolder";
        private const string UndoName = "歯磨き粉の連番を取り込む";
        // 100px = 1（カメラに映る高さ 10.8 = 1080px なので、1920x1080 の書き出しと同じ大きさで映る）。
        private const float PixelsPerUnit = 100f;
        // 切り落とす枠の余白（px）。
        private const int Padding = 2;
        // 口・離れた物をさがすときに「絵がある」とみなす不透明さ。
        private const byte SolidAlpha = 128;
        // チューブの上端の上に、これだけすき間（px）があってその上に絵があれば「離れて飛んでいる物（キャップ）」とみなす。
        private const int DetachGap = 6;

        [MenuItem(MenuPath)]
        private static void Import()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("歯磨き粉の連番", "再生中は取り込めません。再生を止めてから実行してください。", "OK");
                return;
            }

            var folder = EditorUtility.OpenFolderPanel("歯磨き粉の連番（PNG）が入ったフォルダーを選ぶ", EditorPrefs.GetString(LastFolderKey, ""), "");
            if (string.IsNullOrEmpty(folder)) return;
            EditorPrefs.SetString(LastFolderKey, folder);

            var files = Directory.GetFiles(folder, "*.png").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length < 2)
            {
                EditorUtility.DisplayDialog("歯磨き粉の連番", $"PNG が2枚以上入っていません。\n{folder}", "OK");
                return;
            }

            var missing = FindMissingNumbers(files);
            if (missing.Count > 0 && !EditorUtility.DisplayDialog("歯磨き粉の連番",
                    $"番号が抜けているようです（{FormatNumbers(missing)}）。\nこのまま取り込みますか？（抜けたコマは飛ばして再生されます）",
                    "このまま取り込む", "やめる"))
            {
                return;
            }

            ImportResult result;
            try
            {
                result = CropAndWrite(files);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("歯磨き粉の連番", e.Message, "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (result == null) return;

            var sequence = SaveSequence(result);

            var choice = EditorUtility.DisplayDialogComplex("歯磨き粉の連番",
                $"{result.Names.Count} 枚を取り込みました（{result.SourceWidth}x{result.SourceHeight} → 切り落として {result.CropWidth}x{result.CropHeight}）。\n" +
                (result.EjectFrame >= 0
                    ? $"キャップが飛ぶのは {result.EjectFrame} 枚目（0から数えて）と判断しました。\n"
                    : $"キャップが飛ぶコマが見つからなかったので、前の設定（{sequence.ejectFrame} 枚目）のままです。\n") +
                $"1秒の枚数: {sequence.frameRate:0.##}（{sequence.Duration:0.00}秒）。どちらも {SequencePath} の Inspector で変えられます。\n\n" +
                "タイトルのシーンの歯磨き粉に入れますか？\n" +
                "・書き出したときと同じ位置・大きさ … AE の画面と同じ見え方に置き直す\n" +
                "・今の位置・大きさのまま … シーンで調整した位置・大きさを変えない",
                "書き出したときと同じ位置・大きさ", "シーンには入れない", "今の位置・大きさのまま");
            if (choice == 1) return;

            if (!TitleSceneBuilder.OpenTitleScene()) return;
            var opening = UnityEngine.Object.FindAnyObjectByType<TitleOpening>(FindObjectsInactive.Include);
            if (opening == null)
            {
                EditorUtility.DisplayDialog("歯磨き粉の連番",
                    "タイトルのシーンにオープニング（Title Opening）がありません。先に「Tools/タイトル/タイトルシーンを組み立てる」を実行してください（連番は自動で入ります）。", "OK");
                return;
            }

            ApplyToOpening(opening, sequence, placeLikeSource: choice == 0, recordUndo: true);
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = sequence;
            Debug.Log($"[Title] 歯磨き粉の連番（{result.Names.Count} 枚）をオープニングに入れました。▶ 再生して確かめてください。");
        }

        /// <summary>
        /// オープニングの歯磨き粉に連番を入れる。<paramref name="placeLikeSource"/> なら、書き出したときと同じ位置・大きさ（100px = 1、画面の真ん中が基準）に置き直す。
        /// キャップの配置物は連番の絵に含まれるので隠す。組み立てメニューからも使う。
        /// </summary>
        public static void ApplyToOpening(TitleOpening opening, TitleFrameSequence sequence, bool placeLikeSource, bool recordUndo)
        {
            if (recordUndo) Undo.RecordObject(opening, UndoName);
            opening.tubeSequence = sequence;
            EditorUtility.SetDirty(opening);

            var tube = opening.tube;
            if (tube == null || sequence == null || !sequence.IsUsable) return;

            // シーンでも形が分かるよう、キャップが飛ぶ直前のコマを見せておく（再生中は最初のコマから流す）。
            var spriteChild = tube.Find("Sprite");
            var renderer = spriteChild != null ? spriteChild.GetComponent<SpriteRenderer>() : null;
            if (renderer != null)
            {
                if (recordUndo) Undo.RecordObject(renderer, UndoName);
                renderer.sprite = sequence.frames[Mathf.Clamp(sequence.ejectFrame - 1, 0, sequence.Count - 1)];
            }

            if (opening.cap != null)
            {
                if (recordUndo) Undo.RecordObject(opening.cap.gameObject, UndoName);
                opening.cap.gameObject.SetActive(false);
            }

            if (!placeLikeSource) return;

            if (recordUndo) Undo.RecordObject(tube, UndoName);
            var center = Camera.main != null ? (Vector2)Camera.main.transform.position : Vector2.zero;
            tube.position = new Vector3(center.x + sequence.screenOffset.x, center.y + sequence.screenOffset.y, tube.position.z);
            tube.localScale = Vector3.one;
            if (spriteChild != null)
            {
                if (recordUndo) Undo.RecordObject(spriteChild, UndoName);
                spriteChild.localPosition = Vector3.zero;
                spriteChild.localScale = Vector3.one;
                var fit = spriteChild.GetComponent<TitleSpriteFit>();
                if (fit != null)
                {
                    if (recordUndo) Undo.RecordObject(fit, UndoName);
                    fit.mode = TitleSpriteFit.FitMode.None; // 書き出した大きさのまま（100px = 1）
                }
            }
        }

        private sealed class ImportResult
        {
            public readonly List<string> Names = new();
            public readonly List<Vector2> Mouths = new();
            public int SourceWidth, SourceHeight, CropWidth, CropHeight;
            public Vector2 ScreenOffset;
            public int EjectFrame = -1;
        }

        // 1回目: 全コマの絵がある範囲を求める。2回目: その枠で切り落として書き出し、口とキャップをさがす。
        // （1080p を全部メモリに載せると重いので、1枚ずつ読んで捨てる）
        private static ImportResult CropAndWrite(string[] files)
        {
            int width = 0, height = 0;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (var i = 0; i < files.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("歯磨き粉の連番", $"絵がある範囲をさがしています… {i + 1}/{files.Length}", (float)i / files.Length * 0.4f))
                {
                    return null;
                }
                var pixels = Load(files[i], out var w, out var h);
                if (i == 0)
                {
                    width = w;
                    height = h;
                }
                else if (w != width || h != height)
                {
                    throw new InvalidOperationException($"大きさがそろっていません: {Path.GetFileName(files[i])} は {w}x{h}（最初の絵は {width}x{height}）。");
                }
                for (var y = 0; y < h; y++)
                {
                    var row = y * w;
                    for (var x = 0; x < w; x++)
                    {
                        if (pixels[row + x].a == 0) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < 0) throw new InvalidOperationException("どの絵も全部透明でした。背景を透明（RGB + アルファ）で書き出した PNG か確かめてください。");

            var x0 = Mathf.Max(0, minX - Padding);
            var y0 = Mathf.Max(0, minY - Padding);
            var cw = Mathf.Min(width - 1, maxX + Padding) - x0 + 1;
            var ch = Mathf.Min(height - 1, maxY + Padding) - y0 + 1;

            var result = new ImportResult
            {
                SourceWidth = width,
                SourceHeight = height,
                CropWidth = cw,
                CropHeight = ch,
                // 画面の真ん中から見た、切り落とした枠の中心（Unity の画像は下から上なので、そのまま上向き）。
                ScreenOffset = new Vector2((x0 + cw * 0.5f - width * 0.5f) / PixelsPerUnit, (y0 + ch * 0.5f - height * 0.5f) / PixelsPerUnit),
            };

            EnsureFolder(FramesDir);
            var cropped = new Color32[cw * ch];
            var texture = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            try
            {
                for (var i = 0; i < files.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("歯磨き粉の連番", $"切り落として入れています… {i + 1}/{files.Length}", 0.4f + (float)i / files.Length * 0.4f))
                    {
                        return null;
                    }
                    var pixels = Load(files[i], out _, out _);
                    for (var y = 0; y < ch; y++)
                    {
                        Array.Copy(pixels, (y0 + y) * width + x0, cropped, y * cw, cw);
                    }
                    texture.SetPixels32(cropped);
                    var name = Path.GetFileName(files[i]);
                    File.WriteAllBytes(Path.Combine(FramesDir, name), texture.EncodeToPNG());
                    result.Names.Add(name);

                    var mouth = FindMouth(cropped, cw, ch, out var detached);
                    // 絵の中心を原点にした位置（100px = 1）。
                    result.Mouths.Add(new Vector2((mouth.x + 0.5f - cw * 0.5f) / PixelsPerUnit, (mouth.y + 1f - ch * 0.5f) / PixelsPerUnit));
                    if (detached && result.EjectFrame < 0) result.EjectFrame = i;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            // 前に取り込んだ絵のうち、今回に無い名前のものは消す（同じ名前は上書きしたので参照はそのまま）。
            var keep = new HashSet<string>(result.Names, StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FramesDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!keep.Contains(Path.GetFileName(path))) AssetDatabase.DeleteAsset(path);
            }

            EditorUtility.DisplayProgressBar("歯磨き粉の連番", "Unity に読み込ませています…", 0.85f);
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var name in result.Names) ConfigureImporter(FramesDir + "/" + name);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return result;
        }

        private static Color32[] Load(string file, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(file))) throw new InvalidOperationException($"PNG として読めませんでした: {Path.GetFileName(file)}");
                width = texture.width;
                height = texture.height;
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // 口 = チューブの真ん中の縦の線を下から上へたどって、絵が途切れた所。
        // その上にすき間をあけて絵があれば、離れて飛んでいる物（キャップ）がある。
        private static Vector2Int FindMouth(Color32[] pixels, int width, int height, out bool detached)
        {
            detached = false;
            int minX = int.MaxValue, maxX = -1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < SolidAlpha) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                }
            }
            if (maxX < 0) return new Vector2Int(width / 2, height - 1); // 何も描かれていないコマ

            var cx = (minX + maxX) / 2;
            bool Solid(int y)
            {
                for (var x = Mathf.Max(0, cx - 2); x <= Mathf.Min(width - 1, cx + 2); x++)
                {
                    if (pixels[y * width + x].a >= SolidAlpha) return true;
                }
                return false;
            }

            var bottom = 0;
            while (bottom < height && !Solid(bottom)) bottom++;
            if (bottom >= height) return new Vector2Int(cx, height - 1);

            var top = bottom;
            var gap = 0;
            for (var y = bottom; y < height; y++)
            {
                if (Solid(y))
                {
                    top = y;
                    gap = 0;
                }
                else if (++gap >= DetachGap)
                {
                    break;
                }
            }

            for (var y = top + DetachGap; y < height && !detached; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < SolidAlpha) continue;
                    detached = true;
                    break;
                }
            }
            return new Vector2Int(cx, top);
        }

        private static void ConfigureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect; // コマごとに形を計算させない（軽い）
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static TitleFrameSequence SaveSequence(ImportResult result)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<TitleFrameSequence>(SequencePath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<TitleFrameSequence>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }
            Undo.RecordObject(sequence, UndoName);
            sequence.frames = result.Names.Select(n => AssetDatabase.LoadAssetAtPath<Sprite>(FramesDir + "/" + n)).ToArray();
            sequence.mouths = result.Mouths.ToArray();
            sequence.screenOffset = result.ScreenOffset;
            if (result.EjectFrame >= 0) sequence.ejectFrame = result.EjectFrame;
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
            return sequence;
        }

        // 名前の最後の数字が飛んでいる所（例: 012 の次が 014 なら 13）。
        private static List<int> FindMissingNumbers(string[] files)
        {
            var numbers = new List<int>();
            foreach (var file in files)
            {
                var match = Regex.Match(Path.GetFileNameWithoutExtension(file), @"(\d+)$");
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var n)) return new List<int>();
                numbers.Add(n);
            }
            numbers.Sort();
            var missing = new List<int>();
            for (var i = 1; i < numbers.Count; i++)
            {
                for (var n = numbers[i - 1] + 1; n < numbers[i]; n++) missing.Add(n);
            }
            return missing;
        }

        private static string FormatNumbers(List<int> numbers) =>
            numbers.Count <= 8 ? string.Join(", ", numbers) : string.Join(", ", numbers.Take(8)) + $" ほか {numbers.Count - 8} 枚";

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    /// <summary>連番ファイルの Inspector。枚数・長さ・キャップが飛ぶ時刻を出し、自動で求めた口の位置などは隠す。</summary>
    [CustomEditor(typeof(TitleFrameSequence))]
    internal sealed class TitleFrameSequenceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var sequence = (TitleFrameSequence)target;
            EditorGUILayout.HelpBox(
                $"{sequence.Count} 枚 ・ {sequence.Duration:0.00} 秒。キャップが飛ぶのは {sequence.EjectTime:0.00} 秒の所。\n" +
                "AE の修正版は、メニュー「Tools/タイトル/歯磨き粉の連番を取り込む」で入れ直せます（1秒の枚数はそのまま）。",
                MessageType.None);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "mouths", "screenOffset");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
