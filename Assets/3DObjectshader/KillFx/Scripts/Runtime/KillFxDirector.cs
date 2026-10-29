using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.ObjectToon.KillFx
{
    /// <summary>
    /// 群れの敵が倒れたら（SwarmSystem.AgentsDied）、手描き風エフェクト・効果音・まわりを照らす光を出す。
    /// 群れの仕組みは変えず、公開されている「倒れた」の知らせを聞くだけ。設定が無い・オフなら何もしない。
    /// 大群をまとめて倒しても重くならないよう、1フレームの数・同じ場所に続けて出す間隔・光の数に上限がある。
    /// </summary>
    public sealed class KillFxDirector : MonoBehaviour
    {
        private struct RecentSpawn
        {
            public Vector2 position;
            public float time;
        }

        private static KillFxDirector instance;

        private KillFxSettings settings;
        private ObjectToonProfile toonProfile;
        private SwarmSystem subscribed;
        private KillFxLights lights;
        private readonly List<RecentSpawn> recent = new List<RecentSpawn>();
        private int spawnedFrame = -1;
        private int spawnedThisFrame;
        private int lightFrame = -1;

        public static KillFxDirector Instance => instance;
        public KillFxSettings Settings => settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var loaded = Resources.Load<KillFxSettings>(KillFxSettings.ResourcePath);
            if (loaded == null || instance != null)
            {
                return;
            }

            // DontSave を付けると、エディタで再生を止めても消えずに残るので付けない（DontDestroyOnLoad だけ）。
            var go = new GameObject("[KillFx]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KillFxDirector>();
            instance.settings = loaded;
            instance.toonProfile = Resources.Load<ObjectToonProfile>(ObjectToonProfile.ResourcePath);
            instance.lights = new KillFxLights(go.transform);
        }

        private void OnDestroy()
        {
            Subscribe(null);
            lights?.Dispose();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            // 試合ごとに群れの仕組みが作り直されるので、毎フレーム今のものに付け直す。
            Subscribe(SwarmSystem.Current);
            lights.Tick(Time.deltaTime, settings);
        }

        private void Subscribe(SwarmSystem next)
        {
            if (next == subscribed)
            {
                return;
            }

            if (subscribed != null)
            {
                subscribed.AgentsDied -= HandleAgentsDied;
            }

            subscribed = next;
            if (subscribed != null)
            {
                subscribed.AgentsDied += HandleAgentsDied;
            }
        }

        private bool IsActive()
        {
            if (settings == null || !settings.enabled)
            {
                return false;
            }

            return !settings.onlyOnToonStage || (toonProfile != null && toonProfile.IsAnyTargetLoaded());
        }

        private void HandleAgentsDied(IReadOnlyList<SwarmDeath> deaths)
        {
            if (!IsActive())
            {
                return;
            }

            for (int i = 0; i < deaths.Count; i++)
            {
                var death = deaths[i];
                Play(new Vector2(death.position.x, death.position.y), death.owner);
            }
        }

        /// <summary>その場所で倒したときの演出を出す（上限を超える分は出さない）。テスト用にも使う。</summary>
        public void Play(Vector2 position, int owner)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.light && lightFrame != Time.frameCount)
            {
                // 光は1フレームに1つだけ足す（同じフレームに倒れた敵は、近くの光を明るくし直すだけ）。
                if (lights.Flash(position, LightColor(owner), settings))
                {
                    lightFrame = Time.frameCount;
                }
            }
            else if (settings.light)
            {
                lights.Refresh(position, settings);
            }

            if (!TryReserveEffect(position))
            {
                return;
            }

            var world = new Vector3(position.x, position.y, -settings.height);
            FortressEffectPlayer.PlayOnce(settings.onKill, world, Quaternion.identity, owner);
        }

        private bool TryReserveEffect(Vector2 position)
        {
            if (spawnedFrame != Time.frameCount)
            {
                spawnedFrame = Time.frameCount;
                spawnedThisFrame = 0;
            }

            if (spawnedThisFrame >= settings.maxEffectsPerFrame)
            {
                return false;
            }

            float now = Time.time;
            float minDistanceSqr = settings.minDistance * settings.minDistance;
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                if (now - recent[i].time > settings.minSeconds)
                {
                    recent.RemoveAt(i);
                    continue;
                }

                if ((recent[i].position - position).sqrMagnitude < minDistanceSqr)
                {
                    return false;
                }
            }

            recent.Add(new RecentSpawn { position = position, time = now });
            spawnedThisFrame++;
            return true;
        }

        private Color LightColor(int owner)
        {
            var color = settings.lightUsePlayerColor && owner >= 0 && owner < 4
                ? FortressColors.PlayerColor(owner)
                : settings.lightColor;
            return Color.Lerp(color, Color.white, settings.lightWhiteMix);
        }
    }
}
