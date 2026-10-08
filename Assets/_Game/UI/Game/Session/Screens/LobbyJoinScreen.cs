using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 参加する部屋を選ぶ画面: 同じLANで見つかった部屋の一覧（押すと参加）、アドレスを入れて参加、もどる。
    /// 一覧の1行は「行のひな形」（隠れている子）を複製して作るので、ひな形の見た目を変えれば全部の行が変わる。
    /// </summary>
    [AddComponentMenu("UI Studio/ロビー/部屋を選ぶ画面 (LobbyJoinScreen)")]
    public sealed class LobbyJoinScreen : SessionScreenBase
    {
        [Tooltip("見つかった部屋の行を並べる場所（縦に並べる部品を付けておく）。")]
        public RectTransform listRoot;

        [Tooltip("1行のひな形（ふだんは隠しておく）。")]
        public LobbyHostRow rowTemplate;

        [Tooltip("アドレスを入れて参加する欄。")]
        public InputField addressField;

        [Tooltip("入れたアドレスに参加するボタン。")]
        public Button connectButton;

        [Tooltip("最初の画面へ戻るボタン（接続中なら接続をやめる）。")]
        public Button backButton;

        [Tooltip("接続を待つのをやめるボタン（接続中だけ出す）。")]
        public Button cancelButton;

        private readonly List<LobbyHostRow> _rows = new List<LobbyHostRow>();
        private GameSession _session;

        private void Awake()
        {
            Bind(connectButton, s => s.Join(addressField != null ? addressField.text : string.Empty));
            Bind(backButton, s => s.BackToTop());
            Bind(cancelButton, s => s.CancelConnecting());
            if (addressField != null)
            {
                addressField.onSubmit.AddListener(text => Session?.Join(text));
            }

            if (rowTemplate != null)
            {
                rowTemplate.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _session = Session;
            if (_session != null)
            {
                _session.Changed += Rebuild;
                if (addressField != null && string.IsNullOrEmpty(addressField.text))
                {
                    addressField.SetTextWithoutNotify(_session.LastAddress);
                }
            }

            Rebuild();
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.Changed -= Rebuild;
                _session = null;
            }
        }

        private void Rebuild()
        {
            if (listRoot == null || rowTemplate == null)
            {
                return;
            }

            var hosts = _session != null && _session.Discovery != null ? _session.Discovery.Hosts : (IReadOnlyList<LanDiscovery.FoundHost>)System.Array.Empty<LanDiscovery.FoundHost>();
            while (_rows.Count < hosts.Count)
            {
                var row = Instantiate(rowTemplate, listRoot);
                row.name = $"Host {_rows.Count + 1}";
                _rows.Add(row);
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var visible = i < hosts.Count;
                _rows[i].gameObject.SetActive(visible);
                if (visible)
                {
                    var host = hosts[i];
                    _rows[i].Show(host, _session != null ? _session.SelectedSeat : 0, () => Session?.Join(host));
                }
            }
        }
    }
}
