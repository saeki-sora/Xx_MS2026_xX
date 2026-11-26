using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習のシーンの泡（スマッシュボール）の持ち主（2026-11-13）。練習のシーンを作るメニューが、1人に1つずつ泡を置いて付ける。
    /// 持ち主のビーム（歯磨き粉）でしか割れない（ほかの人が当てても減らない。<see cref="PracticeSceneDriver"/> が Host で直す）。
    /// 泡は持ち主がその研修に来たら出る。画面には持ち主の色の輪と名前が付く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PracticeBallOwner : MonoBehaviour
    {
        [Tooltip("持ち主の席（0 = 1P … 3 = 4P）。")]
        [Range(0, 3)]
        public int playerIndex;
    }
}
