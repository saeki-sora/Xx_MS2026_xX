using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MS2026.Stage
{
    /// <summary>光ページの「まぶしさを抑える」の種類（トーンマップ）。</summary>
    public enum StageToneMapping
    {
        [InspectorName("使わない")] Off,
        [InspectorName("自然（色をあまり変えない）")] Natural,
        [InspectorName("映画風（コントラスト強め）")] Cinematic,
    }

    /// <summary>
    /// 光ページの「まぶしさを抑える」「全体の明るさ（露出）」を画面全体に効かせる。
    /// StageRoot の子に保存されない Volume（[Stage] Look Volume）を作り、必要なときだけカメラの後処理を ON にする。
    /// 明るい所が真っ白・真緑に張り付く（白飛び）のを、色の階調を残したまま抑えるため（2026-10-16）。
    /// </summary>
    public static class StageLookVolume
    {
        public const string ObjectName = "[Stage] Look Volume";

        /// <summary>設定に合わせて作る・更新する・止める。光を当てないとき（apply OFF）は止める。</summary>
        public static void Apply(StageRoot root, StageLightingSettings settings)
        {
            if (root == null)
            {
                return;
            }

            var wanted = settings != null && settings.apply && NeedsPostProcess(settings);
            var volume = Find(root);
            if (!wanted)
            {
                if (volume != null)
                {
                    volume.enabled = false;
                }

                return;
            }

            volume ??= Create(root);
            var profile = volume.sharedProfile;
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }

            if (!profile.TryGet(out ColorAdjustments color))
            {
                color = profile.Add<ColorAdjustments>(true);
            }

            tonemapping.active = settings.toneMapping != StageToneMapping.Off;
            tonemapping.mode.Override(settings.toneMapping == StageToneMapping.Cinematic ? TonemappingMode.ACES : TonemappingMode.Neutral);
            color.active = Mathf.Abs(settings.exposure) > 0.001f;
            color.postExposure.Override(settings.exposure);
            volume.enabled = true;
            EnablePostProcessing(root);
        }

        /// <summary>画面全体の後処理が要るか（まぶしさを抑える、または明るさを変える）。</summary>
        public static bool NeedsPostProcess(StageLightingSettings settings) =>
            settings.toneMapping != StageToneMapping.Off || Mathf.Abs(settings.exposure) > 0.001f;

        private static Volume Find(StageRoot root)
        {
            var child = root.transform.Find(ObjectName);
            return child != null && child.TryGetComponent(out Volume volume) && volume.sharedProfile != null ? volume : null;
        }

        private static Volume Create(StageRoot root)
        {
            var old = root.transform.Find(ObjectName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // シーンにもPrefabにも保存しない（毎回ステージの設定から作り直す）。
            var go = new GameObject(ObjectName) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            go.transform.SetParent(root.transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = ObjectName;
            profile.hideFlags = HideFlags.DontSave;
            volume.sharedProfile = profile;
            return volume;
        }

        /// <summary>同じシーンのカメラの後処理を ON にする（OFF のままだと Volume が効かない）。</summary>
        private static void EnablePostProcessing(StageRoot root)
        {
            if (!root.gameObject.scene.IsValid())
            {
                return;
            }

            foreach (var top in root.gameObject.scene.GetRootGameObjects())
            {
                foreach (var camera in top.GetComponentsInChildren<Camera>(true))
                {
                    var data = camera.GetUniversalAdditionalCameraData();
                    if (data != null && !data.renderPostProcessing)
                    {
                        data.renderPostProcessing = true;
                    }
                }
            }
        }
    }
}
