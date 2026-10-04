using System;
using System.Globalization;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 負荷計測(ベンチマーク)用のコマンドライン引数(Unity API非依存・EditModeテスト対象)。
    /// Tools/NetTest/bench_*.bat から使う。比較のため、改善策を起動引数で切り替えられるようにしている。
    ///
    ///   -fortress-bench                     1秒ごとに計測値をログ([Bench]で始まる1行)へ書く
    ///   -fortress-bench-burst N             (Host/オフラインのみ)群衆をN体一斉投入する
    ///   -fortress-bench-radius R            一斉投入の半径(既定6。要塞デザイナーの既定と同じ)
    ///   -fortress-bench-delay S             投入できる状態になってからS秒後に投入する(既定3)
    ///   -fortress-bench-clients K           Hostは、ClientがK人つながるまで待ってから数え始める
    ///   -fortress-bench-quit S              起動からS秒で自動終了する(ログを確定させるため)
    ///   -fortress-bench-no-wave             ウェーブ(敵の自動出現)を止めて、一斉投入の影響だけを測る
    ///   -fortress-bench-hide-ddrive-overlay D-Driveの通信状態表示(開発ビルドのみ。毎フレーム文字列を作る)を隠して、その分のGCを除いて測る
    ///   -fortress-bench-no-ongui            画面に直接描く部品(OnGUIを持つMonoBehaviour)を全て止めて測る(GCの出どころの切り分け用)
    ///   -fortress-bench-disable A+B+...     指定した型名の部品(MonoBehaviour)を止めて測る(GCや負荷の出どころの二分探索用)。
    ///                                       区切りは + を使う(cmd/batはカンマを引数の区切りとして扱うため。カンマは引用符で囲めば可)
    ///   -fortress-swarm-spawns-per-frame N  1フレームに追加する群衆の上限(0=無制限=改善前の動き)
    ///   -fortress-replica-separation N      Clientの押し合い計算の反復回数(未指定ならSwarmNetworkHubの設定)
    ///   -fortress-net-packet-queue N        1フレームに送受信できるパケット数の上限(未指定ならFortressNetworkBootstrapの設定)
    ///   -fortress-swarm-smoothing render|blend  Clientの寄せ方(render=見た目だけ滑らかに、blend=従来)
    ///   -fortress-swarm-correction-cycle S      普段の補正の間隔(秒)
    ///   -fortress-swarm-correction-min-cycle S  自動調整で縮める間隔の下限(秒)
    ///   -fortress-swarm-adaptive 0|1            補正の間隔(優先度つきでは通信量)の自動調整を切る/入れる
    ///   -fortress-swarm-priority 0|1            優先度つきの補正(段階5)を切る(=番号順に全員を同じ間隔で送る)/入れる
    ///   -fortress-swarm-velocity 0|1            補正に速度を載せない/載せる
    ///   -fortress-swarm-budget-kbs N            優先度つきの補正の普段の通信量(Client1人あたり、KB/秒)
    ///   -fortress-swarm-max-budget-kbs N        同、ズレが大きい間の上限(KB/秒)
    /// </summary>
    public struct NetBenchmarkArgs
    {
        public bool LogEnabled;
        public int BurstCount;
        public float BurstRadius;
        public float BurstDelaySeconds;
        public int ExpectedClients;
        public float QuitAfterSeconds;
        public bool StopWaves;
        public bool HideDDriveOverlay;
        public bool NoOnGui;
        public string[] DisableTypes;
        public int? SpawnsPerFrame;
        public int? ReplicaSeparationIterations;
        public int? PacketQueueSize;
        public SwarmReplicaSmoothing? Smoothing;
        public float? CorrectionCycleSeconds;
        public float? MinCorrectionCycleSeconds;
        public bool? AdaptiveCorrection;
        public bool? PriorityCorrection;
        public bool? SendVelocity;
        public float? BudgetKBps;
        public float? MaxBudgetKBps;

        public bool AnyBenchmark => LogEnabled || BurstCount > 0 || QuitAfterSeconds > 0f || StopWaves;

        public static NetBenchmarkArgs Parse(string[] args)
        {
            var result = new NetBenchmarkArgs { BurstRadius = 6f, BurstDelaySeconds = 3f };
            if (args == null)
            {
                return result;
            }

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-fortress-bench":
                        result.LogEnabled = true;
                        break;
                    case "-fortress-bench-burst":
                        result.BurstCount = NextInt(args, ref i) ?? 0;
                        break;
                    case "-fortress-bench-radius":
                        result.BurstRadius = NextFloat(args, ref i) ?? result.BurstRadius;
                        break;
                    case "-fortress-bench-delay":
                        result.BurstDelaySeconds = NextFloat(args, ref i) ?? result.BurstDelaySeconds;
                        break;
                    case "-fortress-bench-clients":
                        result.ExpectedClients = NextInt(args, ref i) ?? 0;
                        break;
                    case "-fortress-bench-quit":
                        result.QuitAfterSeconds = NextFloat(args, ref i) ?? 0f;
                        break;
                    case "-fortress-bench-no-wave":
                        result.StopWaves = true;
                        break;
                    case "-fortress-bench-hide-ddrive-overlay":
                        result.HideDDriveOverlay = true;
                        break;
                    case "-fortress-bench-no-ongui":
                        result.NoOnGui = true;
                        break;
                    case "-fortress-bench-disable":
                        var list = i + 1 < args.Length ? args[++i] : string.Empty;
                        result.DisableTypes = list.Split(new[] { ',', '+', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        break;
                    case "-fortress-swarm-spawns-per-frame":
                        result.SpawnsPerFrame = NextInt(args, ref i);
                        break;
                    case "-fortress-replica-separation":
                        result.ReplicaSeparationIterations = NextInt(args, ref i);
                        break;
                    case "-fortress-net-packet-queue":
                        result.PacketQueueSize = NextInt(args, ref i);
                        break;
                    case "-fortress-swarm-smoothing":
                        result.Smoothing = NextSmoothing(args, ref i);
                        break;
                    case "-fortress-swarm-correction-cycle":
                        result.CorrectionCycleSeconds = NextFloat(args, ref i);
                        break;
                    case "-fortress-swarm-correction-min-cycle":
                        result.MinCorrectionCycleSeconds = NextFloat(args, ref i);
                        break;
                    case "-fortress-swarm-adaptive":
                        result.AdaptiveCorrection = NextBool(args, ref i);
                        break;
                    case "-fortress-swarm-priority":
                        result.PriorityCorrection = NextBool(args, ref i);
                        break;
                    case "-fortress-swarm-velocity":
                        result.SendVelocity = NextBool(args, ref i);
                        break;
                    case "-fortress-swarm-budget-kbs":
                        result.BudgetKBps = NextFloat(args, ref i);
                        break;
                    case "-fortress-swarm-max-budget-kbs":
                        result.MaxBudgetKBps = NextFloat(args, ref i);
                        break;
                }
            }

            return result;
        }

        private static SwarmReplicaSmoothing? NextSmoothing(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                return null;
            }

            i++;
            switch (args[i].ToLowerInvariant())
            {
                case "render":
                    return SwarmReplicaSmoothing.RenderOffset;
                case "blend":
                    return SwarmReplicaSmoothing.BlendSimulation;
                default:
                    return null;
            }
        }

        private static bool? NextBool(string[] args, ref int i)
        {
            var value = NextInt(args, ref i);
            return value.HasValue ? value.Value != 0 : (bool?)null;
        }

        private static int? NextInt(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                return null;
            }

            i++;
            return int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (int?)null;
        }

        private static float? NextFloat(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                return null;
            }

            i++;
            return float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : (float?)null;
        }
    }
}
