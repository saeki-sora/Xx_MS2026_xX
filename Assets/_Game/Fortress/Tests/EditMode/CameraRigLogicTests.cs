using MS2026.Fortress.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class CameraRigLogicTests
    {
        private const float Tolerance = 1e-3f;

        private sealed class FixedProvider : IViewerIndexProvider
        {
            private readonly int? _viewer;

            public FixedProvider(string label, int? viewer)
            {
                Label = label;
                _viewer = viewer;
            }

            public string Label { get; }

            public bool TryGetViewerIndex(out int viewer)
            {
                viewer = _viewer ?? ViewerIndex.Overview;
                return _viewer.HasValue;
            }
        }

        [Test]
        public void 視点は答えを持つ最初の情報源で決まる()
        {
            var resolver = new LocalViewerResolver(new IViewerIndexProvider[]
            {
                new FixedProvider("A", null),
                new FixedProvider("B", 2),
                new FixedProvider("C", 1)
            });

            Assert.AreEqual(2, resolver.Resolve(ViewerIndex.Overview, out var source));
            Assert.AreEqual("B", source);
        }

        [Test]
        public void どの情報源も答えなければ既定値になる()
        {
            var resolver = new LocalViewerResolver(new IViewerIndexProvider[] { new FixedProvider("A", null) });

            Assert.AreEqual(3, resolver.Resolve(3, out var source));
            Assert.AreEqual(LocalViewerResolver.FallbackLabel, source);
            Assert.AreEqual(ViewerIndex.Overview, resolver.Resolve(99, out _));
        }

        [Test]
        public void 起動引数のプレイヤー番号を視点として使う()
        {
            var provider = new LaunchArgsViewerIndexProvider(new[] { "game.exe", "-fortress-player", "2" });

            Assert.IsTrue(provider.TryGetViewerIndex(out var viewer));
            Assert.AreEqual(2, viewer);
            Assert.IsFalse(new LaunchArgsViewerIndexProvider(new[] { "game.exe" }).TryGetViewerIndex(out _));
        }

        [Test]
        public void 本人だけの演出は他の画面では鳴らない()
        {
            var reaction = CameraReaction.CreateDefault(CameraFeedbackEvent.TurretOverheated);
            reaction.audience = CameraFeedbackAudience.RelatedPlayerOnly;
            var request = new CameraFeedbackRequest(CameraFeedbackEvent.TurretOverheated, 1);

            Assert.AreEqual(1f, reaction.StrengthFor(request, 1), Tolerance);
            Assert.AreEqual(0f, reaction.StrengthFor(request, 0), Tolerance);
            Assert.AreEqual(0f, reaction.StrengthFor(request, ViewerIndex.Overview), Tolerance);
        }

        [Test]
        public void 全員向けの演出は関係者以外だけ弱められる()
        {
            var reaction = CameraReaction.CreateDefault(CameraFeedbackEvent.SmashBallBroken);
            reaction.audience = CameraFeedbackAudience.Everyone;
            reaction.othersStrength = 0.25f;
            var request = new CameraFeedbackRequest(CameraFeedbackEvent.SmashBallBroken, 3);

            Assert.AreEqual(1f, reaction.StrengthFor(request, 3), Tolerance);
            Assert.AreEqual(0.25f, reaction.StrengthFor(request, 0), Tolerance);
        }

        [Test]
        public void 関係者のいない出来事は大きさに応じた強さで全員に鳴る()
        {
            var reaction = CameraReaction.CreateDefault(CameraFeedbackEvent.CoreDamaged);
            reaction.audience = CameraFeedbackAudience.RelatedPlayerOnly;
            reaction.fullStrengthAmount = 10f;
            reaction.minStrength = 0.2f;

            var small = new CameraFeedbackRequest(CameraFeedbackEvent.CoreDamaged, ViewerIndex.Overview, amount: 0f);
            var large = new CameraFeedbackRequest(CameraFeedbackEvent.CoreDamaged, ViewerIndex.Overview, amount: 50f);

            Assert.AreEqual(0.2f, reaction.StrengthFor(small, 2), Tolerance);
            Assert.AreEqual(1f, reaction.StrengthFor(large, 2), Tolerance);
        }

        [Test]
        public void 無効な演出は鳴らない()
        {
            var reaction = CameraReaction.CreateDefault(CameraFeedbackEvent.SmashBallBroken);
            reaction.enabled = false;

            Assert.AreEqual(0f, reaction.StrengthFor(new CameraFeedbackRequest(CameraFeedbackEvent.SmashBallBroken, 0), 0), Tolerance);
        }

        [Test]
        public void 寄りの演出は終わると元の広さに戻り自動で取り除かれる()
        {
            var settings = new ZoomPunchSettings { zoomAmount = 0.5f, focusTowardSource = 0f, attackSeconds = 0.1f, holdSeconds = 0.1f, releaseSeconds = 0.1f };
            var stack = new CameraModifierStack();
            stack.Add(new ZoomPunchModifier(settings, null, 1f));
            var baseView = CameraViewSettings.Default;

            var peak = stack.Apply(baseView, 0.15f);
            Assert.AreEqual(baseView.orthographicSize * 0.5f, peak.orthographicSize, Tolerance);

            stack.Apply(baseView, 1f);
            Assert.AreEqual(0, stack.Count);
            Assert.AreEqual(baseView.orthographicSize, stack.Apply(baseView, 0.1f).orthographicSize, Tolerance);
        }

        [Test]
        public void 演出は上限を超えると古い物から捨てられる()
        {
            var stack = new CameraModifierStack(2);
            var settings = new SimpleShakeSettings { duration = 10f };
            stack.Add(new SimpleShakeModifier(settings, 1f));
            stack.Add(new SimpleShakeModifier(settings, 1f));
            stack.Add(new SimpleShakeModifier(settings, 1f));

            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void 視点の遷移は指定秒数で目標に到達する()
        {
            var blend = new CameraViewBlend();
            var from = CameraViewSettings.Default;
            var to = from;
            to.center = new Vector2(10f, 0f);

            blend.Begin(from, 1f);
            var halfway = blend.Evaluate(to, 0.5f);
            Assert.AreEqual(5f, halfway.center.x, Tolerance);
            Assert.IsTrue(blend.IsBlending);

            var end = blend.Evaluate(to, 0.6f);
            Assert.IsTrue(end.Approximately(to));
            Assert.IsFalse(blend.IsBlending);
        }

        [Test]
        public void プリセットはプレイヤー分が足りなければ全体視点で代用する()
        {
            var preset = ScriptableObject.CreateInstance<CameraViewPreset>();
            try
            {
                preset.players = new CameraViewSettings[1];
                preset.overview.center = new Vector2(7f, 7f);

                Assert.AreEqual(new Vector2(7f, 7f), preset.GetView(3).center);

                var view = CameraViewSettings.Default;
                view.center = new Vector2(1f, 2f);
                preset.SetView(3, view);
                Assert.AreEqual(ViewerIndex.PlayerCount, preset.players.Length);
                Assert.AreEqual(new Vector2(1f, 2f), preset.GetView(3).center);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void 演出設定は全ての出来事の項目を自動で揃える()
        {
            var config = ScriptableObject.CreateInstance<CameraFeedbackConfig>();
            try
            {
                Assert.IsTrue(config.EnsureAllEvents());
                Assert.IsFalse(config.EnsureAllEvents());
                Assert.IsNotNull(config.Get(CameraFeedbackEvent.CoreDamaged));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
