using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Cpu
{
    /// <summary>
    /// 自動プレイ（開発用）: 全部の席を CPU にして、試合を続けて回す。<see cref="CpuDirector"/> が持つ。
    /// ・始め方: .exe は起動オプション <c>-autoplay N</c>（<c>-timescale X</c> で早送り）、エディタはメニュー「Tools/CPU/自動プレイで Play する」
    /// ・試合が終わったら（または決めた時間を過ぎたら）、少し待ってから、今のステージのシーンを読み直して次の試合へ
    ///   （ステージのシーンだけを読み直すと、共通の Game のシーンはステージの仕組みが読み込む）
    /// ・決めた数を回し終えたら、早送りを戻して止まる（最後の試合の画面のまま）
    /// ・ゲームの画面から直接 Play したとき（エディタで Game やステージを開いて Play、オフライン）のためのもの。
    ///   タイトル・キャラ選択・LAN の流れの中では使わない（通信・性能テストでの使い方は段10）。
    /// </summary>
    public sealed class CpuAutoplay
    {
        private int _remaining;
        private float _restartAt = -1f;
        private string _restartScenePath;
        private bool _finished;

        /// <summary>
        /// エディタから頼まれた自動プレイ（試合数、0 = 頼まれていない）。エディタのメニューが Play に入ったときに入れる。
        /// </summary>
        public static int EditorRequestMatches { get; set; }

        /// <summary>エディタで、ビルドの設定に入っていないシーンを読み込む口（エディタの側が入れる）。</summary>
        public static Func<string, bool> EditorSceneLoader { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            // EditorRequestMatches はエディタが Play に入った後に入れるので、ここでは消さない（Play を止めたときにエディタが消す）。
            EditorSceneLoader = null;
        }

        /// <summary>自動プレイ中か。</summary>
        public bool Active { get; private set; }

        /// <summary>回す試合の数。</summary>
        public int TotalMatches { get; private set; }

        /// <summary>終わった試合の数。</summary>
        public int DoneMatches => TotalMatches - _remaining;

        /// <summary>早送りの倍率。</summary>
        public float TimeScale { get; private set; } = 1f;

        /// <summary>次の試合を待っている（実時間の残り秒。待っていなければ 0）。</summary>
        public float WaitingSeconds => _restartAt > 0f ? Mathf.Max(0f, _restartAt - Time.unscaledTime) : 0f;

        /// <summary>頼まれていれば始める（何度呼んでもよい）。</summary>
        public void TryStart(CpuSettings settings)
        {
            if (Active || _finished)
            {
                return;
            }

            var launch = CpuLaunchOptions.Current;
            var matches = launch.AutoplayMatches ?? EditorRequestMatches;
            if (matches <= 0)
            {
                return;
            }

            Active = true;
            TotalMatches = matches;
            _remaining = matches;
            TimeScale = Mathf.Clamp(launch.TimeScale ?? (settings != null ? settings.autoplayTimeScale : 1f), 1f, 4f);
            ApplyTimeScale();
            Debug.Log($"[CPU] 自動プレイを始めます: {matches} 試合・{TimeScale:0.#} 倍速");
        }

        /// <summary>試合が始まったとき（早送りを入れ直す）。</summary>
        public void OnMatchBegan()
        {
            if (Active)
            {
                ApplyTimeScale();
            }
        }

        /// <summary>試合が終わって記録を閉じたとき。</summary>
        public void OnMatchClosed(CpuSettings settings)
        {
            if (!Active)
            {
                return;
            }

            _remaining--;
            if (_remaining <= 0)
            {
                Finish($"[CPU] 自動プレイが終わりました（{TotalMatches} 試合）。");
                return;
            }

            _restartScenePath = SceneManager.GetActiveScene().path;
            _restartAt = Time.unscaledTime + (settings != null ? settings.autoplayWaitSeconds : 3f);
        }

        /// <summary>毎フレーム。試合が長すぎるときは打ち切り、待ち時間が過ぎたら次の試合を始める。</summary>
        public void Tick(CpuSettings settings, CpuMatchRecorder recorder)
        {
            if (!Active)
            {
                return;
            }

            var maxSeconds = (settings != null ? settings.autoplayMaxMinutes : 20f) * 60f;
            if (recorder != null && recorder.InMatch && recorder.MatchSeconds > maxSeconds)
            {
                Debug.LogWarning($"[CPU] 自動プレイ: 試合が {maxSeconds / 60f:0} 分を過ぎても終わらないので打ち切ります。");
                recorder.Abort("打ち切り");
            }

            if (_restartAt > 0f && Time.unscaledTime >= _restartAt)
            {
                _restartAt = -1f;
                Restart();
            }
        }

        /// <summary>止める（早送りを戻す）。</summary>
        public void Stop()
        {
            if (Active)
            {
                Finish(null);
            }
        }

        private void Restart()
        {
            var path = _restartScenePath;
            if (string.IsNullOrEmpty(path))
            {
                Finish("[CPU] 自動プレイ: 読み直すシーンが分からないので止めます。");
                return;
            }

            var index = SceneUtility.GetBuildIndexByScenePath(path);
            if (index >= 0)
            {
                SceneManager.LoadScene(index, LoadSceneMode.Single);
                return;
            }

            if (EditorSceneLoader != null && EditorSceneLoader(path))
            {
                return;
            }

            Finish($"[CPU] 自動プレイ: シーン {path} を読み直せないので止めます（ビルドの設定に入っていない）。");
        }

        private void ApplyTimeScale()
        {
            Time.timeScale = TimeScale;
        }

        private void Finish(string message)
        {
            Active = false;
            _finished = true;
            _restartAt = -1f;
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(message))
            {
                Debug.Log(message);
            }
        }
    }
}
