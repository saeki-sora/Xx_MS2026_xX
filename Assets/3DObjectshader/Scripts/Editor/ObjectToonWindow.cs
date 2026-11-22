using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 3Dオブジェクトのトゥーン調整ウィンドウ（Tools › 3Dオブジェクト トゥーン › 調整ウィンドウ）。
    /// デザイナーさんがシーンビューの横に細長く置いて、見ながら触るための1列の画面。
    /// 上から: 題名とプレビュー／いま調整している物／見た目の欄（よく触る8つ＋見出し）／物ごと／準備／説明の帯。
    /// この窓は並べるだけで、中身は Window/ の各欄、状態は ToonContext が持つ。
    /// 元の材質・シーン・Prefab は変えない（材質の差し替えは実行時とプレビュー中だけ）。
    /// </summary>
    public sealed class ObjectToonWindow : EditorWindow
    {
        private const string MenuRoot = "Tools/3Dオブジェクト トゥーン/";
        private const string StylePath = ObjectToonMaterials.Root + "/Scripts/Editor/Window/ObjectToonWindow.uss";
        private const string DefaultHint = "項目にマウスを乗せると、ここに説明が出ます。値はすぐシーンに出ます（Ctrl+Z で戻せます）。";

        [SerializeField] private bool previewWanted = true;

        private ToonContext _context;
        private ToonLookSection _look;
        private ToonObjectsSection _objects;
        private ToonSetupSection _setup;
        private Label _status;
        private Label _targetText;
        private VisualElement _targetBar;
        private Button _backToCommon;
        private Label _playNote;
        private Toggle _preview;
        private int _lastCommonDirty = -1;
        private bool _built;

        [MenuItem(MenuRoot + "調整ウィンドウ")]
        public static void Open()
        {
            var window = GetWindow<ObjectToonWindow>();
            window.titleContent = new GUIContent("3Dトゥーン調整");
            window.minSize = new Vector2(280, 360);
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ObjectToonEditPreview.IsOn = previewWanted;
            }
        }

        // 閉じたらプレビューも止める（ほかの作業中にトゥーンの材質が入ったままにならないように）。
        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ObjectToonEditPreview.IsOn = false;
            }

            _setup?.Dispose();
        }

        public void CreateGUI()
        {
            // マウスの下の部品の tooltip を下の帯に出す（StudioShell と同じ仕組み）。作り直しても1回だけ登録する。
            rootVisualElement.RegisterCallback<PointerOverEvent>(OnPointerOver, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_status != null)
                {
                    _status.text = DefaultHint;
                }
            });
            Build();
        }

        private void Build()
        {
            _setup?.Dispose();
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("sk-root");
            AddStyle(root, StudioShell.StylePath);
            AddStyle(root, StylePath);
            _built = false;

            if (ObjectToonMaterials.ToonShader == null || ObjectToonMaterials.SpriteShader == null)
            {
                root.Add(StudioUi.Note("シェーダー（" + ObjectToonMaterials.ShaderName + " / " + ObjectToonMaterials.SpriteShaderName +
                                       "）が見つかりません。コンソールにシェーダーのエラーが出ていないか確認してください。", NoteKind.Error));
                return;
            }

            var profile = ObjectToonMaterials.LoadProfile();
            if (profile == null || profile.commonSettings == null || profile.spriteCommonSettings == null)
            {
                root.Add(StudioUi.Empty("◐", "トゥーンの設定ファイルがまだありません",
                    "ステージの3Dの物を、トゥーン（アニメ塗り＋輪郭線）で描くための設定を作ります。元の材質・シーン・Prefab は変えません。",
                    "はじめる（設定ファイルを作る）", () =>
                    {
                        ObjectToonMaterials.EnsureProfile();
                        Build();
                    }));
                return;
            }

            _context = new ToonContext();
            root.Add(BuildHeader());
            root.Add(BuildTargetBar());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("tw-scroll");
            var body = new VisualElement();
            body.AddToClassList("tw-body");
            _look = new ToonLookSection(_context);
            _objects = new ToonObjectsSection(_context);
            _setup = new ToonSetupSection(_context);
            body.Add(_look);
            body.Add(_objects);
            body.Add(_setup);
            scroll.Add(body);
            root.Add(scroll);
            root.Add(BuildStatusBar());

            _context.TargetChanged += RefreshTargetBar;
            _context.RefreshMissing();
            RefreshTargetBar();
            RefreshPlayState();
            _lastCommonDirty = EditorUtility.GetDirtyCount(profile.commonSettings);
            _built = true;
        }

        private static void AddStyle(VisualElement root, string path)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }
            else
            {
                Debug.LogWarning("[ObjectToon] スタイルシートが見つかりません: " + path);
            }
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("tw-header");
            var title = new Label("トゥーン調整");
            title.AddToClassList("tw-header-title");
            header.Add(title);

            _preview = new Toggle("エディタでも見る")
            {
                value = ObjectToonEditPreview.IsOn,
                tooltip = "この窓を開いている間、再生しなくてもシーンビューをトゥーンで見ます（シーンのファイルには保存されません）。",
            };
            _preview.RegisterValueChangedCallback(e =>
            {
                previewWanted = e.newValue;
                ObjectToonEditPreview.IsOn = e.newValue;
            });
            header.Add(_preview);

            var wrap = new VisualElement();
            wrap.Add(header);
            _playNote = new Label("再生中です。ここで変えた値はすぐゲームに出て、止めたあとも残ります。");
            _playNote.AddToClassList("tw-play");
            wrap.Add(_playNote);
            return wrap;
        }

        private VisualElement BuildTargetBar()
        {
            _targetBar = new VisualElement();
            _targetBar.AddToClassList("tw-target");
            _targetBar.tooltip = "上の項目がどの物の値を動かすか。物ごとの調整は、下の「物ごと」で物を選んで「調整」を押すと始まります。";
            _targetText = new Label();
            _targetText.AddToClassList("tw-target-text");
            _targetBar.Add(_targetText);
            _backToCommon = StudioUi.Button("全部に戻る", () => _context.UseCommon(), "全部の物（共通の設定）の調整に戻ります。", small: true);
            _targetBar.Add(_backToCommon);
            return _targetBar;
        }

        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("sk-statusbar");
            bar.AddToClassList("tw-statusbar");
            bar.Add(StudioUi.Styled(new Label("?"), "sk-status-icon"));
            _status = StudioUi.Styled(new Label(DefaultHint), "sk-status-text");
            bar.Add(_status);
            return bar;
        }

        private void OnPointerOver(PointerOverEvent evt)
        {
            if (_status == null)
            {
                return;
            }

            for (var element = evt.target as VisualElement; element != null && element != rootVisualElement; element = element.parent)
            {
                if (!string.IsNullOrEmpty(element.tooltip))
                {
                    _status.text = element.tooltip;
                    return;
                }
            }

            _status.text = DefaultHint;
        }

        private void RefreshTargetBar()
        {
            if (_context == null || _targetBar == null)
            {
                return;
            }

            bool common = _context.IsCommon;
            _targetText.text = "調整中: " + _context.TargetLabel;
            _targetBar.EnableInClassList("tw-target--object", !common);
            _backToCommon.style.display = common ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshPlayState()
        {
            if (_playNote == null)
            {
                return;
            }

            bool playing = EditorApplication.isPlaying;
            _playNote.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
            _preview.SetEnabled(!playing);
            _preview.SetValueWithoutNotify(ObjectToonEditPreview.IsOn);
        }

        // 共通の設定が材質の画面（インスペクター）など、ほかの所で変わったときも配って表示を合わせる。
        private void OnInspectorUpdate()
        {
            if (!_built)
            {
                return;
            }

            var profile = ObjectToonMaterials.LoadProfile();
            if (profile == null || profile.commonSettings == null)
            {
                Build();
                return;
            }

            int dirty = EditorUtility.GetDirtyCount(profile.commonSettings);
            if (dirty != _lastCommonDirty)
            {
                _lastCommonDirty = dirty;
                ObjectToonMaterials.PushCommon(profile.commonSettings, profile.entries);
                _look?.Refresh();
            }
        }

        private void OnUndoRedo()
        {
            if (_built)
            {
                _context.OnExternalChange();
                RefreshTargetBar();
            }
        }

        private void OnSelectionChanged()
        {
            if (_built)
            {
                _objects.Rebuild();
            }
        }

        private void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            if (_built)
            {
                _context.RefreshMissing();
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            RefreshPlayState();
        }
    }
}
