using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の右のモニター。自分が決定するまで、今選んでいるキャラのスキルの動画（キャラのファイルの「スキルの動画」）を流す。
    /// ・カーソルを動かすと、その瞬間に次のキャラの動画に替えて最初から流す（音なし・くり返し）
    /// ・動画が無いキャラは、立ち絵と「スキル動画は準備中」を出す
    /// ・自分が決定すると、右の蝶番を軸に扉のように開いて消え、後ろから「ほかの人の欄」が出てくる。取り消すと閉じて戻ってくる
    /// </summary>
    [AddComponentMenu("キャラ選択/スキル動画のモニター (SkillMonitor)")]
    public sealed class SkillMonitor : MonoBehaviour
    {
        [Tooltip("開く・閉じるで回る部分（中心＝右の蝶番の所にしておく）。空ならこの物。")]
        public RectTransform door;

        [Tooltip("全体の濃さ（開ききる前に消す）。")]
        public CanvasGroup group;

        [Tooltip("動画を映す所（モニターの白い所）。")]
        public RawImage screen;

        [Tooltip("動画を流す部品（音なし・くり返しにしておく）。")]
        public VideoPlayer player;

        [Tooltip("動画が無いときに出す物（立ち絵と「準備中」の文字をまとめた物）。")]
        public GameObject placeholder;

        [Tooltip("動画が無いときに出す立ち絵。")]
        public Image placeholderPortrait;

        [Tooltip("動画が無いときに出す文字（{0} にスキル名が入る）。")]
        public Text placeholderText;

        [Tooltip("動画が無いときの文字。{0} = スキル名。")]
        public string placeholderFormat = "{0}\n<size=26>スキル動画は準備中</size>";

        [Tooltip("動画を描く大きさ（ピクセル。動画の縦横比に合わせると、にじまない）。")]
        public Vector2Int renderSize = new Vector2Int(1280, 720);

        [Header("開く・閉じる")]
        [Tooltip("開くのにかける秒数。")]
        [Min(0.01f)]
        public float openSeconds = 0.5f;

        [Tooltip("閉じて戻ってくるのにかける秒数。")]
        [Min(0.01f)]
        public float closeSeconds = 0.4f;

        [Tooltip("開ききったときの角度（度）。")]
        public float openAngle = 100f;

        /// <summary>開いている度合い（0 = 閉じている、1 = 開ききって消えた）。ほかの人の欄が出るのに使う。</summary>
        public float Openness => _open;

        private RenderTexture _texture;
        private CharacterDefinition _shown;
        private bool _hasShown;
        private bool _opened;
        private float _open;

        private void Awake()
        {
            if (player != null)
            {
                player.playOnAwake = false;
                player.isLooping = true;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                _texture = new RenderTexture(Mathf.Max(16, renderSize.x), Mathf.Max(16, renderSize.y), 0) { name = "SkillMonitor" };
                player.targetTexture = _texture;
            }

            if (screen != null)
            {
                screen.texture = _texture;
                screen.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.targetTexture = null;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        /// <summary>状態を表示する（毎フレーム呼ばれる）。opened = 自分が決定した（モニターを開いて消す）。</summary>
        public void Show(CharacterDefinition character, bool opened)
        {
            if (!_hasShown || character != _shown)
            {
                _shown = character;
                _hasShown = true;
                ShowCharacter(character);
            }

            if (opened != _opened)
            {
                _opened = opened;
                if (player != null && player.clip != null)
                {
                    // 開いている（見えない）間は止めておき、閉じたらそのキャラの動画を最初から流す。
                    if (opened)
                    {
                        player.Pause();
                    }
                    else
                    {
                        Restart();
                    }
                }
            }

            var dt = Time.unscaledDeltaTime;
            _open = Mathf.MoveTowards(_open, opened ? 1f : 0f, dt / (opened ? openSeconds : closeSeconds));
            var k = opened ? CharacterSelectEase.CubicIn(_open) : 1f - CharacterSelectEase.BackOut(1f - _open);
            var target = door != null ? door : (RectTransform)transform;
            target.localRotation = Quaternion.Euler(0f, -openAngle * k, 0f);
            if (group != null)
            {
                group.alpha = 1f - Mathf.Clamp01((_open - 0.6f) / 0.4f);
            }
        }

        private void ShowCharacter(CharacterDefinition character)
        {
            var clip = character != null ? character.skillVideo : null;
            var hasVideo = clip != null && player != null;
            if (player != null)
            {
                if (hasVideo)
                {
                    player.clip = clip;
                    Restart();
                }
                else
                {
                    player.Stop();
                    player.clip = null;
                }
            }

            if (screen != null)
            {
                screen.enabled = hasVideo;
            }

            if (placeholder != null && placeholder.activeSelf == hasVideo)
            {
                placeholder.SetActive(!hasVideo);
            }

            if (placeholderPortrait != null)
            {
                var sprite = character != null ? character.sprite : null;
                placeholderPortrait.enabled = sprite != null;
                placeholderPortrait.sprite = sprite;
                placeholderPortrait.color = character != null ? character.spriteTint : Color.white;
            }

            if (placeholderText != null)
            {
                var skill = character != null && !string.IsNullOrEmpty(character.skillName) ? character.skillName : "スキル";
                placeholderText.text = string.Format(placeholderFormat, skill);
            }
        }

        private void ClearTexture()
        {
            if (_texture == null)
            {
                return;
            }

            var active = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;
        }

        private void Restart()
        {
            if (player == null || player.clip == null)
            {
                return;
            }

            // 最初から流す（前のキャラの最後の絵が一瞬残らないよう、止めて画面を消してから流す）。
            player.Stop();
            ClearTexture();
            player.time = 0;
            player.Play();
        }
    }
}
