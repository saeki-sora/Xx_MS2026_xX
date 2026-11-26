using System;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習のシーン（PracticeTutorial.unity）だけで使う調整（2026-11-13）。本番の設定ファイル（砲台の LaserTuningConfig など）は書き換えず、
    /// 練習のシーンを読み込んだときに、そのシーンの中の砲台・スマッシュボールにだけ当てる。0 の値は「本番のまま」。
    /// </summary>
    [Serializable]
    public sealed class PracticeSceneTuning
    {
        [Header("砲台（熱）")]
        [Tooltip("思いきり握ってから、オーバーヒートするまでの秒数（0 = 本番のまま）。短くすると研修2がすぐ体験できる。")]
        [Min(0f)]
        public float secondsToOverheat = 5f;

        [Tooltip("熱がいっぱいの状態から、手を離して熱が 0 に戻るまでの秒数（0 = 本番のまま）。短くすると冷めるのが早い。")]
        [Min(0f)]
        public float secondsToCoolDown = 2f;

        [Tooltip("オーバーヒートしたら撃てない秒数（0 = 本番のまま）。")]
        [Min(0f)]
        public float overheatSilenceSeconds = 1.5f;

        [Tooltip("砲台が回る速さの倍率（1 = 本番のまま）。")]
        [Min(0.05f)]
        public float turnSpeedMultiplier = 1f;

        [Header("泡（スマッシュボール）")]
        [Tooltip("OFF = 泡を動かさない（その場に止める。ねらいやすい）。ON = 本番と同じく、ふわふわ動く。")]
        public bool smashBallsMove = false;

        [Tooltip("自分がこの課題に来るまで、泡を出さない（研修3「泡を割る」なら Bubble）。Grip にすると最初から出す。")]
        public PracticeStep showSmashBallsFrom = PracticeStep.Bubble;

        [Tooltip("割れた泡を直すまでの秒数（何度でも試せるように）。")]
        [Min(0f)]
        public float smashBallRepairSeconds = 2.5f;

        [Header("敵")]
        [Tooltip("OFF = 練習の敵を出さない。")]
        public bool enemiesEnabled = true;

        [Tooltip("1か所から敵が出てくる間隔（秒。0 = シーンを作ったときの値のまま）。「敵を1人ずつ別にする」が OFF のときだけ使う。")]
        [Min(0f)]
        public float enemySpawnIntervalSeconds = 0f;

        [Header("敵を1人ずつ別にする（2026-11-19）")]
        [Tooltip("ON = 敵は1人ずつ別（自分の画面にだけ出て、自分の歯磨き粉でしか倒せない。ほかの人に倒されて研修が進まない、を防ぐ）。\n" +
                 "OFF = 本番と同じく、4人で同じ敵（通信で同じ物を見る）。")]
        public bool separateEnemies = true;

        [Tooltip("自分の敵が同時に出る数の上限。")]
        [Min(1)]
        public int localEnemyMax = 7;

        [Tooltip("自分の敵が1体出てくる間隔（秒）。")]
        [Min(0.2f)]
        public float localEnemySpawnSeconds = 1.2f;

        [Tooltip("敵が出てくる所の、真ん中の歯からの距離（メートル）。")]
        [Min(2f)]
        public float localEnemySpawnRadius = 13f;

        [Tooltip("敵が出てくる向きの広がり（自分の砲台の向き ± この角度）。")]
        [Range(0f, 180f)]
        public float localEnemySpreadDegrees = 60f;

        [Tooltip("敵の体力の倍率（1 = 本番の敵と同じ。0.5 なら半分の時間で倒せる）。")]
        [Range(0.05f, 2f)]
        public float localEnemyHealthScale = 0.5f;

        [Tooltip("敵の歩く速さの倍率（1 = 本番の敵と同じ）。")]
        [Min(0.1f)]
        public float localEnemySpeedScale = 1f;
    }
}
