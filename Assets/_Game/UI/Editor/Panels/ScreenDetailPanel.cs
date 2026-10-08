using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>選んだ画面の設定（名前・レイヤー・起動時に開く・戻るで閉じる・モーダル・出る／消える動き）と、試すボタン。</summary>
    public sealed class ScreenDetailPanel : VisualElement
    {
        private static readonly (string path, string label)[] Fields =
        {
            ("screenId", "画面の名前（プログラム用）"),
            ("displayName", "表示名"),
            ("layer", "レイヤー（段）"),
            ("memo", "メモ"),
            ("openOnStart", "起動時に開く"),
            ("closeOnBack", "戻る（Esc）で閉じる"),
            ("modal", "モーダル（下を押せなくする）"),
            ("modalDim", "後ろにかける色"),
            ("showMotion", "画面が出るときの動き"),
            ("hideMotion", "画面が消えるときの動き"),
            ("firstSelected", "最初に選ばれる部品"),
            ("onShown", "出終わったとき"),
            ("onHidden", "消え終わったとき")
        };

        private readonly UiStudioContext _context;
        private readonly VisualElement _body;
        private readonly Label _previewNote;
        private UiScreen _bound;

        public ScreenDetailPanel(UiStudioContext context)
        {
            _context = context;
            AddToClassList("sk-detail");
            _body = new VisualElement();
            Add(_body);
            _previewNote = StudioUi.Styled(new Label(), "sk-muted");
        }

        public void Refresh()
        {
            if (_bound != _context.SelectedScreen)
            {
                _bound = _context.SelectedScreen;
                Rebuild();
            }

            if (_bound != null)
            {
                var instance = PreviewInstance();
                _previewNote.text = Application.isPlaying
                    ? "Play中: 「開く」「閉じる」で実際に開け閉めできます。"
                    : instance != null ? "「出る／消える動きを試す」で、Playせずに動きを確かめられます。" : "動きを試すには、先に「この画面を編集」で開いてください。";
            }
        }

        private void Rebuild()
        {
            _body.Clear();
            _body.Unbind();
            if (_bound == null)
            {
                _body.Add(StudioUi.Empty("▢", "画面を選んでください", "左の一覧で選ぶと、ここに設定が出ます。新しく作るときは上の雛形から。"));
                return;
            }

            _body.Add(StudioUi.PageTitle(_bound.Label));
            var persistent = EditorUtility.IsPersistent(_bound);
            _body.Add(StudioUi.Row(
                StudioUi.Button(persistent ? "プロジェクトで見る" : "ヒエラルキーで見る", Reveal,
                    persistent ? "この画面の Prefab ファイルを、プロジェクトウィンドウで光らせて選びます。" : "シーンに置かれたこの画面を、ヒエラルキーで光らせて選びます。", small: true),
                StudioUi.Styled(new Label(persistent ? AssetDatabase.GetAssetPath(_bound) : "シーンに直接置かれた画面"), "sk-muted")));
            _body.Add(StudioUi.Row(
                StudioUi.Button("この画面を編集", () => UiStudioContext.OpenForEditing(_bound), "Prefab の編集画面で開きます。シーンビューで部品を動かしたり、「レイヤー」「動き」ページで整理できます。", primary: true),
                StudioUi.Button("出る動きを試す", () => Preview(true), "Playせずに、画面が出るときの動き（子の部品の動きも含む）を再生します。", small: true),
                StudioUi.Button("消える動きを試す", () => Preview(false), "Playせずに、画面が消えるときの動きを再生します。", small: true),
                StudioUi.Button("開く", () => WithRoot(r => r.Open(_bound.screenId)), "Play中に、この画面を開きます。", small: true),
                StudioUi.Button("閉じる", () => WithRoot(r => r.Close(_bound.screenId)), "Play中に、この画面を閉じます。", small: true)));
            _body.Add(_previewNote);

            var so = new SerializedObject(_bound);
            var card = StudioUi.Card();
            foreach (var (path, label) in Fields)
            {
                var property = so.FindProperty(path);
                if (property != null)
                {
                    card.Add(new PropertyField(property, label) { tooltip = property.tooltip });
                }
            }

            card.Bind(so);
            _body.Add(card);

            _body.Add(new TmpFontPanel(() => _bound));

            _body.Add(StudioUi.Row(
                StudioUi.Button("複製", Duplicate, "この画面をコピーして、別の名前の新しい画面を作ります。", small: true),
                StudioUi.Button("一覧から外す", RemoveFromCatalog, "画面の一覧から外します（Prefab ファイルは消しません）。", small: true),
                StudioUi.Button("この画面を削除", Delete, "画面を一覧から外し、Prefab ファイルも消します（ファイルはパソコンのごみ箱へ移るので、そこから戻せます）。シーンに直接置いた画面はシーンから消します（Ctrl+Z で戻せます）。", small: true)));
        }

        private void Reveal()
        {
            if (EditorUtility.IsPersistent(_bound))
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = _bound.gameObject;
                EditorGUIUtility.PingObject(_bound.gameObject);
                return;
            }

            Selection.activeGameObject = _bound.gameObject;
            EditorGUIUtility.PingObject(_bound.gameObject);
        }

        private void Delete()
        {
            var screen = _bound;
            if (!EditorUtility.IsPersistent(screen))
            {
                if (EditorUtility.DisplayDialog("画面を削除", $"シーンに置かれた「{screen.Label}」を消します（Ctrl+Z で戻せます）。", "削除する", "やめる"))
                {
                    Undo.DestroyObjectImmediate(screen.gameObject);
                    _context.MarkScreensDirty();
                    _context.SelectScreen(null);
                }

                return;
            }

            var path = AssetDatabase.GetAssetPath(screen);
            var placed = UiScreenAssets.FindSceneInstances(screen);
            var message = $"「{screen.Label}」を削除します。\n\n・画面の一覧から外します\n・Prefab ファイル（{path}）をパソコンのごみ箱へ移します（ごみ箱から戻せます）";
            if (placed.Count > 0)
            {
                message += $"\n・シーンに置かれている {placed.Count} 個も消します";
            }

            if (!EditorUtility.DisplayDialog("画面を削除", message, "削除する", "やめる"))
            {
                return;
            }

            UiScreenAssets.Delete(screen, placed);
            _context.MarkScreensDirty();
            _context.SelectScreen(null);
        }

        /// <summary>動きを試せる実体（Prefab の編集画面で開いている物か、シーンの物）。</summary>
        private UiScreen PreviewInstance()
        {
            if (_bound == null)
            {
                return null;
            }

            if (!EditorUtility.IsPersistent(_bound))
            {
                return _bound;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == AssetDatabase.GetAssetPath(_bound) && stage.prefabContentsRoot.TryGetComponent<UiScreen>(out var staged)
                ? staged
                : null;
        }

        private void Preview(bool show)
        {
            var instance = PreviewInstance();
            if (instance == null)
            {
                UiStudioContext.OpenForEditing(_bound);
                return;
            }

            UiMotionPreview.PlayScreen(instance, show);
        }

        private static void WithRoot(System.Action<UiRoot> action)
        {
            if (Application.isPlaying && UiRoot.Active != null)
            {
                action(UiRoot.Active);
            }
        }

        private void Duplicate()
        {
            if (!EditorUtility.IsPersistent(_bound))
            {
                return;
            }

            var source = AssetDatabase.GetAssetPath(_bound);
            var id = UiTemplateFactory.UniqueId(_bound.screenId);
            var path = StudioAssets.UniquePath(UiStudioSetup.ScreensFolder, $"{id}.prefab");
            AssetDatabase.CopyAsset(source, path);
            var copy = AssetDatabase.LoadAssetAtPath<UiScreen>(path);
            copy.screenId = id;
            copy.displayName = _bound.displayName + "（コピー）";
            EditorUtility.SetDirty(copy);
            var catalog = UiStudioSetup.Catalog;
            Undo.RecordObject(catalog, "画面を複製");
            catalog.screens.Add(copy);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            _context.MarkScreensDirty();
            _context.SelectScreen(copy);
        }

        private void RemoveFromCatalog()
        {
            var catalog = UiStudioSetup.Catalog;
            if (!catalog.screens.Contains(_bound) ||
                !EditorUtility.DisplayDialog("一覧から外す", $"「{_bound.Label}」を画面の一覧から外します。名前で開けなくなります（Prefab は残ります）。", "外す", "やめる"))
            {
                return;
            }

            Undo.RecordObject(catalog, "画面を一覧から外す");
            catalog.screens.Remove(_bound);
            EditorUtility.SetDirty(catalog);
            _context.MarkScreensDirty();
            _context.SelectScreen(null);
        }
    }
}
