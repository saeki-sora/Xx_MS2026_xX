using System;
using UnityEngine;
using ShakeId = DDrive.Foundation.Identity.AssetId<DDrive.Runtime.CameraShake.ShakeMarker>;

namespace MS2026.Fortress.Cameras
{
    /// <summary>揺れの鳴らし方。</summary>
    public enum CameraShakeBackend
    {
        /// <summary>組み込みの簡易揺れ(アセット不要)。</summary>
        Simple,

        /// <summary>D-Driveの揺れアセット(CameraFx)。未設定なら簡易揺れで代用する。</summary>
        DDrive
    }

    /// <summary>1つの出来事に対する揺れの設定。</summary>
    [Serializable]
    public sealed class CameraShakeReaction
    {
        public bool enabled = true;

        [Tooltip("簡易揺れ(アセット不要) か D-Driveの揺れアセットか。")]
        public CameraShakeBackend backend = CameraShakeBackend.Simple;

        [Tooltip("揺れの強さの倍率。")]
        [Range(0f, 3f)]
        public float strength = 1f;

        [Tooltip("D-Driveの揺れアセット。Tools > D-Drive > Asset Browser で Shake を作成して選ぶ。")]
        public ShakeId ddriveShake;

        public SimpleShakeSettings simple = new SimpleShakeSettings();

        public bool UsesDDriveAsset => backend == CameraShakeBackend.DDrive && ddriveShake.IsValid;
    }

    /// <summary>1つの出来事に対する寄り(ズームパンチ)の設定。</summary>
    [Serializable]
    public sealed class CameraZoomReaction
    {
        public bool enabled = true;
        public ZoomPunchSettings punch = new ZoomPunchSettings();
    }

    /// <summary>出来事1種類ぶんのカメラ演出の設定(誰の画面で・どれくらいの頻度で・揺れ/寄り)。</summary>
    [Serializable]
    public sealed class CameraReaction
    {
        public CameraFeedbackEvent eventType;

        [Tooltip("OFFならこの出来事ではカメラ演出を鳴らさない。")]
        public bool enabled = true;

        [Tooltip("どの画面で鳴らすか。")]
        public CameraFeedbackAudience audience = CameraFeedbackAudience.Everyone;

        [Tooltip("「全員」のとき、関係者以外の画面での強さ。1で本人と同じ、0で本人だけ。")]
        [Range(0f, 1f)]
        public float othersStrength = 0.5f;

        [Tooltip("連続で起きたとき、この秒数の間は次を鳴らさない。コアへの連続ダメージなどで画面が揺れっぱなしになるのを防ぐ。")]
        [Min(0f)]
        public float cooldownSeconds;

        [Tooltip("出来事の大きさ(ダメージ量など)がこの値で強さ100%。0なら大きさに関係なく常に100%。")]
        [Min(0f)]
        public float fullStrengthAmount;

        [Tooltip("出来事が小さいときの最低の強さ。")]
        [Range(0f, 1f)]
        public float minStrength = 0.3f;

        public CameraShakeReaction shake = new CameraShakeReaction();
        public CameraZoomReaction zoom = new CameraZoomReaction();

        public static CameraReaction CreateDefault(CameraFeedbackEvent eventType)
        {
            var reaction = new CameraReaction { eventType = eventType };

            switch (eventType)
            {
                case CameraFeedbackEvent.SmashBallBroken:
                    reaction.audience = CameraFeedbackAudience.Everyone;
                    reaction.othersStrength = 0.4f;
                    reaction.shake.simple = new SimpleShakeSettings { amplitude = 0.35f, rollAmplitude = 0.8f, frequency = 16f, duration = 0.4f };
                    reaction.zoom.punch = new ZoomPunchSettings { zoomAmount = 0.12f, focusTowardSource = 0.25f, attackSeconds = 0.08f, holdSeconds = 0.12f, releaseSeconds = 0.4f };
                    break;

                case CameraFeedbackEvent.CoreDamaged:
                    reaction.audience = CameraFeedbackAudience.Everyone;
                    reaction.othersStrength = 1f;
                    reaction.cooldownSeconds = 0.25f;
                    reaction.fullStrengthAmount = 10f;
                    reaction.minStrength = 0.3f;
                    reaction.shake.simple = new SimpleShakeSettings { amplitude = 0.15f, rollAmplitude = 0.3f, frequency = 22f, duration = 0.2f };
                    reaction.zoom.enabled = false;
                    break;

                case CameraFeedbackEvent.TurretOverheated:
                    reaction.audience = CameraFeedbackAudience.RelatedPlayerOnly;
                    reaction.shake.simple = new SimpleShakeSettings { amplitude = 0.2f, rollAmplitude = 0.5f, frequency = 20f, duration = 0.3f };
                    reaction.zoom.punch = new ZoomPunchSettings { zoomAmount = 0.06f, focusTowardSource = 0.15f, attackSeconds = 0.06f, holdSeconds = 0.05f, releaseSeconds = 0.3f };
                    break;
            }

            return reaction;
        }

        /// <summary>
        /// この画面(viewer)での強さ(0なら鳴らさない)。関係者のいない出来事は全員に同じ強さで鳴らす。
        /// 全体視点(-1)は常に「関係者以外」として扱う。
        /// </summary>
        public float StrengthFor(in CameraFeedbackRequest request, int viewer)
        {
            if (!enabled)
            {
                return 0f;
            }

            var intensity = IntensityFromAmount(request);
            if (!request.HasRelatedPlayer)
            {
                return intensity;
            }

            var isRelated = viewer == request.RelatedPlayer;
            var audienceScale = audience switch
            {
                CameraFeedbackAudience.RelatedPlayerOnly => isRelated ? 1f : 0f,
                CameraFeedbackAudience.OthersOnly => isRelated ? 0f : 1f,
                _ => isRelated ? 1f : othersStrength
            };

            return intensity * audienceScale;
        }

        private float IntensityFromAmount(in CameraFeedbackRequest request)
        {
            if (fullStrengthAmount <= 0f || !request.HasAmount)
            {
                return 1f;
            }

            return Mathf.Lerp(minStrength, 1f, Mathf.Clamp01(request.Amount / fullStrengthAmount));
        }
    }
}
