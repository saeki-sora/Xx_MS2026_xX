using UnityEditor;
using UnityEngine;

namespace MS2026.Title.EditorTools
{
    /// <summary>タイトル画面の部品の Inspector に共通で出す「試す」ボタン。</summary>
    internal static class TitleInspectorButtons
    {
        public static void Draw()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.LabelField("▶ Play 中は、ここに「最初から再生」「再生中に変えた値を残す」ボタンが出ます。", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var controller = Object.FindAnyObjectByType<TitleScreenController>();
                    using (new EditorGUI.DisabledScope(controller == null))
                    {
                        if (GUILayout.Button(new GUIContent("▶ 最初から再生", "タイトル画面を暗転からやり直す（値を変えたらこれで見直す）"), GUILayout.Height(24)))
                        {
                            controller.Restart();
                        }
                    }
                    if (GUILayout.Button(new GUIContent("再生中に変えた値を残す", "今の値を写し取り、再生を止めたときにシーンへ書き戻す（位置は対象外）"), GUILayout.Height(24)))
                    {
                        TitlePlayModeKeeper.Keep();
                    }
                }
                EditorGUILayout.LabelField(TitlePlayModeKeeper.HasPending
                        ? "✓ 値を写し取り済み。再生を止めるとシーンに書き戻します。"
                        : "再生中に変えた値は、「残す」を押さないと再生を止めたときに元に戻ります。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    [CustomEditor(typeof(TitleElementMotion))]
    internal sealed class TitleElementMotionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TitleInspectorButtons.Draw();
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(TitleOpening))]
    internal sealed class TitleOpeningEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TitleInspectorButtons.Draw();
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(TitleCameraMotion))]
    internal sealed class TitleCameraMotionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TitleInspectorButtons.Draw();
            DrawDefaultInspector();
        }
    }

    /// <summary>
    /// 進行役の Inspector。上に「試す」ボタンと、全部の配置物のタイミング・位置・大きさを1か所で変えられる一覧を出す。
    /// </summary>
    [CustomEditor(typeof(TitleScreenController))]
    internal sealed class TitleScreenControllerEditor : Editor
    {
        private static bool showOverview = true;

        public override void OnInspectorGUI()
        {
            TitleInspectorButtons.Draw();
            var controller = (TitleScreenController)target;

            showOverview = EditorGUILayout.BeginFoldoutHeaderGroup(showOverview, "流れの一覧（ここで全部まとめて調整できます）");
            if (showOverview) DrawOverview(controller);
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }

        private static void DrawOverview(TitleScreenController controller)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("① オープニング（歯磨き粉）", EditorStyles.boldLabel);
                if (controller.opening != null)
                {
                    DrawSelectButton(controller.opening);
                    DrawFields(controller.opening,
                        ("appearDelay", "出てくるまで（秒）"),
                        ("swellDuration", "つまんでふくらむ（秒）"),
                        ("swellAmount", "ふくらむ量"),
                        ("pinchAmount", "つまんだ所の細さ"),
                        ("pinchHeight", "つまむ高さ"),
                        ("zoomIn", "カメラが寄る量"),
                        ("rumble", "ブルブルの強さ"),
                        ("ejectDuration", "噴き出して伸びきる（秒）"),
                        ("ejectShake", "噴き出した揺れ"),
                        ("capLaunchSpeed", "キャップが飛ぶ速さ"),
                        ("capSpin", "キャップの回転"),
                        ("capGravity", "キャップの重力"),
                        ("curtainDelay", "噴き出して→垂れてくる（秒）"),
                        ("curtainDropDuration", "どろーっと覆いきる（秒）"),
                        ("curtainEase", "垂れ方の緩急"),
                        ("curtainUnevenness", "垂れ方のでこぼこ"),
                        ("curtainHold", "覆ったままの時間（秒）"),
                        ("revealStyle", "消え方"),
                        ("revealDuration", "消えきる（秒）"),
                        ("handOffDelay", "消え始めて→登場まで（秒）"));
                    DrawTransform(controller.opening.tube, "歯磨き粉");
                }
                else
                {
                    EditorGUILayout.LabelField("（オープニングなし）", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("② 背景（最初から）", EditorStyles.boldLabel);
                if (controller.background != null) DrawElement(controller.background);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("③ 幕が抜けた後に登場（秒は「オープニングの合図」から）", EditorStyles.boldLabel);
                if (controller.afterOpening != null)
                {
                    foreach (var element in controller.afterOpening)
                    {
                        if (element != null) DrawElement(element);
                    }
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("④ カメラ・スタート", EditorStyles.boldLabel);
                DrawFields(controller,
                    ("fadeInSeconds", "最初の明転（秒）"),
                    ("startZoom", "スタート時に寄る量"),
                    ("startShake", "スタート時の揺れ"),
                    ("startHoldSeconds", "スタート→暗転まで（秒）"),
                    ("fadeOutSeconds", "暗転にかける（秒）"));
                if (controller.cameraMotion != null)
                {
                    DrawFields(controller.cameraMotion,
                        ("shakeDuration", "揺れが収まるまで（秒）"),
                        ("shakeFrequency", "揺れの細かさ"),
                        ("rumbleFrequency", "ブルブルの細かさ"));
                    var cam = controller.cameraMotion.GetComponent<Camera>();
                    if (cam != null && cam.orthographic)
                    {
                        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                        {
                            EditorGUI.BeginChangeCheck();
                            var viewHeight = EditorGUILayout.FloatField(new GUIContent("画面に映る高さ", "大きいほど引いて広く映す（初期値 10.8）。再生中は寄りの演出が使うので変えられない。"),
                                cam.orthographicSize * 2f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(cam, "タイトルのカメラ");
                                cam.orthographicSize = Mathf.Max(0.5f, viewHeight * 0.5f);
                            }
                        }
                    }
                }
            }
        }

        private static void DrawElement(TitleElementMotion element)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSelectButton(element);
                DrawFields(element,
                    ("enterDelay", "登場を始める（秒）"),
                    ("enterDuration", "登場にかける（秒）"),
                    ("enterEase", "動きの緩急"),
                    ("moveFrom", "どこから来るか（ずれ）"),
                    ("rotateFrom", "回りながら来る（度）"),
                    ("glitchIn", "登場中のノイズ"),
                    ("shakeOnLand", "着地の揺れ"),
                    ("squashOnLand", "着地のむにっ"),
                    ("flashOnLand", "着地の白い光"));
                DrawTransform(element.transform, null);
            }
        }

        private static void DrawSelectButton(Component component)
        {
            if (GUILayout.Button(component.name, EditorStyles.linkLabel))
            {
                Selection.activeObject = component.gameObject;
                EditorGUIUtility.PingObject(component.gameObject);
            }
        }

        private static void DrawFields(Object target, params (string property, string label)[] fields)
        {
            var so = new SerializedObject(target);
            so.Update();
            foreach (var (property, label) in fields)
            {
                var prop = so.FindProperty(property);
                if (prop == null) continue;
                EditorGUILayout.PropertyField(prop, new GUIContent(label, prop.tooltip));
            }
            so.ApplyModifiedProperties();
        }

        // 位置と大きさ（再生中は動きが毎フレーム上書きするので、編集中だけ変えられる）。
        private static void DrawTransform(Transform t, string label)
        {
            if (t == null) return;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorGUI.BeginChangeCheck();
                var position = EditorGUILayout.Vector2Field(new GUIContent(label == null ? "位置" : label + "の位置", "画面の真ん中が (0, 0)。横 ±9.6、縦 ±5.4 が画面の端。"),
                    t.localPosition);
                var size = EditorGUILayout.FloatField(new GUIContent(label == null ? "大きさ（倍率）" : label + "の大きさ（倍率）", "1 = 今の画像の大きさ。"), t.localScale.x);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(t, "タイトルの位置・大きさ");
                    t.localPosition = new Vector3(position.x, position.y, t.localPosition.z);
                    t.localScale = Vector3.one * Mathf.Max(0.01f, size);
                }
            }
        }
    }
}
