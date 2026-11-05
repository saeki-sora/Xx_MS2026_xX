using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MS2026.Spectator
{
    /// <summary>F10 の確認表示に出す、観客ページの係の様子。</summary>
    internal struct SpectatorHostReport
    {
        public bool Open;
        public string StartError;
        public int Port;
        public string Role;
        public string Phase;
        public string Message;
        public int Spectators;
        public int Connections;
        public int Allowance;
        public int Pool;
        public int PoolMax;
        public int BossesLeft;
        public bool StageReady;
        public int Exits;
        public string LastEvent;
        public float LastEventAge;
    }

    /// <summary>
    /// 会場の設営で使う「観客ページの確認表示」（F10 で出し入れ。ビルドでも使える。起動時に自動で作られる）。
    /// 右上に、スマホで開く住所・この PC の役目（Host / Client）・スマホに出ている様子・観客の人数・
    /// 試合の上限の残り・ボスのたまり・最後に起きたこと、を出す。F8（握力の値）・F9（デバッグ表示）とは別。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SpectatorStatusOverlay : MonoBehaviour
    {
        public const Key ToggleKey = Key.F10;

        private const float Width = 560f;
        private const float LineHeight = 22f;
        private const float Padding = 12f;

        private readonly List<string> _lines = new List<string>();
        private bool _shown;
        private readonly List<string> _urls = new List<string>();
        private float _urlsAt = -100f;
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _warning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SpectatorStatusOverlay>() != null)
            {
                return;
            }

            // DontSave は付けない（付けると Play を止めても消えずに残る）。
            var go = new GameObject("[Spectator] Status Overlay") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<SpectatorStatusOverlay>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame)
            {
                _shown = !_shown;
            }
        }

        private void OnGUI()
        {
            if (!_shown)
            {
                return;
            }

            EnsureStyles();
            var host = SpectatorHost.Instance;
            _lines.Clear();
            var warning = false;
            if (host == null)
            {
                warning = true;
                _lines.Add("観客ページは動いていません。");
                _lines.Add(Application.isEditor
                    ? "エディタでは Tools/観客の菌/エディタの Play でも観客ページを動かす をオンにして Play し直す"
                    : $"起動オプション {SpectatorConfig.DisableFlag} が付いていないか確かめる");
            }
            else
            {
                var r = host.Report();
                if (Time.unscaledTime - _urlsAt > 5f)
                {
                    _urls.Clear();
                    foreach (var address in SpectatorAddresses.LanIPv4())
                    {
                        _urls.Add("    http://" + address + ":" + r.Port + "/");
                    }

                    if (_urls.Count == 0)
                    {
                        _urls.Add("    （LAN の住所が見つからない。ネットワークにつながっているか確かめる）");
                    }

                    _urlsAt = Time.unscaledTime;
                }

                if (r.Open)
                {
                    _lines.Add("状態: 開いている");
                    _lines.Add("スマホで開く住所（ブースの Wi-Fi の住所を QR に使う）:");
                    _lines.AddRange(_urls);
                }
                else
                {
                    warning = true;
                    _lines.Add($"状態: 開けなかった（番号 {r.Port}）: {r.StartError}");
                    _lines.Add($"ほかのアプリが同じ番号を使っていないか確かめる（{SpectatorConfig.PortFlag} で番号を変えられる）");
                }

                _lines.Add("この PC: " + r.Role);
                _lines.Add($"スマホに出ている様子: {r.Phase}「{r.Message}」");
                _lines.Add(r.StageReady ? $"ステージ: 出口 {r.Exits} か所" : "ステージ: まだ読み込まれていない（試合の外）");
                _lines.Add($"観客: {r.Spectators} 人（つないでいる線 {r.Connections} 本）");
                _lines.Add($"この試合: 上限の残り {r.Allowance} 体・ボスまで {r.Pool}/{r.PoolMax}・残りのボス {r.BossesLeft} 体");
                _lines.Add(r.LastEventAge < 0f ? "最後に起きたこと: まだ無い" : $"最後に起きたこと: {r.LastEvent}（{r.LastEventAge:0} 秒前）");
            }

            var height = Padding * 2f + LineHeight * (_lines.Count + 1);
            var panel = new Rect(Screen.width - Width - Padding, Padding, Width, height);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = old;

            var x = panel.x + Padding;
            var y = panel.y + Padding;
            var w = Width - Padding * 2f;
            GUI.Label(new Rect(x, y, w, LineHeight), $"観客ページ（{ToggleKey} で閉じる）", _title);
            y += LineHeight;
            for (var i = 0; i < _lines.Count; i++)
            {
                GUI.Label(new Rect(x, y, w, LineHeight), _lines[i], warning && i == 0 ? _warning : _label);
                y += LineHeight;
            }
        }

        private void EnsureStyles()
        {
            if (_title != null)
            {
                return;
            }

            _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.5f, 0.89f, 1f) } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = false, normal = { textColor = Color.white } };
            _warning = new GUIStyle(_label) { normal = { textColor = new Color(1f, 0.7f, 0.4f) } };
        }
    }
}
