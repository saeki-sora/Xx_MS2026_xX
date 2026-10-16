using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>ツールバーのボタンとメニューの文言・アイコン・右クリックメニューの中身（表示の種類に依存しない）。</summary>
    internal static class PlayRecorderUi
    {
        public const string ToolName = "録画しながらプレイ";
        public const string PreferencesPath = "Preferences/録画しながらプレイ";

        public readonly struct MenuEntry
        {
            public readonly string Label;
            public readonly Action Action;
            public readonly bool Enabled;
            public readonly bool IsSeparator;

            public MenuEntry(string label, Action action, bool enabled = true)
            {
                Label = label;
                Action = action;
                Enabled = enabled;
                IsSeparator = false;
            }

            MenuEntry(bool separator)
            {
                Label = "";
                Action = null;
                Enabled = false;
                IsSeparator = separator;
            }

            public static MenuEntry Separator => new MenuEntry(true);

            /// <summary>押せない、情報を見せるだけの行。</summary>
            public static MenuEntry Info(string text) => new MenuEntry(text, null, false);
        }

        public static string Label()
        {
            var rec = PlayRecorderController.Recording;
            if (rec != null)
                return (rec.IsPaused ? "‖ 録画一時停止 " : "■ 録画停止 ") + MovieEncodeJob.FormatTime(rec.Elapsed);
            if (PlayRecorderController.WillRecordOnPlay) return "● 録画準備中…";
            if (EditorApplication.isPlaying) return "● ここから録画";
            var job = PlayRecorderController.EncodeJob;
            if (job != null) return $"● 録画プレイ（書き出し {job.Progress01:0%}）";
            return "● 録画プレイ";
        }

        public static string Tooltip()
        {
            float mb = PlayRecorderSettings.LimitMB;
            var rec = PlayRecorderController.Recording;
            if (rec != null)
            {
                var plan = EstimatedPlan(rec.Elapsed);
                return $"録画中です。今止めると {plan.Height}p {plan.Fps}fps で {mb:0.#}MB 以内に収まります（長くなるほど自動で画質を下げ、途中で切ることはありません）。\n" +
                       "押す → 録画だけ止める（プレイは続く）。プレイを止めても録画は止まり、自動で動画に書き出します。\n右クリック → 録画フォルダ・設定";
            }
            if (EditorApplication.isPlaying)
                return $"押すと、今のプレイの途中から録画を始めます（{mb:0.#}MB 以内）。\n右クリック → 録画フォルダ・設定";
            string encoding = PlayRecorderController.EncodeJob != null ? "\n（前の録画を裏で書き出し中。プレイ中は休み、終わったら再開します）" : "";
            return $"押すとプレイが始まり、同時にゲーム画面と音を録画します。\nプレイを止めると {mb:0.#}MB 以内の MP4 に自動で書き出します。{encoding}\n右クリック → 録画フォルダ・設定";
        }

        static Texture2D s_Icon;

        public static Texture2D Icon()
        {
            if (s_Icon == null) s_Icon = EditorGUIUtility.IconContent("Animation.Record").image as Texture2D;
            return s_Icon;
        }

        /// <summary>録画の長さごとの見込み（ボタンのヒントと設定画面の表で使う）。</summary>
        public static VideoPlan EstimatedPlan(double durationSec)
        {
            double audio = 0;
            if (PlayRecorderSettings.RecordAudio)
            {
                int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
                string ext = Application.platform == RuntimePlatform.LinuxEditor ? ".webm" : ".mp4";
                string key = $"MS2026.PlayRecorder.AudioBps.{Application.unityVersion}.{ext}.{rate}.2";
                audio = EditorPrefs.GetFloat(key, 24000f); // まだ測っていなければ 192kbps と見込む
                if (audio * durationSec > PlayRecorderSettings.LimitBytes * RecordingPlan.SafetyFactor * 0.25) audio = 0;
            }
            return RecordingPlan.Preview(PlayRecorderSettings.MaxHeight, PlayRecorderSettings.MaxFps,
                PlayRecorderSettings.LimitBytes, durationSec, audio);
        }

        public static List<MenuEntry> ContextMenu()
        {
            string last = PlayRecorderSettings.LastOutputPath;
            var list = StatusEntries();
            list.Add(new MenuEntry("進み具合の画面を開く", () => Progress.ShowDetails(false), PlayRecorderController.EncodeJob != null));
            list.Add(MenuEntry.Separator);
            list.AddRange(new[]
            {
                new MenuEntry("録画フォルダを開く", OpenOutputFolder),
                new MenuEntry("最後の録画を再生", () => EditorUtility.OpenWithDefaultApp(last), !string.IsNullOrEmpty(last) && File.Exists(last)),
                new MenuEntry("設定…", OpenSettings),
                new MenuEntry("ほかのプロジェクトに入れる…", PlayRecorderInstaller.InstallToOtherProject),
                new MenuEntry("配布用 zip を作る…", PlayRecorderInstaller.ExportZip),
            });
            return list;
        }

        /// <summary>右クリックメニューの一番上に出す「今どうなっているか」の行（押せない）。</summary>
        public static List<MenuEntry> StatusEntries()
        {
            var list = new List<MenuEntry>();
            var rec = PlayRecorderController.Recording;
            if (rec != null)
            {
                var plan = EstimatedPlan(rec.Elapsed);
                list.Add(MenuEntry.Info($"録画中 {MovieEncodeJob.FormatTime(rec.Elapsed)}（今止めると {plan.Height}p {plan.Fps}fps）"));
            }

            var job = PlayRecorderController.EncodeJob;
            if (job != null)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    list.Add(MenuEntry.Info($"書き出し {job.Progress01:0%}（プレイ中は休み。止めると再開）"));
                }
                else
                {
                    double eta = job.SecondsRemaining;
                    string etaText = eta >= 0 ? $"・残り約 {MovieEncodeJob.FormatTime(Math.Max(1, eta))}" : "";
                    list.Add(MenuEntry.Info($"書き出し中 {job.Progress01:0%}{etaText}（{job.Status}）"));
                    list.Add(MenuEntry.Info($"　{MovieEncodeJob.FormatTime(job.Duration)} の録画 → {PlayRecorderSettings.LimitMB:0.#}MB 以内に"));
                }
            }

            int queued = PlayRecorderController.QueuedCount();
            if (queued > 0)
            {
                string when = EditorApplication.isPlayingOrWillChangePlaymode ? "（プレイを止めると始まります）" : "";
                list.Add(MenuEntry.Info($"書き出し待ち {queued} 本{when}"));
            }

            if (list.Count == 0) list.Add(MenuEntry.Info("書き出し中の録画はありません"));
            return list;
        }

        public static void OpenOutputFolder()
        {
            string folder = PlayRecorderSettings.OutputFolderAbsolute;
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder + Path.DirectorySeparatorChar);
        }

        public static void OpenSettings() => SettingsService.OpenUserPreferences(PreferencesPath);

        public static GenericMenu ToGenericMenu(List<MenuEntry> entries)
        {
            var menu = new GenericMenu();
            foreach (var e in entries)
            {
                if (e.IsSeparator)
                {
                    menu.AddSeparator("");
                    continue;
                }
                if (e.Enabled)
                {
                    var action = e.Action;
                    menu.AddItem(new GUIContent(e.Label), false, () => action());
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(e.Label));
                }
            }
            return menu;
        }
    }
}
