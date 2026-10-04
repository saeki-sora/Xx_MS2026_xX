using System;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Vfx;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 「その瞬間に出すエフェクトと効果音」1組。このプロジェクトの演出欄（レーザー・破壊可能物・スマッシュボール）は
    /// すべてこの形にそろえてある。素材はD-Driveの番号札（VFX/SE）で指定するので、使い回し（プール）・音の連打の間引き・
    /// 差し替え・Validationは D-Drive 側の仕組みがそのまま効く。鳴らすのは <see cref="FortressEffectPlayer"/>。
    /// </summary>
    [Serializable]
    public sealed class FortressEffect
    {
        [Tooltip("出すエフェクト（D-DriveのVFX）。空なら出さない。")]
        public AssetId<VfxMarker> vfx;

        [Tooltip("鳴らす効果音（D-DriveのSE）。空なら鳴らさない。音量・同時に鳴らす数・連打の間引きはSE側の設定で調整する。")]
        public AssetId<SeMarker> se;

        [Tooltip("ONなら、エフェクトの調整項目（下の名前）にプレイヤー色を入れる。エフェクト側にその項目が無ければ何もしない。")]
        public bool tintWithPlayerColor = true;

        [Tooltip("プレイヤー色を入れる、VFXの調整項目（VFX Editor の Params）のラベル名。")]
        public string colorParam = "Color";

        public bool HasVfx => vfx.IsValid;
        public bool HasSe => se.IsValid;
        public bool IsEmpty => !HasVfx && !HasSe;
    }
}
