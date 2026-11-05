using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 螺旋で進む敵の設定（<see cref="EnemyTypeDefinition.spiral"/>）。「螺旋で進む」を ON にすると、
    /// 見えない点のまわりをくるくる回りながら、その点ごとコアへ近づく（まっすぐ進まない敵）。
    /// コアへ近づく速さは今までどおり「移動速度」、回る速さは 半径 × 回る速さ で決まる。
    /// 回る速さが近づく速さより速いと輪を描き、遅いと波打つだけになる。今は群衆(Swarm)モードの敵だけで動く。
    /// </summary>
    [Serializable]
    public sealed class EnemySpiralSettings
    {
        [Tooltip("ON にすると、この敵はくるくる回りながらコアへ近づく。")]
        public bool enabled;

        [Tooltip("回る輪の半径（ワールド単位）。大きいほど大回りする。")]
        [Min(0.05f)]
        public float radius = 0.7f;

        [Tooltip("1 秒に回る回数。0.5 なら 2 秒で 1 周。")]
        [Min(0.05f)]
        public float turnsPerSecond = 0.5f;

        [Tooltip("ON なら、回る向き（右回り・左回り）を 1 体ずつばらばらにする。OFF なら全員左回り。")]
        public bool mixDirections = true;

        /// <summary>回る分の速さ（m/秒。半径 × 角速度）。</summary>
        public float CircleSpeed => radius * turnsPerSecond * Mathf.PI * 2f;

        /// <summary>このままだと輪を描くか（回る速さが近づく速さより速いか）。</summary>
        public bool MakesLoops(float moveSpeed)
        {
            return CircleSpeed > moveSpeed;
        }
    }
}
