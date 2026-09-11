using UnityEngine;

namespace MS2026.ShaderFX
{
    // Spatial trigger application (design doc §5「Volume型の空間適用」) — "この水域に入ったオブジェクトに
    // 屈折がかかる" etc. Built directly on EffectDirector.PushOverlay/PopOverlay, so a Volume layers its
    // Profile on top of whatever the object's own Profile already is instead of replacing it outright
    // (the same base+overlay merge used everywhere else — see PushOverlay's own comment for why that
    // matters for "常時トゥーン + 一時的な効果" style stacking).
    //
    // Works with both 3D physics (Collider/Rigidbody) and 2D physics (Collider2D/Rigidbody2D) — Unity
    // calls whichever OnTrigger(Enter|Exit)(2D)? pair matches the collider types actually involved, so
    // both can simply coexist on this one component. No [RequireComponent] here since exactly one of
    // Collider/Collider2D is required, not both, and RequireComponent can't express "either of these".
    [AddComponentMenu("ShaderFX/Effect Volume")]
    public sealed class ShaderFXVolume : MonoBehaviour
    {
        [Tooltip("このエリアに入ったオブジェクトへ重ねて適用するProfileです。オブジェクト本来のProfileは失われず、" +
                 "このProfileが持つモジュールの種類だけが上書きされます(例: 本来のリムライトはそのまま、屈折だけ追加)。")]
        public EffectProfile overlayProfile;

        [Tooltip("重ね掛けの優先度です。複数のVolumeやオーバーレイが同じ種類のモジュールを同時に指定した場合、" +
                 "値が大きい方が勝ちます。")]
        public int priority;

        [Tooltip("空にすると、このVolume専用の自動生成された名前を使います(他のVolumeと干渉しません)。" +
                 "複数のVolumeで同じ名前を明示的に指定すると、後から入った方が先に入った方の適用を上書きします" +
                 "(例: 同種の「スピードアップ床」を複数配置し、重複して数えたくない場合)。")]
        public string layerName;

        private string EffectiveLayerName => string.IsNullOrEmpty(layerName) ? $"ShaderFXVolume_{GetInstanceID()}" : layerName;

        private void Reset()
        {
            var col3D = GetComponent<Collider>();
            if (col3D != null) col3D.isTrigger = true;

            var col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.isTrigger = true;
        }

        private void HandleEnter(EffectTarget target)
        {
            if (overlayProfile == null || target == null) return;
            EffectDirector.Instance.PushOverlay(target, EffectiveLayerName, overlayProfile, priority);
        }

        private void HandleExit(EffectTarget target)
        {
            if (!EffectDirector.HasInstance || target == null) return;
            EffectDirector.Instance.PopOverlay(target, EffectiveLayerName);
        }

        // --- 3D physics. Unity's rules require at least one of the two overlapping colliders to
        // carry a non-kinematic Rigidbody for trigger callbacks to fire at all — a standard Unity
        // constraint, not something this component can work around. ---

        private void OnTriggerEnter(Collider other) => HandleEnter(other.GetComponentInParent<EffectTarget>());

        private void OnTriggerExit(Collider other) => HandleExit(other.GetComponentInParent<EffectTarget>());

        // --- 2D physics. Same Rigidbody2D requirement as above, just the 2D equivalent. ---

        private void OnTriggerEnter2D(Collider2D other) => HandleEnter(other.GetComponentInParent<EffectTarget>());

        private void OnTriggerExit2D(Collider2D other) => HandleExit(other.GetComponentInParent<EffectTarget>());

        // If `target` is destroyed while still inside the Volume, Unity does not reliably raise
        // OnTriggerExit(2D)? — but that's not a leak here: EffectDirector already prunes a destroyed
        // target's overlay entries as part of the same self-healing sweep that cleans up its
        // registration (see PruneStaleRegistrations), so no extra handling is needed on this side.
    }
}
