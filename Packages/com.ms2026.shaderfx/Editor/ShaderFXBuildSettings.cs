using UnityEngine;

namespace MS2026.ShaderFX.Editor
{
    [CreateAssetMenu(menuName = "ShaderFX/Build Settings (Variant Stripping)", fileName = "ShaderFXBuildSettings")]
    public sealed class ShaderFXBuildSettings : ScriptableObject
    {
        [Tooltip("有効にすると、ビルド時に「プロジェクト内の EffectProfile アセットで実際に使われている組み合わせ」" +
                  "以外の ShaderFX/Uber バリアントを除外し、ビルドサイズ・ビルド時間を削減します。\n\n" +
                  "注意: スクリプトだけで動的生成し、.asset として保存していない EffectProfile の組み合わせは、" +
                  "この走査には含まれません。そのような使い方をする予定がある場合は無効のままにしてください " +
                  "(誤って除外されると、実行時にそのバリアントだけ効果が出ない、または見た目が壊れます)。")]
        public bool enableVariantStripping;
    }
}
