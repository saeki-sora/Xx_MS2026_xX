using System;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>カットインの時間と絵の寄り方（大きい窓・小さい窓で共通）。</summary>
    [Serializable]
    public sealed class SkillCutInMotion
    {
        [Tooltip("左から稲妻が走って窓が開くまでの秒数。")]
        [SkillLabel("開く時間（秒）")]
        [Min(0.01f)]
        public float openSeconds = 0.12f;

        [Tooltip("開いてから閉じ始めるまでの秒数。")]
        [SkillLabel("見せる時間（秒）")]
        [Min(0f)]
        public float holdSeconds = 1.25f;

        [Tooltip("窓が目の高さの帯につぶれ、横一本の稲妻になって消えるまでの秒数。")]
        [SkillLabel("閉じる時間（秒）")]
        [Min(0.01f)]
        public float closeSeconds = 0.23f;

        [Tooltip("見せている間に、絵がゆっくり寄る量（1 = 寄らない）。")]
        [SkillLabel("ゆっくり寄る量（倍）")]
        [Range(1f, 1.3f)]
        public float slowZoom = 1.06f;

        [Tooltip("途中で一瞬グッと寄る量（0 = 寄らない）。表情違いの絵に切り替わるのも同じ瞬間。")]
        [SkillLabel("途中でグッと寄る量")]
        [Range(0f, 0.2f)]
        public float punchZoom = 0.06f;

        [Tooltip("グッと寄る（表情が変わる）タイミング。見せる時間の何割目か。")]
        [SkillLabel("グッと寄るタイミング（割合）")]
        [Range(0f, 1f)]
        public float punchAt = 0.4f;

        [Tooltip("開いた瞬間とグッと寄った瞬間に、窓が白く光る強さ。")]
        [SkillLabel("白く光る強さ")]
        [Range(0f, 1f)]
        public float flash = 0.85f;

        /// <summary>開いてから消え終わるまでの秒数。</summary>
        public float TotalSeconds => openSeconds + holdSeconds + closeSeconds;
    }

    /// <summary>自分が撃ったときに画面の左に出る大きい窓の形。数字はすべて画面の幅・高さに対する割合。</summary>
    [Serializable]
    public sealed class SkillCutInBigWindow
    {
        [Tooltip("窓の右のふちの、画面の上の端での位置（画面の幅の割合）。")]
        [SkillLabel("右のふち（上）")]
        [Range(0.1f, 0.9f)]
        public float edgeTop = 0.34f;

        [Tooltip("窓の右のふちの、画面の下の端での位置（画面の幅の割合）。上より大きいと「／」でなく「＼」向きに傾く。")]
        [SkillLabel("右のふち（下）")]
        [Range(0.1f, 0.9f)]
        public float edgeBottom = 0.46f;

        [Tooltip("顔を置く場所（画面の左下が (0, 0)、右上が (1, 1)）。")]
        [SkillLabel("顔を置く場所")]
        public Vector2 faceAnchor = new Vector2(0.2f, 0.56f);

        [Tooltip("絵の高さ（画面の高さの何倍か）。大きいほど顔が大きく映る。ラフの絵では顔が絵の高さの4分の1ほど。")]
        [SkillLabel("絵の大きさ（画面の高さの倍）")]
        [Range(0.5f, 4f)]
        public float pictureHeight = 2f;

        [Tooltip("閉じるときにつぶれる帯の太さ（画面の高さの割合）。")]
        [SkillLabel("閉じるときの帯の太さ")]
        [Range(0.02f, 0.5f)]
        public float bandHeight = 0.2f;

        [Tooltip("閉じるときの帯の傾き（右の端が左の端より、画面の高さの何割上がるか）。")]
        [SkillLabel("閉じるときの帯の傾き")]
        [Range(-0.5f, 0.5f)]
        public float bandTilt = 0.13f;
    }

    /// <summary>小さい窓の消え方。</summary>
    public enum SkillCutInSmallEnd
    {
        /// <summary>欄の丸いアイコンに吸い込まれるように縮んで消える。</summary>
        [InspectorName("欄のアイコンに吸い込まれる")]
        ShrinkToIcon,

        /// <summary>大きい窓と同じく、細い帯につぶれて横一本の稲妻になって消える。</summary>
        [InspectorName("大きい窓と同じ（帯→線）")]
        Line
    }

    /// <summary>
    /// ほかの人が撃ったときに、その人の HUD の欄（ほかの3人の欄）から飛び出す小さい窓（2026-11-03 の比較ページの B）。
    /// 欄の丸いアイコンの中心から右へ稲妻が走って斜めの窓が開き、最後はアイコンに吸い込まれて消える。開いている間、アイコンにもその人の顔が出る。
    /// 数字は HUD と同じ 1920×1080 の画面での大きさ。
    /// </summary>
    [Serializable]
    public sealed class SkillCutInSmallWindow
    {
        [Tooltip("OFFなら、ほかの人が撃ってもこの画面には何も出さない。")]
        [SkillLabel("出す")]
        public bool show = true;

        [Tooltip("窓の幅と高さ。高さが欄の間隔（90）より大きいと上下の欄に少しかぶる（後から撃った人の窓が上に重なる）。")]
        [SkillLabel("大きさ")]
        public Vector2 size = new Vector2(560f, 192f);

        [Tooltip("窓の左の端は欄の丸いアイコンの中心、上下は欄の真ん中。そこからのずれ。")]
        [SkillLabel("位置のずれ")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("窓の右のふちの傾き（上の端が下の端よりどれだけ左にあるか）。")]
        [SkillLabel("右のふちの傾き")]
        public float slant = 52f;

        [Tooltip("顔を置く場所（窓の左下が (0, 0)、右上が (1, 1)）。")]
        [SkillLabel("顔を置く場所")]
        public Vector2 faceAnchor = new Vector2(0.33f, 0.48f);

        [Tooltip("絵の高さ（窓の高さの何倍か）。")]
        [SkillLabel("絵の大きさ（窓の高さの倍）")]
        [Range(1f, 8f)]
        public float pictureHeight = 3f;

        [Tooltip("消え方。")]
        [SkillLabel("消え方")]
        public SkillCutInSmallEnd end = SkillCutInSmallEnd.ShrinkToIcon;

        [Tooltip("ONなら、窓が出ている間、欄の丸いアイコンの上にもその人の顔（キャラの色の輪つき）を出す。")]
        [SkillLabel("欄のアイコンにも顔を出す")]
        public bool iconFace = true;

        [Tooltip("アイコンの上に出す顔の丸の大きさ（欄の高さの何倍か）。")]
        [SkillLabel("アイコンの顔の大きさ（欄の高さの倍）")]
        [Range(0.5f, 2f)]
        public float iconFaceSize = 1.15f;

        [Tooltip("アイコンの顔の丸での、絵の高さ（丸の直径の何倍か）。大きいほど顔が大きく映る。")]
        [SkillLabel("アイコンの顔の寄り（直径の倍）")]
        [Range(1f, 10f)]
        public float iconPictureHeight = 5.2f;

        [Tooltip("HUD に「ほかの3人の欄」（OtherPlayers/Other1〜3）が見つからないときに使う、1段目の欄の位置（画面の左下から）と大きさ。")]
        [SkillLabel("欄が見つからないときの1段目（位置）")]
        public Vector2 fallbackRowPosition = new Vector2(44f, 408f);

        [Tooltip("欄が見つからないときの、欄1つの大きさ。")]
        [SkillLabel("欄が見つからないときの欄の大きさ")]
        public Vector2 fallbackRowSize = new Vector2(320f, 80f);

        [Tooltip("欄が見つからないときの、段と段の間隔（下へ）。")]
        [SkillLabel("欄が見つからないときの段の間隔")]
        public float fallbackRowPitch = 90f;
    }

    /// <summary>窓のふちの稲妻。</summary>
    [Serializable]
    public sealed class SkillCutInEdge
    {
        [Tooltip("稲妻の芯（白い線）の太さ。")]
        [SkillLabel("芯の太さ")]
        [Range(1f, 20f)]
        public float coreWidth = 5f;

        [Tooltip("まわりの光（キャラの色）の太さ。")]
        [SkillLabel("光の太さ")]
        [Range(0f, 80f)]
        public float glowWidth = 30f;

        [Tooltip("稲妻のギザギザの大きさ。")]
        [SkillLabel("ギザギザの大きさ")]
        [Range(0f, 40f)]
        public float jag = 12f;

        [Tooltip("稲妻の折れ目の数。")]
        [SkillLabel("折れ目の数")]
        [Range(2, 40)]
        public int segments = 14;

        [Tooltip("ギザギザを描き直す間隔（秒）。小さいほど激しくビリビリする。")]
        [SkillLabel("描き直す間隔（秒）")]
        [Range(0.01f, 0.3f)]
        public float refreshSeconds = 0.045f;

        [Tooltip("小さい窓での太さ・ギザギザの倍率。")]
        [SkillLabel("小さい窓での大きさ（倍）")]
        [Range(0.1f, 1f)]
        public float smallScale = 0.6f;
    }

    /// <summary>技名の文字。</summary>
    [Serializable]
    public sealed class SkillCutInText
    {
        [Tooltip("OFFなら技名を出さない。")]
        [SkillLabel("出す")]
        public bool show = true;

        [Tooltip("文字のフォント。空なら Unity に入っている標準のフォント（日本語は PC の文字で出る。HUD と同じ）。")]
        [SkillLabel("フォント")]
        public Font font;

        [Tooltip("大きい窓での文字の大きさ。")]
        [SkillLabel("大きさ（大きい窓）")]
        public int bigSize = 72;

        [Tooltip("小さい窓での文字の大きさ。")]
        [SkillLabel("大きさ（小さい窓）")]
        public int smallSize = 42;

        [Tooltip("大きい窓での文字の左下の位置（画面の左下が (0, 0)、右上が (1, 1)）。")]
        [SkillLabel("位置（大きい窓）")]
        public Vector2 bigPosition = new Vector2(0.025f, 0.06f);

        [Tooltip("文字のふちの太さ（色はキャラの色）。")]
        [SkillLabel("ふちの太さ")]
        [Range(0f, 8f)]
        public float outline = 4f;
    }

    /// <summary>カットインの音（D-Drive の SE）。</summary>
    [Serializable]
    public sealed class SkillCutInSounds
    {
        [Tooltip("窓が開くときの音。")]
        [SkillLabel("開く音")]
        public FortressEffect open = new FortressEffect();

        [Tooltip("窓が閉じ始めるときの音。")]
        [SkillLabel("閉じる音")]
        public FortressEffect close = new FortressEffect();

        [Tooltip("ONなら、ほかの人の小さい窓でも開く・閉じる音を鳴らす。")]
        [SkillLabel("小さい窓でも効果音を鳴らす")]
        public bool smallPlaysSe = true;

        [Tooltip("ONなら、ほかの人の小さい窓でもキャラの声を鳴らす（4台の PC から同時に声が出ることになる）。")]
        [SkillLabel("小さい窓でも声を鳴らす")]
        public bool smallPlaysVoice;
    }
}
