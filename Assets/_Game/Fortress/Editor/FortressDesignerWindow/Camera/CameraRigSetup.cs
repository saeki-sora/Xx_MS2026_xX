using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// カメラリグのセットアップ(作成・足りない部品の追加)と、構成ミスの検出。すべてUndoで戻せる。
    /// </summary>
    public static class CameraRigSetup
    {
        private const string RigObjectName = "FortressCameraRig";

        /// <summary>
        /// シーンのカメラ(Main Camera優先)をリグの子に入れ、演出・デバッグ用の部品と既定のアセットを揃える。
        /// 今のカメラの見え方をそのまま全員の初期視点として取り込むので、セットアップ直後に画面が変わることはない。
        /// </summary>
        public static FortressCameraRig CreateRig()
        {
            var camera = FindSceneCamera();
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            var rigObject = new GameObject(RigObjectName);
            Undo.RegisterCreatedObjectUndo(rigObject, "Create Camera Rig");

            if (camera != null)
            {
                rigObject.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                Undo.SetTransformParent(camera.transform, rigObject.transform, "Parent Camera To Rig");
                Undo.RecordObject(camera.transform, "Parent Camera To Rig");
                camera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            var rig = Undo.AddComponent<FortressCameraRig>(rigObject);
            rig.targetCamera = camera;
            rig.preset = CameraPresetEditing.CreatePresetAsset("CameraViewPreset_Default");
            CaptureCameraIntoPreset(camera, rig.preset);

            EnsureParts(rig);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = rigObject;
            return rig;
        }

        /// <summary>演出・ビルボード・ガイド・ホットキーの部品と、未割り当てのアセットを追加する(既にある物には触らない)。</summary>
        public static void EnsureParts(FortressCameraRig rig)
        {
            if (rig == null)
            {
                return;
            }

            var go = rig.gameObject;

            if (rig.targetCamera == null)
            {
                Undo.RecordObject(rig, "Assign Camera");
                rig.EnsureCamera();
            }

            if (rig.preset == null)
            {
                Undo.RecordObject(rig, "Assign Camera Preset");
                rig.preset = CameraPresetEditing.CreatePresetAsset("CameraViewPreset_Default");
                CaptureCameraIntoPreset(rig.targetCamera, rig.preset);
            }

            var director = go.GetComponent<CameraFeedbackDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<CameraFeedbackDirector>(go);
            }

            if (director.config == null)
            {
                Undo.RecordObject(director, "Assign Camera Feedback Config");
                director.config = CameraPresetEditing.CreateFeedbackConfigAsset();
            }

            if (go.GetComponent<BillboardDirector>() == null)
            {
                Undo.AddComponent<BillboardDirector>(go);
            }

            if (go.GetComponent<CameraGuideOverlay>() == null)
            {
                Undo.AddComponent<CameraGuideOverlay>(go);
            }

            if (go.GetComponent<CameraDebugHotkeys>() == null)
            {
                Undo.AddComponent<CameraDebugHotkeys>(go);
            }

            EditorUtility.SetDirty(rig);
        }

        /// <summary>カメラを「位置・回転ゼロの子」に直す。</summary>
        public static void FixCameraParenting(FortressCameraRig rig)
        {
            if (rig == null || rig.targetCamera == null)
            {
                return;
            }

            var cameraTransform = rig.targetCamera.transform;
            if (cameraTransform.parent != rig.transform)
            {
                Undo.SetTransformParent(cameraTransform, rig.transform, "Parent Camera To Rig");
            }

            Undo.RecordObject(cameraTransform, "Reset Camera Local Pose");
            cameraTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        /// <summary>構成の問題点を列挙する(タブ上部に警告として出す)。</summary>
        public static List<(MessageType type, string message)> Diagnose(CameraTabContext context)
        {
            var issues = new List<(MessageType, string)>();
            var rig = context.Rig;
            if (rig == null)
            {
                return issues;
            }

            if (context.RigCount > 1)
            {
                issues.Add((MessageType.Warning, $"カメラリグが{context.RigCount}個あります。1つだけにしてください（最後に有効になった物だけが動きます）。"));
            }

            if (rig.targetCamera == null)
            {
                issues.Add((MessageType.Error, "動かすカメラが見つかりません。リグの子にCameraを置いてください。"));
            }
            else
            {
                if (!rig.targetCamera.CompareTag("MainCamera"))
                {
                    issues.Add((MessageType.Warning, "カメラのTagがMainCameraではありません。D-Driveの揺れ(CameraFx)はCamera.mainにだけ効きます。"));
                }

                if (!Application.isPlaying && !IsCameraDirectChildAtOrigin(rig))
                {
                    issues.Add((MessageType.Warning, "カメラがリグの「位置・回転ゼロの子」になっていません。リグの計算と実際の見た目がずれます。"));
                }
            }

            if (rig.preset == null)
            {
                issues.Add((MessageType.Error, "視点プリセットが割り当てられていません。"));
            }

            if (rig.preset != null && rig.preset.billboard.enabled && context.Billboards == null)
            {
                issues.Add((MessageType.Warning, "このプリセットは「絵を立たせる」がONですが、適用する係(BillboardDirector)がありません。「足りない部品を追加」で追加できます。"));
            }

            if (context.Director == null)
            {
                issues.Add((MessageType.Info, "演出(揺れ・寄り)の係(CameraFeedbackDirector)がありません。「足りない部品を追加」で追加できます。"));
            }
            else if (context.FeedbackConfig == null)
            {
                issues.Add((MessageType.Warning, "演出の設定(CameraFeedbackConfig)が未割り当てです。"));
            }

            return issues;
        }

        public static bool IsCameraDirectChildAtOrigin(FortressCameraRig rig)
        {
            var cameraTransform = rig.targetCamera != null ? rig.targetCamera.transform : null;
            return cameraTransform != null
                   && cameraTransform.parent == rig.transform
                   && cameraTransform.localPosition.sqrMagnitude < 1e-6f
                   && Quaternion.Angle(cameraTransform.localRotation, Quaternion.identity) < 0.01f;
        }

        private static Camera FindSceneCamera()
        {
            if (Camera.main != null)
            {
                return Camera.main;
            }

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            return cameras.Length > 0 ? cameras[0] : null;
        }

        /// <summary>カメラの今の見え方を、全体視点と4人全員の初期視点として取り込む。</summary>
        private static void CaptureCameraIntoPreset(Camera camera, CameraViewPreset preset)
        {
            if (camera == null || preset == null)
            {
                return;
            }

            var view = CameraSceneViewBridge.ViewFromCamera(camera, CameraViewSettings.Default);
            foreach (var viewer in ViewerIndex.All)
            {
                preset.SetView(viewer, view);
            }

            preset.nearClip = camera.nearClipPlane;
            preset.farClip = camera.farClipPlane;
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
        }
    }
}
