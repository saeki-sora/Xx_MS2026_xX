using System;
using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Ui;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MS2026.UI
{
    /// <summary>
    /// 画面の部品に「きっかけ → 動き（＋効果音）」を付ける。例: 出たとき=ポンと出る、押したとき=ぷにっと縮む、
    /// 値が減ったとき=ブルッと震える。動きは D-Drive の UI の動きなので、種類や長さはいつでも差し替えられる。
    /// 画面（UiScreen）の出る／消えるに合わせて、子の部品の「出たとき／消えるとき」も自動で再生される。
    /// </summary>
    [AddComponentMenu("UI Studio/動き (UiElementMotion)")]
    [DisallowMultipleComponent]
    public sealed class UiElementMotion : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerDownHandler, ISelectHandler, ISubmitHandler
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("いつ動くか。")]
            public UiMotionTrigger trigger = UiMotionTrigger.OnShow;

            [Tooltip("「名前で呼んだとき」の名前。")]
            public string name;

            [Tooltip("「値が増えた／減ったとき」に見る値の名前。")]
            [UiValueKey]
            public string valueKey;

            [Tooltip("動かす部品（空ならこの部品）。")]
            public RectTransform target;

            public UiMotion motion = new UiMotion();
        }

        private struct Pending
        {
            public Entry Entry;
            public double StartAt;
        }

        public List<Entry> entries = new List<Entry>();

        private readonly List<Pending> _pending = new List<Pending>();
        private readonly List<Handle<UiTweenMarker>> _running = new List<Handle<UiTweenMarker>>();
        private readonly List<Handle<UiTweenMarker>> _loops = new List<Handle<UiTweenMarker>>();
        private readonly List<IDisposable> _valueSubscriptions = new List<IDisposable>();
        private readonly Dictionary<string, float> _lastValues = new Dictionary<string, float>();
        private Action _onHideComplete;
        private bool _waitingHide;

        /// <summary>このきっかけの動きが1つでもあるか。</summary>
        public bool Has(UiMotionTrigger trigger)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.trigger == trigger && !entry.motion.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>きっかけの動きを再生する。onComplete はその動きが全部終わったとき（無ければすぐ）呼ばれる。</summary>
        public void Play(UiMotionTrigger trigger, Action onComplete = null)
        {
            var any = false;
            foreach (var entry in entries)
            {
                if (entry != null && entry.trigger == trigger && !entry.motion.IsEmpty)
                {
                    Schedule(entry);
                    any = true;
                }
            }

            if (trigger == UiMotionTrigger.OnHide)
            {
                StopLoops();
                if (any && UiMotionPlayers.Current.CanPlay)
                {
                    _onHideComplete = onComplete;
                    _waitingHide = true;
                    return;
                }
            }

            if (trigger == UiMotionTrigger.OnShow)
            {
                StartLoops();
            }

            onComplete?.Invoke();
        }

        /// <summary>名前で呼ぶ（「名前で呼んだとき」の動き）。ボタンの OnClick などから使える。</summary>
        public void PlayNamed(string motionName)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.trigger == UiMotionTrigger.Manual && entry.name == motionName && !entry.motion.IsEmpty)
                {
                    Schedule(entry);
                }
            }
        }

        /// <summary>再生中の動きが残っているか（消える動きの待ち合わせに使う）。</summary>
        public bool IsBusy => _pending.Count > 0 || _running.Count > 0;

        public void OnPointerClick(PointerEventData eventData) => Play(UiMotionTrigger.OnClick);
        public void OnSubmit(BaseEventData eventData) => Play(UiMotionTrigger.OnClick);
        public void OnPointerEnter(PointerEventData eventData) => Play(UiMotionTrigger.OnHover);
        public void OnSelect(BaseEventData eventData) => Play(UiMotionTrigger.OnHover);
        public void OnPointerDown(PointerEventData eventData) => Play(UiMotionTrigger.OnPress);

        private void OnEnable()
        {
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.valueKey) ||
                    (entry.trigger != UiMotionTrigger.OnValueUp && entry.trigger != UiMotionTrigger.OnValueDown))
                {
                    continue;
                }

                var key = entry.valueKey;
                _lastValues.Remove(key);
                _valueSubscriptions.Add(UiValues.Subscribe(key, value => OnValue(key, value)));
            }

            if (GetComponentInParent<UiScreen>(true) == null)
            {
                StartLoops();
            }
        }

        private void OnDisable()
        {
            foreach (var subscription in _valueSubscriptions)
            {
                subscription?.Dispose();
            }

            _valueSubscriptions.Clear();
            _pending.Clear();
            StopLoops();
            FinishHideWait();
        }

        private void Update()
        {
            var player = UiMotionPlayers.Current;
            var now = Time.unscaledTimeAsDouble;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].StartAt <= now)
                {
                    var entry = _pending[i].Entry;
                    _pending.RemoveAt(i);
                    Launch(entry, player);
                }
            }

            for (var i = _running.Count - 1; i >= 0; i--)
            {
                if (!player.IsPlaying(_running[i]))
                {
                    _running.RemoveAt(i);
                }
            }

            if (_waitingHide && !IsBusy)
            {
                FinishHideWait();
            }
        }

        private void Schedule(Entry entry)
        {
            if (!UiMotionPlayers.Current.CanPlay)
            {
                return;
            }

            if (entry.motion.delay <= 0f)
            {
                Launch(entry, UiMotionPlayers.Current);
                return;
            }

            _pending.Add(new Pending { Entry = entry, StartAt = Time.unscaledTimeAsDouble + entry.motion.delay });
        }

        private void Launch(Entry entry, IUiMotionPlayer player)
        {
            var target = entry.target != null ? entry.target : transform as RectTransform;
            if (target == null)
            {
                return;
            }

            var handle = player.Play(entry.motion, target);
            (entry.trigger == UiMotionTrigger.Loop ? _loops : _running).Add(handle);
        }

        private void StartLoops()
        {
            StopLoops();
            foreach (var entry in entries)
            {
                if (entry != null && entry.trigger == UiMotionTrigger.Loop && !entry.motion.IsEmpty)
                {
                    Schedule(entry);
                }
            }
        }

        private void StopLoops()
        {
            var player = UiMotionPlayers.Current;
            foreach (var handle in _loops)
            {
                player.Stop(handle, true);
            }

            _loops.Clear();
        }

        private void OnValue(string key, UiValue value)
        {
            var number = value.AsNumber;
            if (_lastValues.TryGetValue(key, out var last) && Application.isPlaying)
            {
                var trigger = number > last ? UiMotionTrigger.OnValueUp : number < last ? UiMotionTrigger.OnValueDown : (UiMotionTrigger?)null;
                if (trigger != null)
                {
                    foreach (var entry in entries)
                    {
                        if (entry != null && entry.trigger == trigger && entry.valueKey == key && !entry.motion.IsEmpty)
                        {
                            Schedule(entry);
                        }
                    }
                }
            }

            _lastValues[key] = number;
        }

        private void FinishHideWait()
        {
            if (!_waitingHide)
            {
                return;
            }

            _waitingHide = false;
            var callback = _onHideComplete;
            _onHideComplete = null;
            callback?.Invoke();
        }
    }
}
