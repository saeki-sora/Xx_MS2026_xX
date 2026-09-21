using System;
using System.Collections.Generic;
using System.IO;
using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Recording;
using MS2026.GripInputBridge.Transports;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.GripInputBridge.EditorTools
{
    /// <summary>
    /// 握力入力ブリッジの本番用エディタウィンドウ（Phase 6）。設計書 9章 参照。
    /// タブ構成: はじめに / シミュレータ / キャリブレーション / 診断。
    /// UI Toolkit(USS)でダーク基調＋プレイヤーカラー統一のデザインにしている。
    /// </summary>
    public sealed class GripInputBridgeWindow : EditorWindow
    {
        private const string UssPath = "Assets/_Tools/GripInputBridge/Editor/GripInputBridgeWindow/GripInputBridgeWindow.uss";
        private const string CalibrationProfileFolder = "Assets/_Tools/GripInputBridge/CalibrationProfiles";

        // P1=赤 / P2=青 / P3=緑 / P4=黄。他のツール(将来の会場運営ダッシュボード等)でも
        // 同じ配色を使い回す想定(設計書9.1)。
        private static readonly Color[] PlayerColors =
        {
            new Color(0.898f, 0.282f, 0.302f),
            new Color(0.203f, 0.549f, 0.949f),
            new Color(0.250f, 0.702f, 0.450f),
            new Color(0.949f, 0.800f, 0.153f)
        };

        private enum Tab
        {
            GettingStarted,
            Simulator,
            Calibration,
            Diagnostics
        }

        private Tab _currentTab = Tab.GettingStarted;
        private VisualElement _contentRoot;
        private readonly Dictionary<Tab, Button> _tabButtons = new Dictionary<Tab, Button>();

        // シミュレータタブ専用のトランスポート。入力元を実機/記録再生に切り替えても
        // ここに保持しておくことで、いつでもシミュレータへ戻せる。
        private readonly SimulatedGripTransport _simulatedTransport = new SimulatedGripTransport();

        private Button _recordButton;

        private readonly Label[] _valueLabels = new Label[GripInputBridgeConstants.PlayerCount];
        private readonly ProgressBar[] _valueBars = new ProgressBar[GripInputBridgeConstants.PlayerCount];
        private readonly VisualElement[] _statusDots = new VisualElement[GripInputBridgeConstants.PlayerCount];
        private readonly Label[] _statusLabels = new Label[GripInputBridgeConstants.PlayerCount];

        private readonly VisualElement[] _diagnosticsRows = new VisualElement[GripInputBridgeConstants.PlayerCount];
        private readonly Label[] _diagnosticsStatusLabels = new Label[GripInputBridgeConstants.PlayerCount];
        private readonly Label[] _diagnosticsValueLabels = new Label[GripInputBridgeConstants.PlayerCount];

        private int _calibrationPlayerIndex;
        private bool _isCalibrating;
        private float _calibrationRemainingSeconds;
        private float _calibrationMaxObserved;
        private double _lastEditorTime;
        private Label _calibrationCountdownLabel;
        private Label _calibrationResultLabel;
        private Button _calibrationStartButton;
        private Button _calibrationSaveButton;

        [MenuItem("Tools/握力入力ブリッジ/ウィンドウを開く")]
        private static void Open()
        {
            var window = GetWindow<GripInputBridgeWindow>();
            window.titleContent = new GUIContent("握力入力ブリッジ");
            window.minSize = new Vector2(560, 480);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("gib-root");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[GripInputBridge] スタイルシートが見つかりません: {UssPath}");
            }

            rootVisualElement.Add(BuildTabBar());
            rootVisualElement.Add(BuildInputSourceBar());

            _contentRoot = new VisualElement();
            _contentRoot.AddToClassList("gib-content-root");
            rootVisualElement.Add(_contentRoot);

            SwitchTab(Tab.GettingStarted);

            _lastEditorTime = EditorApplication.timeSinceStartup;
            rootVisualElement.schedule.Execute(OnTick).Every(100);
        }

        // ---------------------------------------------------------------
        // タブ切り替え
        // ---------------------------------------------------------------

        private VisualElement BuildTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("gib-tab-bar");

            AddTabButton(bar, Tab.GettingStarted, "はじめに", "ツールの使い方を3ステップで説明します。初めて開いた方はまずこちら。");
            AddTabButton(bar, Tab.Simulator, "シミュレータ", "実機の代わりにキーボードや波形プリセットで握力入力を確認・調整します。");
            AddTabButton(bar, Tab.Calibration, "キャリブレーション", "プレイヤーごとの最大握力を計測し、較正データを保存します。");
            AddTabButton(bar, Tab.Diagnostics, "診断", "4人分の接続状態や異常を確認します。本番運用中の監視にも使えます。");

            return bar;
        }

        private void AddTabButton(VisualElement parent, Tab tab, string label, string tooltip)
        {
            var button = new Button(() => SwitchTab(tab)) { text = label, tooltip = tooltip };
            button.AddToClassList("gib-tab-button");
            _tabButtons[tab] = button;
            parent.Add(button);
        }

        private void SwitchTab(Tab tab)
        {
            _currentTab = tab;

            foreach (var kvp in _tabButtons)
            {
                kvp.Value.EnableInClassList("gib-tab-button--active", kvp.Key == tab);
            }

            _contentRoot.Clear();
            switch (tab)
            {
                case Tab.GettingStarted:
                    _contentRoot.Add(BuildGettingStartedTab());
                    break;
                case Tab.Simulator:
                    _contentRoot.Add(BuildSimulatorTab());
                    break;
                case Tab.Calibration:
                    _contentRoot.Add(BuildCalibrationTab());
                    break;
                case Tab.Diagnostics:
                    _contentRoot.Add(BuildDiagnosticsTab());
                    break;
            }
        }

        // ---------------------------------------------------------------
        // 入力元バー（常に画面上部に表示）
        // ---------------------------------------------------------------

        private VisualElement BuildInputSourceBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("gib-input-source-bar");

            var label = new Label("入力元:")
            {
                tooltip = "握力の値をどこから取得するかを選びます。実機が無くても「シミュレータ」で動作を確認できます。"
            };
            bar.Add(label);

            var options = new List<string> { "シミュレータ" };
