using UnityEngine;

namespace MS2026.GripInputBridge
{
    /// <summary>
    /// <see cref="GripInputBridge"/> を毎フレーム更新するための自動起動コンポーネント。
    /// シーンに何も配置しなくても、Play開始時に自分自身を生成してエッジイベント
    /// （OnGripStarted/OnGripReleased）が確実に発火するようにする。
    ///
    /// 実行順を早めに固定しているのは、他のゲームスクリプトが同フレーム内で
    /// <c>GripInputBridge.Provider.GetGripValue</c> を読んだときに、
    /// 前フレームの古い値ではなく必ずこのフレームでサンプリングした値を見られるようにするため。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    internal sealed class GripInputBridgeRuntimeDriver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<GripInputBridgeRuntimeDriver>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(GripInputBridgeRuntimeDriver))
            {
                hideFlags = HideFlags.HideInHierarchy
            };
            DontDestroyOnLoad(go);
            go.AddComponent<GripInputBridgeRuntimeDriver>();
        }

        private void Update()
        {
            GripInputBridge.Tick();
        }
    }
}
