using System;
using System.Collections.Generic;

namespace MS2026.Spectator
{
    /// <summary>送った結果。</summary>
    public enum SpectatorSendResult
    {
        Ok,

        /// <summary>まだ覚えていない人（先に状態を聞いていない）。</summary>
        Unknown,

        /// <summary>次に送れるまで待っている。</summary>
        CoolingDown
    }

    /// <summary>観客1人の今の様子（写し。スレッドをまたいで渡してよい）。</summary>
    public struct SpectatorInfo
    {
        public int Number;
        public string Name;
        public int ColorIndex;
        public double CooldownRemaining;
        public int Sent;
    }

    /// <summary>
    /// 観客の名簿（スマホの id → 番号・自動の名前・色・待ち時間）。Web サーバーのスレッドから同時に呼ばれるので鍵をかける。
    /// 時刻は呼ぶ側が渡す（テストで時間を進められるように）。Unity に頼らない。
    /// </summary>
    public sealed class SpectatorRoster
    {
        private sealed class Entry
        {
            public int Number;
            public string Name;
            public int ColorIndex;
            public double ReadyAt;
            public int Sent;
        }

        private readonly object _lock = new object();
        private readonly Dictionary<string, Entry> _byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly int _max;
        private readonly double _cooldown;
        private int _nextNumber = 1;

        public SpectatorRoster(int maxSpectators, double cooldownSeconds)
        {
            _max = Math.Max(1, maxSpectators);
            _cooldown = Math.Max(0, cooldownSeconds);
        }

        public double CooldownSeconds => _cooldown;

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _byId.Count;
                }
            }
        }

        /// <summary>スマホが作る id の形（英数字 8〜32 文字）。それ以外は受け付けない。</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 8 || id.Length > 32)
            {
                return false;
            }

            foreach (var c in id)
            {
                var ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>覚えている人ならその様子、初めての人なら番号と名前を付けて覚える。名簿がいっぱい・おかしな id なら false。</summary>
        public bool TryGetOrAdd(string id, int colorIndex, double now, out SpectatorInfo info)
        {
            info = default;
            if (!IsValidId(id))
            {
                return false;
            }

            lock (_lock)
            {
                if (!_byId.TryGetValue(id, out var entry))
                {
                    if (_byId.Count >= _max)
                    {
                        return false;
                    }

                    var number = _nextNumber++;
                    entry = new Entry
                    {
                        Number = number,
                        Name = SpectatorNames.Make(number),
                        ColorIndex = SpectatorColors.Clamp(colorIndex),
                        ReadyAt = 0
                    };
                    _byId.Add(id, entry);
                }

                info = ToInfo(entry, now);
                return true;
            }
        }

        public void SetColor(string id, int colorIndex)
        {
            if (id == null)
            {
                return;
            }

            lock (_lock)
            {
                if (_byId.TryGetValue(id, out var entry))
                {
                    entry.ColorIndex = SpectatorColors.Clamp(colorIndex);
                }
            }
        }

        /// <summary>送ってよいか確かめ、よければ待ち時間を始める。info には送った後の様子が入る。</summary>
        public SpectatorSendResult TrySend(string id, double now, out SpectatorInfo info)
        {
            info = default;
            if (id == null)
            {
                return SpectatorSendResult.Unknown;
            }

            lock (_lock)
            {
                if (!_byId.TryGetValue(id, out var entry))
                {
                    return SpectatorSendResult.Unknown;
                }

                if (now < entry.ReadyAt)
                {
                    info = ToInfo(entry, now);
                    return SpectatorSendResult.CoolingDown;
                }

                entry.ReadyAt = now + _cooldown;
                entry.Sent++;
                info = ToInfo(entry, now);
                return SpectatorSendResult.Ok;
            }
        }

        /// <summary>送ったのに出せなかったとき（試合が変わった等）、待ち時間を取り消す。</summary>
        public void Refund(string id)
        {
            if (id == null)
            {
                return;
            }

            lock (_lock)
            {
                if (_byId.TryGetValue(id, out var entry))
                {
                    entry.ReadyAt = 0;
                    entry.Sent = Math.Max(0, entry.Sent - 1);
                }
            }
        }

        private static SpectatorInfo ToInfo(Entry entry, double now) => new SpectatorInfo
        {
            Number = entry.Number,
            Name = entry.Name,
            ColorIndex = entry.ColorIndex,
            CooldownRemaining = Math.Max(0, entry.ReadyAt - now),
            Sent = entry.Sent
        };
    }

    /// <summary>自動で付ける名前（「むしばい菌 #23」）。番号が同じなら必ず同じ名前。</summary>
    public static class SpectatorNames
    {
        private static readonly string[] Words =
        {
            "むしばい", "ねばねば", "しみしみ", "ぐらぐら", "ざらざら", "もやもや", "ちくちく", "べたべた", "ぬるぬる", "どろどろ", "ぷくぷく", "ちゅるちゅる"
        };

        public static string Make(int number)
        {
            var n = Math.Max(1, number);
            var word = Words[(n * 7) % Words.Length];
            return word + "菌 #" + n;
        }
    }

    /// <summary>観客が選べる色（プレイヤーの4色＝赤・青・緑・黄とかぶらない色）。スマホのページも同じ順で持つ。</summary>
    public static class SpectatorColors
    {
        public static readonly string[] Hex = { "#ff7eb6", "#b48cff", "#ff9a4d", "#f0f0f0" };

        public static readonly string[] Names = { "ピンク", "むらさき", "オレンジ", "しろ" };

        public static int Count => Hex.Length;

        public static int Clamp(int index) => index >= 0 && index < Hex.Length ? index : 0;
    }
}
