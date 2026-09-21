using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>画面左上の計測表示（敵の数・FPS・処理時間）。実機での負荷確認用。</summary>
    public static class SwarmHud
    {
        private static GUIStyle _style;
        private static string _text = string.Empty;
        private static float _nextTextUpdate;

        public static void Draw(SwarmSystem system)
        {
            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 10, 8, 8)
            };

            // 文字列の生成を毎フレームやるとGC負荷になるので、1秒に4回だけ更新する。
            if (Time.unscaledTime >= _nextTextUpdate)
            {
                _nextTextUpdate = Time.unscaledTime + 0.25f;
                var stats = system.Stats;
                var fps = stats.frameMs > 0.01f ? 1000f / stats.frameMs : 0f;
                _text =
                    $"敵 {stats.alive} / {stats.capacity}\n" +
                    $"FPS {fps:0}  ({stats.frameMs:0.0} ms)\n" +
                    $"メイン待ち {stats.simulationMs:0.00} / ジョブ {stats.jobLatencyMs:0.0} ms\n" +
                    $"描画バッチ {stats.drawBatches}\n" +
                    $"撃破 {stats.totalKilled} / コア到達 {stats.totalArrived}";
            }

            GUI.Box(new Rect(10f, 10f, 240f, 118f), _text, _style);
        }
    }
}
