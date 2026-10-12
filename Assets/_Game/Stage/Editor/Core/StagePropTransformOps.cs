using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景オブジェクトの置き方を変える操作（すべて Undo 対応）。シーンの配置ツールと「ステージ配置」パネルが使う。
    /// ・位置・向き … 根元（StageProp）を動かす。通れない範囲は根元の子なので一緒に付いてくる（作り直し不要）
    /// ・大きさ・高さ・傾き … 中のモデルだけを変える（根元の大きさは1のまま・根元は傾けない。すき間の余白などをメートルのまま保ち、
    ///                  2D の通れない範囲がずれないようにするため）。変えた後は通れない範囲を作り直す（<see cref="StageFootprintRefresher"/>）
    /// </summary>
    public static class StagePropTransformOps
    {
        public const float MinScaleFactor = 0.05f;

        /// <summary>大きさ・高さを変える対象（取り込んだモデル。無ければ Visual）。</summary>
        public static Transform Model(StageProp prop) =>
            prop.modelRoot != null ? prop.modelRoot : prop.visualRoot != null ? prop.visualRoot : prop.transform;

        /// <summary>見た目全体の箱（ワールド）。描画する物が無ければ根元の点。</summary>
        public static Bounds VisualBounds(StageProp prop) => prop.VisualBounds;

        /// <summary>
        /// 床からの高さ（見た目のいちばん下が床からどれだけ浮いているか。マイナスなら床に埋まっている）。
        /// 頂点で測る（傾けた物は見た目の箱が実際より下にはみ出すため）。
        /// </summary>
        public static float Lift(StageProp prop)
        {
            var bottom = StagePropShape.TryLowestZ(prop, out var z) ? z : VisualBounds(prop).max.z;
            return prop.transform.position.z - bottom;
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

        /// <summary>見た目のモデルの傾き（度。根元の向きが基準。X = X軸まわり、Y = Y軸まわり）。</summary>
        public static Vector2 Tilt(StageProp prop) => prop.modelTilt;

        /// <summary>傾ける支点: 根元の真下（真上）の、見た目のいちばん下の高さの点。</summary>
        public static Vector3 TiltPivot(StageProp prop) => prop.transform.position + Vector3.back * Lift(prop);

        /// <summary>今の傾きでの見た目の「上」の向き（ワールド）。</summary>
        public static Vector3 TiltUpWorld(StageProp prop) => prop.transform.rotation * StagePlaceMath.TiltUp(prop.modelTilt);

        /// <summary>
        /// 見た目のモデルを傾ける（根元は傾けない）。根元の真下の床の点を中心に回し、床からの高さは傾ける前のまま保つ
        /// （床に置いた物は床に置いたまま、斜めに立つ）。通れない範囲は作り直す。
        /// </summary>
        public static void SetTilt(StageProp prop, Vector2 tilt)
        {
            tilt = StagePlaceMath.WrapTilt(tilt);
            var old = prop.modelTilt;
            var model = Model(prop);
            if (model == prop.transform || (StagePlaceMath.WrapTilt(tilt - old)).sqrMagnitude < 1e-8f)
            {
                return;
            }

            var lift = Lift(prop);
            var pivot = ModelTiltPivot(prop, model);
            Undo.RecordObject(model, "傾ける");
            Undo.RecordObject(prop, "傾ける");
            var root = prop.transform.rotation;
            var delta = root * StagePlaceMath.TiltRotation(tilt) * Quaternion.Inverse(StagePlaceMath.TiltRotation(old)) * Quaternion.Inverse(root);
            model.SetPositionAndRotation(pivot + delta * (model.position - pivot), delta * model.rotation);
            prop.modelTilt = tilt;
            // 支点はモデルに付いた点として覚える（高さを直すとモデルと一緒に上下するので、傾け直しを重ねても足元が横にずれない）。
            prop.modelTiltPivot = model.InverseTransformPoint(pivot);
            SetLift(prop, lift);
            StageFootprintRefresher.Request(prop, 1f);
        }

        // 傾けている間は、覚えておいた支点（モデルに付いた点）を使う。まっすぐのとき・覚えた点が根元の真上から外れているとき
        // （モデルを差し替えた・手で動かした など）は、根元の真下の、見た目のいちばん下の点にする。
        private static Vector3 ModelTiltPivot(StageProp prop, Transform model)
        {
            if (prop.modelTilt.sqrMagnitude > 1e-6f)
            {
                var stored = model.TransformPoint(prop.modelTiltPivot);
                var root = prop.transform.position;
                if (new Vector2(stored.x - root.x, stored.y - root.y).sqrMagnitude < 1e-6f)
                {
                    return stored;
                }
            }

            return TiltPivot(prop);
        }

        /// <summary>
        /// 根元が X/Y に傾いているときに、見た目はそのままで根元だけまっすぐにした後、モデルの傾きの記録を今の見た目に合わせる。
        /// oldRoot はまっすぐにする前の根元の向き。
        /// </summary>
        public static void AdoptTilt(StageProp prop, Quaternion oldRoot)
        {
            var upWorld = oldRoot * StagePlaceMath.TiltUp(prop.modelTilt);
            Undo.RecordObject(prop, "根元をまっすぐにする");
            prop.modelTilt = StagePlaceMath.TiltFromUp(Quaternion.Inverse(prop.transform.rotation) * upWorld);
        }

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
