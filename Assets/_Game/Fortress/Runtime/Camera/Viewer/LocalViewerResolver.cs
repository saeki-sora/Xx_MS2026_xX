using System.Collections.Generic;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 情報源を優先順に問い合わせ、最初に答えが返ってきた視点番号を採用する。どれも答えなければ既定値。
    /// 既定の優先順: デバッグ上書き → 通信 → 起動引数 → 既定値。
    /// </summary>
    public sealed class LocalViewerResolver
    {
        public const string FallbackLabel = "既定値(リグの設定)";

        private readonly List<IViewerIndexProvider> _providers;

        public LocalViewerResolver(IEnumerable<IViewerIndexProvider> providers)
        {
            _providers = new List<IViewerIndexProvider>(providers);
        }

        public static LocalViewerResolver CreateDefault()
        {
            return new LocalViewerResolver(new IViewerIndexProvider[]
            {
                new DebugViewerIndexProvider(),
                new NetworkViewerIndexProvider(),
                new LaunchArgsViewerIndexProvider()
            });
        }

        public IReadOnlyList<IViewerIndexProvider> Providers => _providers;

        /// <summary>高い優先度で割り込ませたいときは先頭に、控えめにしたいときは末尾に追加する。</summary>
        public void AddProvider(IViewerIndexProvider provider, bool highestPriority = false)
        {
            if (provider == null || _providers.Contains(provider))
            {
                return;
            }

            if (highestPriority)
            {
                _providers.Insert(0, provider);
            }
            else
            {
                _providers.Add(provider);
            }
        }

        public int Resolve(int fallbackViewer, out string sourceLabel)
        {
            foreach (var provider in _providers)
            {
                if (provider.TryGetViewerIndex(out var viewer) && ViewerIndex.IsValid(viewer))
                {
                    sourceLabel = provider.Label;
                    return viewer;
                }
            }

            sourceLabel = FallbackLabel;
            return ViewerIndex.IsValid(fallbackViewer) ? fallbackViewer : ViewerIndex.Overview;
        }
    }
}
