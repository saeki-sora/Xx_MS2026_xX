using System;
using UnityEditor;
using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// 録画プレイの司令塔。ボタンが押されたら「再生開始＋録画開始」、プレイが止まったら録画を止め、
    /// エディタに戻ったら裏で少しずつ動画を書き出す（プレイ中は書き出しを休む）。
    /// 再生開始時のドメインリロードをまたぐため、「次のプレイで録画する」印は SessionState に置く。
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayRecorderController
    {
        const string RecordOnPlayKey = "MS2026.PlayRecorder.RecordOnPlay";
        const double EncodeBudgetMs = 25;          // Unity を触っているとき（操作を重くしない）
        const double EncodeBudgetBackgroundMs = 150; // Unity が裏にあるとき（動画を見ている間などに一気に進める）
        const string HintShownKey = "MS2026.PlayRecorder.HintShown";
        const double ScanInterval = 2.0;

        static ActiveRecording s_Recording;
        static MovieEncodeJob s_Job;
        static int s_ProgressId = -1;
        static bool s_CancelRequested;
        static double s_NextScan;
        static double s_NextUiTick;
        static string s_LastUiKey;

        /// <summary>ボタンの見た目を変えるべきとき（毎秒の時間表示・状態の切り替わり）に呼ばれる。</summary>
        public static event Action Changed;

        static PlayRecorderController()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.pauseStateChanged += s => s_Recording?.SetPaused(s == PauseState.Paused);
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += StopRecording;
            EditorApplication.update += Tick;
            EditorApplication.delayCall += ShowFirstTimeHint;
        }

        /// <summary>入れた直後に 1 回だけ、ボタンの場所を案内する（プロジェクトごと）。</summary>
        static void ShowFirstTimeHint()
        {
            string key = HintShownKey + "." + PlayRecorderSettings.ProjectRoot;
            if (EditorPrefs.GetBool(key, false)) return;
            EditorPrefs.SetBool(key, true);
            Debug.Log("[録画プレイ] ツールバーの再生ボタンの横に「● 録画プレイ」ボタンを追加しました。\n" +
                      "見当たらないときは、メニュー Tools › " + PlayRecorderUi.ToolName + " › ツールバーにボタンを表示する を選んでください。\n" +
                      "メニュー Tools › " + PlayRecorderUi.ToolName + "、ショートカット Ctrl+Shift+Alt+R からも使えます。");
        }

        public static bool IsRecording => s_Recording != null;
        public static ActiveRecording Recording => s_Recording;
        public static MovieEncodeJob EncodeJob => s_Job;
        public static bool WillRecordOnPlay => SessionState.GetBool(RecordOnPlayKey, false);

        /// <summary>書き出しを待っている録画の数（今書き出し中の 1 本と録画中の 1 本は数えない）。</summary>
        public static int QueuedCount()
        {
            int n = 0;
            foreach (var dir in SessionFiles.PendingSessions(s_Recording?.Dir))
                if (s_Job == null || !string.Equals(dir, s_Job.Dir, StringComparison.OrdinalIgnoreCase)) n++;
            return n;
        }

        /// <summary>ツールバーのボタン・メニュー・ショートカットから呼ぶ。</summary>
        public static void Toggle()
        {
            if (s_Recording != null)
            {
                StopRecording();
                return;
            }
            if (EditorApplication.isPlaying)
            {
                StartRecording();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(RecordOnPlayKey, true);
            EditorApplication.EnterPlaymode();
            NotifyChanged();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (SessionState.GetBool(RecordOnPlayKey, false))
                    {
                        SessionState.EraseBool(RecordOnPlayKey);
                        StartRecording();
                    }
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    StopRecording();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    SessionState.EraseBool(RecordOnPlayKey);
                    s_NextScan = 0; // すぐ書き出しを始める
                    break;
            }
            NotifyChanged();
        }

        static void OnBeforeAssemblyReload()
        {
            StopRecording();
            // 書き出し途中ならいったん閉じる（一時フォルダは残るので、リロード後に最初からやり直す）
            if (s_Job != null)
            {
                s_Job.Dispose();
                s_Job = null;
                FinishProgress(Progress.Status.Canceled);
            }
        }

        static void StartRecording()
        {
            if (s_Recording != null) return;
            try
            {
                s_Recording = ActiveRecording.Start();
                if (EditorApplication.isPaused) s_Recording.SetPaused(true);
                Debug.Log($"[録画プレイ] 録画を始めました（上限 {PlayRecorderSettings.LimitMB:0.#}MB。長さに合わせて画質を自動で決めます）");
            }
            catch (Exception e)
            {
                s_Recording = null;
                Debug.LogError("[録画プレイ] 録画を始められませんでした: " + e.Message);
                Debug.LogException(e);
            }
            NotifyChanged();
        }

        static void StopRecording()
        {
            var r = s_Recording;
            if (r == null) return;
            s_Recording = null;
            try
            {
                double sec = r.Elapsed;
                r.Stop();
                bool keepsPlaying = EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode;
                string when = keepsPlaying ? "プレイを止めると動画に書き出します" : "動画に書き出します";
                Debug.Log($"[録画プレイ] 録画を止めました（{MovieEncodeJob.FormatTime(sec)}・{r.CapturedFrames} 枚）。{when}。");
            }
            catch (Exception e)
            {
                Debug.LogError("[録画プレイ] 録画を止めるときにエラーが出ました: " + e.Message);
                Debug.LogException(e);
            }
            s_NextScan = 0;
            NotifyChanged();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;

            if (s_Recording != null && !EditorApplication.isPlaying) StopRecording();

            // 念のため：再生開始のイベントを取り逃したとき
            if (s_Recording == null && EditorApplication.isPlaying && !EditorApplication.isPaused
                && SessionState.GetBool(RecordOnPlayKey, false))
            {
                SessionState.EraseBool(RecordOnPlayKey);
                StartRecording();
            }

            if (now >= s_NextUiTick)
            {
                s_NextUiTick = now + 0.25;
                string key = PlayRecorderUi.Label();
                if (key != s_LastUiKey)
                {
                    s_LastUiKey = key;
                    Changed?.Invoke();
                }
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (s_Job != null && s_ProgressId >= 0)
                    Progress.Report(s_ProgressId, s_Job.Progress01, "プレイ中は書き出しを休んでいます");
                return;
            }

            if (s_Job == null)
            {
                if (now < s_NextScan) return;
                s_NextScan = now + ScanInterval;
                StartNextJob();
                if (s_Job == null) return;
            }

            if (s_CancelRequested)
            {
                s_CancelRequested = false;
                HandleCancel();
                return;
            }

            s_Job.Step(UnityEditorInternal.InternalEditorUtility.isApplicationActive ? EncodeBudgetMs : EncodeBudgetBackgroundMs);
            if (s_ProgressId >= 0) Progress.Report(s_ProgressId, s_Job.Progress01, s_Job.Status);
            if (s_Job.IsFinished) FinishJob();
        }

        static void StartNextJob()
        {
            foreach (var dir in SessionFiles.PendingSessions(s_Recording?.Dir))
            {
                var job = MovieEncodeJob.TryCreate(dir, out string reason);
                if (job == null)
                {
                    Debug.LogWarning($"[録画プレイ] 書き出せる中身がないので捨てました: {reason}");
                    SessionFiles.DeleteQuietly(dir);
                    continue;
                }
                s_Job = job;
                StartProgress();
                NotifyChanged();
                return;
            }
        }

        static void StartProgress()
        {
            s_ProgressId = Progress.Start("録画を書き出し中",
                $"{PlayRecorderSettings.LimitMB:0.#}MB 以内に収めています（{MovieEncodeJob.FormatTime(s_Job.Duration)}）");
            Progress.RegisterCancelCallback(s_ProgressId, () =>
            {
                s_CancelRequested = true;
                return true;
            });
        }

        static void FinishProgress(Progress.Status status)
        {
            if (s_ProgressId >= 0 && Progress.Exists(s_ProgressId)) Progress.Finish(s_ProgressId, status);
            s_ProgressId = -1;
        }

        static void HandleCancel()
        {
            s_ProgressId = -1; // キャンセルされた項目はもう使えない
            bool discard = EditorUtility.DisplayDialog("録画プレイ",
                "書き出しをやめて、この録画を捨てますか？\n（捨てると元に戻せません）", "捨てる", "書き出しを続ける");
            if (!discard)
            {
                StartProgress();
                return;
            }
            var job = s_Job;
            s_Job = null;
            job.Dispose();
            SessionFiles.DeleteQuietly(job.Dir);
            Debug.Log("[録画プレイ] 録画を捨てました。");
            NotifyChanged();
        }

        static void FinishJob()
        {
            var job = s_Job;
            s_Job = null;
            job.Dispose();
            if (job.Succeeded)
            {
                FinishProgress(Progress.Status.Succeeded);
                SessionFiles.DeleteQuietly(job.Dir);
                PlayRecorderSettings.LastOutputPath = job.OutputPath;
                EditorWindow.focusedWindow?.ShowNotification(new GUIContent("録画を保存しました"));
                if (PlayRecorderSettings.RevealWhenDone) EditorUtility.RevealInFinder(job.OutputPath);
            }
            else
            {
                FinishProgress(Progress.Status.Failed);
                SessionFiles.MarkFailed(job.Dir, job.Error);
                Debug.LogError($"[録画プレイ] 書き出しに失敗しました: {job.Error}\n一時ファイルは {job.Dir} に残しています。");
            }
            NotifyChanged();
        }

        static void NotifyChanged()
        {
            s_LastUiKey = null;
            s_NextUiTick = 0;
            Changed?.Invoke();
        }
    }
}
