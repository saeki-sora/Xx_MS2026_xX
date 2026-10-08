using System.Linq;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MS2026.Title
{
    /// <summary>
    /// タイトル画面の進行役。
    /// 暗転から明ける（背景が出る）→ オープニング（歯磨き粉をつまむ → ふくらんで噴き出す → 画面が歯磨き粉で塗り替わる）→ ロゴ・キャラが落ちてくる → 「スタート」の文字
    /// → 入力待ち → スタート演出 → ゲームのシーンへ。
    /// 演出の途中で入力すると演出を飛ばす（もう一度入力でスタート）。
    /// 握力センサーなど別の入力から始めたいときは <see cref="RequestStart"/> を呼ぶ。
    /// </summary>
    public sealed class TitleScreenController : MonoBehaviour
    {
        [Header("遷移")]
        [Tooltip("スタートで読み込むシーンの名前（Build Profiles のシーン一覧に入っている必要がある）。")]
        public string gameSceneName = "Game";
        [Tooltip("仮のスタート用キー。")]
        public Key startKey = Key.A;

        [Header("オープニング（歯磨き粉）")]
        [Tooltip("歯磨き粉をつまんで噴き出させ、画面を塗り替える演出。幕が抜け始めた後にロゴ・キャラ・スタートの文字が登場する。空なら最初から登場する。")]
        public TitleOpening opening;

        [Header("配置物（それぞれの動きは各オブジェクトの Title Element Motion で調整）")]
        [Tooltip("背景。オープニングと同時に（最初から）出る。")]
        public TitleElementMotion background;
        [Tooltip("オープニングの後に登場するもの（ロゴ・キャラ・スタートの文字など）。登場を始める秒数は各自の Enter Delay。")]
        public TitleElementMotion[] afterOpening;
        [Tooltip("「〇〇でスタート！」の文字（スタート時の効果音の位置に使う）。")]
        public TitleElementMotion pressStart;
        [Tooltip("泡・ばいきんなど、最初から流すパーティクル（任意）。")]
        public ParticleSystem[] ambientParticles;

        [Header("画面全体の演出")]
        public TitleScreenFader fader;
        public TitleCameraMotion cameraMotion;
        [Tooltip("始まりの暗転からの明転にかける秒数。")]
        public float fadeInSeconds = 0.8f;
        [Tooltip("スタートの瞬間の画面フラッシュの色。")]
        public Color startFlashColor = new(0.85f, 1f, 1f, 0.9f);
        [Tooltip("スタートしてから暗転し始めるまでの秒数（この間に点滅などの退場演出を見せる）。")]
        public float startHoldSeconds = 0.55f;
        [Tooltip("暗転にかける秒数。暗転し終わったらゲームのシーンを読み込む。")]
        public float fadeOutSeconds = 0.5f;
        [Tooltip("スタート時のカメラの寄り（0.1 = 10%寄る）。")]
        public float startZoom = 0.12f;
        [Tooltip("スタート時のカメラの揺れ。")]
        public float startShake = 0.15f;

        [Header("エフェクト・効果音（D-Drive）")]
        [Tooltip("スタートを押した瞬間（「スタート」の文字の位置）。着地の音などは各配置物の On Land に入れる。")]
        public FortressEffect onStart = new() { tintWithPlayerColor = false };

        private enum State { Intro, Ready, Starting }

        private State state;
        private float time;
        private bool dropStarted;
        private float introEndTime = float.MaxValue;
        private float startTime;
        private float holdSeconds;
        private bool fadeOutStarted;
        private bool loadRequested;

        private TitleElementMotion[] AllElements =>
            new[] { background }.Concat(afterOpening ?? System.Array.Empty<TitleElementMotion>()).Where(e => e != null).Distinct().ToArray();

        private void Start()
        {
            AdoptUnlistedElements();
            if (opening != null) opening.Finished += BeginDrop;
            Begin();
        }

        /// <summary>
        /// タイトル画面を最初からやり直す（調整用。Inspector の「▶ 最初から再生」から呼ばれる）。
        /// ゲームのシーンを読み込み始めた後は何もしない。
        /// </summary>
        public void Restart()
        {
            if (loadRequested) return;
            foreach (var element in AllElements) element.ResetToStart();
            if (opening != null) opening.ResetToStart();
            if (cameraMotion != null) cameraMotion.ResetMotion();
            if (ambientParticles != null)
            {
                foreach (var ps in ambientParticles)
                {
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            Begin();
        }

        private void Begin()
        {
            state = State.Intro;
            time = 0f;
            dropStarted = false;
            introEndTime = float.MaxValue;
            fadeOutStarted = false;

            if (fader != null)
            {
                fader.Set(Color.black);
                fader.FadeTo(Color.clear, fadeInSeconds);
            }

            foreach (var element in AllElements)
            {
                element.Landed -= OnElementLanded; // やり直しで二重に登録しないように
                element.Landed += OnElementLanded;
            }
            if (background != null) background.PlayEnter();

            if (ambientParticles != null)
            {
                foreach (var ps in ambientParticles)
                {
                    if (ps != null) ps.Play(true);
                }
            }

            if (opening != null)
            {
                opening.Play();
            }
            else
            {
                BeginDrop();
            }
        }

        private void OnDestroy()
        {
            foreach (var element in AllElements) element.Landed -= OnElementLanded;
            if (opening != null) opening.Finished -= BeginDrop;
        }

        private void Update()
        {
            time += Time.deltaTime;

            switch (state)
            {
                case State.Intro:
                    if (StartPressed())
                    {
                        SkipIntro();
                    }
                    else if (dropStarted && time >= introEndTime)
                    {
                        state = State.Ready;
                    }
                    break;

                case State.Ready:
                    if (StartPressed()) RequestStart();
                    break;

                case State.Starting:
                    UpdateStarting();
                    break;
            }
        }

        /// <summary>スタートする（入力待ちのときだけ受け付ける。演出中なら演出を飛ばす）。</summary>
        public void RequestStart()
        {
            if (state == State.Intro)
            {
                SkipIntro();
                return;
            }
            if (state != State.Ready) return;

            state = State.Starting;
            startTime = time;

            var exitTime = 0f;
            foreach (var element in AllElements)
            {
                element.PlayExit();
                exitTime = Mathf.Max(exitTime, element.ExitDuration);
            }
            // 退場演出が待ち時間より長ければ、それが見えるまで待つ。
            holdSeconds = Mathf.Max(startHoldSeconds, exitTime * 0.8f);

            if (fader != null)
            {
                // 色を保ったまま透明にする（Color.clear へ向かうと途中で黒ずむ）。
                var flashEnd = startFlashColor;
                flashEnd.a = 0f;
                fader.FadeFromTo(startFlashColor, flashEnd, 0.35f);
            }
            if (cameraMotion != null)
            {
                cameraMotion.Shake(startShake);
                cameraMotion.ZoomTo(startZoom, holdSeconds + fadeOutSeconds);
            }

            TitleEffects.Play(onStart, pressStart != null ? pressStart.transform.position : transform.position);
        }

        // 配置物は登場の合図を受けるまで隠れて待つので、一覧（Background / After Opening）から漏れると永遠に表示されない。
        // 子にある漏れた配置物は After Opening に足して、ちゃんと登場させる（古いシーンや手で足した物の救済）。
        private void AdoptUnlistedElements()
        {
            var listed = AllElements;
            var unlisted = GetComponentsInChildren<TitleElementMotion>(true).Where(e => !listed.Contains(e)).ToArray();
            if (unlisted.Length == 0) return;

            Debug.LogWarning("[Title] Title Screen Controller の一覧に入っていない配置物があったので、オープニングの後に登場させます: "
                             + string.Join(", ", unlisted.Select(e => e.name))
                             + "。メニュー「Tools/タイトル/タイトルシーンを組み立てる」で作り直すか、After Opening に入れてください。", this);
            afterOpening = (afterOpening ?? System.Array.Empty<TitleElementMotion>()).Concat(unlisted).ToArray();
        }

        // オープニングが終わった合図で、ロゴ・キャラ・スタートの文字の登場を始める。
        private void BeginDrop()
        {
            if (dropStarted) return;
            dropStarted = true;

            var longest = 0f;
            if (afterOpening != null)
            {
                foreach (var element in afterOpening)
                {
                    if (element == null) continue;
                    element.PlayEnter();
                    longest = Mathf.Max(longest, element.EnterEndTime);
                }
            }
            introEndTime = time + longest;
        }

        private void UpdateStarting()
        {
            var elapsed = time - startTime;
            if (!fadeOutStarted && elapsed >= holdSeconds)
            {
                fadeOutStarted = true;
                if (fader != null) fader.FadeTo(Color.black, fadeOutSeconds);
            }

            if (!loadRequested && elapsed >= holdSeconds + fadeOutSeconds)
            {
                loadRequested = true;
                LoadGameScene();
            }
        }

        private void SkipIntro()
        {
            if (opening != null) opening.Skip(); // 合図が出て BeginDrop が呼ばれる
            BeginDrop();
            foreach (var element in AllElements) element.SkipToIdle();
            if (fader != null) fader.Set(Color.clear);
            state = State.Ready;
        }

        private void OnElementLanded(TitleElementMotion element)
        {
            if (cameraMotion != null) cameraMotion.Shake(element.shakeOnLand);
        }

        private bool StartPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[startKey].wasPressedThisFrame;
        }

        private void LoadGameScene()
        {
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"[Title] シーン「{gameSceneName}」を読み込めません。File > Build Profiles のシーン一覧に入っているか、名前が合っているか確認してください。", this);
                return;
            }
            SceneManager.LoadSceneAsync(gameSceneName);
        }
    }
}
