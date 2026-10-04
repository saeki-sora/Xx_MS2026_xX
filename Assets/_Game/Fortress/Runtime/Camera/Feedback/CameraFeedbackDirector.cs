using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// ゲーム内の出来事(スマッシュボール破壊・コア被弾・オーバーヒート)を拾い、設定(<see cref="CameraFeedbackConfig"/>)に従って
    /// 「この画面の持ち主に関係あるか」「連続しすぎていないか」を判断してから、揺れ・寄りを鳴らす。
    /// 各PCは自分の画面(=自分の視点)だけを揺らすので、通信で演出を同期する必要はない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFeedbackDirector : MonoBehaviour
    {
        [Tooltip("出来事ごとの演出設定。")]
        public CameraFeedbackConfig config;

        [Tooltip("演出を重ねるカメラリグ。未設定ならシーンで有効なリグを使う。")]
        public FortressCameraRig rig;

        private readonly List<ICameraFeedbackSource> _sources = new List<ICameraFeedbackSource>
        {
            new SmashBallFeedbackSource(),
            new CoreDamageFeedbackSource(),
            new TurretOverheatFeedbackSource()
        };

        private readonly Dictionary<CameraFeedbackEvent, float> _lastPlayedTime = new Dictionary<CameraFeedbackEvent, float>();
        private bool _sourcesEnabled;

        /// <summary>演出を鳴らした(または鳴らさなかった)ときに通知する。ツールの履歴表示用。強さ0は「この画面では鳴らさない」判断。</summary>
        public event Action<CameraFeedbackRequest, float> Played;

        private FortressCameraRig Rig => rig != null ? rig : FortressCameraRig.Active;

        private void OnEnable()
        {
            _sourcesEnabled = true;
            foreach (var source in _sources)
            {
                source.Enable(Raise);
            }
        }

        private void OnDisable()
        {
            _sourcesEnabled = false;
            foreach (var source in _sources)
            {
                source.Disable();
            }
        }

        /// <summary>出来事の種類を増やしたとき用。有効中に追加してもすぐ購読を始める。</summary>
        public void AddSource(ICameraFeedbackSource source)
        {
            if (source == null || _sources.Contains(source))
            {
                return;
            }

            _sources.Add(source);
            if (_sourcesEnabled)
            {
                source.Enable(Raise);
            }
        }

        /// <summary>演出を依頼する。ゲームコードから直接呼んでもよい。</summary>
        public void Raise(CameraFeedbackRequest request) => Play(request, false);

        /// <summary>ignoreCooldownはツールの試し撃ち用。</summary>
        public void Play(CameraFeedbackRequest request, bool ignoreCooldown)
        {
            var reaction = config != null ? config.Get(request.EventType) : null;
            if (reaction == null)
            {
                return;
            }

            if (!ignoreCooldown && IsCoolingDown(request.EventType, reaction.cooldownSeconds))
            {
                return;
            }

            var currentRig = Rig;
            var viewer = currentRig != null ? currentRig.CurrentViewer : ViewerIndex.Overview;
            var strength = reaction.StrengthFor(request, viewer) * config.globalStrength;

            if (strength > 0f)
            {
                _lastPlayedTime[request.EventType] = Time.unscaledTime;
                CameraReactionPlayer.Play(reaction, request, strength, currentRig);
            }

            Played?.Invoke(request, strength);
        }

        private bool IsCoolingDown(CameraFeedbackEvent eventType, float cooldownSeconds)
        {
            return cooldownSeconds > 0f
                   && _lastPlayedTime.TryGetValue(eventType, out var last)
                   && Time.unscaledTime - last < cooldownSeconds;
        }
    }
}
