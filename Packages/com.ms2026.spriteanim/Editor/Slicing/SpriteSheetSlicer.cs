using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.SpriteAnim.Editor
{
    /// <summary>
    /// 1枚のテクスチャをグリッドで自動スライスし、Unity 標準の Sprite アセットとして書き出すユーティリティ。
    /// デザイナーは Sprite Editor を手で操作する必要がなく、列数・行数を入力するだけでよい。
    /// </summary>
    public static class SpriteSheetSlicer
    {
        /// <summary>
        /// 左上を index 0 として、左→右、上→下の読み順で Sprite 配列を返す（例: 4x2 なら index 0〜3 が上段、4〜7 が下段）。
        /// </summary>
        public static Sprite[] SliceGrid(
            Texture2D texture,
            int columns,
            int rows,
            int paddingX = 0,
            int paddingY = 0,
            int marginX = 0,
            int marginY = 0,
            Vector2? pivot = null,
            float pixelsPerUnit = 100f)
        {
            if (texture == null)
            {
                Debug.LogError("[SpriteSheetSlicer] テクスチャが null です。");
                return Array.Empty<Sprite>();
            }

            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[SpriteSheetSlicer] テクスチャがプロジェクト内のアセットではありません。");
                return Array.Empty<Sprite>();
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[SpriteSheetSlicer] TextureImporter が取得できませんでした: " + path);
                return Array.Empty<Sprite>();
            }

            int texW = texture.width;
            int texH = texture.height;
            int usableW = texW - marginX * 2;
            int usableH = texH - marginY * 2;
            int cellW = Mathf.Max(1, (usableW - paddingX * (columns - 1)) / columns);
            int cellH = Mathf.Max(1, (usableH - paddingY * (rows - 1)) / rows);
            Vector2 pivotValue = pivot ?? new Vector2(0.5f, 0.5f);

#pragma warning disable 0618 // TextureImporter.spritesheet / SpriteMetaData は新しい Sprite Editor Data Provider API に置き換わりつつあるが、スクリプトからの一括スライスには最も簡潔で安定している。
            var metas = new List<SpriteMetaData>(columns * rows);
            for (int row = 0; row < rows; row++)
            {
                int gridRowFromBottom = rows - 1 - row; // テクスチャのY原点は左下なので、上の行ほどYが大きい
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    float x = marginX + col * (cellW + paddingX);
                    float y = marginY + gridRowFromBottom * (cellH + paddingY);

                    metas.Add(new SpriteMetaData
                    {
                        name = $"{texture.name}_{index}",
                        rect = new Rect(x, y, cellW, cellH),
                        pivot = pivotValue,
                        alignment = (int)SpriteAlignment.Custom,
                    });
                }
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = metas.ToArray();
            importer.spritePixelsPerUnit = pixelsPerUnit;
#pragma warning restore 0618

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAllAssetsAtPath(path);
            var result = new Sprite[columns * rows];
            string prefix = texture.name + "_";
            foreach (var asset in loaded)
            {
                if (asset is Sprite sprite && sprite.name.StartsWith(prefix) &&
                    int.TryParse(sprite.name.Substring(prefix.Length), out int idx) &&
                    idx >= 0 && idx < result.Length)
                {
                    result[idx] = sprite;
                }
            }

            return result;
        }

        /// <summary>セルの縦横ピクセルサイズを指定してスライスする（列数・行数は自動計算）。</summary>
        public static Sprite[] SliceByCellSize(
            Texture2D texture,
            int cellWidth,
            int cellHeight,
            int paddingX = 0,
            int paddingY = 0,
            int marginX = 0,
            int marginY = 0,
            Vector2? pivot = null,
            float pixelsPerUnit = 100f)
        {
            if (texture == null) return Array.Empty<Sprite>();
            cellWidth = Mathf.Max(1, cellWidth);
            cellHeight = Mathf.Max(1, cellHeight);

            int usableW = texture.width - marginX * 2;
            int usableH = texture.height - marginY * 2;
            int columns = Mathf.Max(1, (usableW + paddingX) / (cellWidth + paddingX));
            int rows = Mathf.Max(1, (usableH + paddingY) / (cellHeight + paddingY));

            return SliceGrid(texture, columns, rows, paddingX, paddingY, marginX, marginY, pivot, pixelsPerUnit);
        }
    }
}
