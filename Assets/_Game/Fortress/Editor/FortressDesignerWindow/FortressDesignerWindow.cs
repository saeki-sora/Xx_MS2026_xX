using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「握れ、灼ける前に」の土台システム（砲台配置／握力とレーザーの連動／敵の湧き）を
    /// インスペクターの隣にタブとしてドッキングできる専用ツール。設計はGripInputBridgeWindowに揃えてある。
    /// タブ構成: はじめに / 砲台配置 / 敵ウェーブ / テスト。
    /// </summary>
    public sealed class FortressDesignerWindow : EditorWindow
    {
        private const string UssPath =
            "Assets/_Game/Fortress/Editor/FortressDesignerWindow/FortressDesignerWindow.uss";

        private enum Tab
        {
            GettingStarted,
            Turrets,
            EnemyWaves,
            Navigation,
            Swarm,
            Test
        }

        private Tab _currentTab = Tab.GettingStarted;
        private VisualElement _contentRoot;
        private readonly Dictionary<Tab, Button> _tabButtons = new Dictionary<Tab, Button>();

        private GettingStartedTabView _gettingStartedTab;
        private TurretsTabView _turretsTab;
        private EnemyWavesTabView _enemyWavesTab;
        private NavigationTabView _navigationTab;
        private SwarmTabView _swarmTab;
        private TestTabView _testTab;

        [MenuItem("Tools/要塞/要塞デザイナーを開く")]
        private static void Open()
        {
            var window = GetWindow<FortressDesignerWindow>();
            window.titleContent = new GUIContent("要塞デザイナー");
            window.minSize = new Vector2(620, 520);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("fd-root");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[FortressDesigner] スタイルシートが見つかりません: {UssPath}");
            }

            _gettingStartedTab = new GettingStartedTabView(this);
            _turretsTab = new TurretsTabView();
            _enemyWavesTab = new EnemyWavesTabView();
            _navigationTab = new NavigationTabView();
            _swarmTab = new SwarmTabView();
            _testTab = new TestTabView();

            rootVisualElement.Add(BuildTabBar());

            _contentRoot = new ScrollView(ScrollViewMode.Vertical);
            _contentRoot.AddToClassList("fd-content-root");
            rootVisualElement.Add(_contentRoot);

            SwitchTab(Tab.GettingStarted);

            rootVisualElement.schedule.Execute(OnTick).Every(150);
        }

        private VisualElement BuildTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("fd-tab-bar");

            AddTabButton(bar, Tab.GettingStarted, "はじめに", "このツールの使い方を説明します。初めて開いた方はまずこちら。");
            AddTabButton(bar, Tab.Turrets, "砲台配置", "4基の砲台の位置・チューニング(太さ/熱/オーバーヒート)を調整します。");
            AddTabButton(bar, Tab.EnemyWaves, "敵ウェーブ", "敵が湧く位置・数・タイミングを調整します。");
            AddTabButton(bar, Tab.Navigation, "経路・障害物", "敵の迂回経路の設定・可視化・到達チェックと、障害物（破壊可能/壁/地帯）の編集をします。");
            AddTabButton(bar, Tab.Swarm, "群衆", "数千体の敵の計測・ストレステスト・押し合い（流体らしさ）の調整・敵の種類の編集をします。");
            AddTabButton(bar, Tab.Test, "テスト", "Play Mode中に握力・熱・ウェーブ再生をリアルタイムで確認します。");

            return bar;
        }

        private void AddTabButton(VisualElement parent, Tab tab, string label, string tooltip)
        {
            var button = new Button(() => SwitchTab(tab)) { text = label, tooltip = tooltip };
            button.AddToClassList("fd-tab-button");
            _tabButtons[tab] = button;
            parent.Add(button);
        }

        private void SwitchTab(Tab tab)
        {
            _currentTab = tab;

            foreach (var kvp in _tabButtons)
            {
                kvp.Value.EnableInClassList("fd-tab-button--active", kvp.Key == tab);
            }

            _contentRoot.Clear();

            VisualElement content = tab switch
            {
                Tab.GettingStarted => _gettingStartedTab,
                Tab.Turrets => _turretsTab,
                Tab.EnemyWaves => _enemyWavesTab,
                Tab.Navigation => _navigationTab,
                Tab.Swarm => _swarmTab,
                Tab.Test => _testTab,
                _ => _gettingStartedTab
            };

            _contentRoot.Add(content);
            RefreshCurrentTab();
        }

        private void OnTick()
        {
            RefreshCurrentTab();
        }

        private void RefreshCurrentTab()
        {
            switch (_currentTab)
            {
                case Tab.Turrets:
                    _turretsTab.Refresh();
                    break;
                case Tab.EnemyWaves:
                    _enemyWavesTab.Refresh();
                    break;
                case Tab.Navigation:
                    _navigationTab.Refresh();
                    break;
                case Tab.Swarm:
                    _swarmTab.Refresh();
                    break;
                case Tab.Test:
                    _testTab.Refresh();
                    break;
            }
        }

        internal void SwitchToTurretsTab() => SwitchTab(Tab.Turrets);
        internal void SwitchToEnemyWavesTab() => SwitchTab(Tab.EnemyWaves);
        internal void SwitchToNavigationTab() => SwitchTab(Tab.Navigation);
        internal void SwitchToSwarmTab() => SwitchTab(Tab.Swarm);
    }
}
