using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI
{
    public enum UiButtonActionKind
    {
        /// <summary>名前の画面を開く。</summary>
        OpenScreen,

        /// <summary>名前の画面を閉じる。</summary>
        CloseScreen,

        /// <summary>このボタンが入っている画面を閉じる。</summary>
        CloseThisScreen,

        /// <summary>戻る（一番手前の画面を閉じる）。</summary>
        Back,

        /// <summary>幕を使って、名前の画面に切り替える。</summary>
        SwitchScreen,

        /// <summary>幕を使って、シーンを読み込む。</summary>
        LoadScene,

        /// <summary>名前の動きを再生する（UiElementMotion の「名前で呼んだとき」）。</summary>
        PlayMotion
    }

    /// <summary>
    /// ボタンを押したときにすることを、プログラムなしで決める（画面を開く・閉じる・戻る・切り替える・シーンを読む・動きを再生）。
    /// 同じ GameObject の Button に自動でつながる。
    /// </summary>
    [AddComponentMenu("UI Studio/ボタンの役目 (UiButtonAction)")]
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonAction : MonoBehaviour
    {
        [Tooltip("押したときにすること。")]
        public UiButtonActionKind action = UiButtonActionKind.OpenScreen;

        [Tooltip("開く／閉じる／切り替える画面の名前、読み込むシーンの名前、または再生する動きの名前。")]
        public string target;

        [Tooltip("「切り替える」「シーンを読む」ときの幕（空なら暗転）。")]
        public UiScreenTransition transition;

        [Tooltip("「名前の動きを再生」の対象（空ならこのボタン自身）。")]
        public UiElementMotion motionTarget;

        private Button _button;

        private void OnEnable()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Run);
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(Run);
            }
        }

        /// <summary>設定どおりの役目を実行する（ボタンを押したときに呼ばれる）。</summary>
        public void Run()
        {
            var root = UiRoot.Active;
            switch (action)
            {
                case UiButtonActionKind.OpenScreen when root != null:
                    root.Open(target);
                    break;
                case UiButtonActionKind.CloseScreen when root != null:
                    root.Close(target);
                    break;
                case UiButtonActionKind.CloseThisScreen when root != null:
                    var screen = GetComponentInParent<UiScreen>();
                    if (screen != null)
                    {
                        root.Close(screen.screenId);
                    }

                    break;
                case UiButtonActionKind.Back when root != null:
                    root.Back();
                    break;
                case UiButtonActionKind.SwitchScreen when root != null:
                    var from = GetComponentInParent<UiScreen>();
                    root.SwitchTo(target, transition, closeScreenId: from != null ? from.screenId : null);
                    break;
                case UiButtonActionKind.LoadScene when root != null:
                    root.LoadScene(target, transition);
                    break;
                case UiButtonActionKind.PlayMotion:
                    var motion = motionTarget != null ? motionTarget : GetComponent<UiElementMotion>();
                    if (motion != null)
                    {
                        motion.PlayNamed(target);
                    }

                    break;
                default:
                    Debug.LogWarning("[UI] UIの置き場所（UiRoot）がシーンにありません。");
                    break;
            }
        }
    }
}
