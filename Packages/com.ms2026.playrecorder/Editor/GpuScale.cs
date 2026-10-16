using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>GPU で縮小コピー。大きく縮めるときは半分ずつ縮めてチラつき（エイリアス）を抑える。</summary>
    internal static class GpuScale
    {
        public static void Blit(Texture src, RenderTexture dst, bool flipY)
        {
            Texture cur = src;
            RenderTexture tmp = null;
            int w = src.width, h = src.height;
            while (w > dst.width * 2 && h > dst.height * 2)
            {
                w /= 2;
                h /= 2;
                var next = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                next.filterMode = FilterMode.Bilinear;
                Graphics.Blit(cur, next);
                if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
                cur = tmp = next;
            }

            if (flipY) Graphics.Blit(cur, dst, new Vector2(1f, -1f), new Vector2(0f, 1f));
            else Graphics.Blit(cur, dst);

            if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
        }

        public static RenderTexture Ensure(RenderTexture rt, int width, int height, string name)
        {
            if (rt != null && rt.width == width && rt.height == height) return rt;
            if (rt != null)
            {
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            var created = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
            };
            created.Create();
            return created;
        }

        public static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
            rt = null;
        }
    }
}
