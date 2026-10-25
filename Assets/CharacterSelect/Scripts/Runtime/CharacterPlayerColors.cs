using MS2026.Fortress;
using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラ選択で選んだキャラの色（<see cref="CharacterDefinition.themeColor"/>）を、ゲーム中のその人の色にする
    /// （レーザー・狙いの線・HUD の番号・リザルトの札など、<see cref="FortressColors.PlayerColor"/> を使う所すべて）。
    /// キャラ選択を通っていない（Game を直接 Play した）ときと、その席の人がいないときは席の色のまま。
    /// </summary>
    internal static class CharacterPlayerColors
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            FortressColors.ChosenColor = Chosen;
        }

        private static Color? Chosen(int playerIndex)
        {
            var character = CharacterSelectionResult.Get(playerIndex);
            if (character == null)
            {
                return null;
            }

            var color = character.themeColor;
            color.a = 1f;
            return color;
        }
    }
}