#if MS2026_GRIP_SERIAL_ENABLED
            options.Add("実機");
#endif
            options.Add("記録再生");

            var dropdown = new DropdownField(options, 0)
            {
                tooltip = "実機/シミュレータ/記録再生を、Play Modeを止めずにいつでも切り替えられます（ホットスワップ）。"
            };
            dropdown.RegisterValueChangedCallback(evt => OnInputSourceChanged(evt.newValue));
            bar.Add(dropdown);

            var recButton = new Button(ToggleRecording)
            {
                text = "● REC",
                tooltip = "プレイのログを記録します（設計書10章）。記録中の保存先はConsoleに表示されます。"
            };
            recButton.AddToClassList("gib-rec-button");
            bar.Add(recButton);
            _recordButton = recButton;

            // 既定はシミュレータ入力を使う。
            GripInputBridge.SetTransport(_simulatedTransport);

            return bar;
        }

        private void OnInputSourceChanged(string selection)
        {
            if (selection == "シミュレータ")
            {
                GripInputBridge.SetTransport(_simulatedTransport);
                return;
            }

            if (selection == "実機")
            {
                ActivateSerialTransport();
                return;
            }

            if (selection == "記録再生")
            {
                ActivateReplayTransport();
            }
        }

        private void ActivateSerialTransport()
        {
#if MS2026_GRIP_SERIAL_ENABLED
            var guids = AssetDatabase.FindAssets("t:GripDeviceConfig");
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "設定が見つかりません",
                    "GripDeviceConfigアセットが見つかりません。\n" +
                    "Projectウィンドウで右クリック > Create > MS2026 > Grip Input Bridge > Device Config で作成してください。",
                    "OK");
                return;
            }

            var configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var deviceConfig = AssetDatabase.LoadAssetAtPath<GripDeviceConfig>(configPath);
            GripInputBridge.SetTransport(new SerialGripTransport(deviceConfig));
