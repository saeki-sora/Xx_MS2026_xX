using System;
using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MS2026.UI
{
    public enum UiScreenState
    {
        Hidden,
        Showing,
        Shown,
        Hiding
    }

    /// <summary>
    /// 画面1枚（タイトル・ロビー・HUD・ポーズ…）。どのレイヤー（段）に出すか、出る／消えるときの動き、
    /// 戻る（Esc）で閉じるか、下の画面を押せなくするか（モーダル）を決める。開け閉めは UiRoot から名前で行う。
    /// 出る／消えるときは、子の部品の「動き」（UiElementMotion の 出たとき／消えるとき）も一緒に再生する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    [AddComponentMenu("UI Studio/画面 (UiScreen)")]
    public sealed class UiScreen : MonoBehaviour
    {
        private const string BlockerName = "[Modal Blocker]";

        [Tooltip("画面の名前（プログラムやボタンから開くときの名前。英数字推奨）。例: Lobby")]
        public string screenId = "NewScreen";

        [Tooltip("ツールに出す日本語の名前。")]
        public string displayName = "新しい画面";

        [Tooltip("どのレイヤー（段）に出すか。")]
        [UiLayerId]
        public string layer = "Menu";

        [Tooltip("メモ（どんなときに出る画面か等）。")]
        [TextArea(1, 4)]
        public string memo;

        [Tooltip("ONなら、ゲームが始まったときに自動で開く。")]
        public bool openOnStart;

        [Tooltip("ONなら、戻る（Esc・ゲームパッドのB）で閉じる。")]
        public bool closeOnBack = true;

        [Tooltip("ONなら、この画面が開いている間、下の画面は押せない（後ろを暗くする）。ポップアップ向け。")]
        public bool modal;

        [Tooltip("モーダルのときに後ろにかける色。")]
        public Color modalDim = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("画面全体が出るときの動き。")]
        public UiMotion showMotion = UiMotion.FromPreset(UiPreset.FadeIn, 0.2f);

        [Tooltip("画面全体が消えるときの動き。")]
        public UiMotion hideMotion = UiMotion.FromPreset(UiPreset.FadeOut, 0.15f);

        [Tooltip("出たときに最初に選ばれる部品（キーボード・ゲームパッドで操作するとき）。")]
        public Selectable firstSelected;

        [Tooltip("出終わったときに呼ばれる。")]
        public UnityEvent onShown = new UnityEvent();

        [Tooltip("消え終わったときに呼ばれる。")]
        public UnityEvent onHidden = new UnityEvent();

        private readonly List<Handle<UiTweenMarker>> _rootMotions = new List<Handle<UiTweenMarker>>();
        private readonly List<UiElementMotion> _children = new List<UiElementMotion>();
        private CanvasGroup _group;
        private Action _onDone;
        private int _childrenPending;

        public UiScreenState State { get; private set; } = UiScreenState.Hidden;

        public bool IsVisible => State == UiScreenState.Showing || State == UiScreenState.Shown;

        public string Label => string.IsNullOrWhiteSpace(displayName) ? screenId : displayName;

        public event Action<UiScreen> Shown;
        public event Action<UiScreen> Hidden;

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        /// <summary>出す（動きつき）。done は出終わったとき。</summary>
        public void Show(Action done = null)
        {
            gameObject.SetActive(true);
            EnsureBlocker();
            Group.alpha = 1f;
            Group.interactable = false;
            Group.blocksRaycasts = true;
            State = UiScreenState.Showing;
            _onDone = done;
            PlayAll(UiMotionTrigger.OnShow, showMotion);
        }

        /// <summary>消す（動きつき）。消し終わったら GameObject を非表示にする。</summary>
        public void Hide(Action done = null)
        {
            if (!gameObject.activeSelf)
            {
                State = UiScreenState.Hidden;
                done?.Invoke();
                return;
            }

            Group.interactable = false;
            State = UiScreenState.Hiding;
            _onDone = done;
            PlayAll(UiMotionTrigger.OnHide, hideMotion);
        }

        /// <summary>動きなしで、すぐ出す／消す（画面切り替えの幕の裏など）。</summary>
        public void SetVisibleImmediate(bool visible)
        {
            StopRootMotions();
            gameObject.SetActive(visible);
            Group.alpha = 1f;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
            State = visible ? UiScreenState.Shown : UiScreenState.Hidden;
            if (visible)
            {
                EnsureBlocker();
                SelectFirst();
            }
        }

        private void PlayAll(UiMotionTrigger trigger, UiMotion rootMotion)
        {
            StopRootMotions();
            var player = UiMotionPlayers.Current;
            if (!rootMotion.IsEmpty && player.CanPlay)
            {
                _rootMotions.Add(player.Play(rootMotion, (RectTransform)transform));
            }

            // 表示中の部品だけ（非表示の部品は Update が動かず、消える動きの待ち合わせが終わらなくなるため）。
            GetComponentsInChildren(false, _children);
            _childrenPending = _children.Count;
            foreach (var child in _children)
            {
                child.Play(trigger, () => _childrenPending--);
            }

            CheckFinished();
        }

        private void Update()
        {
            if (State == UiScreenState.Showing || State == UiScreenState.Hiding)
            {
                CheckFinished();
            }
        }

        private void CheckFinished()
        {
            var player = UiMotionPlayers.Current;
            _rootMotions.RemoveAll(h => !player.IsPlaying(h));
            if (_rootMotions.Count > 0 || _childrenPending > 0)
            {
                return;
            }

            if (State == UiScreenState.Showing)
            {
                State = UiScreenState.Shown;
                Group.interactable = true;
                SelectFirst();
                Finish();
                onShown.Invoke();
                Shown?.Invoke(this);
            }
            else if (State == UiScreenState.Hiding)
            {
                State = UiScreenState.Hidden;
                Finish();
                gameObject.SetActive(false);
                onHidden.Invoke();
                Hidden?.Invoke(this);
            }
        }

        private void Finish()
        {
            var done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        private void StopRootMotions()
        {
            var player = UiMotionPlayers.Current;
            foreach (var handle in _rootMotions)
            {
                player.Stop(handle, true);
            }

            _rootMotions.Clear();
        }

        private void SelectFirst()
        {
            if (firstSelected != null && EventSystem.current != null && Application.isPlaying)
            {
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
            }
        }

        /// <summary>モーダルなら、後ろを暗くして押せなくする板を一番奥の子として用意する。</summary>
        private void EnsureBlocker()
        {
            var existing = transform.Find(BlockerName);
            if (!modal)
            {
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                }

                return;
            }

            if (existing == null)
            {
                var go = new GameObject(BlockerName, typeof(RectTransform), typeof(Image));
                go.hideFlags = HideFlags.DontSave;
                existing = go.transform;
                existing.SetParent(transform, false);
                var rect = (RectTransform)existing;
                // 親の外側まで大きく広げて、画面全体を覆う。
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-4000f, -4000f);
                rect.offsetMax = new Vector2(4000f, 4000f);
            }

            existing.gameObject.SetActive(true);
            existing.SetAsFirstSibling();
            existing.GetComponent<Image>().color = modalDim;
        }
    }
}
