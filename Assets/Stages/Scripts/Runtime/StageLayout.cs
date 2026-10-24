using System;
using System.Collections;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Stages
{
    /// <summary>
    /// ステージのシーンに 1 つ置く「配置の目印」（`[Stage] Layout`）。共通のゲームのシーン（Game）にあるコアと砲台 4 台を、
    /// このステージの目印の位置・向きへ動かし、カメラの視点をこのステージの物にする。
    /// ステージと Game のどちらが先に読み込まれても、両方そろった時点で 1 回当てはめる（<see cref="TryApply"/>）。
    /// ステージのシーンだけを開いて Play したときは、Game を後から重ねて読み込む（ステージを作る人がそのまま試せるように）。
    /// 目印は位置を決めるだけで、通信はしない（全員の PC が同じステージを読み込むので同じ位置になる）。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/ステージ/配置の目印（コア・砲台・カメラ）")]
    public sealed class StageLayout : MonoBehaviour
    {
        public const int TurretCount = 4;

        [Tooltip("このステージの情報（無くても動く。ルーレットの名前などに使う）。")]
        public StageDefinition definition;

        [Tooltip("コアの位置の目印（空ならコアは動かさない）。")]
        public Transform coreAnchor;

        [Tooltip("砲台の位置・向きの目印（P1〜P4。空の席は動かさない）。向きは目印の Z の回転。")]
        public Transform[] turretAnchors = new Transform[TurretCount];

        [Tooltip("このステージのカメラの視点（空なら Game のカメラのまま）。砲台・コアの位置を変えたら、視点も合わせて作ること。")]
        public CameraViewPreset cameraPreset;

        [Tooltip("ステージの中身（群衆・敵を出す係・障害物・経路）の親。ステージが Game より先に読み込まれたときは、Game がそろうまで止めておく" +
                 "（先に敵が湧いたり、コアが無いまま試合が始まったりしないように）。")]
        public GameObject content;

        [Tooltip("ON なら、このシーンだけを開いて Play したとき、共通のゲームのシーン（Game）を重ねて読み込む。")]
        public bool loadCommonSceneWhenAlone = true;

        private bool _applied;
        private bool _holding;

        /// <summary>今読み込まれているステージの目印（無ければ null）。</summary>
        public static StageLayout Active { get; private set; }

        /// <summary>目印がコア・砲台に当てはめられたとき（ステージが変わるたび）。</summary>
        public static event Action<StageLayout> Applied;

        /// <summary>コア・砲台に当てはめ終わったか。</summary>
        public bool IsApplied => _applied;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                DisableCameraRigsInThisScene();
            }

            // ロビー経由ではステージを Game より先に読み込む。Game が来るまで中身を止めておき、そろったら動かす（TryApply）。
            // この部品は中身より先に動く（DefaultExecutionOrder -900）ので、止めた中身の Awake（群衆の準備など）はまだ呼ばれていない。
            if (Application.isPlaying && content != null && content.activeSelf && !IsCommonScenePresent())
            {
                content.SetActive(false);
                _holding = true;
            }
        }

        private void OnEnable()
        {
            Active = this;
            _applied = false;
            TryApply();
            if (Application.isPlaying && loadCommonSceneWhenAlone && !_applied)
            {
                StartCoroutine(LoadCommonSceneIfAlone());
            }
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void Start()
        {
            // カメラは Start の頃に動き出すので、もう一度当てはめる（同じ値なので何度呼んでもよい）。
            if (Application.isPlaying)
            {
                TryApply(true);
            }
        }

        /// <summary>
        /// コア・砲台・カメラに当てはめる。コアか砲台がまだ無い（Game が読み込まれていない）ときは何もしない。
        /// force なら、当てはめ済みでももう一度当てはめる。当てはめたら true。
        /// </summary>
        public bool TryApply(bool force = false)
        {
            if (!Application.isPlaying || (_applied && !force))
            {
                return _applied;
            }

            var core = FindFirstObjectByType<CoreCrystalController>();
            var turrets = FindObjectsByType<LaserTurret>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (core == null && turrets.Length == 0)
            {
                return false;
            }

            if (core != null && coreAnchor != null)
            {
                core.transform.position = Flat(coreAnchor.position, core.transform.position.z);
            }

            for (var i = 0; i < turrets.Length; i++)
            {
                var turret = turrets[i];
                var anchor = AnchorFor(turret.playerIndex);
                if (turret == null || anchor == null)
                {
                    continue;
                }

                turret.transform.SetPositionAndRotation(
                    Flat(anchor.position, turret.transform.position.z),
                    Quaternion.Euler(0f, 0f, anchor.eulerAngles.z));
            }

            ApplyCamera();

            // 止めておいた中身を動かす（コア・砲台を動かした後なので、群衆・経路は正しいコアの位置で始まる）。
            if (_holding && content != null)
            {
                _holding = false;
                content.SetActive(true);
            }

            // ステージのシーンを「アクティブなシーン」（主役）にする。環境光・霧・空はアクティブなシーンの物が使われ、Play 中に作られる物もそこに入る。
            // 見た目の設定はステージごとに持ち、共通の Game は触らない（ユーザー決定 2026-10-24）。本番・Game だけ・ステージだけ、どの流れでも同じになる。
            MakeStageSceneActive();

            // コアの位置が変わったので、敵の経路を作り直す。
            if (EnemyNavigation.Current is NavigationField field)
            {
                field.MarkDirty();
            }

            var first = !_applied;
            _applied = true;
            if (first)
            {
                Applied?.Invoke(this);
            }

            return true;
        }

        /// <summary>その席の砲台の目印（無ければ null）。</summary>
        public Transform AnchorFor(int playerIndex)
        {
            return turretAnchors != null && playerIndex >= 0 && playerIndex < turretAnchors.Length ? turretAnchors[playerIndex] : null;
        }

        private void ApplyCamera()
        {
            if (cameraPreset == null)
            {
                return;
            }

            var rig = FindCommonCameraRig();
            if (rig == null || rig.preset == cameraPreset)
            {
                return;
            }

            rig.SetPreset(cameraPreset, 0f);
            rig.SnapToTarget();
        }

        private void MakeStageSceneActive()
        {
            var scene = gameObject.scene;
            if (scene.IsValid() && scene.isLoaded && SceneManager.GetActiveScene() != scene)
            {
                SceneManager.SetActiveScene(scene);
            }
        }

        /// <summary>共通のシーン（Game）のカメラリグ。ステージのシーンに紛れ込んだリグは使わない。</summary>
        private FortressCameraRig FindCommonCameraRig()
        {
            var active = FortressCameraRig.Active;
            if (active != null && active.gameObject.scene != gameObject.scene)
            {
                return active;
            }

            foreach (var rig in FindObjectsByType<FortressCameraRig>(FindObjectsSortMode.None))
            {
                if (rig.gameObject.scene != gameObject.scene)
                {
                    return rig;
                }
            }

            return null;
        }

        /// <summary>
        /// ステージのシーンに置かれたカメラリグを止める。カメラは Game にだけ置く約束（2 つ動くと、どちらの視点になるか決まらない）。
        /// 要塞デザイナーでステージだけを開いて「カメラリグをセットアップ」を押すと、ここに入ってしまう（2026-10-24 に Stage2_Bath で起きた）。
        /// </summary>
        private void DisableCameraRigsInThisScene()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var rig in root.GetComponentsInChildren<FortressCameraRig>(true))
                {
                    Debug.LogWarning($"[Stage] ステージのシーン「{gameObject.scene.name}」にカメラリグ「{rig.name}」があります。使わずに止めます" +
                                     "（カメラは Game にだけ置き、ステージの視点は「配置の目印」の cameraPreset に入れること。要塞デザイナー › カメラで消せます）。", rig);
                    rig.gameObject.SetActive(false);
                }
            }
        }

        private static Vector3 Flat(Vector3 position, float z) => new Vector3(position.x, position.y, z);

        /// <summary>このシーンだけで Play したとき、1 フレーム待ってから（同時に読み込まれる物を待つ）Game を重ねて読み込む。</summary>
        private IEnumerator LoadCommonSceneIfAlone()
        {
            yield return null;
            if (_applied || IsNetworkRunning())
            {
                yield break;
            }

            var catalog = StageCatalog.Load();
            var common = catalog != null ? catalog.commonSceneName : "Game";
            if (string.IsNullOrEmpty(common) || SceneManager.GetSceneByName(common).isLoaded)
            {
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(common))
            {
                Debug.LogWarning($"[Stage] 共通のゲームのシーン「{common}」がビルドのシーン一覧に無いので、重ねて読み込めません（Tools › ステージ › ビルドのシーン一覧を整える）。");
                yield break;
            }

            Debug.Log($"[Stage] ステージだけで Play したので、共通のゲームのシーン「{common}」を重ねて読み込みます。");
            SceneManager.LoadSceneAsync(common, LoadSceneMode.Additive);
        }

        /// <summary>共通のゲームのシーン（コアか砲台）がもう読み込まれているか。</summary>
        private static bool IsCommonScenePresent()
        {
            return FindFirstObjectByType<CoreCrystalController>() != null
                   || FindObjectsByType<LaserTurret>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0;
        }

        internal static bool IsNetworkRunning()
        {
            var manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening;
        }

        private void OnDrawGizmos()
        {
            if (coreAnchor != null)
            {
                Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.9f);
                Gizmos.DrawWireSphere(coreAnchor.position, 1.2f);
            }

            if (turretAnchors == null)
            {
                return;
            }

            for (var i = 0; i < turretAnchors.Length; i++)
            {
                var anchor = turretAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                Gizmos.color = FortressColors.PlayerColor(i);
                Gizmos.DrawWireSphere(anchor.position, 0.7f);
                Gizmos.DrawLine(anchor.position, anchor.position + anchor.up * 1.6f);
            }
        }
    }
}
