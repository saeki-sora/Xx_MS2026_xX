using UnityEditor;
using UnityEngine;

namespace MS2026.CyberCleanLook.EditorTools
{
    /// <summary>
    /// WAVE クリアの「キラーン」（コアの位置で光る演出）だけを見て調整するための画面（エディタの中だけ。ゲームには入らない）。
    /// Play 中に、キラーンを何度でも出す・くり返す・スローにする・ゲームを止めて光だけを見る、ができる。
    /// 大きさや色は設定（Resources/CyberCleanSettings）をその場で書き換えるので、Play を止めても残る。
    /// </summary>
    public sealed class KiraPreviewWindow : EditorWindow
    {
        private const string SettingsPath = "Assets/CyberCleanLook/Resources/CyberCleanSettings.asset";

        private bool _repeat;
        private float _interval = 1.5f;
        private float _speed = 1f;
        private bool _frozen;
        private float _timeScaleBefore = 1f;
        private double _next;
        private CyberCleanSettings _settings;
        private SerializedObject _serialized;

        [MenuItem("Tools/電脳クリーニングの見た目/キラーンを見る（確認用）")]
        private static void Open()
        {
            var window = GetWindow<KiraPreviewWindow>("キラーンを見る");
            window.minSize = new Vector2(320f, 420f);
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += HandlePlayMode;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            ResetPreview();
        }

        // Play を止めるときは、速さとゲームの時間を元に戻す。
        private void HandlePlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                ResetPreview();
                _repeat = false;
                Repaint();
            }
        }

        private void ResetPreview()
        {
            CyberCleanScreenFx.KiraPreviewSpeed = 1f;
            _speed = 1f;
            if (_frozen)
            {
                _frozen = false;
                if (Application.isPlaying)
                {
                    Time.timeScale = _timeScaleBefore;
                }
            }
        }

        private void Tick()
        {
            if (!_repeat || !Application.isPlaying || !CyberCleanScreenFx.IsReady)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup >= _next)
            {
                _next = EditorApplication.timeSinceStartup + _interval;
                CyberCleanScreenFx.Test(1);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("WAVE クリアの「キラーン」だけを見る", EditorStyles.boldLabel);

            var ready = Application.isPlaying && CyberCleanScreenFx.IsReady;
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play してから使います。ステージ1で Play すると、ここから何度でもキラーンを出せます。下の大きさや色は、Play していなくても変えられます。", MessageType.Info);
            }
            else if (!CyberCleanScreenFx.IsReady)
            {
                EditorGUILayout.HelpBox("画面演出の係がいません。設定の「出すステージ」に入っているステージで Play してください（試合の開始演出が終わるまで待つこともあります）。", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("キラーンを出す", GUILayout.Height(34)))
                {
                    CyberCleanScreenFx.Test(1);
                }

                EditorGUILayout.Space(4);
                var repeat = EditorGUILayout.Toggle(new GUIContent("くり返し出す", "下の秒数ごとに、キラーンを自動で出し続ける。"), _repeat);
                if (repeat != _repeat)
                {
                    _repeat = repeat;
                    _next = EditorApplication.timeSinceStartup;
                }

                _interval = EditorGUILayout.Slider(new GUIContent("出す間隔（秒）"), _interval, 0.5f, 5f);

                var speed = EditorGUILayout.Slider(new GUIContent("速さ", "1 でそのまま。小さいほどゆっくり動く（スロー再生）。"), _speed, 0.1f, 1f);
                if (!Mathf.Approximately(speed, _speed))
                {
                    _speed = speed;
                    CyberCleanScreenFx.KiraPreviewSpeed = _speed;
                }

                var frozen = EditorGUILayout.Toggle(new GUIContent("ゲームを止める", "敵やビームの動きを止めて、光だけを見る（キラーンは止まらない）。Play を止めると元に戻る。"), _frozen);
                if (frozen != _frozen)
                {
                    _frozen = frozen;
                    if (_frozen)
                    {
                        _timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                        Time.timeScale = 0f;
                    }
                    else
                    {
                        Time.timeScale = _timeScaleBefore;
                    }
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("見た目（設定にそのまま残る）", EditorStyles.boldLabel);
            if (!EnsureSettings())
            {
                EditorGUILayout.HelpBox("設定がまだありません。下のボタンで作ると、ここで変えられるようになります。", MessageType.Info);
                if (GUILayout.Button("設定を作る"))
                {
                    EditorApplication.ExecuteMenuItem("Tools/電脳クリーニングの見た目/設定を開く（無ければ作る）");
                }

                return;
            }

            _serialized.Update();
            Field("kiraScale", "大きさ");
            Field("kiraStrength", "光の強さ");
            Field("kiraSeconds", "光っている秒数");
            Field("kiraColor", "光の色");
            Field("kiraAccentColor", "もう1つの色");
            Field("kiraSortingOrder", "描く順番（HUD は 100）");
            if (_serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_settings);
            }

            EditorGUILayout.HelpBox("描く順番だけは、次に Play したときから効きます。", MessageType.None);
        }

        private void Field(string property, string label)
        {
            var prop = _serialized.FindProperty(property);
            if (prop != null)
            {
                EditorGUILayout.PropertyField(prop, new GUIContent(label, prop.tooltip));
            }
        }

        private bool EnsureSettings()
        {
            if (_settings == null)
            {
                _settings = AssetDatabase.LoadAssetAtPath<CyberCleanSettings>(SettingsPath);
                _serialized = _settings != null ? new SerializedObject(_settings) : null;
            }

            return _settings != null;
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}
