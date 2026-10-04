using System.Text;
using MS2026.Fortress.Hud;
using MS2026.Fortress.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// ネット対戦に必要なシーン上のオブジェクトを配置するメニュー。何度実行しても、足りない物だけを追加する。
    /// </summary>
    internal static class FortressNetSceneSetup
    {
        private const string MenuPath = "Tools/要塞/ネットワーク/同期オブジェクトをシーンに配置";
        private const string NetSyncObjectName = "[Fortress] NetSync";
        private const string HudObjectName = "[Fortress] LocalTurretHud";

        [MenuItem(MenuPath)]
        private static void PlaceSyncObjects()
        {
            var report = new StringBuilder();

            if (Object.FindFirstObjectByType<NetworkManager>() == null)
            {
                report.AppendLine("⚠ シーンにNetworkManagerがありません。先にネットワーク基盤(Phase 0)の設定を行ってください。");
            }

            var hub = Object.FindFirstObjectByType<TurretNetworkHub>();
            if (hub == null)
            {
                var go = new GameObject(NetSyncObjectName, typeof(NetworkObject), typeof(TurretNetworkHub));
                Undo.RegisterCreatedObjectUndo(go, "砲台の同期オブジェクトを配置");
                report.AppendLine($"・「{NetSyncObjectName}」を追加しました(砲台4台の同期)。");
            }
            else
            {
                report.AppendLine($"・砲台の同期は配置済みです({hub.name})。");
            }

            // 同期用の部品は全て同じNetworkObject(NetSync)に付ける。フェーズが進んで部品が増えたらここに足す。
            var netSync = Object.FindFirstObjectByType<TurretNetworkHub>().gameObject;
            EnsureSyncComponent<DestructibleNetworkHub>(netSync, "破壊可能物・スマッシュボールの同期", report);
            EnsureSyncComponent<EnemyNetworkHub>(netSync, "Actorの敵(ボスなど)の同期", report);
            EnsureSyncComponent<SwarmNetworkHub>(netSync, "群衆の同期", report);

            var hud = Object.FindFirstObjectByType<LocalTurretGaugeHud>();
            if (hud == null)
            {
                var go = new GameObject(HudObjectName, typeof(LocalTurretGaugeHud));
                Undo.RegisterCreatedObjectUndo(go, "自分のゲージを配置");
                report.AppendLine($"・「{HudObjectName}」を追加しました(自分の熱・チャージのゲージ)。");
            }
            else
            {
                report.AppendLine($"・自分のゲージは配置済みです({hud.name})。");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            report.AppendLine();
            report.Append("シーンを保存(Ctrl+S)してからビルドしてください。保存しないと、ビルドに含まれません。");
            EditorUtility.DisplayDialog("ネットワーク同期オブジェクト", report.ToString(), "OK");
        }

        private static void EnsureSyncComponent<T>(GameObject netSync, string label, StringBuilder report) where T : Component
        {
            if (netSync.GetComponent<T>() != null)
            {
                report.AppendLine($"・{label}は配置済みです。");
                return;
            }

            Undo.AddComponent<T>(netSync);
            report.AppendLine($"・「{netSync.name}」に{label}を追加しました。");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanPlaceSyncObjects()
        {
            // 再生中にシーンへ追加しても、再生を止めると消えてしまうため。
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
