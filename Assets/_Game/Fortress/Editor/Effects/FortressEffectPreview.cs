using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Vfx;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 演出欄（<see cref="FortressEffect"/>）の「▶ 試す」ボタンの中身。Play中に、その欄の演出をそれらしい場所で1回鳴らす。
    /// ・レーザーの設定(<see cref="LaserTuningConfig"/>)の欄 → その設定を使っている砲台の発射口（着弾点・群衆ヒットは少し前方）
    /// ・シーンの物（破壊可能物など）の欄 → その物の位置
    /// ・それ以外（プリセットのアセットなど）→ ゲームカメラ（無ければSceneビュー）の中心
    /// ループするエフェクトも確認後に消えるよう、数秒で止める。
    /// </summary>
    public static class FortressEffectPreview
    {
        private const float StopAfterSeconds = 2f;
        private const float AheadDistance = 4f;

        private static readonly List<(Handle<VfxMarker> handle, double stopAt)> Running = new List<(Handle<VfxMarker>, double)>();

        /// <summary>レーザーの欄を試すときに使う砲台（プレイヤー番号0-3）。砲台配置タブで選ぶ。</summary>
        public static int PreferredPlayer { get; set; }

        public static bool CanPlay => Application.isPlaying && (Vfx.IsBound || Audio.IsBound);

        public static void Play(SerializedProperty property)
        {
            if (!CanPlay)
            {
                return;
            }

            FortressEffect effect;
            try
            {
                effect = property.boxedValue as FortressEffect;
            }
            catch (System.Exception)
            {
                effect = null;
            }

            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            ResolvePose(property, out var position, out var rotation, out var player);
            var handle = FortressEffectPlayer.SpawnVfx(effect, position, rotation, player);
            FortressEffectPlayer.PlaySound(effect, position);

            if (handle != Handle<VfxMarker>.Invalid)
            {
                if (Running.Count == 0)
                {
                    EditorApplication.update += StopExpired;
                }

                Running.Add((handle, EditorApplication.timeSinceStartup + StopAfterSeconds));
            }
        }

        private static void ResolvePose(SerializedProperty property, out Vector3 position, out Quaternion rotation, out int player)
        {
            var target = property.serializedObject.targetObject;
            player = PreferredPlayer;
            rotation = Quaternion.identity;

            if (target is LaserTuningConfig tuning && TryFindTurret(tuning, out var turret))
            {
                var muzzle = turret.muzzle != null ? turret.muzzle : turret.transform;
                player = turret.playerIndex;
                position = muzzle.position;
                rotation = muzzle.rotation;
                if (property.name != nameof(LaserEffectSettings.muzzle))
                {
                    // 着弾点・群衆ヒットは、レーザーの少し先に、砲台の方を向けて出す。
                    Vector2 aim = turret.AimDirection;
                    position += (Vector3)(aim * AheadDistance);
                    rotation = FortressEffectPlayer.RotationFacing(-aim);
                }

                return;
            }

            if (target is Component component)
            {
                position = component.transform.position;
                return;
            }

            position = ViewCenter();
        }

        private static bool TryFindTurret(LaserTuningConfig tuning, out LaserTurret found)
        {
            found = null;
            foreach (var turret in Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                if (turret.tuning != tuning)
                {
                    continue;
                }

                if (found == null || turret.playerIndex == PreferredPlayer)
                {
                    found = turret;
                }
            }

            return found != null;
        }

        private static Vector3 ViewCenter()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                var p = camera.transform.position;
                return new Vector3(p.x, p.y, 0f);
            }

            var sceneView = SceneView.lastActiveSceneView;
            return sceneView != null ? new Vector3(sceneView.pivot.x, sceneView.pivot.y, 0f) : Vector3.zero;
        }

        private static void StopExpired()
        {
            var now = EditorApplication.timeSinceStartup;
            for (var i = Running.Count - 1; i >= 0; i--)
            {
                if (!Application.isPlaying)
                {
                    Running.RemoveAt(i);
                    continue;
                }

                if (Running[i].stopAt > now)
                {
                    continue;
                }

                var handle = Running[i].handle;
                FortressEffectPlayer.Stop(ref handle);
                Running.RemoveAt(i);
            }

            if (Running.Count == 0)
            {
                EditorApplication.update -= StopExpired;
            }
        }
    }
}
