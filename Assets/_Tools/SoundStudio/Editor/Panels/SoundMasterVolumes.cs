using System;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// SE 全体・声全体・BGM 全体の音量（「はじめに」に置く。毎回は触らないので上の帯には出さない）。
    /// 音のバランスの設定ファイルが無いときは、初めて 1 から動かしたときに作る。
    /// </summary>
    public sealed class SoundMasterVolumes : VisualElement
    {
        private readonly SoundStudioContext _context;
        private readonly Slider _se;
        private readonly Slider _voice;
        private readonly Slider _bgm;

        public SoundMasterVolumes(SoundStudioContext context)
        {
            _context = context;
            AddToClassList("ss-masters");
            _se = Master("SE", "効果音（声以外）全体の音量。ゲームの全部の効果音に効く。", (b, v) => b.seVolume = v);
            _voice = Master("声", "声全体の音量（「声」の印の付いた欄に効く）。", (b, v) => b.voiceVolume = v);
            _bgm = Master("BGM", "BGM 全体の音量。試聴している BGM にも、その場で効く。", (b, v) => b.bgmVolume = v);
            Refresh();
            schedule.Execute(Refresh).Every(500);
        }

        private Slider Master(string label, string tooltip, Action<FortressSoundBalance, float> set)
        {
            // 名前・バー・数字を自分で並べる（Slider の名前の欄は幅を広く取り、バーが短くなるため）。
            var row = new VisualElement { tooltip = tooltip };
            row.AddToClassList("ss-master");
            row.Add(new Label(label) { name = "label" });
            var slider = new Slider(0f, 1f) { value = 1f, showInputField = true, tooltip = tooltip };
            slider.AddToClassList("ss-master-slider");
            row.Add(slider);
            slider.RegisterValueChangedCallback(e =>
            {
                var balance = _context.Balance;
                if (balance == null)
                {
                    if (Mathf.Approximately(e.newValue, 1f))
                    {
                        return;
                    }

                    balance = _context.EnsureBalance();
                }

                Undo.RecordObject(balance, "サウンドスタジオ: 全体音量");
                set(balance, e.newValue);
                EditorUtility.SetDirty(balance);
                _context.Preview.ApplyBgmMaster();
            });
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => Save());
            slider.RegisterCallback<FocusOutEvent>(_ => Save());
            Add(row);
            return slider;
        }

        private void Save()
        {
            if (_context.Balance != null)
            {
                AssetDatabase.SaveAssetIfDirty(_context.Balance);
            }
        }

        private void Refresh()
        {
            var balance = _context.Balance;
            SetIfIdle(_se, balance != null ? balance.seVolume : 1f);
            SetIfIdle(_voice, balance != null ? balance.voiceVolume : 1f);
            SetIfIdle(_bgm, balance != null ? balance.bgmVolume : 1f);
        }

        // 触っている間は書き戻さない（Undo などで値が変わったときに合わせる）。
        private static void SetIfIdle(Slider slider, float value)
        {
            var focused = slider.panel?.focusController?.focusedElement as VisualElement;
            if (focused == null || (focused != slider && !slider.Contains(focused)))
            {
                slider.SetValueWithoutNotify(value);
            }
        }
    }
}
