using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボス戦の配置を、ステージの「遊べる範囲」から自動で作るときの割合（<see cref="BossDefinition.formation"/>）。
    /// 横の割合は範囲の左端を 0、右端を 1。縦は下端を 0、上端を 1。ステージに目印（<see cref="BossFormationMarkers"/>）があればそちらが優先。
    /// </summary>
    [Serializable]
    public sealed class BossFormationShape
    {
        [Tooltip("コアの横の場所（範囲の左端 0 〜 右端 1）。")]
        [Range(0f, 0.5f)]
        public float coreX01 = 0.08f;

        [Tooltip("4人の縦一列の横の場所。")]
        [Range(0f, 0.6f)]
        public float turretX01 = 0.2f;

        [Tooltip("縦一列のいちばん上（P1）の高さ（下端 0 〜 上端 1）。")]
        [Range(0.5f, 1f)]
        public float turretTop01 = 0.84f;

        [Tooltip("縦一列のいちばん下（P4）の高さ。")]
        [Range(0f, 0.5f)]
        public float turretBottom01 = 0.16f;

        [Tooltip("ボスが止まる場所（右端寄り）。ただしレーザーが部位に届く距離より遠くにはしない。")]
        [Range(0.3f, 1f)]
        public float bossStopX01 = 0.74f;

        [Tooltip("ボスの体の前の端が、レーザーの届く距離のこの割合の所で止まる（届く距離より遠くならこちらを使う）。")]
        [Range(0.3f, 1f)]
        public float bossStopRangeFraction = 0.88f;

        [Tooltip("ボスが「コアに届いた」とする線（4人の列から、体の半径のこの倍だけ右）。")]
        [Min(0f)]
        public float reachLineOffset = 0.25f;

        [Tooltip("残さない障害物を逃がすとき、遊べる範囲の上下の端からさらに外へ出す距離（メートル）。")]
        [Min(0f)]
        public float escapeMargin = 1.5f;

        [Tooltip("自動で残す障害物の数（ステージに残し方の指定が無いとき）。")]
        [Range(0, 4)]
        public int autoKeepCount = 2;

        [Tooltip("自動で残す障害物の横の場所。")]
        [Range(0.2f, 0.8f)]
        public float keepX01 = 0.45f;

        [Tooltip("自動で残す障害物の高さ（上の物・下の物）。")]
        public Vector2 keepY01 = new Vector2(0.75f, 0.25f);
    }

    /// <summary>ボス戦の配置（全員のPCで同じ計算。ワールド座標）。</summary>
    public struct BossFormationLayout
    {
        /// <summary>遊べる範囲（見える範囲と経路の範囲の重なり）。</summary>
        public Rect area;

        public Vector2 core;

        /// <summary>P1〜P4 の砲台の場所（P1 が上）。</summary>
        public Vector2[] turrets;

        /// <summary>砲台の扇形の真ん中の向き（<see cref="LaserTurret.AimAngleDegrees"/> と同じ。ボスの方）。</summary>
        public float turretAngle;

        /// <summary>ボスの体の半径（メートル）。</summary>
        public float bossRadius;

        /// <summary>ボスが止まる場所（上下にゆれる真ん中）。</summary>
        public Vector2 bossStop;

        /// <summary>ボスが入ってくる前にいる場所（画面の右の外）。</summary>
        public Vector2 bossEntry;

        /// <summary>ボスの中心がこの X より左に来たら「コアに届いた」。</summary>
        public float reachCenterX;

        /// <summary>上下にゆれる大きさ（メートル）。</summary>
        public float bobAmplitude;

        public bool IsValid => turrets != null && turrets.Length == 4 && bossRadius > 0f;
    }

    /// <summary>ボス戦の配置の計算（Unity の物を使わない。テストあり）。</summary>
    public static class BossFormationMath
    {
        /// <summary>
        /// 遊べる範囲から配置を作る。laserRange はレーザーの届く距離（ボスが遠すぎて部位に届かない、を防ぐ）。
        /// </summary>
        public static BossFormationLayout Compute(Rect area, BossFormationShape shape, float bodyRadius01, float bobAmount01, float laserRange)
        {
            shape ??= new BossFormationShape();
            var w = Mathf.Max(0.01f, area.width);
            var h = Mathf.Max(0.01f, area.height);
            var radius = Mathf.Max(0.1f, h * Mathf.Clamp(bodyRadius01, 0.02f, 0.45f));
            var midY = area.center.y;

            var layout = new BossFormationLayout
            {
                area = area,
                bossRadius = radius,
                turrets = new Vector2[4],
                turretAngle = -90f // 右（+X）向き
            };

            layout.core = new Vector2(area.xMin + w * shape.coreX01, midY);
            var turretX = area.xMin + w * Mathf.Max(shape.turretX01, shape.coreX01 + 0.02f);
            var top = area.yMin + h * shape.turretTop01;
            var bottom = area.yMin + h * shape.turretBottom01;
            for (var i = 0; i < 4; i++)
            {
                layout.turrets[i] = new Vector2(turretX, Mathf.Lerp(top, bottom, i / 3f));
            }

            // 止まる場所: 右端寄り。ただし体の前の端がレーザーの届く距離より遠くならない所まで寄せる。
            var stopX = area.xMin + w * shape.bossStopX01;
            if (laserRange > 0f)
            {
                stopX = Mathf.Min(stopX, turretX + laserRange * shape.bossStopRangeFraction + radius * 0.9f);
            }

            stopX = Mathf.Max(stopX, turretX + radius * 2.2f);
            layout.bossStop = new Vector2(stopX, midY);
            layout.bossEntry = new Vector2(Mathf.Max(area.xMax, stopX) + radius * 2.4f, midY);
            layout.reachCenterX = turretX + radius * (1f + shape.reachLineOffset);
            layout.bobAmplitude = Mathf.Max(0f, (h * 0.5f - radius * 1.05f) * Mathf.Clamp01(bobAmount01));
            return layout;
        }

        /// <summary>ボスが今の X にいるときの「近づき具合」（止まる場所 0 〜 コアに届く 1）。</summary>
        public static float Approach01(in BossFormationLayout layout, float bossX)
        {
            var span = layout.bossStop.x - layout.reachCenterX;
            return span > 1e-4f ? Mathf.Clamp01((layout.bossStop.x - bossX) / span) : 1f;
        }

        /// <summary>止まる場所からコアに届くまで approachSeconds で近づく速さ（メートル/秒）。</summary>
        public static float ApproachSpeed(in BossFormationLayout layout, float approachSeconds) =>
            Mathf.Max(0f, layout.bossStop.x - layout.reachCenterX) / Mathf.Max(0.1f, approachSeconds);

        /// <summary>残さない障害物の逃げ先: 近い方の上下の端の外へ（横の場所はそのまま、範囲の中に収める）。</summary>
        public static Vector2 EscapePosition(Vector2 position, Rect area, float objectRadius, float margin)
        {
            var x = Mathf.Clamp(position.x, area.xMin, area.xMax);
            var up = position.y >= area.center.y;
            var y = up ? area.yMax + objectRadius + margin : area.yMin - objectRadius - margin;
            return new Vector2(x, y);
        }

        /// <summary>自動で残す障害物の行き先（index 番目）。</summary>
        public static Vector2 KeepPosition(int index, Rect area, BossFormationShape shape)
        {
            shape ??= new BossFormationShape();
            var y01 = index % 2 == 0 ? shape.keepY01.x : shape.keepY01.y;
            var x01 = shape.keepX01 + (index / 2) * 0.12f;
            return new Vector2(area.xMin + area.width * x01, area.yMin + area.height * y01);
        }

        /// <summary>2つの矩形の重なり（重ならなければ a）。</summary>
        public static Rect IntersectOr(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax - xMin > 0.5f && yMax - yMin > 0.5f ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : a;
        }

        /// <summary>なめらかに始まってなめらかに止まる（0〜1）。</summary>
        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>勢いよく出て、ゆっくり止まる（0〜1）。</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }
}
