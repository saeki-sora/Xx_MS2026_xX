using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 「同じグループのスマッシュボールは同時に1つだけ出現する」を担当する登録簿。
    /// メンバーの管理と「今アクティブなのは誰か」の判定だけを行い、
    /// 表示切り替えのタイミング（何秒後に次を出すか等）はSmashBallModule側が決める。
    /// </summary>
    public static class SmashBallRotationGroup
    {
        private const string DefaultGroupId = "__default__";

        private static readonly Dictionary<string, List<SmashBallModule>> Groups = new Dictionary<string, List<SmashBallModule>>();
        private static readonly Dictionary<string, SmashBallModule> ActiveMembers = new Dictionary<string, SmashBallModule>();

        public static void Register(SmashBallModule module)
        {
            var key = KeyOf(module);
            if (!Groups.TryGetValue(key, out var members))
            {
                members = new List<SmashBallModule>();
                Groups[key] = members;
            }

            if (!members.Contains(module))
            {
                members.Add(module);
            }
        }

        public static void Unregister(SmashBallModule module)
        {
            var key = KeyOf(module);
            if (Groups.TryGetValue(key, out var members))
            {
                members.Remove(module);
            }

            if (ActiveMembers.TryGetValue(key, out var active) && active == module)
            {
                ActiveMembers.Remove(key);
            }
        }

        /// <summary>起動直後の状態を決める。グループ内で最初に呼ばれた1つがアクティブになり、残りは待機(非表示)になる。</summary>
        public static void ResolveInitial(SmashBallModule module)
        {
            var key = KeyOf(module);
            if (!ActiveMembers.ContainsKey(key))
            {
                ActiveMembers[key] = module;
                return;
            }

            if (ActiveMembers[key] != module)
            {
                module.gameObject.SetActive(false);
            }
        }

        /// <summary>アクティブだったモジュールが割れたときに呼ぶ。少し後で呼び出し側がActivateNextを呼ぶことを想定。</summary>
        public static void ActivateNext(SmashBallModule from)
        {
            var key = KeyOf(from);
            if (ActiveMembers.TryGetValue(key, out var active) && active == from)
            {
                ActiveMembers.Remove(key);
            }

            if (!Groups.TryGetValue(key, out var members))
            {
                return;
            }

            foreach (var candidate in members)
            {
                if (candidate != null && candidate != from && !candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.SetActive(true);
                    ActiveMembers[key] = candidate;
                    return;
                }
            }
        }

        /// <summary>再生などで再び利用可能になったときに呼ぶ。既に他が活動中なら、自分は待機に戻る。</summary>
        public static void Reconsider(SmashBallModule module)
        {
            var key = KeyOf(module);
            if (!ActiveMembers.TryGetValue(key, out var active) || active == null)
            {
                ActiveMembers[key] = module;
                return;
            }

            if (active != module)
            {
                module.gameObject.SetActive(false);
            }
        }

        private static string KeyOf(SmashBallModule module)
        {
            var id = module.settings.rotationGroupId;
            return string.IsNullOrEmpty(id) ? DefaultGroupId : id;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Groups.Clear();
            ActiveMembers.Clear();
        }
    }
}
