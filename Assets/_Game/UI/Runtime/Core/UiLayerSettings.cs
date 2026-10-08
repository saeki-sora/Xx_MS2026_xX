using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.UI
{
    /// <summary>
    /// 画面の重なりの段（レイヤー）の一覧。下の段ほど奥、上の段ほど手前に出る。
    /// 例: 背景 → HUD（ゲーム中の表示）→ メニュー → ポップアップ → 通知 → 画面切り替え。
    /// 段ごとに1枚の Canvas が作られ、画面（UiScreen）はどれかの段に入る。
    /// </summary>
    [CreateAssetMenu(menuName = "UI/Layer Settings", fileName = "UiLayerSettings")]
    public sealed class UiLayerSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("段の名前（画面の設定で選ぶ名前。英数字推奨）。")]
            public string id = "Menu";

            [Tooltip("ツールに出す日本語の名前。")]
            public string label = "メニュー";

            [Tooltip("この段の説明（何を入れる段か）。")]
            public string description;

            [Tooltip("描く順番。大きいほど手前。")]
            public int sortOrder = 200;

            [Tooltip("ツールでの色分け。")]
            public Color color = new Color(0.31f, 0.76f, 0.97f);

            [Tooltip("ONなら、この段で一度に開ける画面は1つだけ（新しく開くと前の画面は閉じる）。")]
            public bool oneAtATime;
        }

        [Tooltip("下（奥）から順に並べる。")]
        public List<Layer> layers = new List<Layer>();

        [Tooltip("基準の画面サイズ（この大きさで作った画面が、実際の画面に合わせて拡大縮小される）。")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Tooltip("横と縦のどちらに合わせるか（0=横幅、1=高さ、0.5=半々）。")]
        [Range(0f, 1f)]
        public float matchWidthOrHeight = 0.5f;

        [Tooltip("セーフエリア（テレビ等で端が切れても大丈夫な範囲）の割合。UIスタジオのガイド線に使う。")]
        [Range(0.8f, 1f)]
        public float safeArea = 0.92f;

        public Layer Find(string id)
        {
            foreach (var layer in layers)
            {
                if (layer != null && layer.id == id)
                {
                    return layer;
                }
            }

            return null;
        }

        /// <summary>よく使う段をひととおり入れる。</summary>
        public void ResetToDefaults()
        {
            layers = new List<Layer>
            {
                new Layer { id = "Background", label = "背景", description = "一番奥。タイトルの背景絵など。", sortOrder = 0, color = new Color(0.55f, 0.6f, 0.68f) },
                new Layer { id = "HUD", label = "HUD", description = "ゲーム中ずっと出ている表示（熱ゲージ・コアのHPなど）。", sortOrder = 100, color = new Color(0.37f, 0.83f, 0.61f) },
                new Layer { id = "Menu", label = "メニュー", description = "タイトル・ロビー・リザルトなど、1画面まるごとの画面。", sortOrder = 200, color = new Color(0.31f, 0.76f, 0.97f), oneAtATime = true },
                new Layer { id = "Popup", label = "ポップアップ", description = "確認・ポーズなど、上に重ねて出す小窓。", sortOrder = 300, color = new Color(0.88f, 0.36f, 1f) },
                new Layer { id = "Toast", label = "通知", description = "「P2が参加しました」などの短いお知らせ。押せない。", sortOrder = 400, color = new Color(1f, 0.78f, 0.34f) },
                new Layer { id = "Transition", label = "画面切り替え", description = "画面切り替えの幕（ワイプ等）。自動で使われる。", sortOrder = 900, color = new Color(1f, 0.48f, 0.24f) },
                new Layer { id = "Debug", label = "デバッグ", description = "開発用の表示。一番手前。", sortOrder = 1000, color = new Color(0.62f, 0.64f, 0.68f) }
            };
        }

        private void Reset() => ResetToDefaults();
    }

    /// <summary>この文字列の欄は「レイヤー（段）の名前」。エディタで一覧から選べるようになる。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UiLayerIdAttribute : PropertyAttribute
    {
    }
}
