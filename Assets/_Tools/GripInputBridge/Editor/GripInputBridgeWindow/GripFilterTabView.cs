using System.Collections.Generic;
using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Processing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.GripInputBridge.EditorTools
{
    /// <summary>
    /// 握力入力ブリッジの「なめらかさ」タブ。
    /// 同じ生の値を「メディアン＋EMA」と「メディアン＋1€」に通してグラフで重ねて比べ、
    /// 1€の調整値を動かし、選んだフィルターをゲームで使う設定として保存する。
    /// 比べている間はゲームの値には何もしない（保存ボタンを押したときだけゲームに効く）。
    /// </summary>
    internal sealed class GripFilterTabView : VisualElement
    {
        private const string GameSettingsAssetPath =
            "Assets/_Tools/GripInputBridge/Resources/" + GripFilterSettings.GameSettingsResourcePath + ".asset";

        private const string ChoiceNone = "なし（生の値のまま）";
        private const string ChoiceEma = "メディアン＋EMA";
        private const string ChoiceOneEuro = "メディアン＋1€（おすすめ）";

        private static readonly Color RawColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color EmaColor = new Color(0.36f, 0.62f, 0.98f);
        private static readonly Color OneEuroColor = new Color(1f, 0.55f, 0.25f);

        private readonly GripFilterGraph _graph;
        private readonly Label _gameStatusLabel;
        private readonly DropdownField _gameChoice;
        private readonly Slider _minCutoff;
        private readonly Slider _beta;
        private readonly Slider _derivativeCutoff;

        private int _playerIndex;
        private int _lastFrame = -1;
        private int _windowSize = 3;
        private float _emaAlpha = 0.3f;
        private IGripSignalFilter _emaFilter;
        private IGripSignalFilter _oneEuroFilter;

        public GripFilterTabView()
        {
            var root = new ScrollView();
            Add(root);
            style.flexGrow = 1;

            var title = new Label("なめらかさ（ノイズフィルター）");
            title.AddToClassList("gib-section-title");
            root.Add(title);

            var help = new Label(
                "同じ握力の値を2つのフィルターに通して重ねて見比べます。青＝メディアン＋EMA、橙＝メディアン＋1€、薄い線＝生の値。" +
                "線が細かく震えていなければ「揺れが取れている」、生の値の立ち上がりに近いほど「遅れが少ない」です。" +
                "見比べるだけではゲームは変わりません。下の「保存してゲームで使う」を押したときだけ効きます。");
            help.AddToClassList("gib-help-text");
            root.Add(help);

            if (!Application.isPlaying)
            {
                root.Add(new HelpBox("Play Modeで実行すると線が流れます。入力元を「記録再生」にすると、記録した握りで何度でも比べられます。", HelpBoxMessageType.Info));
            }

            var settings = LoadOrDefaultSettings(out var isSaved);
            _windowSize = settings.windowSize;
            _emaAlpha = settings.emaAlpha;

            _gameStatusLabel = new Label();
            _gameStatusLabel.AddToClassList("gib-status-label");
            root.Add(_gameStatusLabel);

            // ── グラフ ──
            var playerRow = new VisualElement();
            playerRow.AddToClassList("gib-wizard-row");
            playerRow.Add(new Label("見るプレイヤー:"));
            var playerOptions = new List<string> { "P1", "P2", "P3", "P4" };
            var playerDropdown = new DropdownField(playerOptions, 0) { tooltip = "グラフに出すプレイヤー（センサー）を選びます。" };
            playerDropdown.RegisterValueChangedCallback(evt =>
            {
                _playerIndex = Mathf.Max(0, playerOptions.IndexOf(evt.newValue));
                RebuildFilters();
            });
            playerRow.Add(playerDropdown);
            root.Add(playerRow);

            _graph = new GripFilterGraph(new[] { RawColor, EmaColor, OneEuroColor });
            root.Add(_graph);
            root.Add(BuildLegend());

            // ── 1€の調整（3つだけ） ──
            var tuning = new Foldout { text = "1€フィルターの調整", value = true };
            _minCutoff = AddSlider(tuning, "ゆっくり時のならし", 0.1f, 10f, settings.oneEuroMinCutoff,
                "小さいほど、止まっているときの震えが消えます（Hz）。小さすぎると、ゆっくり握るときに少し遅れます。おすすめ 1.5。");
            _beta = AddSlider(tuning, "速さへの反応", 0f, 20f, settings.oneEuroBeta,
                "大きいほど、ぐっと握った瞬間の遅れが減ります。大きすぎると、速い揺れまで通ります。おすすめ 2。");
            _derivativeCutoff = AddSlider(tuning, "速さの見積もりのならし", 0.1f, 10f, settings.oneEuroDerivativeCutoff,
                "LANで届く値の段々を「速い動き」と取り違えないようにします（Hz）。普段は 1 のままで大丈夫です。");
            var resetButton = new Button(ResetTuning) { text = "おすすめ値に戻す", tooltip = "3つの調整をおすすめ値（1.5 / 2 / 1）に戻します。" };
            tuning.Add(resetButton);
            root.Add(tuning);

            // ── ゲームで使う ──
            var useRow = new VisualElement();
            useRow.AddToClassList("gib-wizard-row");
            useRow.Add(new Label("ゲームで使う:"));
            var choices = new List<string> { ChoiceNone, ChoiceEma, ChoiceOneEuro };
            _gameChoice = new DropdownField(choices, Mathf.Max(0, choices.IndexOf(ChoiceFromType(isSaved ? settings.filterType : GripFilterType.None))))
            {
                tooltip = "ゲームで使うフィルターです。「保存してゲームで使う」を押すまでは変わりません。"
            };
            useRow.Add(_gameChoice);
            var saveButton = new Button(SaveForGame)
            {
                text = "保存してゲームで使う",
                tooltip = "選んだフィルターと1€の調整値を保存し、すぐにゲームへ反映します。ビルドしたゲームでも起動時から使われます。\n保存先: " + GameSettingsAssetPath
            };
            useRow.Add(saveButton);
            root.Add(useRow);

            UpdateGameStatus();
            RebuildFilters();

            RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.update += Sample);
            RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.update -= Sample);
        }

        private VisualElement BuildLegend()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 8;
            AddLegendItem(row, RawColor, "生の値");
            AddLegendItem(row, EmaColor, "メディアン＋EMA");
            AddLegendItem(row, OneEuroColor, "メディアン＋1€");
            return row;
        }

        private static void AddLegendItem(VisualElement parent, Color color, string text)
        {
            var swatch = new VisualElement();
            swatch.style.width = 14;
            swatch.style.height = 3;
            swatch.style.marginRight = 4;
            swatch.style.backgroundColor = color;
            swatch.style.alignSelf = Align.Center;
            parent.Add(swatch);

            var label = new Label(text);
            label.style.marginRight = 14;
            parent.Add(label);
        }

        private Slider AddSlider(VisualElement parent, string label, float min, float max, float value, string tooltip)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true, tooltip = tooltip };
            slider.RegisterValueChangedCallback(_ => RebuildFilters());
            parent.Add(slider);
            return slider;
        }

        private void ResetTuning()
        {
            var defaults = ScriptableObject.CreateInstance<GripFilterSettings>();
            _minCutoff.value = defaults.oneEuroMinCutoff;
            _beta.value = defaults.oneEuroBeta;
            _derivativeCutoff.value = defaults.oneEuroDerivativeCutoff;
            Object.DestroyImmediate(defaults);
        }

        // 比べる用のフィルターを作り直す（ゲームが使っているフィルターとは別の物）。
        private void RebuildFilters()
        {
            if (_minCutoff == null)
            {
                return;
            }

            _emaFilter = new MedianThenEmaFilter(_windowSize, _emaAlpha);
            _oneEuroFilter = new MedianThenOneEuroFilter(_windowSize, _minCutoff.value, _beta.value, _derivativeCutoff.value);
            _graph?.ClearHistory();
        }

        private void Sample()
        {
            if (!Application.isPlaying || _emaFilter == null)
            {
                return;
            }

            // 1フレームに1回だけ通す（同じ値を何度も通すとフィルターの記憶が壊れる）。
            if (Time.frameCount == _lastFrame)
            {
                return;
            }

            _lastFrame = Time.frameCount;
            var raw = GripInputBridge.Provider.GetRawValue(_playerIndex);
            _graph.Add(OneEuroFilter.MonotonicSeconds(), raw, _emaFilter.Filter(raw), _oneEuroFilter.Filter(raw));
        }

        private void SaveForGame()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GripFilterSettings>(GameSettingsAssetPath);
            var isNew = asset == null;
            if (isNew)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(GameSettingsAssetPath)?.Replace('\\', '/'));
                asset = ScriptableObject.CreateInstance<GripFilterSettings>();
            }

            Undo.RecordObject(asset, "ゲームで使うフィルターを変える");
            asset.filterType = TypeFromChoice(_gameChoice.value);
            asset.oneEuroMinCutoff = _minCutoff.value;
            asset.oneEuroBeta = _beta.value;
            asset.oneEuroDerivativeCutoff = _derivativeCutoff.value;

            if (isNew)
            {
                AssetDatabase.CreateAsset(asset, GameSettingsAssetPath);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            GripInputBridge.SetFilterSettings(asset);
            UpdateGameStatus();
            Debug.Log($"[GripInputBridge] ゲームで使うフィルターを「{_gameChoice.value}」にしました: {GameSettingsAssetPath}");
        }

        private void UpdateGameStatus()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GripFilterSettings>(GameSettingsAssetPath);
            _gameStatusLabel.text = asset == null
                ? "ゲームで今使っているフィルター: なし（生の値のまま。設定はまだ保存されていません）"
                : $"ゲームで今使っているフィルター: {ChoiceFromType(asset.filterType)}";
        }

        private static GripFilterSettings LoadOrDefaultSettings(out bool isSaved)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GripFilterSettings>(GameSettingsAssetPath);
            isSaved = asset != null;
            return isSaved ? asset : ScriptableObject.CreateInstance<GripFilterSettings>();
        }

        private static string ChoiceFromType(GripFilterType type)
        {
            return type switch
            {
                GripFilterType.None => ChoiceNone,
                GripFilterType.MedianThenExponentialMovingAverage => ChoiceEma,
                GripFilterType.MedianThenOneEuro => ChoiceOneEuro,
                _ => type.ToString()
            };
        }

        private static GripFilterType TypeFromChoice(string choice)
        {
            return choice switch
            {
                ChoiceEma => GripFilterType.MedianThenExponentialMovingAverage,
                ChoiceOneEuro => GripFilterType.MedianThenOneEuro,
                _ => GripFilterType.None
            };
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
