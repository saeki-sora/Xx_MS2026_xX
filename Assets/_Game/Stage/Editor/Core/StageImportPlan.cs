using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>モデルの中の「目印」（MARK_ で始まる名前の空の物）の種類。</summary>
    public enum StageMarkerKind
    {
        None,
        Core,
        Turret,
        Spawn
    }

    /// <summary>取り込みの設定（取り込みページの入力）。</summary>
    [Serializable]
    public sealed class StageImportOptions
    {
        [Tooltip("ONなら、モデルのすぐ下の子を1つずつ別の背景オブジェクトにする（シャンプー・歯ブラシ…を別々に扱える）。")]
        public bool splitChildren = true;

        [Tooltip("ONなら、Maya（Yが上）の向きを、ゲームの床（XYが床、カメラ側が上）に合わせて回す。普通はON。")]
        public bool convertMayaAxes = true;

        [Tooltip("床の上での向き（90度ずつ）。取り込んだら画面と向きが違ったときに回す。")]
        public int yawSteps;

        [Tooltip("大きさの倍率。Mayaの1cmはUnityでは0.01になるので、ゲームの大きさに合わせて大きくする。")]
        public float scale = 1f;

        [Tooltip("ONなら、全体の真ん中をゲームの中心（コアのある場所、0,0）に合わせる。OFFならMayaの位置のまま。")]
        public bool centerOnOrigin;

        [Tooltip("ONなら、取り込んだ物のマテリアルを専用シェーダー（透け・焦げ・光り対応）に切り替える。")]
        public bool convertMaterials = true;

        /// <summary>モデルの座標 → ゲームの座標（StageRootから見た）への変換。中心合わせのずらしは含まない。</summary>
        public Matrix4x4 ConversionMatrix()
        {
            var axes = convertMayaAxes ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            var yaw = Quaternion.Euler(0f, 0f, 90f * (((yawSteps % 4) + 4) % 4));
            return Matrix4x4.TRS(Vector3.zero, yaw * axes, Vector3.one * Mathf.Max(0.0001f, scale));
        }
    }

    /// <summary>取り込む物1つ分の予定（取り込みページの表の1行）。</summary>
    public sealed class StageImportNode
    {
        public Transform Source;
        public string Path;
        public string Name;
        public bool Include = true;
        public StagePropRole Role;
        public StageMarkerKind Marker;
        public int MarkerIndex;
        public int TriangleCount;

        /// <summary>変換後の大きさ（ゲームの単位）。表示用。</summary>
        public Vector3 Size;

        /// <summary>変換後の中心（StageRootから見た位置）。</summary>
        public Vector3 Center;

        public bool IsMarker => Marker != StageMarkerKind.None;
    }

    /// <summary>取り込みの予定一式。</summary>
    public sealed class StageImportPlan
    {
        public GameObject SourceAsset;
        public readonly List<StageImportNode> Nodes = new List<StageImportNode>();

        /// <summary>取り込む物全体（目印を除く）の範囲。中心合わせに使う。</summary>
        public Bounds TotalBounds;

        public Vector3 CenterOffset(StageImportOptions options) =>
            options.centerOnOrigin ? new Vector3(-TotalBounds.center.x, -TotalBounds.center.y, 0f) : Vector3.zero;
    }
}
