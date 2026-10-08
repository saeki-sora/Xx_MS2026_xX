using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景オブジェクトの置き方を変える操作（すべて Undo 対応）。シーンの配置ツールと「ステージ配置」パネルが使う。
    /// ・位置・向き … 根元（StageProp）を動かす。通れない範囲は根元の子なので一緒に付いてくる（作り直し不要）
    /// ・大きさ・高さ … 中のモデルだけを変える（根元の大きさは1のまま。すき間の余白などをメートルのまま保つため）。
    ///                  変えた後は通れない範囲を作り直す（<see cref="StageFootprintRefresher"/>）
    /// </summary>
    public static class StagePropTransformOps
    {
        public const float MinScaleFactor = 0.05f;

        /// <summary>大きさ・高さを変える対象（取り込んだモデル。無ければ Visual）。</summary>
        public static Transform Model(StageProp prop) =>
            prop.modelRoot != null ? prop.modelRoot : prop.visualRoot != null ? prop.visualRoot : prop.transform;

        /// <summary>見た目全体の箱（ワールド）。描画する物が無ければ根元の点。</summary>
        public static Bounds VisualBounds(StageProp prop) => prop.VisualBounds;

        /// <summary>床からの高さ（見た目のいちばん下が床からどれだけ浮いているか。マイナスなら床に埋まっている）。</summary>
        public static float Lift(StageProp prop)
        {
            var bounds = VisualBounds(prop);
            return prop.transform.position.z - bounds.max.z;
        }

        /// <summary>見た目の大きさ（幅・奥行き・高さ、メートル）。</summary>
        public static Vector3 Size(StageProp prop) => VisualBounds(prop).size;

        public static void RecordMove(IReadOnlyList<StageProp> props, string label)
        {
            foreach (var prop in props)
            {
                Undo.RecordObject(prop.transform, label);
            }
        }

        public static void Move(StageProp prop, Vector2 worldDelta)
        {
            var t = prop.transform;
            t.position += new Vector3(worldDelta.x, worldDelta.y, 0f);
        }

        /// <summary>根元を中心に、床の上で回す（Z回転だけ）。</summary>
        public static void SetYaw(StageProp prop, float degrees)
        {
            var t = prop.transform;
            t.rotation = Quaternion.Euler(0f, 0f, degrees);
        }

        public static float Yaw(StageProp prop) => prop.transform.eulerAngles.z;

        /// <summary>モデルを根元（足元）を中心に factor 倍にする。高さ（浮き）も同じ倍率で変わる。</summary>
        public static void ScaleModel(StageProp prop, float factor)
        {
            factor = Mathf.Max(MinScaleFactor, factor);
            var model = Model(prop);
            Undo.RecordObject(model, "大きさを変える");
            var pivot = prop.transform.position;
            model.position = pivot + (model.position - pivot) * factor;
            model.localScale *= factor;
            StageFootprintRefresher.Request(prop, factor);
        }

        /// <summary>モデルを上下に動かして、床からの高さを lift にする。</summary>
        public static void SetLift(StageProp prop, float lift)
        {
            var delta = lift - Lift(prop);
            if (Mathf.Abs(delta) < 1e-5f)
            {
                return;
            }

            var model = Model(prop);
            Undo.RecordObject(model, "高さを変える");
            model.position += Vector3.back * delta; // 床から上は −Z
            StageFootprintRefresher.Request(prop, 1f);
        }

        /// <summary>動かし終えたときの後始末（敵の通り道の作り直しを知らせ、シーンを変更ありにする）。</summary>
        public static void Commit()
        {
            NavigationObstacle.NotifyChanged();
            StageSceneService.MarkSceneDirty();
        }
    }

    /// <summary>
    /// 大きさ・高さを変えたときの「通れない範囲」の作り直しを、まとめて少し遅らせて行う（ドラッグ中に毎フレーム作り直さない）。
    /// 手で直した形（manuallyEdited）は作り直さず、大きさの倍率だけ形に掛ける。
    /// </summary>
    [InitializeOnLoad]
    public static class StageFootprintRefresher
    {
        private const double Delay = 0.15;

        private static readonly HashSet<StageProp> Pending = new HashSet<StageProp>();
        private static double _due;

        static StageFootprintRefresher()
        {
            EditorApplication.update += Update;
        }

        public static void Request(StageProp prop, float scaleFactor)
        {
            if (prop == null || prop.footprint == null)
            {
                return;
            }

            if (prop.footprint.manuallyEdited)
            {
                if (!Mathf.Approximately(scaleFactor, 1f))
                {
                    ScaleManualShape(prop, scaleFactor);
                }

                return;
            }

            Pending.Add(prop);
            _due = EditorApplication.timeSinceStartup + Delay;
        }

        /// <summary>待っている作り直しをすぐ行う（ドラッグを離したとき）。</summary>
        public static void Flush()
        {
            if (Pending.Count == 0)
            {
                return;
            }

            var props = new List<StageProp>(Pending);
            Pending.Clear();
            foreach (var prop in props)
            {
                if (prop != null)
                {
                    StagePropComposer.RebuildFootprint(prop);
                }
            }

            SceneView.RepaintAll();
        }

        private static void Update()
        {
            if (Pending.Count > 0 && EditorApplication.timeSinceStartup >= _due)
            {
                Flush();
            }
        }

        private static void ScaleManualShape(StageProp prop, float factor)
        {
            var collider = prop.footprint.Collider;
            var pivot = (Vector2)collider.transform.InverseTransformPoint(prop.transform.position);
            Undo.RecordObject(collider, "大きさを変える");
            for (var p = 0; p < collider.pathCount; p++)
            {
                var path = collider.GetPath(p);
                for (var i = 0; i < path.Length; i++)
                {
                    path[i] = pivot + (path[i] - pivot) * factor;
                }

                collider.SetPath(p, path);
            }

            NavigationObstacle.NotifyChanged();
        }
    }
}
