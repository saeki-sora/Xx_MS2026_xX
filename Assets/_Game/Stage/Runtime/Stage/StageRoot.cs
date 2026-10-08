using System.Collections;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Loop;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// シーンに1つ置く「今のステージ」の置き場所。ステージ（StageSet）のPrefabを子に持ち、
    /// 光・カメラの見え方・反応の共通設定・BGMを、そのステージのものにする。
    /// 背景の反応に必要な部品（透かす目印・レーザーの熱・群衆の押し寄せ）も自動で付く。
    /// 編集中に「シーンに出す」のはステージ背景スタジオが行う（Prefabのつながりを保ったまま置くため）。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(StageFocusPoints))]
    [RequireComponent(typeof(StageLaserHeatScanner))]
    [RequireComponent(typeof(StageSwarmPressure))]
    public sealed class StageRoot : MonoBehaviour
    {
        public const string SunName = "[Stage] Sun";

        [Tooltip("今シーンに出ているステージ。")]
        public StageSet current;

        [Tooltip("今のステージのPrefabから作った、シーン内の実体。")]
        public GameObject instance;

        [Tooltip("ステージの光の設定を当てる太陽（Directional Light）。空なら自動で作る。")]
        public Light sun;

        [Tooltip("ONなら、Play開始時にステージのBGMを流す。")]
        public bool playBgmOnStart = true;

        public static StageRoot Active { get; private set; }

        public StageLookProfile LookProfile => current != null ? current.lookProfile : null;

        private void OnEnable()
        {
            Active = this;
            StageLookProfile.Current.ApplyGlobals();
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
                // 背景が無くなったら群衆の影の2回目の描画も止める（描画の負荷を元に戻す）。
                Shader.SetGlobalFloat(StageShaderIds.SilhouetteEnabled, 0f);
            }
        }

        private void Start()
        {
            if (!Application.isPlaying || current == null)
            {
                return;
            }

            ApplyEnvironment(applyCamera: false);
            if (playBgmOnStart && current.bgm.IsValid)
            {
                StartCoroutine(PlayBgmWhenReady(current));
            }
        }

        /// <summary>光・反応の設定（と必要ならカメラの見え方）を今のステージのものにする。</summary>
        public void ApplyEnvironment(bool applyCamera)
        {
            if (current == null)
            {
                return;
            }

            if (current.lighting.apply)
            {
                current.lighting.ApplyTo(EnsureSun());
            }

            StageLookProfile.Current.ApplyGlobals();

            var rig = FortressCameraRig.Active != null ? FortressCameraRig.Active : FindFirstObjectByType<FortressCameraRig>();
            if (applyCamera && current.cameraPreset != null && rig != null)
            {
                if (Application.isPlaying)
                {
                    rig.SetPreset(current.cameraPreset, rig.viewerSwitchBlendSeconds);
                }
                else
                {
                    rig.preset = current.cameraPreset;
                }
            }
        }

        /// <summary>
        /// 実行中にステージを入れ替える（ステージ選択などの将来用）。
        /// ネット対戦中（接続中）は入れ替えない（false を返す）。壊せる壁の同期表（DestructibleNetworkHub）が
        /// 接続時のシーンで作られるため、対戦中に入れ替えると PC ごとに壁の状態がずれる。対戦中に入れ替えるには、
        /// 全員が同じタイミングで入れ替えて同期表を作り直す仕組みが別途必要（マルチプレイの宿題）。
        /// </summary>
        public bool SwitchAtRuntime(StageSet next)
        {
            if (FortressNet.IsNetworked)
            {
                // 壊せる壁の同期表が古いままになり、PCごとに壁の状態がずれるので、対戦中は入れ替えない。
                Debug.LogWarning("[Stage] ネット対戦中はステージを入れ替えられません（壊せる壁の同期がずれるため）。接続前に入れ替えてください。");
                return false;
            }

            if (instance != null)
            {
                Destroy(instance);
            }

            current = next;
            instance = next != null && next.stagePrefab != null ? Instantiate(next.stagePrefab, transform) : null;
            ApplyEnvironment(applyCamera: true);
            return true;
        }

        /// <summary>太陽を探す。無ければ子に作る。</summary>
        public Light EnsureSun()
        {
            if (sun != null)
            {
                return sun;
            }

            var existing = transform.Find(SunName);
            if (existing != null && existing.TryGetComponent(out sun))
            {
                return sun;
            }

            var go = new GameObject(SunName);
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            return sun;
        }

        private static IEnumerator PlayBgmWhenReady(StageSet stage)
        {
            // D-Driveの準備（カタログ登録）が終わる前に鳴らすと無音の代役になるので待つ。
            const float timeout = 10f;
            var waited = 0f;
            while (DDriveRuntimeBootstrap.Instance == null || !DDriveRuntimeBootstrap.Instance.IsReady)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > timeout)
                {
                    yield break;
                }

                yield return null;
            }

            Audio.PlayBgm(stage.bgm);
        }
    }
}
