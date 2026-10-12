using System.IO;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>Play 中の変更の一覧（書き戻し先ごと）。行ごとに「残す」チェック・見る・捨てる。</summary>
    public sealed class PlayChangeListPanel : VisualElement
    {
        private int _version = -1;
        private bool _wasPlaying;

        public void Refresh()
        {
            if (_version == UiPlayChangeStore.Version && _wasPlaying == EditorApplication.isPlaying)
            {
                return;
            }

            _version = UiPlayChangeStore.Version;
            _wasPlaying = EditorApplication.isPlaying;
            Rebuild();
        }

        private void Rebuild()
        {
            Clear();
            var items = UiPlayChangeStore.Items;
            if (items.Count == 0)
            {
                Add(StudioUi.Empty("◉", "変更はありません",
                    "Play 中に UI の部品（文字・位置・色・動き・画面の設定など）を変えると、ここに出ます。インスペクター・シーンビュー・このスタジオのどこで変えても記録されます。"));
                return;
            }

            foreach (var group in UiPlayChangeList.GroupByDestination(items))
            {
                var target = group[0].target;
                var isPrefab = target.destination == UiPlayDestination.Prefab;
                var title = string.IsNullOrEmpty(target.assetPath) ? "（書き戻し先なし）" : Path.GetFileName(target.assetPath);
                var header = StudioUi.Row(
                    StudioUi.Chip(isPrefab ? "Prefab" : "シーン", isPrefab ? ChipKind.Info : ChipKind.Accent,
                        isPrefab ? "一覧の画面の Prefab ファイルに書き戻します。" : "シーンに置いた物に書き戻します（シーンを開いていなければ、開いて保存して閉じます）。"),
                    StudioUi.Styled(new Label(title), "sk-card-title"));
                header.tooltip = target.assetPath;
                header.style.marginTop = 10;
                Add(header);

                foreach (var change in group)
                {
                    Add(BuildRow(change));
                }
            }
        }

        private static VisualElement BuildRow(UiPlayChange change)
        {
            var row = StudioUi.Styled(new VisualElement(), "sk-list-item");
            row.style.height = StyleKeyword.Auto;
            row.style.paddingTop = row.style.paddingBottom = 3;

            var keep = new Toggle { value = change.keep && change.CanApply, tooltip = "チェックした変更を「選んだ物を残す」で書き戻します。" };
            keep.SetEnabled(change.CanApply);
            keep.RegisterValueChangedCallback(e =>
            {
                change.keep = e.newValue;
                UiPlayChangeStore.Touch();
            });
            keep.style.marginRight = 6;
            row.Add(keep);

            var text = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };
            text.Add(StudioUi.Styled(new Label($"{change.target.where}  ›  {change.target.componentLabel}"), "sk-list-label"));
            var value = StudioUi.Styled(new Label($"{change.label}:  {UiPlayValueText.Short(change.before)}  →  {UiPlayValueText.Short(change.after)}"), "sk-list-meta");
            value.tooltip = $"欄: {change.propertyPath}\n前: {change.before}\n後: {change.after}";
            text.Add(value);
            if (!change.CanApply)
            {
                var problem = new Label("残せません: " + change.problem);
                problem.style.color = new Color(1f, 0.78f, 0.34f);
                problem.style.whiteSpace = WhiteSpace.Normal;
                text.Add(problem);
            }

            row.Add(text);
            row.Add(StudioUi.Button("見る", () => Reveal(change), "Play 中なら動いている部品を、止めたあとなら書き戻し先のファイルを選びます。", small: true));
            row.Add(StudioUi.Button("✕", () => UiPlayChangeStore.Remove(change), "この変更を捨てます（一覧から外します）。", small: true));
            return row;
        }

        private static void Reveal(UiPlayChange change)
        {
            var live = UiPlayChangeRecorder.LiveTarget(change);
            if (live != null)
            {
                var go = live is Component component ? component.gameObject : live as GameObject;
                Selection.activeObject = go != null ? go : live;
                EditorGUIUtility.PingObject(Selection.activeObject);
                return;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(change.target.assetPath);
            if (asset != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }
    }
}
