using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>見た目のPrefabを、オブジェクトの大きさ（Transformのスケール）にどう合わせるか。</summary>
    public enum DestructibleFitMode
    {
        /// <summary>縦横それぞれを大きさにぴったり合わせる（絵が引き伸ばされる）。</summary>
        Stretch,

        /// <summary>縦横比を保ったまま、大きさの範囲に収まる最大サイズにする。</summary>
        Contain,

        /// <summary>Prefab本来の大きさのまま表示する（大きさの設定は当たり判定にだけ効く）。</summary>
        Original
    }

    /// <summary>耐久が一定以下になったときに切り替わる見た目の段階（ひび割れ・半壊など）。</summary>
    [Serializable]
    public sealed class DestructibleDamageStage
    {
        [Tooltip("耐久の割合（0-1）がこの値以下になると、この段階の見た目に切り替わる。")]
        [Range(0f, 1f)]
        public float healthBelow = 0.5f;

        [Tooltip("この段階の見た目Prefab（2Dスプライトでも3Dモデルでも可）。空なら絵は替えず、暗くなる演出だけが進む。")]
        public GameObject visualPrefab;
    }

    public enum DestructibleHealthBarMode
    {
        Never,
        WhenDamaged,
        Always
    }

    /// <summary>頭上に出す耐久ゲージの設定。</summary>
    [Serializable]
    public sealed class DestructibleHealthBarSettings
    {
        [Tooltip("耐久ゲージを表示するタイミング。")]
        public DestructibleHealthBarMode mode = DestructibleHealthBarMode.WhenDamaged;

        [Tooltip("ゲージの長さ(ワールド単位)。0ならオブジェクトの横幅に合わせる。")]
        [Min(0f)]
        public float width;

        [Tooltip("ゲージの太さ(ワールド単位)。")]
        [Min(0.02f)]
        public float height = 0.12f;

        [Tooltip("オブジェクトの上端からの距離(ワールド単位)。")]
        public float offsetY = 0.2f;

        public Color fillColor = new Color(0.4f, 0.9f, 0.4f);
        public Color backColor = new Color(0f, 0f, 0f, 0.6f);
    }

    /// <summary>見た目に関する設定。絵はPrefabで差し替えるので、2Dでも3Dでもアニメ付きでも使える。</summary>
    [Serializable]
    public sealed class DestructibleVisualSettings
    {
        [Tooltip("通常時の見た目Prefab（SpriteRendererでもMeshRendererでも可）。空なら仮の四角が表示される。")]
        public GameObject visualPrefab;

        [Tooltip("見た目をオブジェクトの大きさにどう合わせるか。")]
        public DestructibleFitMode fitMode = DestructibleFitMode.Stretch;

        [Tooltip("見た目の位置のずらし量（オブジェクトの大きさに対する割合。0.5で半分ずれる）。")]
        public Vector2 visualOffset;

        [Tooltip("見た目の描画順に加える値。地形より手前/奥に見せたいときに調整する。")]
        public int sortingOrderOffset;

        [Tooltip("耐久が減るにつれて見た目を切り替える段階。耐久の割合が低い段階ほど優先される。")]
        public List<DestructibleDamageStage> damageStages = new List<DestructibleDamageStage>();

        [Tooltip("耐久が減るにつれて見た目を暗くするか。")]
        public bool darkenWithDamage = true;

        [Tooltip("壊れている間に表示する見た目Prefab（瓦礫・破片など）。空なら通常の見た目を薄い「跡」として残す。")]
        public GameObject destroyedPrefab;

        [Tooltip("破壊中に、元の位置に残る「跡」の濃さ。0で完全に消える。再生位置の目印になる。（破壊時Prefabが空の場合のみ。3Dは透明対応のマテリアルが必要）")]
        [Range(0f, 1f)]
        public float destroyedGhostAlpha = 0.15f;

        [Tooltip("ONなら、見た目Prefabが空のときの仮の四角の色を、下の色で上書きする。")]
        public bool controlPlaceholderColor = true;

        [Tooltip("見た目Prefabが空のときの、仮の四角の色。")]
        public Color placeholderColor = new Color(0.72f, 0.52f, 0.34f);

        [Header("被ダメージ時の反応")]
        [Tooltip("ダメージを受けた瞬間に色が光る長さ(秒)。0で光らない。")]
        [Min(0f)]
        public float hitFlashSeconds = 0.06f;

        [Tooltip("ダメージを受けたときに光る色。")]
        public Color hitFlashColor = Color.white;

        [Tooltip("ダメージを受けたときに見た目が震える大きさ（オブジェクトの大きさに対する割合）。0で震えない。")]
        [Range(0f, 0.3f)]
        public float hitShake = 0.03f;

        [Header("耐久ゲージ")]
        public DestructibleHealthBarSettings healthBar = new DestructibleHealthBarSettings();

        /// <summary>現在の耐久の割合に対応する段階の番号。どの段階にも当たらなければ-1。</summary>
        public int ResolveStage(float health01)
        {
            var result = -1;
            var lowest = float.PositiveInfinity;
            for (var i = 0; i < damageStages.Count; i++)
            {
                var stage = damageStages[i];
                if (stage != null && health01 <= stage.healthBelow && stage.healthBelow < lowest)
                {
                    lowest = stage.healthBelow;
                    result = i;
                }
            }

            return result;
        }
    }
}
