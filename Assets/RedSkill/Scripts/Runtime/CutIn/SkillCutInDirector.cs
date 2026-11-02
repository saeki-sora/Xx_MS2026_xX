using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills
{
    /// <summary>
    /// カットインを画面に出す係（シーンに1つ。スキルを配る係が <see cref="Ensure"/> で作る）。
    /// スキル欄からの合図（<see cref="SkillCutIn.Cue"/>）を受けて、自分（このPCのプレイヤー）なら画面の左に大きい窓、
    /// ほかの人なら HUD の「ほかの3人の欄」のうちその人の欄のそばに小さい窓を出す。
    /// 同じ場所にもう窓が出ていたら、その窓を閉じて新しい窓を開く（後から撃った人に切り替える）。
    /// 自分専用の Canvas（画面に重ねる型、HUD と同じ 1920×1080 基準）に描くので、HUD のプレハブは触らない。
    /// 時間は Time.unscaledDeltaTime で進める（ヒットストップなどで止まらない）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class SkillCutInDirector : MonoBehaviour
    {
        private const string ObjectName = "[RedSkill] Skill Cut-In";
        private const string OtherRowsName = "OtherPlayers";
        private const float RowSearchInterval = 2f;

        private static SkillCutInDirector _instance;

        private readonly List<Live> _live = new List<Live>();
        private SkillCutInSettings _settings;
        private Canvas _canvas;
        private RectTransform _windows;
        private RectTransform _otherRows;
        private float _nextRowSearch;

        private struct Live
        {
            public int Player;
            public SkillCutInWindow Window;
        }

        /// <summary>今のシーンの係（無ければ null）。</summary>
        public static SkillCutInDirector Instance => _instance;

        /// <summary>設定を入れ、係が無ければ作る。設定が null ならカットインなし（係も作らない）。</summary>
        public static SkillCutInDirector Ensure(SkillCutInSettings settings)
        {
            SkillCutIn.Settings = settings;
            if (settings == null)
            {
                return _instance;
            }

            if (_instance == null)
            {
                var go = new GameObject(ObjectName, typeof(RectTransform));
                _instance = go.AddComponent<SkillCutInDirector>();
            }

            _instance._settings = settings;
            _instance._canvas.sortingOrder = settings.sortingOrder;
            return _instance;
        }

        /// <summary>このPCのプレイヤー番号（ネット対戦でないときは見ている視点、それも無ければ P1）。</summary>
        public static int LocalPlayer()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap != null && bootstrap.LocalPlayerIndex >= 0)
            {
                return bootstrap.LocalPlayerIndex;
            }

            var rig = FortressCameraRig.Active;
            return rig != null && ViewerIndex.IsPlayer(rig.CurrentViewer) ? rig.CurrentViewer : 0;
        }

        /// <summary>効果音・声を鳴らす（このゲームの SE は位置で変わらない）。</summary>
        internal static void PlaySound(FortressEffect effect)
        {
            FortressEffectPlayer.PlaySound(effect, Vector3.zero);
        }

        /// <summary>
        /// カットインを出す。<paramref name="big"/> が null なら、このPCのプレイヤーなら大きい窓、ほかの人なら小さい窓。
        /// （エディタの試し再生からも呼ぶ）
        /// </summary>
        public void Show(int playerIndex, SkillCutInEntry entry, bool? big = null)
        {
            if (_settings == null || entry == null || entry.picture == null)
            {
                return;
            }

            var isBig = big ?? playerIndex == LocalPlayer();
            if (!isBig && !_settings.small.show)
            {
                return;
            }

            // 同じ場所（大きい窓どうし、または同じ人の小さい窓）に出ている窓は閉じて、新しい窓に切り替える。
            foreach (var live in _live)
            {
                if (live.Window.IsBig == isBig && (isBig || live.Player == playerIndex))
                {
                    live.Window.RequestClose();
                }
            }

            var window = new SkillCutInWindow(_settings, entry, isBig, _windows);
            var added = new Live { Player = playerIndex, Window = window };
            Place(added);
            _live.Add(added);
            window.Tick(0f);

            var sounds = _settings.sounds;
            if (isBig || sounds.smallPlaysSe)
            {
                PlaySound(sounds.open);
            }

            if (isBig || sounds.smallPlaysVoice)
            {
                PlaySound(entry.voice);
            }
        }

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 150;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var windows = new GameObject("Windows", typeof(RectTransform));
            _windows = (RectTransform)windows.transform;
            _windows.SetParent(transform, false);
            _windows.anchorMin = Vector2.zero;
            _windows.anchorMax = Vector2.one;
            _windows.pivot = Vector2.zero;
            _windows.offsetMin = Vector2.zero;
            _windows.offsetMax = Vector2.zero;
        }

        private void OnEnable()
        {
            SkillCutIn.Cue += HandleCue;
        }

        private void OnDisable()
        {
            SkillCutIn.Cue -= HandleCue;
        }

        private void OnDestroy()
        {
            foreach (var live in _live)
            {
                live.Window.Destroy();
            }

            _live.Clear();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void HandleCue(PlayerSkillSlot slot)
        {
            if (_settings != null && slot != null)
            {
                Show(slot.PlayerIndex, _settings.Find(slot.Skill));
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];
                Place(live);
                live.Window.Tick(dt);
                if (live.Window.Done)
                {
                    _live.RemoveAt(i);
                }
            }
        }

        // 窓の形と絵の置き場所を、今の画面の大きさ（と HUD の欄の位置）から決める。
        private void Place(Live live)
        {
            var screen = _windows.rect.size;
            if (live.Window.IsBig)
            {
                var big = _settings.big;
                var anchor = Vector2.Scale(big.faceAnchor, screen);
                live.Window.Place(SkillCutInMath.BigShapes(big, screen, anchor.y), anchor, big.pictureHeight * screen.y);
                return;
            }

            var small = _settings.small;
            var row = RowRect(live.Player);
            var rect = new Rect(row.xMax + small.gap, row.center.y - small.size.y * 0.5f, small.size.x, small.size.y);
            var smallAnchor = rect.position + Vector2.Scale(small.faceAnchor, rect.size);
            live.Window.Place(SkillCutInMath.SmallShapes(rect, small.slant), smallAnchor, small.pictureHeight * rect.height);
        }

        /// <summary>その人の「ほかの3人の欄」（HUD の OtherPlayers/Other1〜3）の位置。見つからなければ設定の位置。</summary>
        private Rect RowRect(int playerIndex)
        {
            var local = LocalPlayer();
            var slot = Mathf.Clamp(playerIndex < local ? playerIndex : playerIndex - 1, 0, 2);
            var row = FindRow(slot);
            if (row != null && TryToLocal(row, out var rect))
            {
                return rect;
            }

            var small = _settings.small;
            return new Rect(small.fallbackRowPosition - new Vector2(0f, slot * small.fallbackRowPitch), small.fallbackRowSize);
        }

        private RectTransform FindRow(int slot)
        {
            if (_otherRows == null && Time.unscaledTime >= _nextRowSearch)
            {
                _nextRowSearch = Time.unscaledTime + RowSearchInterval;
                foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (rect.name == OtherRowsName && rect.GetComponentInParent<Canvas>() != null)
                    {
                        _otherRows = rect;
                        break;
                    }
                }
            }

            if (_otherRows == null)
            {
                return null;
            }

            var row = _otherRows.Find($"Other{slot + 1}") as RectTransform;
            return row != null && row.gameObject.activeInHierarchy ? row : null;
        }

        // 別の Canvas の欄の四隅を、この係の画面の座標（左下が (0, 0)）に直す。
        private bool TryToLocal(RectTransform row, out Rect rect)
        {
            rect = default;
            var canvas = row.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            var root = canvas.rootCanvas;
            var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            var corners = new Vector3[4];
            row.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, corner);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_windows, screenPoint, null, out var local))
                {
                    return false;
                }

                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 1f && rect.height > 1f;
        }
    }
}
