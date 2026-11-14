using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD 移動に、ほかの仕組み（ステージなど）から知らせる口。
    /// Fortress からはステージのコードが見えないので、ステージの側がここへ登録する。
    /// </summary>
    public static class TurretMoveHooks
    {
        private static readonly List<Func<bool>> Holds = new List<Func<bool>>();
        private static readonly bool[] Downed = new bool[4];
        private static Action<string, TurretMoveRegion> _floorProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Holds.Clear();
            _floorProvider = null;
            Array.Clear(Downed, 0, Downed.Length);
        }

        /// <summary>
        /// このプレイヤー（0〜3）が菌にやられて倒れているか（全PCで同じ。Host が決めて配った値）。
        /// 倒れている間はスキルを使えない（スキル欄 MS2026.Skills.PlayerSkillSlot がこれを見る）。
        /// </summary>
        public static bool IsDowned(int player) => player >= 0 && player < Downed.Length && Downed[player];

        /// <summary>倒れている砲台に付ける、ビームを止める当たりの名前。</summary>
        public const string DownedBlockerName = "[実験] 倒れた砲台のビーム止め";

        /// <summary>
        /// この当たりが「倒れている砲台のビーム止め」か。ビームの出だしがこの中にある（倒れた仲間の真上に立って撃った）ときは、
        /// 長さ 0 で止まらないよう、レーザーの判定がこれを使って無視する。
        /// </summary>
        public static bool IsDownedBlocker(Collider2D collider) => collider != null && collider.gameObject.name == DownedBlockerName;

        internal static void SetDowned(int player, bool downed)
        {
            if (player >= 0 && player < Downed.Length)
            {
                Downed[player] = downed;
            }
        }

        /// <summary>
        /// true を返す間、移動とカメラの追従を止める（カメラは元の視点のまま）。落下演出など、砲台を別の係が動かしている間に使う。
        /// </summary>
        public static void AddHold(Func<bool> isHolding)
        {
            if (isHolding != null && !Holds.Contains(isHolding))
            {
                Holds.Add(isHolding);
            }
        }

        /// <summary>止めている係が1つでもあるか。</summary>
        public static bool IsHeld
        {
            get
            {
                for (var i = 0; i < Holds.Count; i++)
                {
                    if (Holds[i]())
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 動ける範囲を入れる係を登録する。1つ目の引数は枠の名前（<see cref="TurretMoveSettings.areaFrameName"/>）。
        /// その名前の床があれば枠の内側の輪郭（<see cref="TurretMoveRegion.Frames"/>）だけを、無ければ床の長方形（<see cref="TurretMoveRegion.Rects"/>）を入れる。
        /// </summary>
        public static void SetFloorAreaProvider(Action<string, TurretMoveRegion> provider)
        {
            _floorProvider = provider;
        }

        /// <summary>動ける範囲を region に入れる（前の中身は消す）。何も無ければ false。</summary>
        internal static bool TryGetFloorArea(string frameName, TurretMoveRegion region)
        {
            region.Clear();
            _floorProvider?.Invoke(frameName, region);
            return !region.IsEmpty;
        }
    }
}
