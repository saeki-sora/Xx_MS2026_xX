using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.ObjectToon.KillFx
{
    /// <summary>
    /// 倒した場所を一瞬照らす点の光を、数を決めて使い回す（作っては消すことをしない）。
    /// 影は付けない（影を作る光は重い）。床・ステージの物・3Dのトゥーンは URP の追加の光として受ける。
    /// </summary>
    public sealed class KillFxLights : IDisposable
    {
        private sealed class Slot
        {
            public Light light;
            public float age;
            public float peak;
            public bool active;
        }

        private readonly Transform parent;
        private readonly List<Slot> slots = new List<Slot>();

        public KillFxLights(Transform parent)
        {
            this.parent = parent;
        }

        /// <summary>新しく光を点ける。近くに光っている光があれば、それを明るくし直す。点けた（し直した）ら true。</summary>
        public bool Flash(Vector2 position, Color color, KillFxSettings settings)
        {
            var slot = FindNear(position, settings.lightMergeDistance) ?? FindFree(settings.maxLights);
            if (slot == null)
            {
                return false;
            }

            var light = slot.light;
            light.transform.position = new Vector3(position.x, position.y, -settings.lightHeight);
            light.color = color;
            light.range = settings.lightRange;
            slot.peak = settings.lightIntensity;
            slot.age = 0f;
            slot.active = true;
            light.intensity = slot.peak;
            light.enabled = true;
            return true;
        }

        /// <summary>同じフレームに近くで倒れたときは、近くの光の消えかけを戻すだけ。</summary>
        public void Refresh(Vector2 position, KillFxSettings settings)
        {
            var slot = FindNear(position, settings.lightMergeDistance);
            if (slot != null)
            {
                slot.age = Mathf.Min(slot.age, settings.lightSeconds * 0.25f);
            }
        }

        public void Tick(float deltaTime, KillFxSettings settings)
        {
            float duration = Mathf.Max(0.01f, settings != null ? settings.lightSeconds : 0.3f);
            foreach (var slot in slots)
            {
                if (!slot.active)
                {
                    continue;
                }

                slot.age += deltaTime;
                float t = slot.age / duration;
                if (t >= 1f || slot.light == null)
                {
                    slot.active = false;
                    if (slot.light != null)
                    {
                        slot.light.enabled = false;
                    }

                    continue;
                }

                // 一瞬で明るくなって、すっと消える（アニメの「パッ」と光る感じ）。
                float fade = 1f - t;
                slot.light.intensity = slot.peak * fade * fade;
            }
        }

        private Slot FindNear(Vector2 position, float distance)
        {
            float best = distance * distance;
            Slot found = null;
            foreach (var slot in slots)
            {
                if (!slot.active || slot.light == null)
                {
                    continue;
                }

                float d = ((Vector2)slot.light.transform.position - position).sqrMagnitude;
                if (d <= best)
                {
                    best = d;
                    found = slot;
                }
            }

            return found;
        }

        // 空いている光。無ければ上限まで作る。上限なら一番古い（消えかけの）光を使う。
        private Slot FindFree(int max)
        {
            Slot oldest = null;
            int usable = 0;
            foreach (var slot in slots)
            {
                if (slot.light == null)
                {
                    continue;
                }

                if (usable++ >= max)
                {
                    slot.light.enabled = false;
                    slot.active = false;
                    continue;
                }

                if (!slot.active)
                {
                    return slot;
                }

                if (oldest == null || slot.age > oldest.age)
                {
                    oldest = slot;
                }
            }

            if (usable < max)
            {
                var go = new GameObject("KillLight") { hideFlags = HideFlags.HideInHierarchy };
                go.transform.SetParent(parent, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.enabled = false;
                var slot = new Slot { light = light };
                slots.Add(slot);
                return slot;
            }

            return oldest;
        }

        public void Dispose()
        {
            foreach (var slot in slots)
            {
                if (slot.light != null)
                {
                    UnityEngine.Object.Destroy(slot.light.gameObject);
                }
            }

            slots.Clear();
        }
    }
}
