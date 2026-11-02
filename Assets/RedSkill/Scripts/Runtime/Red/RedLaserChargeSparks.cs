using MS2026.Fortress.Billboards;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// 溜め（スキルを使える間〜撃つ前のため）の間、砲台の先端（レーザーが出る所）で手描きの火花のアニメを繰り返す（<see cref="RedLaserVisuals"/> が1つ持つ）。
    /// 絵は設定ファイルの「溜めの火花」のコマ（濃い色・明るい色の2枚重ね。同じコマ番号を同時に出す）。
    /// 何個かを少しずつずらして出し、1回終わるごとに向き・裏返し・位置を変えるので、同じ絵の繰り返しに見えにくい。
    /// ためでは大きく・速くなる。床に寝かせたまま描く（レーザーと同じ平面）。
    /// </summary>
    internal sealed class RedLaserChargeSparks
    {
        private const int MaxBursts = 4;
        private const float ZOffset = -0.06f;

        private readonly RedLaserSkill _def;
        private readonly GameObject _root;
        private readonly Burst[] _bursts = new Burst[MaxBursts];
        private bool _shown;

        /// <summary>火花1個分（2枚重ねの絵）。</summary>
        private sealed class Burst
        {
            public SpriteRenderer Dark;
            public SpriteRenderer Light;
            /// <summary>何コマ目か（小数。マイナスの間は次を出すまで待っている）。</summary>
            public float Frame;
            public float Angle;
            public bool Flip;
            public Vector2 Offset;
        }

        public RedLaserChargeSparks(RedLaserSkill definition, Transform parent)
        {
            _def = definition;
            _root = new GameObject("ChargeSparks") { hideFlags = HideFlags.DontSave };
            _root.transform.SetParent(parent, false);
            for (var i = 0; i < MaxBursts; i++)
            {
                _bursts[i] = new Burst
                {
                    Dark = CreateRenderer("Spark" + i + "Dark"),
                    Light = CreateRenderer("Spark" + i + "Light")
                };
            }
        }

        /// <summary>
        /// 毎フレーム呼ぶ。<paramref name="charging"/> = 使える状態か、<paramref name="windup01"/> = ため（0〜1、ため中でなければ負）。
        /// <paramref name="tip"/> = 砲台の先端、<paramref name="aim"/> = 砲台の向き。
        /// </summary>
        public void Tick(float dt, bool charging, float windup01, Vector3 tip, Vector2 aim)
        {
            var settings = _def.sparks;
            var frameCount = Mathf.Max(Length(settings.darkFrames), Length(settings.lightFrames));
            var wanted = settings.enabled && frameCount > 0 && (charging || windup01 >= 0f);
            if (!wanted)
            {
                if (_shown)
                {
                    HideAll();
                }

                _shown = false;
                return;
            }

            var count = Mathf.Clamp(settings.count, 1, MaxBursts);
            if (!_shown)
            {
                // 出し始め: 1個目はすぐ、残りは1回分の長さを等分した分だけ遅らせる。
                for (var i = 0; i < MaxBursts; i++)
                {
                    Restart(_bursts[i], settings);
                    _bursts[i].Frame = -frameCount * i / (float)count;
                }

                _shown = true;
            }

            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = Vector2.up;
            }

            aim.Normalize();
            var windup = windup01 >= 0f ? windup01 : 0f;
            var fps = Mathf.Max(1f, settings.framesPerSecond) * Mathf.Lerp(1f, settings.windupSpeedMultiplier, windup);
            var size = settings.size * Mathf.Lerp(1f, settings.windupSizeMultiplier, windup);
            var center = (Vector2)tip + aim * settings.forwardOffset;
            var aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 90f;

            for (var i = 0; i < MaxBursts; i++)
            {
                var burst = _bursts[i];
                if (i >= count)
                {
                    burst.Dark.enabled = false;
                    burst.Light.enabled = false;
                    continue;
                }

                burst.Frame += dt * fps;
                if (burst.Frame >= frameCount)
                {
                    Restart(burst, settings);
                }

                if (burst.Frame < 0f)
                {
                    burst.Dark.enabled = false;
                    burst.Light.enabled = false;
                    continue;
                }

                var frame = Mathf.Min((int)burst.Frame, frameCount - 1);
                var angle = settings.randomRotation ? burst.Angle : aimAngle;
                var position = new Vector3(center.x + burst.Offset.x, center.y + burst.Offset.y, tip.z + ZOffset);
                var rotation = Quaternion.Euler(0f, 0f, angle);
                Show(burst.Dark, Pick(settings.darkFrames, frame), settings.darkTint, position, rotation, size, burst.Flip, settings.sortingOrder);
                Show(burst.Light, Pick(settings.lightFrames, frame), settings.lightTint, position, rotation, size, burst.Flip, settings.sortingOrder + 1);
            }
        }

        public void HideAll()
        {
            for (var i = 0; i < MaxBursts; i++)
            {
                _bursts[i].Dark.enabled = false;
                _bursts[i].Light.enabled = false;
            }
        }

        /// <summary>次の1回の準備: 少し間をあけ、向き・裏返し・位置を変える。</summary>
        private static void Restart(Burst burst, RedLaserSparkFx settings)
        {
            var gapMin = Mathf.Max(0f, Mathf.Min(settings.gapSeconds.x, settings.gapSeconds.y));
            var gapMax = Mathf.Max(gapMin, Mathf.Max(settings.gapSeconds.x, settings.gapSeconds.y));
            burst.Frame = -Random.Range(gapMin, gapMax) * Mathf.Max(1f, settings.framesPerSecond);
            burst.Angle = Random.Range(0f, 360f);
            burst.Flip = Random.value < 0.5f;
            burst.Offset = Random.insideUnitCircle * settings.jitter;
        }

        private static void Show(SpriteRenderer renderer, Sprite sprite, Color tint, Vector3 position, Quaternion rotation, float size,
            bool flip, int sortingOrder)
        {
            if (sprite == null)
            {
                renderer.enabled = false;
                return;
            }

            // 絵の横幅（ふちの透明な所も含む）が「大きさ」になるようにする。
            var width = Mathf.Max(0.001f, sprite.bounds.size.x);
            var scale = size / width;
            var transform = renderer.transform;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = new Vector3(scale, scale, 1f);
            renderer.sprite = sprite;
            renderer.flipX = flip;
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = true;
        }

        private static Sprite Pick(Sprite[] frames, int frame)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            return frames[Mathf.Min(frame, frames.Length - 1)];
        }

        private static int Length(Sprite[] frames) => frames != null ? frames.Length : 0;

        private SpriteRenderer CreateRenderer(string objectName)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);

            // 床に寝かせたまま描く（ビルボードで起こさない。レーザーと同じ平面に置くため）。
            go.AddComponent<BillboardIgnore>();
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }
    }
}
