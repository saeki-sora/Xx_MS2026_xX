using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// 実験中の要素とのつなぎ（アセンブリ MS2026.CPU.Experimental。砲台の移動・付いていくカメラ）を切る・戻すメニュー。
    /// 切ると記号 <see cref="Symbol"/> が PC（Standalone）の設定に入り、つなぎのアセンブリはコンパイルされなくなる
    /// （「移動が不採用になったときの CPU」を、このブランチのまま試せる）。本体のコードはこの記号を見ない。
    /// </summary>
    internal static class CpuExperimentalToggleMenu
    {
        public const string Symbol = "MS2026_CPU_NO_EXPERIMENTAL";
        private const string MenuPath = "Tools/CPU/実験中の要素とのつなぎを切る（移動しない CPU で試す）";

        [MenuItem(MenuPath, priority = 30)]
        private static void Toggle()
        {
            var target = NamedBuildTarget.Standalone;
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s => s.Length > 0).ToList();
            var off = symbols.Contains(Symbol);
            var message = off
                ? "実験中の要素とのつなぎを戻します（CPU が移動などを使うようになります）。\nプロジェクトの設定（Scripting Define Symbols）から " + Symbol + " を消します。"
                : "実験中の要素とのつなぎを切ります（CPU は席から動かず、席のカメラの決まった視点で見ます）。\nプロジェクトの設定（Scripting Define Symbols）に " + Symbol + " を足します。";
            if (!EditorUtility.DisplayDialog("CPU", message, off ? "戻す" : "切る", "やめる"))
            {
                return;
            }

            if (off)
            {
                symbols.Remove(Symbol);
            }
            else
            {
                symbols.Add(Symbol);
            }

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            var off = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';').Contains(Symbol);
            Menu.SetChecked(MenuPath, off);
            return !EditorApplication.isPlaying;
        }
    }
}
