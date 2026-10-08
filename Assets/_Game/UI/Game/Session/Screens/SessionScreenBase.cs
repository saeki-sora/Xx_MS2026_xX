using System;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ロビーの画面の部品の共通部分: ボタンを <see cref="GameSession"/> の操作につなぐだけ。
    /// 見た目（文字・色・表示/非表示）は値の部品（UiBindText / UiBindVisible など）が受け持つので、
    /// ボタンや文字の位置・絵を変えても、ここで参照している部品さえ残っていれば動く。
    /// </summary>
    public abstract class SessionScreenBase : MonoBehaviour
    {
        protected static GameSession Session => GameSession.Active;

        /// <summary>ボタンにロビーの操作をつなぐ（ロビーが無いシーンでは押しても何もしない）。</summary>
        protected static void Bind(Button button, Action<GameSession> action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(() =>
            {
                var session = GameSession.Active;
                if (session != null)
                {
                    action(session);
                }
                else
                {
                    Debug.LogWarning("[Lobby] ロビー（GameSession）が無いシーンなので、このボタンは何もしません。ロビーのシーンから再生してください。");
                }
            });
        }
    }
}
