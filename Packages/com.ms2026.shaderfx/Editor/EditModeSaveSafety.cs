using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.ShaderFX.Editor
{
    // Edit Mode preview swaps EffectTarget renderers to Director-generated, non-persistent
    // Material instances. If that swap ever got written to disk (scene/prefab save) or survived
    // a domain reload while dangling, the saved file would end up referencing a material that
    // doesn't exist as an asset. This class reverts to the real OriginalMaterial right before
    // any such write, then reapplies the preview immediately after.
    [InitializeOnLoad]
    internal static class EditModeSaveSafety
    {
        static EditModeSaveSafety()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabSaved += OnPrefabSaved;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // The Director's own GameObject can survive an Enter/Exit Play Mode transition
            // (depending on Enter Play Mode Options) while other scene objects get fresh native
            // instances underneath it — observed with a large, script-spawned object count.
            // Left alone, that leaves `registered` full of stale/destroyed references that
            // Unity's fake-null check silently no-ops instead of flagging as needing re-adding,
            // so screen-space effects (and anything else keyed off the full registry) quietly
            // stop updating. Rescanning on every settled transition is cheap (once, not
            // per-frame) and makes the Director self-heal regardless of the exact cause.
            if (change == PlayModeStateChange.EnteredPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                if (EffectDirector.HasInstance) EffectDirector.Instance.RescanScene();
            }

            // If "Enter Play Mode Options" is set to skip the scene reload, the same Edit Mode
            // EffectDirector (hidden + unsaved) carries straight into Play Mode instead of being
            // recreated fresh — re-sync its visibility flags on every transition so it doesn't stay
            // stuck hidden from the Hierarchy while actually playing.
            EffectDirector.SyncEditorVisibility();
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (!Application.isPlaying && EffectDirector.HasInstance) EffectDirector.Instance.RevertAllToOriginal();
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (!Application.isPlaying && EffectDirector.HasInstance) EffectDirector.Instance.ReapplyAll();
        }

        private static void OnPrefabSaving(GameObject prefabRoot)
        {
            if (!Application.isPlaying && EffectDirector.HasInstance) EffectDirector.Instance.RevertAllToOriginal();
        }

        private static void OnPrefabSaved(GameObject prefabRoot)
        {
            if (!Application.isPlaying && EffectDirector.HasInstance) EffectDirector.Instance.ReapplyAll();
        }

        private static void OnBeforeAssemblyReload()
        {
            if (EffectDirector.HasInstance) EffectDirector.Instance.RevertAllToOriginal();
        }
    }
}
