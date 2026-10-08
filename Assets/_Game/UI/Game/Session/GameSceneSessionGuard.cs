using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ゲームのシーンに置く番人。ロビー（<see cref="GameSession"/>）から重ねて読み込まれたときだけ、
    /// 「このシーンだけで遊ぶとき用」の物（通信の土台・D-Drive・UIの置き場所・EventSystem・センサーの読み取りなど）を、
    /// それらが動き出す前に外す（ロビーの同じ物を使うため。二重にあると通信やセンサーのポートを取り合う）。
    /// ゲームのシーンだけを再生したとき（エディタ・テスト用の bat）は何もしないので、今まで通り遊べる。
    /// どのスクリプトよりも先に動く（実行順 -32000）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-32000)]
    [AddComponentMenu("UI Studio/ロビーから来たときに外す物 (GameSceneSessionGuard)")]
    public sealed class GameSceneSessionGuard : MonoBehaviour
    {
        [Tooltip("このシーンだけで遊ぶとき用の物。ロビーから来たときは、これらを外してロビーの物を使う。")]
        public GameObject[] standaloneOnly = System.Array.Empty<GameObject>();

        /// <summary>今回の読み込みで外したか。</summary>
        public bool RemovedForSession { get; private set; }

        private void Awake()
        {
            if (GameSession.Active == null)
            {
                return;
            }

            foreach (var go in standaloneOnly)
            {
                // 同じシーンの物だけ（ロビー側の物を誤って消さない）。動き出す前に消すので DestroyImmediate を使う。
                if (go != null && go != gameObject && go.scene == gameObject.scene)
                {
                    DestroyImmediate(go);
                }
            }

            RemovedForSession = true;
        }
    }
}
