using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    // 練習のシーン（本物の砲台・敵・スマッシュボール・スキル）を、キャラ選択の上に重ねて読み込む（2026-11-13）。
    // ・通信中: Host が通信経由で読み込む・外す（全員のPCに届き、本番と同じく4人で同期する）。Client は届くのを待つだけ。
    //   ほかのシーンの出し入れの最中なら、終わるまで待ってやり直す（ゲームの読み込みも同じく待ってくれる）。
    // ・通信していない（エディタで試すとき）: 自分で読み込む・外す。
    // ・係（PracticeSceneDriver）が現れたら、練習の様子と自分の席を渡す
    // ・練習が終わったら（全員できた・時間切れ）、すぐ外す（ゲームのシーンと重ならないように）。画面が消えたときにも外す
    // 読み込んでいる間・外した後は、フィールドの枠を紺の板で覆う。
    public sealed partial class PracticeScreen
    {
        private bool _sceneMode;
        private string _sceneName;
        private bool _sceneLoadPending;
        private bool _sceneUnloadPending;
        private bool _sceneUnloadRequested;
        private AsyncOperation _sceneLoad;
        private PracticeSceneDriver _boundDriver;
        private Image _holeCover;
        private Text _holeText;

        private static NetworkManager Net
        {
            get
            {
                var manager = NetworkManager.Singleton;
                return manager != null && manager.IsListening ? manager : null;
            }
        }

        private void BeginScene(string sceneName)
        {
            _sceneMode = true;
            _sceneName = sceneName;
            var net = Net;
            if (net != null)
            {
                // 通信中: Host だけが読み込む（Client には届く）。
                _sceneLoadPending = net.IsServer && !SceneManager.GetSceneByName(sceneName).isLoaded;
                return;
            }

            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                // 前の練習の残り（中止された直後など）。外してから読み直す。
                SceneManager.UnloadSceneAsync(sceneName);
            }

            _sceneLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        private void TickScene()
        {
            if (!_sceneMode)
            {
                return;
            }

            TryNetworkLoad();
            TryNetworkUnload();

            var driver = PracticeSceneDriver.Current;
            if (driver != null && driver != _boundDriver && !_sceneUnloadRequested)
            {
                _boundDriver = driver;
                driver.Bind(_board, _local);
            }

            // 練習が終わったら、すぐ外す（Host がゲームを読み込む前に）。
            if (_board.finished)
            {
                UnloadScene();
            }

            if (_holeCover != null)
            {
                var visible = !_sceneUnloadRequested && driver != null && driver.ViewCamera != null;
                var a = Mathf.MoveTowards(_holeCover.color.a, visible ? 0f : 1f, Time.unscaledDeltaTime * 4f);
                _holeCover.color = new Color(_holeCover.color.r, _holeCover.color.g, _holeCover.color.b, a);
                _holeText.enabled = a > 0.5f && !_sceneUnloadRequested;
            }
        }

        private void TryNetworkLoad()
        {
            if (!_sceneLoadPending || _sceneUnloadRequested)
            {
                return;
            }

            var net = Net;
            if (net == null || !net.IsServer)
            {
                _sceneLoadPending = false;
                return;
            }

            var status = net.SceneManager.LoadScene(_sceneName, LoadSceneMode.Additive);
            if (status == SceneEventProgressStatus.Started)
            {
                _sceneLoadPending = false;
            }
            else if (status != SceneEventProgressStatus.SceneEventInProgress)
            {
                Debug.LogWarning($"[HowToPlay] 練習のシーン「{_sceneName}」を読み込めませんでした（{status}）。ビルドの一覧に入っているか確かめてください。");
                _sceneLoadPending = false;
            }
        }

        private void TryNetworkUnload()
        {
            if (!_sceneUnloadPending)
            {
                return;
            }

            var net = Net;
            var scene = SceneManager.GetSceneByName(_sceneName);
            if (net == null || !net.IsServer || !scene.isLoaded)
            {
                // まだ読み込み中なら、読み終わるのを待ってから外す。
                if (net != null && net.IsServer && !scene.isLoaded && !_sceneLoadPending)
                {
                    return;
                }

                _sceneUnloadPending = false;
                return;
            }

            var status = net.SceneManager.UnloadScene(scene);
            if (status == SceneEventProgressStatus.Started || status != SceneEventProgressStatus.SceneEventInProgress)
            {
                _sceneUnloadPending = false;
            }
        }

        private void UnloadScene()
        {
            if (_sceneUnloadRequested)
            {
                return;
            }

            _sceneUnloadRequested = true;
            _sceneLoadPending = false;
            RestoreCamera();
            var net = Net;
            if (net != null)
            {
                // 通信中: Host が外す（Client には届く）。
                _sceneUnloadPending = net.IsServer;
                TryNetworkUnload();
                return;
            }

            var scene = SceneManager.GetSceneByName(_sceneName);
            if (scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
            else if (_sceneLoad != null && !_sceneLoad.isDone)
            {
                // まだ読み込み中。読み終わったらすぐ外す。
                var name = _sceneName;
                _sceneLoad.completed += _ => SceneManager.UnloadSceneAsync(name);
            }
        }

        private void EndScene()
        {
            if (!_sceneMode)
            {
                return;
            }

            UnloadScene();
            if (_sceneUnloadPending)
            {
                // 画面が消えた（中止など）。外し終わるまで、見えない係に続きを任せる。
                var go = new GameObject("[HowToPlay] Practice Scene Unloader");
                DontDestroyOnLoad(go);
                go.AddComponent<PracticeSceneUnloader>().Begin(_sceneName);
            }
        }
    }

    /// <summary>練習の画面が先に消えたとき、Host が練習のシーンを外し終えるまで続ける係（数秒で消える）。</summary>
    internal sealed class PracticeSceneUnloader : MonoBehaviour
    {
        private string _scene;
        private float _deadline;

        public void Begin(string sceneName)
        {
            _scene = sceneName;
            _deadline = Time.unscaledTime + 10f;
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            var scene = SceneManager.GetSceneByName(_scene);
            if (manager == null || !manager.IsListening || !manager.IsServer || !scene.isLoaded || Time.unscaledTime > _deadline)
            {
                Destroy(gameObject);
                return;
            }

            var status = manager.SceneManager.UnloadScene(scene);
            if (status != SceneEventProgressStatus.SceneEventInProgress)
            {
                Destroy(gameObject);
            }
        }
    }
}
