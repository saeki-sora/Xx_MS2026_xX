using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Title
{
    /// <summary>
    /// タイトル画面のオープニング：
    /// 上向きの歯磨き粉が出てくる → 真ん中をつままれて、まわりがふくらむ（カメラが寄ってブルブル）
    /// → 抑えきれずにキャップが吹き飛び、口から歯磨き粉が上へ噴き出す → 上から画面全体に歯磨き粉が「どろーっ」と垂れてきて塗りつぶす
    /// → 下へ流れ落ちて（または消えて）画面が塗り替わる。流れ落ち始めたら <see cref="Finished"/> を出し、ロゴやキャラが登場する。
    /// 塗り替わる前（どんよりした背景など）と後（泡など）で見せる物を、覆いきった瞬間に入れ替える（<see cref="beforeObjects"/> / <see cref="afterObjects"/>）。
    /// どろーっとした垂れ方は、幕の絵を細い縦の列に分け、列ごとに少しずつ遅らせて落として作っている（先端がでこぼこに進む）。
    /// 歯磨き粉のふくらみは、再生中に絵を横じまに細かく切り分けて、段ごとに横幅を変えて作っている（絵を差し替えてもそのまま効く）。
    /// </summary>
    public sealed class TitleOpening : MonoBehaviour
    {
        public enum RevealStyle
        {
            [InspectorName("下へ抜ける")] SlideDown,
            [InspectorName("ふっと消える")] Fade,
        }

        [Header("配置物")]
        [Tooltip("歯磨き粉（上向き。キャップが上）。子の SpriteRenderer の絵を使う。")]
        public Transform tube;
        [Tooltip("つまむ指など。歯磨き粉の真ん中に向かって寄っていく（左右に1つずつ。空でもよい）。")]
        public Transform[] pinchers;
        [Tooltip("キャップ（歯磨き粉の子に置く）。噴き出す瞬間に吹き飛ぶ。空なら飛ばさない。")]
        public Transform cap;
        [Tooltip("キャップから噴き出す歯磨き粉。根元（下端）を軸に上へ伸びる。")]
        public Transform stream;
        [Tooltip("噴き出したときに飛び散る泡（Particle System）。")]
        public ParticleSystem burstParticles;
        [Tooltip("画面を塗りつぶす歯磨き粉（画面の一番手前の UI。画面いっぱいの枠で、絵は同じオブジェクトの Raw Image の Texture）。")]
        public RectTransform curtain;
        public TitleCameraMotion cameraMotion;

        [Header("出てくる")]
        [Tooltip("始まってから、歯磨き粉が出てくるまでの秒数（暗転から明ける時間と重ねる）。")]
        public float appearDelay = 0.3f;
        [Tooltip("歯磨き粉がポンと出てくるのにかける秒数。")]
        public float appearDuration = 0.45f;

        [Header("つまんでふくらむ")]
        [Tooltip("つまみ続けて、ふくらんでいく秒数。")]
        public float swellDuration = 2f;
        [Tooltip("一番ふくらんだときに、横幅が何割増えるか（0.4 = 40%太くなる）。")]
        public float swellAmount = 0.4f;
        [Tooltip("つまんだところが何割細くなるか（0.5 = 半分の太さ）。")]
        [Range(0f, 0.9f)] public float pinchAmount = 0.5f;
        [Tooltip("つまむ高さ（0 = 歯磨き粉の下端、1 = 上端）。")]
        [Range(0f, 1f)] public float pinchHeight = 0.45f;
        [Tooltip("つまむ幅（歯磨き粉の長さに対する割合）。")]
        [Range(0.02f, 0.5f)] public float pinchWidth = 0.08f;
        [Tooltip("ふくらむ範囲の下端・上端（しっぽとキャップはふくらませない）。")]
        public Vector2 swellRange = new(0.06f, 0.84f);
        [Tooltip("ふくらむにつれて縦にどれだけ伸びるか（0.08 = 8%）。")]
        public float swellStretch = 0.08f;
        [Tooltip("歯磨き粉の震えの幅（ワールドの単位。ふくらむほど強くなる）。")]
        public float tubeTremble = 0.05f;
        [Tooltip("指が真ん中へ寄る距離（ワールドの単位）。")]
        public float pincherTravel = 0.35f;
        [Tooltip("ふくらむ間にカメラが寄る量（0.15 = 15%寄る）。")]
        public float zoomIn = 0.15f;
        [Tooltip("一番ふくらんだときのカメラのブルブルの強さ（ワールドの単位）。")]
        public float rumble = 0.12f;
        [Tooltip("横じまに切り分ける数（多いほどなめらか）。")]
        [Range(8, 96)] public int sliceCount = 48;

        [Header("噴き出す")]
        [Tooltip("噴き出した歯磨き粉が伸びきるまでの秒数。")]
        public float ejectDuration = 0.4f;
        [Tooltip("噴き出した瞬間のカメラの揺れ。")]
        public float ejectShake = 0.4f;
        [Tooltip("噴き出した後、カメラが引いて元に戻るまでの秒数。")]
        public float pullBackDuration = 0.5f;
        [Tooltip("噴き出した後、歯磨き粉がしぼむ秒数。")]
        public float deflateDuration = 0.35f;
        [Tooltip("キャップが飛び出す速さ（ワールドの単位/秒、上向き）。")]
        public float capLaunchSpeed = 16f;
        [Tooltip("キャップが左右にそれる最大の速さ（毎回ランダム）。")]
        public float capLaunchSpread = 3f;
        [Tooltip("キャップの回転の速さ（度/秒。向きは毎回ランダム）。")]
        public float capSpin = 900f;
        [Tooltip("キャップにかかる重力（大きいほど早く落ちてくる。0 なら飛んでいったまま）。")]
        public float capGravity = 20f;

        [Header("塗り替わる")]
        [Tooltip("噴き出してから、上から歯磨き粉が垂れてくるまでの秒数。")]
        public float curtainDelay = 0.25f;
        [Tooltip("垂れてきて画面を覆いきるまでの秒数（一番遅い列が下に着くまで）。")]
        public float curtainDropDuration = 1.3f;
        [Tooltip("垂れ方の緩急（「ゆっくり始まりゆっくり終わる」がどろーっとした感じ）。")]
        public TitleEaseType curtainEase = TitleEaseType.InOutSine;
        [Tooltip("縦の列の数（多いほど先端がなめらか）。")]
        [Range(1, 64)] public int curtainColumns = 28;
        [Tooltip("列ごとの遅れのばらつき（0 = 横一直線で落ちる、0.6 = 大きくでこぼこ）。")]
        [Range(0f, 0.9f)] public float curtainUnevenness = 0.45f;
        [Tooltip("画面が覆われたままの秒数（この間に裏で歯磨き粉などを片付ける）。")]
        public float curtainHold = 0.25f;
        [Tooltip("塗りつぶした歯磨き粉の消え方。")]
        public RevealStyle revealStyle = RevealStyle.SlideDown;
        [Tooltip("消えきるまでの秒数。")]
        public float revealDuration = 0.9f;
        [Tooltip("塗り替わる前だけ見せる物（どんよりした背景など）。歯磨き粉が画面を覆いきった瞬間に消える。")]
        public GameObject[] beforeObjects;
        [Tooltip("塗り替わった後に見せる物（泡など）。覆いきった瞬間に出る（パーティクルはそこから流し始める）。")]
        public GameObject[] afterObjects;
        [Tooltip("消え始めてから、ロゴやキャラの登場の合図を出すまでの秒数。")]
        public float handOffDelay = 0.15f;

        [Header("エフェクト・効果音（D-Drive）")]
        [Tooltip("つまみ始めた瞬間（歯磨き粉の位置）。")]
        public FortressEffect onPinch = new() { tintWithPlayerColor = false };
        [Tooltip("噴き出した瞬間（キャップの位置）。")]
        public FortressEffect onEject = new() { tintWithPlayerColor = false };
        [Tooltip("幕が画面を覆いきった瞬間（画面の真ん中）。")]
        public FortressEffect onCovered = new() { tintWithPlayerColor = false };

        /// <summary>ロゴやキャラの登場を始めてよい合図。</summary>
        public event Action Finished;

        private enum Stage { Idle, Waiting, Appearing, Swelling, Ejecting, Covering, Holding, Revealing, Done }

        private Stage stage = Stage.Idle;
        private float stageTime;
        private float ejectTime;
        private bool finished;

        private Vector3 tubeBasePosition;
        private Vector3 tubeBaseScale;
        private SpriteRenderer tubeRenderer;
        private float tubeBaseAlpha = 1f;
        private readonly List<SpriteRenderer> slices = new();
        private readonly List<float> sliceHeights = new(); // 0..1（下→上）
        private readonly List<Sprite> createdSprites = new();
        private Vector3[] pincherBase = Array.Empty<Vector3>();
        private Vector3 streamBaseScale;
        private Transform capParent;
        private Vector3 capLocalPosition;
        private Quaternion capLocalRotation;
        private Vector3 capLocalScale;
        private Vector3 capVelocity;
        private float capSpinSpeed;
        private bool capFlying;
        private readonly List<RawImage> curtainCols = new();
        private readonly List<float> curtainLags = new(); // 0..1（列ごとの遅れ）

        public bool IsDone => stage == Stage.Done;

        private void Awake()
        {
            if (tube != null)
            {
                tubeBasePosition = tube.localPosition;
                tubeBaseScale = tube.localScale;
                // チューブの絵は子の「Sprite」。無ければ最初に見つかった SpriteRenderer（キャップの絵を拾わないよう名前で優先する）。
                var tubeSpriteChild = tube.Find("Sprite");
                tubeRenderer = tubeSpriteChild != null ? tubeSpriteChild.GetComponent<SpriteRenderer>() : tube.GetComponentInChildren<SpriteRenderer>(true);
                if (tubeRenderer != null) tubeBaseAlpha = tubeRenderer.color.a;
                BuildSlices();
            }

            pincherBase = new Vector3[pinchers?.Length ?? 0];
            for (var i = 0; i < pincherBase.Length; i++)
            {
                if (pinchers[i] != null) pincherBase[i] = pinchers[i].localPosition;
            }

            if (stream != null) streamBaseScale = stream.localScale;
            if (cap != null)
            {
                capParent = cap.parent;
                capLocalPosition = cap.localPosition;
                capLocalRotation = cap.localRotation;
                capLocalScale = cap.localScale;
            }
            BuildCurtainColumns();

            ResetToStart();
        }

        private void OnDestroy()
        {
            foreach (var sprite in createdSprites)
            {
                if (sprite != null) Destroy(sprite);
            }
        }

        /// <summary>始まる前の状態に戻す（隠れて合図を待つ）。「最初から再生」用。</summary>
        public void ResetToStart()
        {
            stage = Stage.Idle;
            stageTime = 0f;
            finished = false;
            if (tube != null)
            {
                tube.gameObject.SetActive(true);
                tube.localPosition = tubeBasePosition;
                tube.localScale = Vector3.zero;
            }
            SetTubeShape(0f, 0f);
            SetTubeAlpha(1f);
            SetPinch(0f);
            SetPinchersVisible(false);
            SetStream(0f);
            SetCurtain(0f, 1f);
            SetPainted(false);
            ResetCap();
            if (burstParticles != null) burstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // キャップを歯磨き粉の上に戻す（歯磨き粉と一緒に出てきて、一緒に震える）。
        private void ResetCap()
        {
            capFlying = false;
            if (cap == null) return;
            cap.SetParent(capParent, false);
            cap.localPosition = capLocalPosition;
            cap.localRotation = capLocalRotation;
            cap.localScale = capLocalScale;
            cap.gameObject.SetActive(true);
        }

        // 噴き出す瞬間：キャップを歯磨き粉から外して、上へ吹き飛ばす。
        private void LaunchCap()
        {
            if (cap == null) return;
            cap.SetParent(transform, true); // 見た目の位置・大きさのまま外す
            capVelocity = new Vector3(UnityEngine.Random.Range(-capLaunchSpread, capLaunchSpread), capLaunchSpeed, 0f);
            capSpinSpeed = capSpin * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            capFlying = true;
        }

        private void UpdateCap(float dt)
        {
            if (!capFlying || cap == null) return;
            capVelocity.y -= capGravity * dt;
            cap.position += capVelocity * dt;
            cap.Rotate(0f, 0f, capSpinSpeed * dt, Space.Self);
        }

        public void Play()
        {
            ResetToStart();
            Next(Stage.Waiting);
        }

        /// <summary>途中を飛ばして終わらせる（入力でのスキップ用）。合図はすぐ出す。</summary>
        public void Skip()
        {
            if (stage == Stage.Done) return;
            HideTubeAndStream();
            SetCurtain(0f, 1f);
            SetPainted(true);
            if (cameraMotion != null)
            {
                cameraMotion.SetRumble(0f);
                cameraMotion.ZoomTo(0f, 0.15f);
            }
            stage = Stage.Done;
            Finish();
        }

        private void Update()
        {
            if (stage == Stage.Idle || stage == Stage.Done) return;
            var dt = Time.deltaTime;
            stageTime += dt;
            if (stage >= Stage.Ejecting) ejectTime += dt;
            UpdateCap(dt);

            switch (stage)
            {
                case Stage.Waiting:
                    if (stageTime >= appearDelay) Next(Stage.Appearing);
                    break;

                case Stage.Appearing:
                {
                    var t = appearDuration <= 0f ? 1f : stageTime / appearDuration;
                    SetTubeTransform(Vector3.zero, TitleEase.Evaluate(TitleEaseType.OutBack, Mathf.Clamp01(t)), 1f);
                    if (t >= 1f) BeginSwell();
                    break;
                }

                case Stage.Swelling:
                {
                    var t = swellDuration <= 0f ? 1f : Mathf.Clamp01(stageTime / swellDuration);
                    // 力は「ゆっくり → 最後にぐっと」強くなる。
                    var grip = TitleEase.Evaluate(TitleEaseType.InCubic, t) * 0.65f + t * 0.35f;
                    var tremble = (Vector3)(UnityEngine.Random.insideUnitCircle * (tubeTremble * grip));
                    SetTubeTransform(tremble, 1f, 1f + swellStretch * grip);
                    SetTubeShape(swellAmount * grip, pinchAmount * grip);
                    SetPinch(grip);
                    if (cameraMotion != null) cameraMotion.SetRumble(rumble * grip);
                    if (t >= 1f) Eject();
                    break;
                }

                case Stage.Ejecting:
                {
                    UpdateAfterEject();
                    if (ejectTime >= curtainDelay)
                    {
                        Next(Stage.Covering);
                    }
                    break;
                }

                case Stage.Covering:
                {
                    UpdateAfterEject();
                    var t = curtainDropDuration <= 0f ? 1f : Mathf.Clamp01(stageTime / curtainDropDuration);
                    SetCurtain(t, 1f);
                    if (t >= 1f)
                    {
                        Next(Stage.Holding);
                        HideTubeAndStream();
                        SetPainted(true);
                        if (cameraMotion != null) cameraMotion.Shake(ejectShake * 0.5f);
                        TitleEffects.Play(onCovered, Camera.main != null ? Camera.main.transform.position + Vector3.forward * 10f : Vector3.zero);
                    }
                    break;
                }

                case Stage.Holding:
                    if (stageTime >= curtainHold) Next(Stage.Revealing);
                    break;

                case Stage.Revealing:
                {
                    var t = revealDuration <= 0f ? 1f : Mathf.Clamp01(stageTime / revealDuration);
                    if (revealStyle == RevealStyle.SlideDown) SetCurtain(1f + t, 1f);
                    else SetCurtain(1f, 1f - t);
                    if (!finished && stageTime >= handOffDelay) Finish();
                    if (t >= 1f)
                    {
                        SetCurtain(0f, 1f);
                        if (!finished) Finish();
                        stage = Stage.Done;
                    }
                    break;
                }
            }
        }

        private void BeginSwell()
        {
            Next(Stage.Swelling);
            SetPinchersVisible(true);
            if (cameraMotion != null) cameraMotion.ZoomTo(zoomIn, swellDuration, TitleEaseType.InOutSine);
            TitleEffects.Play(onPinch, TubePosition);
        }

        private void Eject()
        {
            Next(Stage.Ejecting);
            ejectTime = 0f;
            var mouth = CapPosition;
            LaunchCap();
            if (stream != null) stream.position = new Vector3(mouth.x, mouth.y, stream.position.z);
            if (burstParticles != null)
            {
                burstParticles.transform.position = mouth;
                burstParticles.Play(true);
            }
            if (cameraMotion != null)
            {
                cameraMotion.SetRumble(0f);
                cameraMotion.Shake(ejectShake);
                cameraMotion.ZoomTo(0f, pullBackDuration, TitleEaseType.OutBack);
            }
            TitleEffects.Play(onEject, mouth);
        }

        // 噴き出した後：歯磨き粉が伸び、チューブはしぼむ（幕が覆うまで続ける）。
        private void UpdateAfterEject()
        {
            var e = ejectDuration <= 0f ? 1f : Mathf.Clamp01(ejectTime / ejectDuration);
            SetStream(TitleEase.Evaluate(TitleEaseType.OutCubic, e));

            var d = deflateDuration <= 0f ? 1f : Mathf.Clamp01(ejectTime / deflateDuration);
            var swell = Mathf.Lerp(swellAmount, -0.1f, TitleEase.Evaluate(TitleEaseType.OutCubic, d));
            SetTubeShape(swell, pinchAmount);
            SetTubeTransform(Vector3.zero, 1f, 1f + swellStretch * (1f - d));
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Finished?.Invoke();
        }

        private void Next(Stage next)
        {
            stage = next;
            stageTime = 0f;
        }

        private Vector3 TubePosition => tube != null ? tube.position : transform.position;

        // 歯磨き粉の口（チューブの絵の上端）の位置。絵の大きさから求める。
        private Vector3 CapPosition
        {
            get
            {
                if (tubeRenderer == null || tubeRenderer.sprite == null) return TubePosition;
                var b = tubeRenderer.sprite.bounds;
                return tubeRenderer.transform.TransformPoint(new Vector3(b.center.x, b.max.y, 0f));
            }
        }

        private void SetTubeTransform(Vector3 offset, float scale, float stretchY)
        {
            if (tube == null) return;
            tube.localPosition = tubeBasePosition + offset;
            tube.localScale = Vector3.Scale(tubeBaseScale, new Vector3(scale, scale * stretchY, 1f));
        }

        // 横じま1段ごとに横幅を変えて、「つまんだところが細く、まわりがふくらむ」形にする。
        private void SetTubeShape(float swell, float pinch)
        {
            for (var i = 0; i < slices.Count; i++)
            {
                var v = sliceHeights[i];
                var range = Mathf.Max(0.001f, swellRange.y - swellRange.x);
                var body = Mathf.Clamp01((v - swellRange.x) / range);
                var bulge = (v < swellRange.x || v > swellRange.y) ? 0f : Mathf.Pow(Mathf.Sin(body * Mathf.PI), 0.6f);
                var d = (v - pinchHeight) / Mathf.Max(0.001f, pinchWidth);
                var pinchShape = Mathf.Exp(-d * d);
                var width = 1f + swell * bulge * (1f - pinchShape) - pinch * pinchShape;
                var s = slices[i].transform.localScale;
                slices[i].transform.localScale = new Vector3(Mathf.Max(0.05f, width), s.y, s.z);
            }
        }

        private void SetPinch(float amount)
        {
            if (pinchers == null) return;
            for (var i = 0; i < pinchers.Length; i++)
            {
                if (pinchers[i] == null) continue;
                // 真ん中（x=0）へ向かって寄る。
                var toward = -Mathf.Sign(pincherBase[i].x);
                pinchers[i].localPosition = pincherBase[i] + new Vector3(toward * pincherTravel * amount, 0f, 0f);
            }
        }

        private void SetPinchersVisible(bool visible)
        {
            if (pinchers == null) return;
            foreach (var p in pinchers)
            {
                if (p != null) p.gameObject.SetActive(visible);
            }
        }

        private void SetStream(float length)
        {
            if (stream == null) return;
            stream.gameObject.SetActive(length > 0f);
            var wobble = 1f + 0.12f * Mathf.Sin(Time.time * 40f) * length;
            stream.localScale = new Vector3(streamBaseScale.x * wobble, streamBaseScale.y * Mathf.Max(0.0001f, length), streamBaseScale.z);
        }

        // cover（全体の進み具合）: 0 = 画面の上に隠れている、1 = 画面を覆っている、2 = 画面の下へ流れ落ちた。
        // 列ごとに遅れ（lag）があり、早い列が先に垂れてくる。全部の列が 1 で覆いきる。
        private void SetCurtain(float cover, float alpha)
        {
            if (curtain == null || curtainCols.Count == 0) return;
            var screenHeight = curtain.rect.height > 1f ? curtain.rect.height : 1080f;
            var height = screenHeight * CurtainHeightRatio;
            // 覆いきったとき、絵の下端（垂れたふち）が画面の下からはみ出す量。
            var coveredDrop = screenHeight + (height - screenHeight) * 0.6f;
            var spread = Mathf.Clamp(curtainUnevenness, 0f, 0.9f);

            for (var i = 0; i < curtainCols.Count; i++)
            {
                var col = curtainCols[i];
                float drop;
                if (cover <= 1f)
                {
                    var local = Mathf.Clamp01((cover - curtainLags[i] * spread) / (1f - spread));
                    drop = coveredDrop * TitleEase.Evaluate(curtainEase, local);
                }
                else
                {
                    var local = Mathf.Clamp01((cover - 1f - curtainLags[i] * spread) / (1f - spread));
                    drop = coveredDrop + (screenHeight + height) * TitleEase.Evaluate(TitleEaseType.InCubic, local);
                }
                col.rectTransform.sizeDelta = new Vector2(1f, height); // 横は列の幅＋1px（すき間防止）
                col.rectTransform.anchoredPosition = new Vector2(0f, -drop);
                var c = col.color;
                c.a = alpha;
                col.color = c;
                col.enabled = alpha > 0f && drop > 0.5f && drop < coveredDrop + screenHeight + height;
            }
        }

        private const float CurtainHeightRatio = 1.35f;

        // 幕の絵（curtain の Raw Image）を縦の列に分けて、画面の上に並べる。元の Raw Image は隠す。
        private void BuildCurtainColumns()
        {
            if (curtain == null) return;
            var template = curtain.GetComponent<RawImage>();
            if (template == null || template.texture == null) return;
            template.enabled = false;

            var count = Mathf.Max(1, curtainColumns);
            var seed = UnityEngine.Random.Range(0f, 100f);
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject($"Column {i}", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(curtain, false);
                rect.anchorMin = new Vector2((float)i / count, 1f);
                rect.anchorMax = new Vector2((float)(i + 1) / count, 1f);
                rect.pivot = new Vector2(0.5f, 0f);
                var image = go.AddComponent<RawImage>();
                image.texture = template.texture;
                image.color = template.color;
                image.raycastTarget = false;
                image.uvRect = new Rect((float)i / count, 0f, 1f / count, 1f);
                curtainCols.Add(image);
                // 隣どうしは似た遅れにして、先端がなだらかに波打つようにする。
                var n = Mathf.PerlinNoise(i * 0.35f, seed);
                curtainLags.Add(Mathf.Clamp01((n - 0.2f) / 0.6f));
            }
        }

        // 塗り替わる前／後の物を切り替える（覆われて見えない間に行う）。
        private void SetPainted(bool painted)
        {
            if (beforeObjects != null)
            {
                foreach (var go in beforeObjects)
                {
                    if (go != null) go.SetActive(!painted);
                }
            }
            if (afterObjects == null) return;
            foreach (var go in afterObjects)
            {
                if (go == null) continue;
                var wasActive = go.activeSelf;
                go.SetActive(painted);
                if (!painted || wasActive) continue;
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);
            }
        }

        private void HideTubeAndStream()
        {
            if (tube != null) tube.gameObject.SetActive(false);
            if (stream != null) stream.gameObject.SetActive(false);
            capFlying = false;
            if (cap != null) cap.gameObject.SetActive(false);
            SetPinchersVisible(false);
        }

        private void SetTubeAlpha(float alpha)
        {
            foreach (var sr in slices)
            {
                if (sr == null) continue;
                var c = sr.color;
                c.a = tubeBaseAlpha * alpha;
                sr.color = c;
            }
        }

        // 歯磨き粉の絵を横じまに切り分け、同じ場所に並べる（元の絵は隠す）。
        private void BuildSlices()
        {
            if (tubeRenderer == null || tubeRenderer.sprite == null) return;
            var sprite = tubeRenderer.sprite;
            var rect = sprite.rect;
            var ppu = sprite.pixelsPerUnit;
            var count = Mathf.Clamp(sliceCount, 1, Mathf.Max(1, (int)rect.height));
            var parent = tubeRenderer.transform;

            for (var i = 0; i < count; i++)
            {
                var y0 = Mathf.Round(rect.height * i / count);
                var y1 = Mathf.Round(rect.height * (i + 1) / count);
                var sliceRect = new Rect(rect.x, rect.y + y0, rect.width, y1 - y0);
                var slice = Sprite.Create(sprite.texture, sliceRect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
                slice.name = $"{sprite.name} slice {i}";
                createdSprites.Add(slice);

                var go = new GameObject($"Slice {i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(
                    (rect.width * 0.5f - sprite.pivot.x) / ppu,
                    ((y0 + y1) * 0.5f - sprite.pivot.y) / ppu,
                    0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = slice;
                sr.sharedMaterial = tubeRenderer.sharedMaterial;
                sr.color = tubeRenderer.color;
                sr.sortingLayerID = tubeRenderer.sortingLayerID;
                sr.sortingOrder = tubeRenderer.sortingOrder;
                slices.Add(sr);
                sliceHeights.Add((y0 + y1) * 0.5f / rect.height);
            }
            tubeRenderer.enabled = false;
        }
    }
}
