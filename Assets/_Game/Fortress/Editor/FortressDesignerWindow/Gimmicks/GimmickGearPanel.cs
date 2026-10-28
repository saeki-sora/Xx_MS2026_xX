using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>「仕掛け」タブの歯車の欄: 一覧・置く・選んだ歯車の回り方と腕（壁）。</summary>
    public sealed class GimmickGearPanel
    {
        private static readonly (string path, string label)[] StepFields =
        {
            ("clockwise", "時計回り（右回り）"),
            ("firstTurnAt", "最初に回り始める時刻（秒）"),
            ("stepDegrees", "1回に回る角度（度）"),
            ("turnSeconds", "1回回るのにかける秒数"),
            ("waitSeconds", "止まっている秒数")
        };

        private static readonly (string path, string label)[] ContinuousFields =
        {
            ("clockwise", "時計回り（右回り）"),
            ("firstTurnAt", "回り始める時刻（秒）"),
            ("degreesPerSecond", "回る速さ（度/秒）")
        };

        private static readonly (string path, string label)[] FollowFields =
        {
            ("driver", "かみ合う相手の歯車")
        };

        private static readonly (string path, string label)[] ShapeFields =
        {
            ("teeth", "歯の数（かみ合わせの比）"),
            ("radius", "半径（m・表示の目安）")
        };

        private static readonly (string path, string label)[] EffectFields =
        {
            ("onTurnStart", "回り始めたときの演出"),
            ("onTurnEnd", "止まったときの演出"),
            ("memo", "メモ")
        };

        private readonly List<GimmickGearArm> _arms = new List<GimmickGearArm>();
        private SerializedObject _serialized;
        private bool _foldout = true;
        private GameObject _model;
        private bool _modelMayaUp = true;

        public void Draw(IReadOnlyList<GimmickGear> gears, GimmickGear selected)
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, $"歯車（{gears.Count}個）");
            if (_foldout)
            {
                EditorGUILayout.HelpBox(
                    "歯車は「回る部分（Rotor）」を回します。回る部分の中の腕（壁）が一緒に回るので、回るたびに敵の通り道が開いたり閉じたりします。" +
                    "位置と最初の向きは、歯車（根元）を Scene で動かす・回すだけ。腕の向きを横にすると「通れる」、縦にすると「ふさぐ」のように使います。",
                    MessageType.None);

                foreach (var gear in gears)
                {
                    DrawRow(gear, gear == selected);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(Application.isPlaying))
                    {
                        if (GUILayout.Button(new GUIContent("＋ 歯車を置く", "Scene ビューの真ん中に、棒の腕 1 本つきの歯車を置きます。")))
                        {
                            GimmickPreview.Stop();
                            var pivot = SceneView.lastActiveSceneView != null ? (Vector2)SceneView.lastActiveSceneView.pivot : Vector2.zero;
                            Selection.activeGameObject = GimmickFactory.CreateGear(pivot).gameObject;
                        }
                    }
                }

                if (selected != null)
                {
                    EditorGUILayout.Space(4);
                    DrawSelected(selected, gears);
                }
                else if (gears.Count > 0)
                {
                    EditorGUILayout.LabelField("歯車を選ぶと、ここで回り方と腕を変えられます。", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawRow(GimmickGear gear, bool selected)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var style = selected ? EditorStyles.boldLabel : EditorStyles.label;
                EditorGUILayout.LabelField(gear.name, style, GUILayout.Width(120));
                var arms = gear.Rotor != null ? gear.Rotor.GetComponentsInChildren<GimmickGearArm>(true).Length : 0;
                EditorGUILayout.LabelField(new GUIContent($"{gear.Describe()}・腕 {arms}", gear.memo), EditorStyles.miniLabel);
                if (GUILayout.Button("選ぶ", GUILayout.Width(40)))
                {
                    Selection.activeGameObject = gear.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

        private void DrawSelected(GimmickGear gear, IReadOnlyList<GimmickGear> all)
        {
            EditorGUILayout.LabelField($"選んだ歯車: {gear.name}", EditorStyles.boldLabel);
            if (_serialized == null || _serialized.targetObject != gear)
            {
                _serialized = new SerializedObject(gear);
            }

            _serialized.Update();
            EditorGUI.BeginChangeCheck();
            var motion = _serialized.FindProperty("motion");
            EditorGUILayout.PropertyField(motion, new GUIContent("回り方", motion.tooltip));
            var fields = (GearMotion)motion.enumValueIndex switch
            {
                GearMotion.Step => StepFields,
                GearMotion.Continuous => ContinuousFields,
                GearMotion.Follow => FollowFields,
                _ => System.Array.Empty<(string, string)>()
            };
            DrawFields(fields);
            DrawFields(ShapeFields);
            if (EditorGUI.EndChangeCheck())
            {
                _serialized.ApplyModifiedProperties();
                Repreview();
            }

            if (gear.motion == GearMotion.Follow && gear.driver == null)
            {
                EditorGUILayout.HelpBox("かみ合う相手の歯車を入れてください。", MessageType.Warning);
            }

            if (gear.motion == GearMotion.Step && Mathf.Abs(gear.stepDegrees) > 0f && Mathf.Abs(gear.stepDegrees) % 90f > 0.01f && gear.AffectsNavigation)
            {
                EditorGUILayout.HelpBox("90° の倍数でない角度だと、腕が斜めで止まります（斜めの壁でも経路は作れますが、すき間ができやすくなります）。", MessageType.None);
            }

            DrawArms(gear);
            DrawLooks(gear);

            EditorGUI.BeginChangeCheck();
            DrawFields(EffectFields);
            if (EditorGUI.EndChangeCheck())
            {
                _serialized.ApplyModifiedProperties();
            }

            DrawMoveInto(gear);
        }

        private void DrawFields((string path, string label)[] fields)
        {
            foreach (var (path, label) in fields)
            {
                var property = _serialized.FindProperty(path);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
                }
            }
        }

        private void DrawArms(GimmickGear gear)
        {
            gear.CollectArms(_arms);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"腕（壁）{_arms.Count}本 — 敵が通れない所。一緒に回ります", EditorStyles.miniBoldLabel);
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("腕の形は Play していないときに変えてください（Play 中の変更は戻ります）。", MessageType.None);
            }

            GimmickGearArm remove = null;
            foreach (var arm in _arms)
            {
                if (arm == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GimmickFactory.GetArmPose(arm, out var angle, out var distance);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.LabelField(arm.name, GUILayout.Width(52));
                    angle = Labeled("向き", angle, 28);
                    distance = Labeled("中心から", distance, 46);
                    var length = Labeled("長さ", arm.Length, 28);
                    var thickness = Labeled("太さ", arm.Thickness, 28);
                    var pass = GUILayout.Toggle(arm.laserPassesThrough, new GUIContent("ビーム通す", "ON: レーザーは通り抜ける（敵は通れない）。OFF: レーザーも止める。"), GUILayout.Width(76));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObjects(new Object[] { arm.transform, arm, arm.GetComponent<BoxCollider2D>() }, "歯車の腕を変える");
                        GimmickFactory.SetArmPose(arm, angle, distance);
                        arm.laserPassesThrough = pass;
                        arm.SetSize(length, thickness);
                        NavigationObstacle.NotifyChanged();
                    }

                    if (GUILayout.Button("選ぶ", GUILayout.Width(36)))
                    {
                        Selection.activeGameObject = arm.gameObject;
                    }

                    if (GUILayout.Button("消す", GUILayout.Width(36)))
                    {
                        remove = arm;
                    }
                }
            }

            if (remove != null)
            {
                Undo.DestroyObjectImmediate(remove.gameObject);
                NavigationObstacle.NotifyChanged();
                GUIUtility.ExitGUI();
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("＋ 棒の腕（真ん中を通る）", "歯車の直径より少し長い棒。90° 回るたびに「通れる ↔ ふさぐ」が入れ替わります。")))
                {
                    var angle = _arms.Count == 0 ? 0f : 90f;
                    Selection.activeGameObject = GimmickFactory.AddArm(gear, angle, 0f, gear.radius * 2f + 0.4f, 0.7f).gameObject;
                    Repreview();
                }

                if (GUILayout.Button(new GUIContent("＋ 片側の腕", "真ん中から外へ伸びる腕（時計の針のような形）。")))
                {
                    var angle = 90f * _arms.Count;
                    Selection.activeGameObject = GimmickFactory.AddArm(gear, angle, gear.radius * 0.5f + 0.1f, gear.radius + 0.2f, 0.7f).gameObject;
                    Repreview();
                }
            }
        }

        private static float Labeled(string label, float value, float labelWidth)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            return EditorGUILayout.FloatField(value, GUILayout.Width(42));
        }

        private void DrawLooks(GimmickGear gear)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("見た目", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var rotor = gear.Rotor;
                    var hasDisc = rotor != null && rotor.Find(GimmickFactory.DiscName) != null;
                    var disc = EditorGUILayout.ToggleLeft(new GUIContent("仮の円盤", "回る部分に、歯車の大きさの仮の円を出します（3D モデルを付けたら消してよい）。"), hasDisc, GUILayout.Width(90));
                    if (disc != hasDisc)
                    {
                        GimmickFactory.SetDiscPlaceholder(gear, disc);
                    }

                    var anyPlaceholder = _arms.Any(a => a != null && a.GetComponent<PlaceholderVisual>() != null);
                    var armLook = EditorGUILayout.ToggleLeft(new GUIContent("腕の仮の四角", "腕に仮の四角を出します（3D モデルを付けたら消してよい）。"), anyPlaceholder, GUILayout.Width(110));
                    if (armLook != anyPlaceholder)
                    {
                        foreach (var arm in _arms.Where(a => a != null))
                        {
                            GimmickFactory.SetArmPlaceholder(arm, armLook);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _model = (GameObject)EditorGUILayout.ObjectField(new GUIContent("3D モデルを付ける", "FBX・Prefab を回る部分の中に置きます（一緒に回る）。"), _model, typeof(GameObject), false);
                    _modelMayaUp = GUILayout.Toggle(_modelMayaUp, new GUIContent("Maya の向き", "Y が上のモデル（Maya など）を床に寝かせます。"), GUILayout.Width(80));
                    using (new EditorGUI.DisabledScope(_model == null))
                    {
                        if (GUILayout.Button("付ける", GUILayout.Width(50)))
                        {
                            Selection.activeGameObject = GimmickFactory.AttachModel(gear, _model, _modelMayaUp);
                        }
                    }
                }
            }
        }

        private static void DrawMoveInto(GimmickGear gear)
        {
            var others = Selection.gameObjects.Where(g => g != null && !g.transform.IsChildOf(gear.transform) && g.scene.IsValid()).ToArray();
            if (others.Length == 0 || Application.isPlaying)
            {
                EditorGUILayout.LabelField("今ある壁・壊せる物を一緒に回したいときは、歯車とその物を一緒に選んでください（ボタンが出ます）。", EditorStyles.miniLabel);
                return;
            }

            if (GUILayout.Button(new GUIContent($"選んだ {others.Length} 個を、この歯車と一緒に回す", "選んだ物を歯車の回る部分の中に入れます（位置はそのまま）。")))
            {
                var count = GimmickFactory.MoveIntoRotor(gear, others);
                Debug.Log($"[仕掛け] {count} 個を {gear.name} の回る部分に入れました。");
                Repreview();
            }
        }

        private static void Repreview()
        {
            if (GimmickPreview.Active)
            {
                GimmickPreview.SetTime(GimmickPreview.Time);
            }
        }
    }
}
