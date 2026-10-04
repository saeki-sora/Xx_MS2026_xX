using MS2026.Fortress.Hud;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 画面左上の計測表示（敵の数・FPS・処理時間、ネット対戦中は通信量とHostとのズレ）。実機での負荷確認用。
    /// 位置はDebugOverlayLayoutが決める(D-Driveの通信状態表示などと重ならないよう、その下に並ぶ)。
    /// </summary>
    public static class SwarmHud
    {
        private const float Width = 260f;

        private static GUIStyle _style;
        private static readonly GUIContent Content = new GUIContent();
        private static float _height = 118f;
        private static float _nextTextUpdate;
        private static float _lastSampleTime;
        private static long _lastSent;
        private static long _lastReceived;

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
                Content.text = BuildText(system);
                _height = _style.CalcHeight(Content, Width);
            }

            GUI.Box(DebugOverlayLayout.TopLeft(DebugOverlaySlot.SwarmHud, Width, _height), Content, _style);
        }

        private static string BuildText(SwarmSystem system)
        {
            var stats = system.Stats;
            var fps = stats.frameMs > 0.01f ? 1000f / stats.frameMs : 0f;
            var text =
                $"敵 {stats.alive} / {stats.capacity}{(stats.pendingSpawns > 0 ? $"  (出現待ち {stats.pendingSpawns})" : string.Empty)}\n" +
                $"FPS {fps:0}  ({stats.frameMs:0.0} ms)\n" +
                $"メイン待ち {stats.simulationMs:0.00} / ジョブ {stats.jobLatencyMs:0.0} ms\n" +
                $"描画バッチ {stats.drawBatches}\n" +
                $"撃破 {stats.totalKilled} / コア到達 {stats.totalArrived}";

            if (!FortressNet.IsNetworked)
            {
                return text;
            }

            var now = Time.unscaledTime;
            var elapsed = Mathf.Max(0.001f, now - _lastSampleTime);
            var sentKBs = (NetTrafficStats.BytesSent - _lastSent) / 1024f / elapsed;
            var receivedKBs = (NetTrafficStats.BytesReceived - _lastReceived) / 1024f / elapsed;
            _lastSampleTime = now;
            _lastSent = NetTrafficStats.BytesSent;
            _lastReceived = NetTrafficStats.BytesReceived;

            text += $"\n通信 送信 {sentKBs:0} / 受信 {receivedKBs:0} KB/s";
            if (system.IsReplica)
            {
                var avg = stats.replicaCorrections > 0 ? stats.replicaErrorSum / stats.replicaCorrections : 0f;
                var viewAvg = stats.replicaViewCorrections > 0 ? stats.replicaViewErrorSum / stats.replicaViewCorrections : 0f;
                if (!NetTrafficStats.SwarmFullStateReceived)
                {
                    text += $"\n全体の状態を受信中 (待機 {NetTrafficStats.SwarmBufferedEvents}件)";
                }
                else if (system.ReplicaUsesSnapshots)
                {
                    text += $"\n写真方式 先読み {system.ReplicaExtrapolationSeconds * 1000f:0}ms / 外れ 平均 {avg:0.000} 最大 {stats.replicaErrorMax:0.00}";
                }
                else
                {
                    text += $"\nHostとのズレ 平均 {avg:0.00} (画面内 {viewAvg:0.00}) / 最大 {stats.replicaErrorMax:0.0}";
                }
            }
            else
            {
                if (NetTrafficStats.SwarmSnapshotsSent > 0)
                {
                    text += $"\n写真方式 1枚 {NetTrafficStats.SwarmSnapshotBytes / 1024f:0.0}KB ({NetTrafficStats.SwarmSnapshotBitsPerAgent:0.0}ビット/体)";
                }
                else if (NetTrafficStats.SwarmCorrectionBudgetKBs > 0f)
                {
                    text += NetTrafficStats.SwarmReportedError >= 0f
                        ? $"\n補正 {NetTrafficStats.SwarmCorrectionBudgetKBs:0} KB/s・優先度順 (Clientのズレ {NetTrafficStats.SwarmReportedError:0.00})"
                        : $"\n補正 {NetTrafficStats.SwarmCorrectionBudgetKBs:0} KB/s・優先度順";
                }
                else if (NetTrafficStats.SwarmCorrectionCycle > 0f)
                {
                    text += NetTrafficStats.SwarmReportedError >= 0f
                        ? $"\n補正の間隔 {NetTrafficStats.SwarmCorrectionCycle:0.00}秒 (Clientのズレ {NetTrafficStats.SwarmReportedError:0.00})"
                        : $"\n補正の間隔 {NetTrafficStats.SwarmCorrectionCycle:0.00}秒";
                }

                if (NetTrafficStats.SwarmEventBacklog > 0)
                {
                    text += $"\n送信待ち {NetTrafficStats.SwarmEventBacklog}件";
                }
            }

            return text;
        }
    }
}
