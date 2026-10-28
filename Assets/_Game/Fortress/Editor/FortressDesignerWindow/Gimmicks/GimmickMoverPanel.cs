using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>「仕掛け」タブの動く障害物の欄: 一覧・今ある障害物を動かせるようにする・止まる所の表。</summary>
    public sealed class GimmickMoverPanel
    {
        private SerializedObject _serialized;
        private bool _foldout = true;

        public void Draw(IReadOnlyList<GimmickMover> movers, GimmickMover selected)
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, $"動く障害物（{movers.Count}個）");
            if (_foldout)
            {
                EditorGUILayout.HelpBox(
                    "今ある障害物（壁・地帯・壊せる物）に「止まる所」を決めると、順に動きます。動くと敵の経路は自動で作り直されます。" +
                    "止まる所は、Scene ビューの番号つきの丸をドラッグしても動かせます。出発点は、プレビューしていないときに物を動かすと付いてきます。",
                    MessageType.None);

                foreach (var mover in movers)
                {
                    DrawRow(mover, mover == selected);
                }

                DrawMakeButton();

                if (selected != null)
                {
                    EditorGUILayout.Space(4);
                    DrawSelected(selected);
                }

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawRow(GimmickMover mover, bool selected)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var style = selected ? EditorStyles.boldLabel : EditorStyles.label;
                EditorGUILayout.LabelField(mover.name, style, GUILayout.Width(120));
                EditorGUILayout.LabelField(new GUIContent($"{Kind(mover)}・{mover.Describe()}", mover.memo), EditorStyles.miniLabel);
                if (GUILayout.Button("選ぶ", GUILayout.Width(40)))
                {
                    Selection.activeGameObject = mover.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

        private static string Kind(GimmickMover mover)
        {
            if (mover.GetComponent<DestructibleObstacle>() != null)
            {
                return "壊せる物";
            }

            var obstacle = mover.GetComponent<NavigationObstacle>();
            if (obstacle == null)
            {
                return "障害物";
            }

            return obstacle.mode == NavigationObstacleMode.Cost ? "地帯" : "壁";
        }

        private static void DrawMakeButton()
        {
            var targets = Selection.gameObjects.Where(g => g != null && g.scene.IsValid() && g.GetComponent<GimmickMover>() == null).ToArray();
            using (new EditorGUI.DisabledScope(Application.isPlaying || targets.Length == 0))
            {
                var label = targets.Length == 0
                    ? "（Scene で障害物を選ぶと）選んだ障害物を動かせるようにする"
                    : $"選んだ {targets.Length} 個を動かせるようにする";
                if (GUILayout.Button(new GUIContent(label, "今ある障害物に「動く障害物」を付けます。最初は右へ 3m 行ったり来たりする設定です。")))
                {
                    GimmickPreview.Stop();
                    var reasons = new List<string>();
                    var made = GimmickFactory.MakeMovers(targets, reasons);
                    if (reasons.Count > 0)
                    {
                        EditorUtility.DisplayDialog("動かせない物がありました", string.Join("\n", reasons), "OK");
                    }

                    if (made.Count > 0)
                    {
                        Selection.activeGameObject = made[0].gameObject;
                    }
                }
            }
        }

        private void DrawSelected(GimmickMover mover)
        {
            EditorGUILayout.LabelField($"選んだ動く障害物: {mover.name}", EditorStyles.boldLabel);
            if (_serialized == null || _serialized.targetObject != mover)
            {
                _serialized = new SerializedObject(mover);
            }

            _serialized.Update();
            EditorGUI.BeginChangeCheck();
            Field("loop", "回り終えた後");
            Field("firstMoveAt", "最初に動き出す時刻（秒）");
            if (EditorGUI.EndChangeCheck())
            {
                Apply(mover);
            }

            DrawStops(mover);

            EditorGUI.BeginChangeCheck();
            Field("onMoveStart", "動き出したときの演出");
            Field("onArrive", "着いたときの演出");
            Field("memo", "メモ");
            if (EditorGUI.EndChangeCheck())
            {
                Apply(mover);
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("動かすのをやめる", "「動く障害物」を外し、出発点に戻します（障害物そのものは残ります）。"), GUILayout.Width(130)))
                {
                    GimmickPreview.Stop();
                    Undo.RecordObject(mover.transform, "動かすのをやめる");
                    mover.ApplyAt(0f);
                    Undo.DestroyObjectImmediate(mover);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawStops(GimmickMover mover)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("止まる所（ずれは出発点からの m。右が +X、上が +Y）", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("", GUILayout.Width(64));
                GUILayout.Label("ずれ X", EditorStyles.miniLabel, GUILayout.Width(46));
                GUILayout.Label("ずれ Y", EditorStyles.miniLabel, GUILayout.Width(46));
                GUILayout.Label("回す°", EditorStyles.miniLabel, GUILayout.Width(46));
                GUILayout.Label(new GUIContent("動く秒", "前の所からここまで動く秒数（出発点の行は、最後から戻ってくる秒数）"), EditorStyles.miniLabel, GUILayout.Width(46));
                GUILayout.Label(new GUIContent("止まる秒", "ここで止まっている秒数"), EditorStyles.miniLabel, GUILayout.Width(50));
            }

            var stops = mover.stops;
            var removeAt = -1;
            var moveUp = -1;
            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i] ?? (stops[i] = new MoverStop());
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(i == 0 ? "出発点" : $"{Circled(i)}", GUILayout.Width(64));
                    EditorGUI.BeginChangeCheck();
                    float x, y, angle;
                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        x = EditorGUILayout.FloatField(stop.offset.x, GUILayout.Width(46));
                        y = EditorGUILayout.FloatField(stop.offset.y, GUILayout.Width(46));
                        angle = EditorGUILayout.FloatField(stop.angle, GUILayout.Width(46));
                    }

                    var move = EditorGUILayout.FloatField(stop.moveSeconds, GUILayout.Width(46));
                    var wait = EditorGUILayout.FloatField(stop.waitSeconds, GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(mover, "止まる所を変える");
                        if (i > 0)
                        {
                            stop.offset = new Vector2(x, y);
                            stop.angle = angle;
                        }

                        stop.moveSeconds = Mathf.Max(GimmickMath.MinSeconds, move);
                        stop.waitSeconds = Mathf.Max(0f, wait);
                        Changed(mover);
                    }

                    using (new EditorGUI.DisabledScope(i <= 1))
                    {
                        if (GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(22)))
                        {
                            moveUp = i;
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("消す", EditorStyles.miniButton, GUILayout.Width(36)))
                        {
                            removeAt = i;
                        }
                    }
                }
            }

            if (moveUp > 1)
            {
                Undo.RecordObject(mover, "止まる所の順を変える");
                (stops[moveUp - 1], stops[moveUp]) = (stops[moveUp], stops[moveUp - 1]);
                Changed(mover);
            }

            if (removeAt > 0)
            {
                Undo.RecordObject(mover, "止まる所を消す");
                stops.RemoveAt(removeAt);
                Changed(mover);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("＋ 止まる所を足す", "最後の所から右へ 2m の所に足します（Scene の丸をドラッグして直せます）。")))
                {
                    Undo.RecordObject(mover, "止まる所を足す");
                    var last = stops.Count > 0 ? stops[stops.Count - 1] : new MoverStop();
                    var added = last.Clone();
                    added.offset = (stops.Count > 1 ? last.offset : Vector2.zero) + new Vector2(2f, 0f);
                    stops.Add(added);
                    Changed(mover);
                }

                if (GUILayout.Button(new GUIContent("時間割で見る", "この物が動く時刻に、プレビューの秒数を合わせます。")) && !Application.isPlaying)
                {
                    GimmickPreview.SetTime(mover.firstMoveAt + 0.01f);
                }
            }

            var pose = mover.PoseAt(GimmickClock.Now);
            EditorGUILayout.LabelField(
                $"今（{GimmickClock.Now:0.0}秒）: {(pose.Moving ? "動いている" : "止まっている")}・ずれ ({pose.X:0.##}, {pose.Y:0.##})・{pose.Angle:0.#}°",
                EditorStyles.miniLabel);
        }

        internal static string Circled(int i)
        {
            const string digits = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳";
            return i >= 1 && i <= digits.Length ? digits[i - 1].ToString() : $"({i})";
        }

        private void Field(string path, string label)
        {
            var property = _serialized.FindProperty(path);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }

        private void Apply(GimmickMover mover)
        {
            _serialized.ApplyModifiedProperties();
            Changed(mover);
        }

        private static void Changed(GimmickMover mover)
        {
            EditorUtility.SetDirty(mover);
            mover.MarkChanged();
            if (GimmickPreview.Active)
            {
                GimmickPreview.SetTime(GimmickPreview.Time);
            }
        }
    }
}
