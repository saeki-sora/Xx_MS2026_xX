using System;
using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using MS2026.Fortress.EditorTools;
using UnityEditor;
using UnityEngine;

namespace MS2026.SoundStudio
{
    /// <summary>サウンドスタジオの状態（集めた欄・選んでいる行・絞り込み・全体音量）。ページとパネルはここを見て描く。</summary>
    public sealed class SoundStudioContext
    {
        public const string BalancePath = "Assets/_Game/Fortress/Data/Audio/FortressSoundBalance.asset";

        public List<SoundSlot> Slots { get; private set; } = new List<SoundSlot>();
        public SoundLibrary Library { get; } = new SoundLibrary();
        public SoundSlot Selected { get; private set; }

        /// <summary>「音がない所だけ」。</summary>
        public bool MissingOnly { get; set; }

        /// <summary>名前で探す文字。</summary>
        public string Search { get; set; } = string.Empty;

        /// <summary>中身が変わるたびに増える（ページは自分の番号と違えば描き直す）。</summary>
        public int Version { get; private set; }

        /// <summary>行を選んだとき。</summary>
        public event Action SelectionChanged;

        /// <summary>別のページの行へ飛びたいとき（同じ音を使う所の一覧から）。</summary>
        public event Action<SoundSlot> GoToRequested;

        /// <summary>画面に短いお知らせを出す（窓が入れる）。</summary>
        public Action<string> Notify { get; set; } = _ => { };

        /// <summary>試聴の係（窓が入れる）。</summary>
        public SoundPreview Preview { get; } = new SoundPreview();

        /// <summary>音のバランス（無ければ null。全体音量を初めて動かしたときに作る）。</summary>
        public FortressSoundBalance Balance
        {
            get
            {
                FortressSoundBalanceEditorLoader.Load();
                return FortressSoundBalance.Current;
            }
        }

        public void Rescan()
        {
            Library.Reload();
            var selectedKey = Selected?.Key;
            Slots = SoundSlotScanner.ScanAll();
            foreach (var slot in Slots)
            {
                if (slot.Place == SoundSlotPlace.Asset && slot.Kind == SoundSlotKind.Effect && slot.Value is FortressEffect e && e.isVoice != slot.Voice)
                {
                    // 設定ファイルの「声の印」は、開いたときに表に合わせる（シーンは開かない）。
                    SoundSlotWriter.SyncVoice(slot);
                }
            }

            Selected = selectedKey == null ? null : Slots.FirstOrDefault(s => s.Key == selectedKey);
            Touch();
            if (SoundSlotScanner.LastScanSkippedScenes)
            {
                Notify("Play 中なので、開いていないシーンの欄は調べていません。止めてから［調べ直す］を押してください。");
            }
        }

        /// <summary>調べ直す。失敗しても例外を外へ出さず、お知らせを出す（窓・［調べ直す］から呼ぶ）。</summary>
        public void TryRescan()
        {
            try
            {
                Rescan();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Notify("音の欄を調べられませんでした: " + e.Message + "（［調べ直す］で、もう一度試せます）");
                Touch();
            }
        }

        public void Select(SoundSlot slot)
        {
            Selected = slot;
            SelectionChanged?.Invoke();
        }

        public void GoTo(SoundSlot slot)
        {
            Select(slot);
            GoToRequested?.Invoke(slot);
        }

        /// <summary>中身が変わった（描き直してほしい）。</summary>
        public void Touch()
        {
            Version++;
        }

        /// <summary>書き換えの結果をお知らせにする。</summary>
        public void Report(SoundWriteResult result)
        {
            switch (result)
            {
                case SoundWriteResult.SaveSceneNeeded:
                    Notify("開いているシーンの欄を変えました。シーンを保存してください（Ctrl+S）。");
                    break;
                case SoundWriteResult.Locked:
                    Notify("Play 中は、シーンにある欄は変えられません（止めると消えるため）。止めてから変えてください。");
                    break;
                case SoundWriteResult.Failed:
                    Notify("欄が見つかりませんでした。［調べ直す］を押してください。");
                    break;
            }

            Touch();
        }

        /// <summary>そのページに出す行（絞り込み後）。</summary>
        public IEnumerable<SoundSlot> SlotsOn(SoundPageId page)
        {
            return Slots.Where(s => s.Page == page && Passes(s));
        }

        /// <summary>同じ音（番号）を使っている欄。</summary>
        public IEnumerable<SoundSlot> SlotsUsing(ulong soundId, SoundSlotKind kind)
        {
            return soundId == 0 ? Enumerable.Empty<SoundSlot>() : Slots.Where(s => s.Kind == kind && SoundIdOf(s) == soundId);
        }

        public static ulong SoundIdOf(SoundSlot slot)
        {
            switch (slot?.Value)
            {
                case FortressEffect effect: return effect.se.Value;
                case DDrive.Foundation.Identity.AssetId<DDrive.Runtime.Audio.BgmMarker> bgm: return bgm.Value;
                default: return 0;
            }
        }

        /// <summary>音がない欄の数（候補の行は数えない）。</summary>
        public static int CountMissing(IEnumerable<SoundSlot> slots)
        {
            return slots.Count(s => s.Kind != SoundSlotKind.Candidate && !s.HasSound);
        }

        /// <summary>音のバランスが無ければ作り、ビルドで読まれるよう Preloaded Assets に入れる。</summary>
        public FortressSoundBalance EnsureBalance()
        {
            var balance = Balance;
            if (balance != null)
            {
                return balance;
            }

            EnsureFolder(System.IO.Path.GetDirectoryName(BalancePath)?.Replace('\\', '/'));
            balance = ScriptableObject.CreateInstance<FortressSoundBalance>();
            AssetDatabase.CreateAsset(balance, BalancePath);
            AssetDatabase.SaveAssets();
            var preloaded = PlayerSettings.GetPreloadedAssets().ToList();
            if (!preloaded.Contains(balance))
            {
                preloaded.Add(balance);
                PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
                AssetDatabase.SaveAssets();
            }

            FortressSoundBalance.Current = balance;
            Notify("音のバランスの設定を作りました: " + BalancePath);
            return balance;
        }

        private bool Passes(SoundSlot slot)
        {
            if (MissingOnly && (slot.Kind == SoundSlotKind.Candidate || slot.HasSound))
            {
                return false;
            }

            if (string.IsNullOrEmpty(Search))
            {
                return true;
            }

            return Contains(slot.Label) || Contains(slot.Group) || Contains(slot.OwnerName) || Contains(SoundName(slot));
        }

        private bool Contains(string text) => text != null && text.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>欄に入っている音の名前（無ければ null）。</summary>
        public string SoundName(SoundSlot slot)
        {
            var id = SoundIdOf(slot);
            return slot.Kind == SoundSlotKind.Bgm ? SoundLibrary.NameOf(Library.Bgm(id)) : SoundLibrary.NameOf(Library.Se(id));
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
