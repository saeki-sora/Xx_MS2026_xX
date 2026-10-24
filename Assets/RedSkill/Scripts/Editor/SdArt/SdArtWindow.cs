using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// メニュー「Tools/赤スキル/SDの絵の見やすさを調整」。砲台の SD の絵を、ゲームの小さな表示でも見やすくする処理
    /// （<see cref="SdArtProcessor"/>：線を太く・色・紺のふち・白いふち・影）の値を、プレビューを見ながら変えて、ゲーム用の絵を書き出す（2026-10-24）。
    /// プレビューは「元の絵」と「見やすくした絵」を、ゲームと同じくらいの小ささで、明るい・中くらい・暗い床に置いて並べる。
    /// 値は Assets/RedSkill/Data/SdArtSettings.asset に保存される（チームで共有）。書き出すまでゲームの絵は変わらない。
    /// </summary>
    internal sealed class SdArtWindow : EditorWindow
    {
        private const float RecomputeDelay = 0.25f;
        private const int Zoom = 2;

        private static readonly (string label, Color color)[] Floors =
        {
            ("明るい床", new Color(205 / 255f, 215 / 255f, 220 / 255f)),
            ("中くらいの床", new Color(130 / 255f, 150 / 255f, 160 / 255f)),
            ("暗い床", new Color(55 / 255f, 62 / 255f, 75 / 255f)),
        };

        private SdArtSettings _settings;
        private SerializedObject _serialized;
        private string[] _names = new string[0];
        private int _selected;
        private SdImage _source;
        private string _sourceName;
        private readonly List<Texture2D> _before = new List<Texture2D>();
        private readonly List<Texture2D> _after = new List<Texture2D>();
        private Texture2D _large;
        private bool _showLarge;
        private double _recomputeAt = -1;
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private string _status = "";

        [MenuItem("Tools/赤スキル/SDの絵の見やすさを調整")]
        private static void Open()
        {
            var window = GetWindow<SdArtWindow>("SD の絵の見やすさ");
            window.minSize = new Vector2(760, 520);
        }

        private void OnEnable()
        {
            _settings = SdArtExporter.LoadOrCreateSettings();
            _serialized = new SerializedObject(_settings);
            _names = SdArtExporter.SourceNames();
            _selected = Mathf.Clamp(System.Array.IndexOf(_names, "red_sd.png"), 0, Mathf.Max(0, _names.Length - 1));
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += RequestRecompute;
            RequestRecompute();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= RequestRecompute;
            ClearPreviews();
        }

        private void RequestRecompute()
        {
            _recomputeAt = EditorApplication.timeSinceStartup + RecomputeDelay;
            Repaint();
        }

        // 値を動かしている間は待って、手が止まってから作り直す（1枚 0.3 秒ほどかかるため）。
        private void Tick()
        {
            if (_recomputeAt < 0 || EditorApplication.timeSinceStartup < _recomputeAt)
            {
                return;
            }

            _recomputeAt = -1;
            Recompute();
            Repaint();
        }

        private void Recompute()
        {
            ClearPreviews();
            if (_names.Length == 0 || _settings == null)
            {
                return;
            }

            var name = _names[Mathf.Clamp(_selected, 0, _names.Length - 1)];
            if (_source == null || _sourceName != name)
            {
                _source = SdArtExporter.LoadSource(name);
                _sourceName = name;
            }

            if (_source == null)
            {
                _status = $"{name} を読めませんでした。";
                return;
            }

            var parameters = _settings.ToParams();
            var processed = SdArtProcessor.Process(_source, parameters);
            var charHeight = Mathf.Max(20, _settings.previewHeight);
            var beforeSmall = Shrink(_source, charHeight);
            var afterSmall = Shrink(processed, Mathf.RoundToInt(charHeight * SdArtProcessor.CanvasScale(parameters)));
            foreach (var (_, color) in Floors)
            {
                _before.Add(SdArtExporter.ToTexture(SdArtExporter.OnFloor(beforeSmall, color, 6), FilterMode.Point));
                _after.Add(SdArtExporter.ToTexture(SdArtExporter.OnFloor(afterSmall, color, 6), FilterMode.Point));
            }

            _large = SdArtExporter.ToTexture(processed, FilterMode.Bilinear);
            _status = $"{name}: 書き出す絵 {processed.Width}×{processed.Height}（キャラ部分 {_settings.outHeight}）";
        }

        private static SdImage Shrink(SdImage image, int height)
        {
            var width = Mathf.Max(1, Mathf.RoundToInt(image.Width * (float)height / image.Height));
            return SdArtProcessor.Resize(image, width, Mathf.Max(1, height));
        }

        private void ClearPreviews()
        {
            foreach (var t in _before) DestroyImmediate(t);
            foreach (var t in _after) DestroyImmediate(t);
            _before.Clear();
            _after.Clear();
            if (_large != null) DestroyImmediate(_large);
            _large = null;
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox($"設定 {SdArtExporter.SettingsPath} を作れませんでした。", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                "砲台の SD の絵は、ゲームでは元の絵の約10分の1の小ささで表示されます。ここで、小さくても見やすくする処理（線を太く・色・ふち・影）を調整します。\n" +
                "右のプレビューで「元の絵」と「見やすくした絵」を、ゲームと同じくらいの小ささで比べられます。決まったら「ゲーム用の絵を書き出す」を押すと、4人分の絵が作り直されてゲームに入ります（押すまでゲームの絵は変わりません）。",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSettings();
                DrawPreview();
            }
        }

        private void DrawSettings()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(360)))
            {
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                _serialized.Update();
                EditorGUI.BeginChangeCheck();
                Group("線を太くする（紺色の線だけ）");
                Field("lineGrow", "線を太くする幅");
                Field("lineMaxLum", "線とみなす暗さ");
                Field("lineBlueLead", "線とみなす青さ");
                Field("fillSize", "これより太い濃い青は塗り（太くしない）");
                Field("lineSoft", "太くした線のぼかし");
                Group("色");
                Field("saturation", "鮮やかさ");
                Field("contrast", "明暗の差");
                Group("ふち（ステッカー風）");
                Field("rim", "紺のふち");
                Field("stickerWhite", "白いふち");
                Field("stickerLine", "その外の細い紺の線");
                Group("影");
                Field("shadowBlur", "影のぼかし（0で影なし）");
                Field("shadowGrow", "影の広がり");
                Field("shadowAlpha", "影の濃さ");
                Group("書き出す絵の大きさ");
                Field("outHeight", "キャラ部分の高さ（px）");
                Field("pad", "ふち・影のための余白（px）");
                Group("プレビュー");
                Field("previewHeight", "キャラを何pxで見るか");
                if (EditorGUI.EndChangeCheck())
                {
                    _serialized.ApplyModifiedProperties();
                    RequestRecompute();
                }

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("最初の値に戻す"))
                    {
                        Undo.RecordObject(_settings, "SD の絵の値を最初に戻す");
                        _settings.ResetValues();
                        EditorUtility.SetDirty(_settings);
                        _serialized.Update();
                        RequestRecompute();
                    }

                    if (GUILayout.Button("元の絵のフォルダを開く"))
                    {
                        EditorUtility.RevealInFinder(SdArtExporter.SourceFolder);
                    }
                }

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(_names.Length == 0))
                {
                    if (GUILayout.Button($"ゲーム用の絵を書き出す（{_names.Length} 枚）", GUILayout.Height(32)))
                    {
                        AssetDatabase.SaveAssets();
                        var written = SdArtExporter.ExportAll(_settings);
                        _status = $"{written.Count} 枚を書き出しました。";
                        _serialized.Update();
                    }
                }

                EditorGUILayout.LabelField("余白を変えて書き出すと、砲台の絵の一覧の「絵の高さ」も自動で直します（キャラ自体の大きさは変わりません）。", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPreview()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("見る絵", GUILayout.Width(48));
                    EditorGUI.BeginChangeCheck();
                    _selected = EditorGUILayout.Popup(_selected, _names);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RequestRecompute();
                    }

                    if (GUILayout.Button("元の絵を読み直す", GUILayout.Width(120)))
                    {
                        _names = SdArtExporter.SourceNames();
                        _source = null;
                        RequestRecompute();
                    }

                    _showLarge = GUILayout.Toggle(_showLarge, "大きく見る", "Button", GUILayout.Width(80));
                }

                EditorGUILayout.LabelField(_recomputeAt >= 0 ? "作り直しています…" : _status, EditorStyles.miniLabel);
                if (_names.Length == 0)
                {
                    EditorGUILayout.HelpBox($"元の絵がありません（{SdArtExporter.SourceFolder} に PNG を入れてください）。", MessageType.Warning);
                    return;
                }

                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                if (_showLarge && _large != null)
                {
                    var height = Mathf.Min(position.height - 160, _large.height);
                    var rect = GUILayoutUtility.GetRect(_large.width * height / _large.height, height, GUILayout.ExpandWidth(false));
                    EditorGUI.DrawTextureTransparent(rect, _large, ScaleMode.ScaleToFit);
                }
                else
                {
                    for (var i = 0; i < _before.Count; i++)
                    {
                        EditorGUILayout.LabelField($"{Floors[i].label}　　左: 元の絵　右: 見やすくした絵（ゲームと同じくらいの小ささを {Zoom} 倍で表示）", EditorStyles.miniBoldLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            DrawPixelTexture(_before[i]);
                            GUILayout.Space(8);
                            DrawPixelTexture(_after[i]);
                            GUILayout.FlexibleSpace();
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawPixelTexture(Texture2D texture)
        {
            if (texture == null) return;
            var rect = GUILayoutUtility.GetRect(texture.width * Zoom, texture.height * Zoom, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        private static void Group(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Field(string property, string label)
        {
            var p = _serialized.FindProperty(property);
            if (p != null)
            {
                EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip));
            }
        }
    }
}
