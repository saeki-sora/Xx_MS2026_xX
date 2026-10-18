using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアを宝石のように見せる設定（見た目だけ）。<see cref="CoreCrystalController.gemLook"/> が持ち、<see cref="CoreGemLook"/> が使う。
    /// 宝石の色は、コアの絵の色（HP で汚れていく色）をそのまま使う。
    /// </summary>
    [Serializable]
    public sealed class CoreGemLookSettings
    {
        [Tooltip("OFFにすると、宝石の見た目を重ねない（元の絵のまま）。")]
        public bool enabled = true;

        [Header("宝石")]
        [Range(0f, 1f), Tooltip("宝石の層の濃さ。1に近いほど元の絵が見えなくなる。")]
        public float opacity = 0.92f;

        [Range(0f, 0.15f), Tooltip("後ろの床・背景が曲がって透ける強さ。")]
        public float refraction = 0.035f;

        [Range(0f, 2f), Tooltip("ふちの光の強さ。")]
        public float rimStrength = 0.8f;

        [Range(0f, 2f), Tooltip("左上のつやの点の明るさ。")]
        public float highlight = 0.9f;

        [Range(0f, 2f), Tooltip("中でゆらめく光の筋の強さ。")]
        public float caustic = 0.35f;

        [Header("脈打つ光")]
        [ColorUsage(false), Tooltip("HP が多いときの、中心から脈打つ光の色。")]
        public Color glowColor = new Color(0.75f, 0.95f, 1f);

        [ColorUsage(false), Tooltip("HP が少ないときの光の色（HP が減るほどこの色に近づく）。")]
        public Color hurtColor = new Color(1f, 0.25f, 0.15f);

        [Range(0f, 2f), Tooltip("脈打つ光の明るさ。")]
        public float glowStrength = 0.6f;

        [Min(0f), Tooltip("HP が多いときの脈の速さ（回/秒）。")]
        public float calmPulsePerSecond = 0.5f;

        [Min(0f), Tooltip("HP が0に近いときの脈の速さ（回/秒）。")]
        public float hurtPulsePerSecond = 2.4f;

        [Header("ひび")]
        [Range(0f, 1f), Tooltip("コアの HP がこの割合を下回ると、ひびが入り始める（HP 0 で一番広がる）。0ならひびは入らない。")]
        public float crackStartHealth01 = 0.8f;

        [ColorUsage(false), Tooltip("ひびから漏れる光の色。")]
        public Color crackColor = new Color(1f, 0.45f, 0.2f);

        [Range(0.01f, 0.3f), Tooltip("ひびの線の太さ。")]
        public float crackWidth = 0.06f;

        [Range(0f, 2f), Tooltip("ダメージを受けた瞬間に、ひびと宝石が白く光る秒数。0なら光らない。")]
        public float hitFlashSeconds = 0.25f;
    }

    /// <summary>
    /// コアがダメージを受けた瞬間に広がる、警告の光の輪の設定（見た目だけ）。<see cref="CoreCrystalController.hitRing"/> が持ち、
    /// <see cref="CoreHitRings"/> が使う。ネット対戦の Client にもコアの HP は届くので、全員の画面に出る。
    /// </summary>
    [Serializable]
    public sealed class CoreHitRingSettings
    {
        [Tooltip("OFFにすると、ダメージを受けても輪は出ない。")]
        public bool enabled = true;

        [ColorUsage(false), Tooltip("輪の色。")]
        public Color color = new Color(1f, 0.2f, 0.15f);

        [Range(0f, 4f), Tooltip("輪の明るさ。")]
        public float brightness = 1.5f;

        [Min(0.5f), Tooltip("輪が広がる半径（メートル）。")]
        public float radius = 3.5f;

        [Range(0.1f, 3f), Tooltip("輪が広がって消えるまでの秒数。")]
        public float seconds = 0.6f;

        [Range(0.02f, 1f), Tooltip("輪の太さ（メートル）。")]
        public float width = 0.18f;

        [Range(0f, 2f), Tooltip("次の輪を出せるまでの最短の間隔（秒）。敵がたくさん当たっても輪だらけにならないように。")]
        public float minInterval = 0.25f;
    }
}
