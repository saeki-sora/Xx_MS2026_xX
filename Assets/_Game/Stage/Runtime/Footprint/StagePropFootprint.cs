using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 背景オブジェクトの「敵が通れない範囲」。形は PolygonCollider2D にそのまま保存されるので、実行中は何も計算しない。
    /// 作り直しはステージ背景スタジオ（またはインスペクターのボタン）から <see cref="Rebuild"/> を呼ぶ。
    /// このTransformは床の上（Z回転だけ）に置くこと。2Dの当たり判定はX/Y回転を正しく扱えないため。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class StagePropFootprint : MonoBehaviour
    {
        public FootprintSettings settings = FootprintSettings.Default;

        [Tooltip("ONなら、自動で作った形を手で直したものとして扱い、自動の作り直しで上書きしない。")]
        public bool manuallyEdited;

        [SerializeField, HideInInspector] private int builtSignature;
        [SerializeField, HideInInspector] private int builtVertexCount;
        [SerializeField, HideInInspector] private float builtArea;
        [SerializeField, HideInInspector] private float builtCellSize;

        private PolygonCollider2D _collider;

        public PolygonCollider2D Collider => _collider != null ? _collider : _collider = GetComponent<PolygonCollider2D>();

        public int BuiltVertexCount => builtVertexCount;
        public float BuiltArea => builtArea;
        public float BuiltCellSize => builtCellSize;
        public bool HasShape => Collider != null && Collider.pathCount > 0;

        /// <summary>モデルの形や置き方、設定が、最後に作ったときから変わっているか。</summary>
        public bool IsStale(Transform modelRoot)
        {
            return !manuallyEdited && builtSignature != FootprintMeshSampler.ComputeSignature(modelRoot, transform, settings);
        }

        /// <summary>モデルの形から通れない範囲を作り直す。戻り値は作った結果の統計。</summary>
        public FootprintBuilder.Report Rebuild(Transform modelRoot)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            FootprintMeshSampler.Collect(modelRoot, transform, vertices, triangles);
            var paths = FootprintBuilder.Build(vertices, triangles, settings, out var report);
            ApplyPaths(paths);

            builtSignature = FootprintMeshSampler.ComputeSignature(modelRoot, transform, settings);
            builtVertexCount = report.VertexCount;
            builtArea = report.Area;
            builtCellSize = report.CellSize;
            manuallyEdited = false;
            return report;
        }

        public void ApplyPaths(IReadOnlyList<Vector2[]> paths)
        {
            var polygon = Collider;
            polygon.offset = Vector2.zero;
            polygon.pathCount = paths.Count;
            for (var i = 0; i < paths.Count; i++)
            {
                polygon.SetPath(i, paths[i]);
            }

            NavigationObstacle.NotifyChanged();
        }
    }
}