#else
            EditorUtility.DisplayDialog(
                "実機通信は無効です",
                "Player Settings の Api Compatibility Level が「.NET Framework」でないため、実機通信は現在無効になっています。\n\n" +
                "有効化する手順:\n" +
                "1. Edit > Project Settings > Player > Other Settings > Api Compatibility Level を「.NET Framework」に変更\n" +
                "2. 同じ画面の Scripting Define Symbols に「MS2026_GRIP_SERIAL_ENABLED」を追加",
                "OK");
#endif
        }

        private void ActivateReplayTransport()
        {
            var directory = GripLogsDirectory();
            var path = EditorUtility.OpenFilePanel("再生するログファイルを選択", directory, "jsonl");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                GripInputBridge.SetTransport(new ReplayGripTransport(path));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("読み込みエラー", $"ログファイルの読み込みに失敗しました:\n{ex.Message}", "OK");
            }
        }

        private static string GripLogsDirectory()
        {
            var path = Path.Combine(Application.dataPath, "..", "GripLogs");
            return Directory.Exists(path) ? path : Application.dataPath;
        }

        private void ToggleRecording()
        {
            if (GripInputBridge.IsRecording)
            {
                GripInputBridge.StopRecording();
                _recordButton.text = "● REC";
                Debug.Log("[GripInputBridge] 記録を停止しました。");
            }
            else
            {
                GripInputBridge.StartRecording();
                _recordButton.text = "■ 停止";
                Debug.Log($"[GripInputBridge] 記録を開始しました: {GripInputBridge.CurrentRecordingFilePath}");
            }
        }

        // ---------------------------------------------------------------
        // はじめに タブ
        // ---------------------------------------------------------------

        private static VisualElement BuildGettingStartedTab()
        {
            var root = new ScrollView();

            var title = new Label("握力入力ブリッジ＆シミュレータへようこそ");
            title.AddToClassList("gib-section-title");
            root.Add(title);

            var intro = new Label(
                "このツールは、実機の握力センサーが無くてもゲーム開発・確認ができるようにするための入力基盤です。" +
                "以下の3ステップを覚えておけば迷わず使えます。");
            intro.AddToClassList("gib-help-text");
            root.Add(intro);

            root.Add(BuildStep(
                "① 入力元を選ぶ",
                "画面上部の「入力元」で、シミュレータ(キーボード)・実機・記録再生のいずれかを選びます。" +
                "実機がまだ無い開発中は「シミュレータ」のままでOKです。Play Modeで実行し、" +
                "P1はQキー、P2はWキー、P3はOキー、P4はPキーを押しっぱなしにすると握力が上昇します。"));

            root.Add(BuildStep(
                "② キャリブレーションを行う",
                "「キャリブレーション」タブで、プレイヤーごとに最大握力を計測して保存します。" +
                "これにより、子供と大人が対等に対戦できるようになります（握力の相対値化）。" +
                "本番当日は来場者ごとにこの手順を実行します。"));

            root.Add(BuildStep(
                "③ 診断で状態を確認する",
                "「診断」タブで、4人分の接続状態や異常(切断・値の張り付き等)を確認できます。" +
                "展示本番中はここを見て、スタッフが異常に気づけるようにします。"));

            return root;
        }

        private static VisualElement BuildStep(string title, string body)
        {
            var box = new VisualElement();
            box.AddToClassList("gib-getting-started-step");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("gib-getting-started-step-title");
            box.Add(titleLabel);

            var bodyLabel = new Label(body);
            bodyLabel.style.whiteSpace = WhiteSpace.Normal;
            box.Add(bodyLabel);

            return box;
        }

        // ---------------------------------------------------------------
        // シミュレータ タブ
        // ---------------------------------------------------------------

        private VisualElement BuildSimulatorTab()
        {
            var root = new ScrollView();

            var title = new Label("シミュレータ");
            title.AddToClassList("gib-section-title");
            root.Add(title);

            if (!Application.isPlaying)
            {
                root.Add(new HelpBox("Play Modeで実行すると値が表示されます。", HelpBoxMessageType.Info));
            }

            for (var i = 0; i < GripInputBridgeConstants.PlayerCount; i++)
            {
                root.Add(BuildPlayerPanel(i));
            }

            return root;
        }

        private VisualElement BuildPlayerPanel(int playerIndex)
        {
            var panel = new VisualElement();
            panel.AddToClassList("gib-player-panel");
            panel.style.borderLeftWidth = 3;
            panel.style.borderLeftColor = PlayerColors[playerIndex];

            var header = new VisualElement();
            header.AddToClassList("gib-player-header");

            var dot = new VisualElement { tooltip = "接続状態(色付き=接続中/灰色=未接続)" };
            dot.AddToClassList("gib-status-dot");
            _statusDots[playerIndex] = dot;
            header.Add(dot);

            var nameLabel = new Label($"P{playerIndex + 1}");
            nameLabel.AddToClassList("gib-player-name");
            nameLabel.style.color = PlayerColors[playerIndex];
            header.Add(nameLabel);

            var statusLabel = new Label("---");
            statusLabel.AddToClassList("gib-status-label");
            _statusLabels[playerIndex] = statusLabel;
            header.Add(statusLabel);

            panel.Add(header);

            var valueLabel = new Label("0%")
            {
                tooltip = "現在の握力(正規化値)。ゲームが実際に使う値です。"
            };
            valueLabel.AddToClassList("gib-value-label");
            _valueLabels[playerIndex] = valueLabel;
            panel.Add(valueLabel);

            var bar = new ProgressBar
            {
                lowValue = 0f,
                highValue = 1f,
                value = 0f,
                tooltip = "握力バー(0-100%)。"
            };
            _valueBars[playerIndex] = bar;
            panel.Add(bar);

            var presetRow = new VisualElement();
            presetRow.AddToClassList("gib-preset-row");
            presetRow.Add(new Label("波形プリセット:"));

            var presetOptions = new List<string> { "なし(キーボード操作)", "そっと", "しっかり", "渾身", "リズムテスト" };
            var presetDropdown = new DropdownField(presetOptions, 0)
            {
                tooltip = "キーボードの代わりに、企画書の「そっと/しっかり/渾身」やリズム判定テスト用の波形を自動再生します。"
            };
            var capturedIndex = playerIndex;
            presetDropdown.RegisterValueChangedCallback(evt =>
                _simulatedTransport.SetPreset(capturedIndex, PresetFromLabel(evt.newValue)));
            presetRow.Add(presetDropdown);
            panel.Add(presetRow);

            var keyHint = new Label(GetKeyHint(playerIndex));
            keyHint.AddToClassList("gib-status-label");
            panel.Add(keyHint);

            return panel;
        }

        private static GripWaveformPreset PresetFromLabel(string label)
        {
            return label switch
            {
                "そっと" => GripWaveformPreset.Soft,
                "しっかり" => GripWaveformPreset.Firm,
                "渾身" => GripWaveformPreset.Full,
                "リズムテスト" => GripWaveformPreset.RhythmTestPulse,
                _ => GripWaveformPreset.None
            };
        }

        private static string GetKeyHint(int playerIndex)
        {
            var keys = new[] { "Q", "W", "O", "P" };
            return playerIndex < keys.Length ? $"キーボード操作キー: {keys[playerIndex]}" : string.Empty;
        }

        private void RefreshSimulatorTab()
        {
            for (var i = 0; i < GripInputBridgeConstants.PlayerCount; i++)
            {
                if (_valueLabels[i] == null)
                {
                    continue;
                }

                var status = GripInputBridge.Provider.GetStatus(i);
                var value = GripInputBridge.Provider.GetGripValue(i);

                _valueLabels[i].text = $"{value * 100f:F0}%";
                _valueBars[i].value = value;
                _statusLabels[i].text = status.ToString();
                _statusDots[i].style.backgroundColor =
                    status == GripDeviceStatus.Connected ? PlayerColors[i] : new Color(0.4f, 0.4f, 0.4f);
            }
        }

        // ---------------------------------------------------------------
        // キャリブレーション タブ
        // ---------------------------------------------------------------

        private VisualElement BuildCalibrationTab()
        {
            var root = new ScrollView();

            var title = new Label("キャリブレーション");
            title.AddToClassList("gib-section-title");
            root.Add(title);

            var help = new Label(
                "プレイヤーを選び、そのプレイヤーが最大握力で3秒間握った状態を計測します。" +
                "これにより、子供と大人が対等に対戦できるよう握力を相対値化します。Play Modeで実行してください。");
            help.AddToClassList("gib-help-text");
            root.Add(help);

            var playerRow = new VisualElement();
            playerRow.AddToClassList("gib-wizard-row");
            playerRow.Add(new Label("プレイヤー:"));

            var playerOptions = new List<string> { "P1", "P2", "P3", "P4" };
            var playerDropdown = new DropdownField(playerOptions, _calibrationPlayerIndex)
            {
                tooltip = "較正するプレイヤーを選びます。"
            };
            playerDropdown.RegisterValueChangedCallback(evt =>
                _calibrationPlayerIndex = playerOptions.IndexOf(evt.newValue));
            playerRow.Add(playerDropdown);
            root.Add(playerRow);

            _calibrationCountdownLabel = new Label(string.Empty);
            _calibrationCountdownLabel.AddToClassList("gib-countdown-label");
            root.Add(_calibrationCountdownLabel);

            _calibrationStartButton = new Button(StartCalibration)
            {
                text = "計測開始(3秒間、最大握力で握ってください)",
                tooltip = "3秒間のカウントダウン中、最大握力の値を記録します。"
            };
            root.Add(_calibrationStartButton);

            _calibrationResultLabel = new Label(string.Empty);
            _calibrationResultLabel.AddToClassList("gib-help-text");
            root.Add(_calibrationResultLabel);

            _calibrationSaveButton = new Button(SaveCalibration)
            {
                text = "この結果を保存",
                tooltip = "計測した最大握力を、選択中のプレイヤーの較正データとして保存します。"
            };
            _calibrationSaveButton.SetEnabled(false);
            root.Add(_calibrationSaveButton);

            return root;
        }

        private void StartCalibration()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Play Modeが必要です", "キャリブレーションはPlay Mode中のみ実行できます。", "OK");
                return;
            }

            _isCalibrating = true;
            _calibrationRemainingSeconds = 3f;
            _calibrationMaxObserved = 0f;
            _calibrationSaveButton.SetEnabled(false);
            _calibrationResultLabel.text = string.Empty;
            _calibrationStartButton.SetEnabled(false);
        }

        private void TickCalibration(float deltaSeconds)
        {
            if (Application.isPlaying)
            {
                var raw = GripInputBridge.Provider.GetRawValue(_calibrationPlayerIndex);
                _calibrationMaxObserved = Mathf.Max(_calibrationMaxObserved, raw);
            }

            _calibrationRemainingSeconds -= deltaSeconds;
            if (_calibrationRemainingSeconds > 0f)
            {
                if (_calibrationCountdownLabel != null)
                {
                    _calibrationCountdownLabel.text = Mathf.CeilToInt(_calibrationRemainingSeconds).ToString();
                }

                return;
            }

            _isCalibrating = false;

            if (_calibrationCountdownLabel != null)
            {
                _calibrationCountdownLabel.text = "計測完了";
            }

            if (_calibrationResultLabel != null)
            {
                _calibrationResultLabel.text = $"計測結果(最大値): {_calibrationMaxObserved:F3}";
            }

            _calibrationStartButton?.SetEnabled(true);
            _calibrationSaveButton?.SetEnabled(true);
        }

        private void SaveCalibration()
        {
            EnsureCalibrationFolderExists();
            var assetPath = $"{CalibrationProfileFolder}/P{_calibrationPlayerIndex + 1}_CalibrationProfile.asset";

            var profile = AssetDatabase.LoadAssetAtPath<GripCalibrationProfile>(assetPath);
            var isNew = profile == null;
            if (isNew)
            {
                profile = GripCalibrationProfile.CreateDefault(_calibrationPlayerIndex);
            }

            profile.playerIndex = _calibrationPlayerIndex;
            profile.maxRawValue = Mathf.Max(_calibrationMaxObserved, 0.01f); // 0除算防止の最低値
            profile.lastCalibratedAt = DateTime.Now.ToString("o");

            if (isNew)
            {
                AssetDatabase.CreateAsset(profile, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            ApplyAllCalibrationProfilesFromDisk();

            _calibrationResultLabel.text = $"保存しました: {assetPath}";
            Debug.Log($"[GripInputBridge] P{_calibrationPlayerIndex + 1} の較正データを保存しました " +
                      $"(最大値={profile.maxRawValue:F3}): {assetPath}");
        }

        private static void EnsureCalibrationFolderExists()
        {
            if (AssetDatabase.IsValidFolder(CalibrationProfileFolder))
            {
                return;
            }

            var parts = CalibrationProfileFolder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void ApplyAllCalibrationProfilesFromDisk()
        {
            var profiles = new GripCalibrationProfile[GripInputBridgeConstants.PlayerCount];
            for (var i = 0; i < profiles.Length; i++)
            {
                var path = $"{CalibrationProfileFolder}/P{i + 1}_CalibrationProfile.asset";
                profiles[i] = AssetDatabase.LoadAssetAtPath<GripCalibrationProfile>(path);
            }

            GripInputBridge.SetCalibrationProfiles(profiles);
        }

        // ---------------------------------------------------------------
        // 診断 タブ
        // ---------------------------------------------------------------

        private VisualElement BuildDiagnosticsTab()
        {
            var root = new ScrollView();

            var title = new Label("診断");
            title.AddToClassList("gib-section-title");
            root.Add(title);

            var help = new Label("4人分の接続状態と現在値を一覧できます。展示本番中はここでスタッフが異常に気づけます。");
            help.AddToClassList("gib-help-text");
            root.Add(help);

            for (var i = 0; i < GripInputBridgeConstants.PlayerCount; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("gib-diagnostics-row");
                row.style.borderLeftWidth = 3;
                row.style.borderLeftColor = PlayerColors[i];

                var nameCell = new Label($"P{i + 1}");
                nameCell.AddToClassList("gib-diagnostics-cell");
                row.Add(nameCell);

                var statusCell = new Label("---") { tooltip = "接続状態。Connected以外は要確認です。" };
                statusCell.AddToClassList("gib-diagnostics-cell");
                _diagnosticsStatusLabels[i] = statusCell;
                row.Add(statusCell);

                var valueCell = new Label("---") { tooltip = "現在の握力値。" };
                valueCell.AddToClassList("gib-diagnostics-cell");
                _diagnosticsValueLabels[i] = valueCell;
                row.Add(valueCell);

                _diagnosticsRows[i] = row;
                root.Add(row);
            }

            return root;
        }

        private void RefreshDiagnosticsTab()
        {
            for (var i = 0; i < GripInputBridgeConstants.PlayerCount; i++)
            {
                if (_diagnosticsStatusLabels[i] == null)
                {
                    continue;
                }

                var status = GripInputBridge.Provider.GetStatus(i);
                var value = GripInputBridge.Provider.GetGripValue(i);

                _diagnosticsStatusLabels[i].text = status.ToString();
                _diagnosticsValueLabels[i].text = $"{value * 100f:F0}%";
                _diagnosticsRows[i].EnableInClassList("gib-diagnostics-row--warning", status != GripDeviceStatus.Connected);
            }
        }

        // ---------------------------------------------------------------
        // 定期更新
        // ---------------------------------------------------------------

        private void OnTick()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            if (_isCalibrating)
            {
                TickCalibration(delta);
            }

            if (_currentTab == Tab.Simulator)
            {
                RefreshSimulatorTab();
            }
            else if (_currentTab == Tab.Diagnostics)
            {
                RefreshDiagnosticsTab();
            }
        }
    }
}
