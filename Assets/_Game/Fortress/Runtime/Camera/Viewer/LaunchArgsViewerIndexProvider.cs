using System;
using MS2026.Fortress.Net;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 起動引数 -fortress-player 0-3 が指定されていれば、接続前からその視点にする
    /// （1台のPCで -fortress-tile と併用すると、4つのウィンドウでそれぞれの視点を並べて確認できる）。
    /// </summary>
    public sealed class LaunchArgsViewerIndexProvider : IViewerIndexProvider
    {
        private readonly int? _playerIndex;

        public LaunchArgsViewerIndexProvider()
            : this(Environment.GetCommandLineArgs())
        {
        }

        public LaunchArgsViewerIndexProvider(string[] args)
        {
            _playerIndex = FortressLaunchArgs.Parse(args).PlayerIndex;
        }

        public string Label => "起動引数(-fortress-player)";

        public bool TryGetViewerIndex(out int viewer)
        {
            viewer = _playerIndex ?? ViewerIndex.Overview;
            return _playerIndex.HasValue;
        }
    }
}
