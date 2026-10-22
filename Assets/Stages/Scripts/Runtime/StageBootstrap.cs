using System;
using MS2026.Fortress;
using MS2026.Fortress.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Stages
{
    /// <summary>
    /// 共通のゲームのシーン（Game）に 1 つ置く「ステージを読み込む係」（`[Stage] Bootstrap`）。
    /// ・ロビー経由（もう通信している）: ステージはホストが先に読み込んでいるので、目印を当てはめるだけ。
    /// ・Game だけで Play／テスト用の bat: ステージ（一覧の既定、または起動引数 -fortress-stage）を自分で重ねて読み込み、
    ///   読み終わるまで通信の開始（<see cref="FortressNetworkBootstrap"/>）を待たせる（壊せる物の番号付けが全員そろうように）。
    ///   参加側には、もう読み込んであるステージを NGO にもう一度読み込ませないよう断る設定を入れる（二重になるのを防ぐ）。
    /// ・まだ分けていない Game（ステージの中身が Game にある）では何もしない（今までどおり）。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/ステージ/ステージを読み込む係")]
    public sealed class StageBootstrap : MonoBehaviour
    {
        /// <summary>起動引数: 読み込むステージ（id・シーン名・1 からの番号）。例: -fortress-stage Stage2</summary>
        public const string StageArgument = "-fortress-stage";

        [Tooltip("ON なら、Game だけで Play・起動したとき（ロビーを通らないとき）に、ステージを自分で重ねて読み込む。")]
        public bool loadStageWhenAlone = true;

        [Tooltip("通信の開始を待つ最長の秒数（ステージの読み込みが終わらないときの保険）。")]
        [Min(1f)]
        public float maxWaitSeconds = 20f;

        private AsyncOperation _loading;
        private string _loadingScene;
        private NetworkManager _hookedManager;
        private Func<bool> _gate;

        /// <summary>Game だけで起動して、ステージを読み込んでいる途中か。</summary>
        public bool IsLoadingStage => _loading != null && !_loading.isDone;

        private void Awake()
        {
            if (StageLayout.Active != null)
            {
                StageLayout.Active.TryApply();
                return;
            }

            if (StageLayout.IsNetworkRunning() || !loadStageWhenAlone || SceneHasOwnContent())
            {
                // ロビー経由: ステージはホストが NGO で読み込む（途中参加でも Game より先に、または通信が動き出す前にそろう）。
                return;
            }

            var catalog = StageCatalog.Load();
            if (catalog == null || catalog.Count == 0)
            {
                return;
            }

            // エディタでステージのシーンも一緒に開いて Play したときは、それを使う（ほかのステージを重ねない）。
            if (catalog.FindLoadedStageScene().isLoaded || FindObjectsByType<StageLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
            {
                return;
            }

            var index = ChooseStandaloneStage(catalog);
            if (index < 0)
            {
                Debug.LogWarning("[Stage] 読み込めるステージがありません。ステージの一覧（Resources/StageCatalog）とビルドのシーン一覧を確認してください。");
                return;
            }

            var sceneName = catalog.Get(index).sceneName;
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                return;
            }

            _loadingScene = sceneName;
            _loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            var deadline = Time.realtimeSinceStartup + maxWaitSeconds;
            _gate = () => !IsLoadingStage || Time.realtimeSinceStartup > deadline;
            FortressNetworkBootstrap.AutoStartGate = _gate;
            HookNetworkManager();
            Debug.Log($"[Stage] Game だけで起動したので、ステージ「{catalog.Get(index).DisplayName}」（{sceneName}）を重ねて読み込みます。");
        }

        private void Start()
        {
            StageLayout.Active?.TryApply(true);
        }

        private void Update()
        {
            if (_loading != null && _loading.isDone)
            {
                _loading = null;
                StageLayout.Active?.TryApply(true);
            }
        }

        private void OnDestroy()
        {
            if (FortressNetworkBootstrap.AutoStartGate == _gate)
            {
                FortressNetworkBootstrap.AutoStartGate = null;
            }

            if (_hookedManager != null)
            {
                _hookedManager.OnClientStarted -= HandleClientStarted;
                _hookedManager = null;
            }
        }

        /// <summary>Game だけで起動したときのステージ: 起動引数 → 一覧の既定 → 読み込める最初の物。</summary>
        private static int ChooseStandaloneStage(StageCatalog catalog)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], StageArgument, StringComparison.OrdinalIgnoreCase))
                {
                    var fromArgs = catalog.Find(args[i + 1]);
                    if (catalog.IsPlayable(fromArgs))
                    {
                        return fromArgs;
                    }

                    Debug.LogWarning($"[Stage] 起動引数のステージ「{args[i + 1]}」が見つからないか、ビルドに入っていません。既定のステージを使います。");
                }
            }

            if (catalog.IsPlayable(catalog.defaultStageIndex))
            {
                return catalog.defaultStageIndex;
            }

            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog.IsPlayable(i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>このシーン（Game）がまだステージの中身（群衆）を持っているか（分ける前の古い Game）。</summary>
        private bool SceneHasOwnContent()
        {
            var swarms = FindObjectsByType<SwarmSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var swarm in swarms)
            {
                if (swarm.gameObject.scene == gameObject.scene)
                {
                    return true;
                }
            }

            return false;
        }

        // ───────── テスト用の bat（Game だけで通信）: 参加側の二重読み込みを防ぐ ─────────

        private void HookNetworkManager()
        {
            var manager = NetworkManager.Singleton != null ? NetworkManager.Singleton : FindFirstObjectByType<NetworkManager>();
            if (manager == null)
            {
                return;
            }

            _hookedManager = manager;
            manager.OnClientStarted += HandleClientStarted;
        }

        private void HandleClientStarted()
        {
            var manager = _hookedManager;
            if (manager == null || manager.SceneManager == null)
            {
                return;
            }

            // 参加したとき、ホストの読み込んでいるシーンが届く。ステージは自分でもう読み込んでいるので、もう一度は読み込まない
            // （Game は NGO 自身が「今のシーン」として読み込まずに使う）。ステージのシーンに NetworkObject は置かない約束なので、断っても困らない。
            var catalog = StageCatalog.Load();
            manager.SceneManager.VerifySceneBeforeLoading = (sceneIndex, sceneName, mode) =>
                catalog == null || !catalog.IsStageScene(sceneName) || !SceneManager.GetSceneByName(sceneName).isLoaded;
        }
    }
}
