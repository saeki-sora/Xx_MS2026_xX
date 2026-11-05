using System;
using System.Text;
using MS2026.Fortress;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Spectator
{
    /// <summary>
    /// 観客ページの係（起動時に自動で作られる、見えないオブジェクト。シーンに置く物は無い）。
    /// ・Web サーバーを動かす（ビルドでは常に。エディタではメニューでオンにしたときだけ）
    /// ・1 秒に 4 回、試合の様子とステージの地図を写してサーバーに渡す
    /// ・観客が送った分を取り出し、この PC が Host（かオフライン）なら菌を出す
    /// M1 では Client の PC からは送れない（M3 で Host へ取り次ぐ）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SpectatorHost : MonoBehaviour
    {
        private const float BoardInterval = 0.25f;
        private const string PageResource = "Spectator/SpectatorPage";

        private static SpectatorHost _instance;

        private readonly SpectatorStage _stage = new SpectatorStage();
        private SpectatorApi _api;
        private SpectatorHttpServer _server;
        private int _port;
        private float _boardTimer;

        /// <summary>動いている係（無ければ null）。エディタのメニューが住所を出すのに使う。</summary>
        public static SpectatorHost Instance => _instance;

        public int Port => _port;

        public int SpectatorCount => _api != null ? _api.Roster.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var options = SpectatorConfig.Parse(Environment.GetCommandLineArgs());
            if (options.Disabled || _instance != null)
            {
                return;
            }

            if (Application.isEditor && PlayerPrefs.GetInt(SpectatorConfig.EditorEnabledKey, 0) != 1)
            {
                return;
            }

            var go = new GameObject("[Spectator] Host");
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            DontDestroyOnLoad(go);
            var host = go.AddComponent<SpectatorHost>();
            host._port = options.Port;
        }

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            _api = new SpectatorApi(new SpectatorRoster(SpectatorConfig.MaxSpectators, SpectatorConfig.CooldownSeconds));
            var page = Resources.Load<TextAsset>(PageResource);
            if (page != null)
            {
                _api.SetPage(page.bytes);
            }
            else
            {
                Debug.LogWarning($"[Spectator] スマホのページ（Resources/{PageResource}.html）が見つかりません。");
            }

            _server = new SpectatorHttpServer(_api.Handle, SpectatorConfig.MaxConnections);
            if (!_server.Start(_port, out var error))
            {
                Debug.LogWarning($"[Spectator] 観客ページを開けませんでした（番号 {_port}）: {error}。ほかのアプリが同じ番号を使っていないか確かめてください。起動オプション {SpectatorConfig.PortFlag} で番号を変えられます。");
                _server = null;
                enabled = false;
                return;
            }

            Debug.Log($"[Spectator] 観客ページを開きました: {SpectatorAddresses.Urls(_port)}");
            UpdateBoard();
        }

        private void Update()
        {
            if (_api == null)
            {
                return;
            }

            _boardTimer -= Time.unscaledDeltaTime;
            if (_boardTimer <= 0f)
            {
                _boardTimer = BoardInterval;
                UpdateBoard();
            }

            while (_api.TryDequeue(out var order))
            {
                Carry(order);
            }
        }

        private void Carry(SpectatorOrder order)
        {
            if (order.StageKey != _stage.Key || !FortressNet.HasSimulationAuthority)
            {
                // ステージが変わった・Host でなくなった。待ち時間を返して、もう一度送れるようにする。
                _api.Roster.Refund(order.SpectatorId);
                return;
            }

            var spawned = _stage.Spawn(order.ExitIndex, SpectatorConfig.GermsPerSend);
            if (spawned <= 0)
            {
                _api.Roster.Refund(order.SpectatorId);
            }

            Debug.Log($"[Spectator] {order.Name} が出口 {_stage.ExitId(order.ExitIndex)} から {_stage.GermName} を {spawned} 体送り込んだ");
        }

        private void UpdateBoard()
        {
            _stage.Refresh();
            var phase = ReadPhase(out var message);
            var sb = new StringBuilder(512);
            sb.Append('{')
                .Key("phase").Str(phase).Append(',')
                .Key("msg").Str(message).Append(',')
                .Key("stage").Str(_stage.Key).Append(',')
                .Key("timeLeft").Num(FortressWaves.TimeLeft).Append(',')
                .Key("wave").Num(FortressWaves.IsActive ? FortressWaves.WaveNumber : 0).Append(',')
                .Key("waveCount").Num(FortressWaves.WaveCount).Append(',')
                .Key("germs").Num(SpectatorConfig.GermsPerSend).Append(',')
                .Key("exits");
            _stage.AppendExitsJson(sb);
            sb.Append('}');

            _api.Board = new SpectatorBoard
            {
                Phase = phase,
                StageKey = _stage.Key,
                ExitCount = _stage.ExitCount,
                SharedJson = sb.ToString()
            };
        }

        /// <summary>いま送れるか（play）と、スマホに出す一言。</summary>
        private string ReadPhase(out string message)
        {
            if (!FortressMatch.InMatch || !_stage.IsReady)
            {
                message = "次の試合を待っています";
                return SpectatorPhase.Wait;
            }

            if (FortressMatch.HasEnded)
            {
                message = "試合が終わりました。次の試合を待っています";
                return SpectatorPhase.End;
            }

            if (FortressNet.IsNetworked && !FortressNet.HasSimulationAuthority)
            {
                message = "このPCからはまだ送れません（作業中）";
                return SpectatorPhase.Wait;
            }

            if (!FortressWaves.IsActive)
            {
                message = "出口をタップして菌を送ろう";
                return SpectatorPhase.Play;
            }

            switch (FortressWaves.Phase)
            {
                case WavePhase.Combat:
                    message = "出口をタップして菌を送ろう";
                    return SpectatorPhase.Play;
                case WavePhase.Announce:
                    message = $"WAVE {FortressWaves.WaveNumber} がはじまります";
                    return SpectatorPhase.Wait;
                case WavePhase.Retreat:
                    message = "WAVE CLEAR！ 次のウェーブを待っています";
                    return SpectatorPhase.Wait;
                case WavePhase.Break:
                    message = "歯みがきタイム中。次のウェーブを待っています";
                    return SpectatorPhase.Wait;
                case WavePhase.Finished:
                    message = "試合が終わりました。次の試合を待っています";
                    return SpectatorPhase.End;
                default:
                    message = "まもなく試合がはじまります";
                    return SpectatorPhase.Wait;
            }
        }

        private void OnDestroy()
        {
            _server?.Dispose();
            _server = null;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _server?.Dispose();
            _server = null;
        }
    }
}
