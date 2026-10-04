namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 「この画面は誰の視点か」を答える情報源。通信・起動引数・デバッグ上書きなどを差し替え可能にするための口。
    /// 新しい決め方(ロビーでの席選択など)を足すときは、これを実装して <see cref="LocalViewerResolver"/> に追加する。
    /// </summary>
    public interface IViewerIndexProvider
    {
        /// <summary>ツールに表示する名前（例: 通信 / 起動引数）。</summary>
        string Label { get; }

        /// <summary>視点番号(<see cref="ViewerIndex"/>)が決まっていればtrue。</summary>
        bool TryGetViewerIndex(out int viewer);
    }
}
