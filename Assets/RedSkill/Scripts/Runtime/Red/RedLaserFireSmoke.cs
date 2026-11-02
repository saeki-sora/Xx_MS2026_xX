using MS2026.Fortress.Billboards;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// スキルのレーザーを撃った瞬間に、砲台の先端で手描きの煙（衝撃で左右へ吹き出す煙）のアニメを1回だけ出す（<see cref="RedLaserVisuals"/> が1つ持つ）。
    /// 出る場所は砲台の向いている方向と逆（砲台の後ろ。レーザーの反動で後ろへ吹き出す煙）。
    /// 絵は設定ファイルの「撃った瞬間の煙」のコマ。ふつうは床に寝かせ、角度を砲台のキャラの絵とそろえる（絵の上 = 向きと逆、キャラの絵と同じ回し方）。
    /// 「砲台と一緒に回る」が ON なら、出ている間も砲台の回転について行く（OFF なら撃った瞬間の場所・角度に残る）。
    /// 「カメラの方へ立てる」を ON にすると、絵そのものをカメラと同じ向きに回して立てる。
    /// キャラの絵（描く順番 10）の後ろに出すため、ゲームのビルボードの仕組み（深度を書くので描く順番が効かなくなる）は使わない。
    /// </summary>
    internal sealed class RedLaserFireSmoke
    {
        private const float ZOffset = -0.07f;

        private readonly RedLaserSkill _def;
        private readonly GameObject _root;
        private readonly SpriteRenderer _renderer;
        private bool _playing;
        private float _frame;
        private Vector3 _position;
        private Vector2 _aim = Vector2.up;

        public RedLaserFireSmoke(RedLaserSkill definition, Transform parent)
        {
            _def = definition;
            _root = new GameObject("FireSmoke") { hideFlags = HideFlags.DontSave };
            _root.transform.SetParent(parent, false);

            // ビルボードの仕組みに起こされないようにする（立てるときは自分で回す）。
            _root.AddComponent<BillboardIgnore>();
            _renderer = _root.AddComponent<SpriteRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        /// <summary>撃った瞬間に呼ぶ。<paramref name="center"/> = 砲台の中心、<paramref name="aim"/> = 砲台の向き。</summary>
        public void Play(Vector3 center, Vector2 aim)
        {
            var settings = _def.fireSmoke;
            if (!settings.enabled || settings.frames == null || settings.frames.Length == 0)
            {
                return;
            }

            _aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.up;
            _position = center - (Vector3)(_aim * settings.backOffset);
            _frame = 0f;
            _playing = true;
        }

        /// <summary>毎フレーム呼ぶ。<paramref name="center"/> = 今の砲台の中心、<paramref name="aim"/> = 今の砲台の向き（「砲台と一緒に回る」用）。</summary>
        public void Tick(float dt, Vector3 center, Vector2 aim)
        {
            if (!_playing)
            {
                return;
            }

            var settings = _def.fireSmoke;
            if (settings.followTurret && aim.sqrMagnitude > 0.0001f)
            {
                _aim = aim.normalized;
                _position = center - (Vector3)(_aim * settings.backOffset);
            }

            var frames = settings.frames;
            _frame += dt * Mathf.Max(1f, settings.framesPerSecond);
            if (!settings.enabled || frames == null || frames.Length == 0 || _frame >= frames.Length)
            {
                Hide();
                return;
            }

            var sprite = frames[(int)_frame];
            if (sprite == null)
            {
                _renderer.enabled = false;
                return;
            }

            // 絵の横幅（ふちの透明な所も含む）が「大きさ」になるようにする。
            var scale = settings.size / Mathf.Max(0.001f, sprite.bounds.size.x);
            var position = new Vector3(_position.x, _position.y, _position.z + ZOffset);
            Quaternion rotation;
            var cam = Camera.main;
            if (settings.standUp && cam != null)
            {
                // カメラと同じ向き = 画面に正面を向けて立つ。「画面の上へのずらし」は画面の上方向へ動かす。
                rotation = cam.transform.rotation;
                position += cam.transform.up * settings.screenUpOffset;
            }
            else
            {
                // 床に寝かせる: 絵の上 = 向きと逆（キャラの絵と同じ角度）。
                rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-_aim.y, -_aim.x) * Mathf.Rad2Deg - 90f);
            }

            var transform = _renderer.transform;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = new Vector3(scale, scale, 1f);
            _renderer.sprite = sprite;
            _renderer.color = settings.tint;
            _renderer.sortingOrder = settings.sortingOrder;
            _renderer.enabled = true;
        }

        public void Hide()
        {
            _playing = false;
            _renderer.enabled = false;
        }
    }
}
