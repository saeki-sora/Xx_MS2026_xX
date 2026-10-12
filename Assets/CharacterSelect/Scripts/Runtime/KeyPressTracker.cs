using System;
using UnityEngine.InputSystem;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キー1つの「押した」「離した」を、起きた順に1回ずつ数える（Input System のアクションを使う）。
    /// 毎フレーム「押されているか」だけを見ると、すばやい連打で「離して押し直す」が1フレームの中に収まって見逃され、
    /// 押しがつながってしまう。アクションは入力の出来事を1つずつ処理するので、それが起きない。
    /// </summary>
    internal sealed class KeyPressTracker : IDisposable
    {
        private readonly InputAction _action;
        private int _releases;

        public Key Key { get; }

        /// <summary>今押されているか。</summary>
        public bool IsPressed { get; private set; }

        public KeyPressTracker(Key key)
        {
            Key = key;
            var keyboard = Keyboard.current;
            if (keyboard == null || key == Key.None)
            {
                return;
            }

            _action = new InputAction($"CharaSelect.{key}", InputActionType.Button, "<Keyboard>/" + keyboard[key].name);
            _action.started += _ => IsPressed = true;
            _action.canceled += _ =>
            {
                IsPressed = false;
                _releases++;
            };
            _action.Enable();
        }

        /// <summary>前に呼んでから今までに離した回数を返し、0 に戻す。</summary>
        public int TakeReleases()
        {
            var releases = _releases;
            _releases = 0;
            return releases;
        }

        public void Dispose()
        {
            if (_action == null)
            {
                return;
            }

            _action.Disable();
            _action.Dispose();
        }
    }
}
