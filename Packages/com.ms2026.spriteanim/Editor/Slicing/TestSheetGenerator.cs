using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.SpriteAnim.Editor
{
    /// <summary>
    /// 本物の絵がまだ届いていなくても動作確認できるように、色分け＋通し番号ドット入りの
    /// プレースホルダー・スプライトシートをその場で生成するユーティリティ。
    /// </summary>
    public static class TestSheetGenerator
    {
        /// <summary>指定フォルダにテスト用スプライトシートPNGを作成し、そのアセットパスを返す。</summary>
        public static string CreateTestSpriteSheet(string folder, int columns, int rows, int cellSize)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            cellSize = Mathf.Max(8, cellSize);

            int w = columns * cellSize;
            int h = rows * cellSize;
            var pixels = new Color32[w * h];
            int total = columns * rows;

            for (int row = 0; row < rows; row++)
            {
                int gridRowFromBottom = rows - 1 - row;
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    Color32 cellColor = Color.HSVToRGB((float)index / total, 0.55f, 0.95f);
                    DrawCell(pixels, w, col * cellSize, gridRowFromBottom * cellSize, cellSize, cellColor, index + 1);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            if (!Directory.Exists(folder)) folder = "Assets";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/TestSpriteSheet_{columns}x{rows}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        private static void DrawCell(Color32[] pixels, int texWidth, int originX, int originY, int size, Color32 bg, int frameNumber)
        {
            int border = Mathf.Max(1, size / 32);
            var borderColor = new Color32(20, 20, 20, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[(originY + y) * texWidth + (originX + x)] = isBorder ? borderColor : bg;
                }
            }

            // 左下に、フレーム番号と同じ数だけ白いドットを並べて描く（何番目のフレームかを一目で分かるようにするため）。
            int dot = Mathf.Max(2, size / 16);
            int pad = dot;
            var dotColor = new Color32(255, 255, 255, 255);
            for (int i = 0; i < frameNumber; i++)
            {
                int dx = pad + (i % 8) * (dot + 2);
                int dy = pad + (i / 8) * (dot + 2);
                if (dx + dot >= size || dy + dot >= size) continue;

                for (int yy = 0; yy < dot; yy++)
                for (int xx = 0; xx < dot; xx++)
                    pixels[(originY + dy + yy) * texWidth + (originX + dx + xx)] = dotColor;
            }
        }
    }
}
