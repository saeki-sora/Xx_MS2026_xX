using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MS2026.SpriteAnim.Editor
{
    /// <summary>
    /// スプライトシートのスライスからアニメーション作成・プレビュー・遷移テスト・コード生成までを
    /// 1つのウィンドウで完結させるツール本体。
    /// 想定フロー: ① シートをドラッグ&ドロップしてスライス → ② セットを作成しアニメーションを組む
    ///            → ③ プレビューで確認 → ④ 遷移をテスト → ⑤ 必要ならコードを生成。
    /// </summary>
    public class SpriteAnimStudioWindow : EditorWindow
    {
        [MenuItem("MS2026/スプライトアニメ/スプライトアニメスタジオ")]
        public static void Open()
        {
            var win = GetWindow<SpriteAnimStudioWindow>("スプライトアニメスタジオ");
            win.minSize = new Vector2(760, 560);
        }

        public static void OpenWithSet(SpriteAnimationSet set)
        {
            var win = GetWindow<SpriteAnimStudioWindow>("スプライトアニメスタジオ");
            win.minSize = new Vector2(760, 560);
            win._set = set;
            win._selectedAnimIndex = set != null && set.EditorAnimationsList.Count > 0 ? 0 : -1;
            win.RebuildAnimList();
            win.RebuildFrameList();
        }

        // --- スライス設定 ---
        private Texture2D _sourceTexture;
        private int _columns = 4, _rows = 1;
        private int _paddingX, _paddingY, _marginX, _marginY;
        private float _pixelsPerUnit = 100f;
        private Sprite[] _slicedSprites = Array.Empty<Sprite>();
        private readonly HashSet<int> _selectedSliceIndices = new HashSet<int>();

        // --- セット／アニメーション ---
        private SpriteAnimationSet _set;
        private ReorderableList _animList;
        private int _selectedAnimIndex = -1;
        private ReorderableList _frameList;
        private float _bulkFps = 12f;

        // --- プレビュー再生 ---
        private bool _isPreviewPlaying;
        private int _previewFrameIndex;
        private bool _previewForward = true;
        private float _previewTimer;
        private double _lastEditorTime;
        private bool _onionSkin;
        private float _previewSpeed = 1f;

        // --- 遷移テスト ---
        private int _fromIndex, _toIndex;
        private float _testCrossFade = 0.15f;
        private bool _isTestBlending;
        private float _testBlendTimer;
        private SpriteAnimation _blendFromAnim, _blendToAnim;

        private Vector2 _scroll;

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            RebuildAnimList();
            RebuildFrameList();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastEditorTime);
            _lastEditorTime = now;
            bool needsRepaint = false;

            var anim = GetSelectedAnimation();
            if (_isPreviewPlaying && anim != null && anim.FrameCount > 0)
            {
                _previewTimer += dt * _previewSpeed;
                float dur = Mathf.Max(0.0001f, anim.Frames[_previewFrameIndex].duration);
                int safety = 0;
                while (_previewTimer >= dur && safety++ < 1000)
                {
                    _previewTimer -= dur;
                    var step = SpriteAnimationPlayback.Advance(anim.LoopMode, anim.FrameCount, _previewFrameIndex, _previewForward);
                    _previewFrameIndex = step.frameIndex;
                    _previewForward = step.forward;
                    if (step.finished) { _isPreviewPlaying = false; break; }
                    dur = Mathf.Max(0.0001f, anim.Frames[_previewFrameIndex].duration);
                }
                needsRepaint = true;
            }

            if (_isTestBlending)
            {
                _testBlendTimer += dt;
                if (_testBlendTimer >= _testCrossFade) _isTestBlending = false;
                needsRepaint = true;
            }

            if (needsRepaint) Repaint();
        }

        private SpriteAnimation GetSelectedAnimation()
        {
            if (_set == null) return null;
            var list = _set.EditorAnimationsList;
            if (_selectedAnimIndex < 0 || _selectedAnimIndex >= list.Count) return null;
            return list[_selectedAnimIndex];
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSlicingSection();
            EditorGUILayout.Space();
            DrawSetSection();
            EditorGUILayout.Space();
            DrawPreviewSection();
            EditorGUILayout.Space();
            DrawTransitionSection();
            EditorGUILayout.Space();
            DrawToolsSection();
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------
        // 1. スライス
        // ---------------------------------------------------------------
        private void DrawSlicingSection()
        {
            EditorGUILayout.LabelField("① スプライトシートを読み込んでスライス", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            _sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", _sourceTexture, typeof(Texture2D), false);

            EditorGUILayout.BeginHorizontal();
            _columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", _columns));
            _rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", _rows));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _paddingX = Mathf.Max(0, EditorGUILayout.IntField("Padding X", _paddingX));
            _paddingY = Mathf.Max(0, EditorGUILayout.IntField("Padding Y", _paddingY));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _marginX = Mathf.Max(0, EditorGUILayout.IntField("Margin X", _marginX));
            _marginY = Mathf.Max(0, EditorGUILayout.IntField("Margin Y", _marginY));
            EditorGUILayout.EndHorizontal();

            _pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", _pixelsPerUnit);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_sourceTexture == null))
            {
                if (GUILayout.Button("スライス実行"))
                {
                    _slicedSprites = SpriteSheetSlicer.SliceGrid(_sourceTexture, _columns, _rows, _paddingX, _paddingY, _marginX, _marginY, null, _pixelsPerUnit);
                    _selectedSliceIndices.Clear();
                }
            }

            if (GUILayout.Button("テスト用シートを生成 (実素材が無くても動作確認できます)"))
            {
                string folder = "Assets";
                if (_sourceTexture != null)
                {
                    string existingPath = AssetDatabase.GetAssetPath(_sourceTexture);
                    if (!string.IsNullOrEmpty(existingPath)) folder = Path.GetDirectoryName(existingPath);
                }

                string path = TestSheetGenerator.CreateTestSpriteSheet(folder, _columns, _rows, 64);
                _sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                _slicedSprites = SpriteSheetSlicer.SliceGrid(_sourceTexture, _columns, _rows, 0, 0, 0, 0, null, _pixelsPerUnit);
                _selectedSliceIndices.Clear();
            }
            EditorGUILayout.EndHorizontal();

            if (_slicedSprites.Length > 0) DrawSlicedGrid();

            EditorGUILayout.EndVertical();
        }

        private void DrawSlicedGrid()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"スライス結果: {_slicedSprites.Length} 枚  （クリックで選択、Shift/Ctrlで複数選択 → 下のアニメーションへフレーム追加）");

            const int thumb = 48;
            int perRow = Mathf.Max(1, (int)((EditorGUIUtility.currentViewWidth - 40) / (thumb + 4)));
            int i = 0;
            while (i < _slicedSprites.Length)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < perRow && i < _slicedSprites.Length; c++, i++)
                {
                    int idx = i;
                    var sprite = _slicedSprites[idx];
                    var rect = GUILayoutUtility.GetRect(thumb, thumb, GUILayout.Width(thumb), GUILayout.Height(thumb));

                    if (_selectedSliceIndices.Contains(idx))
                        EditorGUI.DrawRect(rect, new Color(0.2f, 0.5f, 1f, 0.5f));

                    if (sprite != null) DrawSpritePreview(rect, sprite);
                    GUI.Label(new Rect(rect.x, rect.yMax - 14, rect.width, 14), idx.ToString(), EditorStyles.miniLabel);

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.control || Event.current.shift)
                        {
                            if (!_selectedSliceIndices.Add(idx)) _selectedSliceIndices.Remove(idx);
                        }
                        else
                        {
                            _selectedSliceIndices.Clear();
                            _selectedSliceIndices.Add(idx);
                        }
                        Event.current.Use();
                        Repaint();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            using (new EditorGUI.DisabledScope(GetSelectedAnimation() == null || _selectedSliceIndices.Count == 0))
            {
                if (GUILayout.Button($"選択した {_selectedSliceIndices.Count} 枚を現在のアニメーションにフレーム追加"))
                    AddSelectedSlicesAsFrames();
            }
        }

        private void AddSelectedSlicesAsFrames()
        {
            var anim = GetSelectedAnimation();
            if (anim == null) return;

            var frames = new List<SpriteAnimationFrame>(anim.Frames);
            foreach (var idx in _selectedSliceIndices.OrderBy(x => x))
            {
                if (idx < 0 || idx >= _slicedSprites.Length) continue;
                frames.Add(new SpriteAnimationFrame { sprite = _slicedSprites[idx], duration = 1f / Mathf.Max(1f, _bulkFps) });
            }

            anim.EditorSetFrames(frames.ToArray());
            EditorUtility.SetDirty(anim);
            RebuildFrameList();
        }

        // ---------------------------------------------------------------
        // 2. セット／アニメーション
        // ---------------------------------------------------------------
        private void DrawSetSection()
        {
            EditorGUILayout.LabelField("② アニメーションセット", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUI.BeginChangeCheck();
            var newSet = (SpriteAnimationSet)EditorGUILayout.ObjectField("Animation Set", _set, typeof(SpriteAnimationSet), false);
            if (EditorGUI.EndChangeCheck())
            {
                _set = newSet;
                _selectedAnimIndex = _set != null && _set.EditorAnimationsList.Count > 0 ? 0 : -1;
                RebuildAnimList();
                RebuildFrameList();
            }

            if (GUILayout.Button("新規セットを作成"))
            {
                string path = EditorUtility.SaveFilePanelInProject("New Sprite Animation Set", "NewSpriteAnimationSet", "asset", "保存先を選択してください");
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = ScriptableObject.CreateInstance<SpriteAnimationSet>();
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();
                    _set = asset;
                    _selectedAnimIndex = -1;
                    RebuildAnimList();
                    RebuildFrameList();
                }
            }

            if (_set == null)
            {
                EditorGUILayout.HelpBox("セットを作成または選択してください。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();
            float newDefaultCrossFade = EditorGUILayout.FloatField("Default Cross Fade (sec)", _set.DefaultCrossFadeSeconds);
            if (EditorGUI.EndChangeCheck())
            {
                _set.EditorSetDefaultCrossFade(newDefaultCrossFade);
                EditorUtility.SetDirty(_set);
            }

            EditorGUILayout.Space();
            _animList?.DoLayoutList();

            var anim = GetSelectedAnimation();
            if (anim != null) DrawAnimationDetail(anim);

            EditorGUILayout.EndVertical();
        }

        private void DrawAnimationDetail(SpriteAnimation anim)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("選択中のアニメーション", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("Name", anim.AnimationName);
            var newLoop = (SpriteLoopMode)EditorGUILayout.EnumPopup("Loop Mode", anim.LoopMode);
            float newSpeed = EditorGUILayout.FloatField("Speed", anim.Speed);
            if (EditorGUI.EndChangeCheck())
            {
                anim.EditorSetName(newName);
                anim.EditorSetLoopMode(newLoop);
                anim.EditorSetSpeed(newSpeed);
                EditorUtility.SetDirty(anim);
                RebuildAnimList();
            }

            EditorGUILayout.BeginHorizontal();
            _bulkFps = EditorGUILayout.FloatField("FPS", _bulkFps);
            if (GUILayout.Button("全フレームに一括適用", GUILayout.Width(140)))
            {
                var frames = anim.Frames;
                for (int i = 0; i < frames.Length; i++) frames[i].duration = 1f / Mathf.Max(1f, _bulkFps);
                anim.EditorSetFrames(frames);
                EditorUtility.SetDirty(anim);
                RebuildFrameList();
            }
            EditorGUILayout.EndHorizontal();

            _frameList?.DoLayoutList();
        }

        private void RebuildAnimList()
        {
            if (_set == null) { _animList = null; return; }
            var list = _set.EditorAnimationsList;

            _animList = new ReorderableList(list, typeof(SpriteAnimation), true, true, true, true);
            _animList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Animations");
            _animList.drawElementCallback = (r, index, active, focused) =>
            {
                var anim = list[index];
                string label = anim != null ? $"{anim.AnimationName}  ({anim.FrameCount} frames, {anim.LoopMode})" : "(missing)";
                EditorGUI.LabelField(new Rect(r.x, r.y + 1, r.width, EditorGUIUtility.singleLineHeight), label);
            };
            _animList.onSelectCallback = l =>
            {
                _selectedAnimIndex = l.index;
                RebuildFrameList();
            };
            _animList.onAddCallback = l =>
            {
                var anim = ScriptableObject.CreateInstance<SpriteAnimation>();
                anim.name = "NewAnimation";
                anim.EditorSetName("NewAnimation");
                anim.EditorSetFrames(Array.Empty<SpriteAnimationFrame>());
                anim.EditorSetLoopMode(SpriteLoopMode.Loop);

                AssetDatabase.AddObjectToAsset(anim, _set);
                _set.EditorAddAnimation(anim);
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(_set);

                _selectedAnimIndex = list.Count - 1;
                RebuildFrameList();
            };
            _animList.onRemoveCallback = l =>
            {
                var anim = list[l.index];
                _set.EditorRemoveAnimation(anim);
                if (anim != null)
                {
                    AssetDatabase.RemoveObjectFromAsset(anim);
                    UnityEngine.Object.DestroyImmediate(anim, true);
                }
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(_set);

                _selectedAnimIndex = -1;
                RebuildFrameList();
            };
        }

        private void RebuildFrameList()
        {
            var anim = GetSelectedAnimation();
            if (anim == null) { _frameList = null; return; }

            var frames = new List<SpriteAnimationFrame>(anim.Frames);
            _frameList = new ReorderableList(frames, typeof(SpriteAnimationFrame), true, true, true, true);
            _frameList.elementHeight = EditorGUIUtility.singleLineHeight + 6;
            _frameList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Frames (ドラッグで並べ替え可能)");
            _frameList.drawElementCallback = (r, index, active, focused) =>
            {
                var f = frames[index];
                float y = r.y + 2;
                const float spriteW = 140, durW = 60;
                var spriteRect = new Rect(r.x, y, spriteW, EditorGUIUtility.singleLineHeight);
                var durRect = new Rect(r.x + spriteW + 6, y, durW, EditorGUIUtility.singleLineHeight);
                var evtRect = new Rect(r.x + spriteW + durW + 12, y, Mathf.Max(40, r.width - spriteW - durW - 12), EditorGUIUtility.singleLineHeight);

                EditorGUI.BeginChangeCheck();
                var newSprite = (Sprite)EditorGUI.ObjectField(spriteRect, f.sprite, typeof(Sprite), false);
                var newDur = EditorGUI.FloatField(durRect, f.duration);
                var newEvt = EditorGUI.TextField(evtRect, f.eventName);
                if (EditorGUI.EndChangeCheck())
                {
                    f.sprite = newSprite;
                    f.duration = Mathf.Max(0.001f, newDur);
                    f.eventName = newEvt;
                    frames[index] = f;
                    CommitFrames(anim, frames);
                }
            };
            _frameList.onAddCallback = l =>
            {
                frames.Add(new SpriteAnimationFrame { duration = 1f / Mathf.Max(1f, _bulkFps) });
                CommitFrames(anim, frames);
            };
            _frameList.onRemoveCallback = l =>
            {
                frames.RemoveAt(l.index);
                CommitFrames(anim, frames);
            };
            _frameList.onReorderCallback = l => CommitFrames(anim, frames);
        }

        private void CommitFrames(SpriteAnimation anim, List<SpriteAnimationFrame> frames)
        {
            anim.EditorSetFrames(frames.ToArray());
            EditorUtility.SetDirty(anim);
            _previewFrameIndex = 0;
            _previewTimer = 0f;
        }

        // ---------------------------------------------------------------
        // 3. プレビュー
        // ---------------------------------------------------------------
        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("③ プレビュー", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            var anim = GetSelectedAnimation();
            if (anim == null || anim.FrameCount == 0)
            {
                EditorGUILayout.HelpBox("アニメーションを選択し、フレームを追加するとここでプレビューできます。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _previewFrameIndex = Mathf.Clamp(_previewFrameIndex, 0, anim.FrameCount - 1);

            var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            if (_onionSkin && anim.FrameCount > 1)
            {
                int prevIdx = (_previewFrameIndex - 1 + anim.FrameCount) % anim.FrameCount;
                var prevSprite = anim.Frames[prevIdx].sprite;
                if (prevSprite != null)
                {
                    var c = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.3f);
                    DrawSpritePreviewFit(rect, prevSprite);
                    GUI.color = c;
                }
            }

            var sprite = anim.Frames[_previewFrameIndex].sprite;
            if (sprite != null) DrawSpritePreviewFit(rect, sprite);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("|<", GUILayout.Width(30))) { _previewFrameIndex = Mathf.Max(0, _previewFrameIndex - 1); _isPreviewPlaying = false; _previewTimer = 0; }
            if (GUILayout.Button(_isPreviewPlaying ? "Pause" : "Play", GUILayout.Width(60))) _isPreviewPlaying = !_isPreviewPlaying;
            if (GUILayout.Button("Stop", GUILayout.Width(50))) { _isPreviewPlaying = false; _previewFrameIndex = 0; _previewTimer = 0; _previewForward = true; }
            if (GUILayout.Button(">|", GUILayout.Width(30))) { _previewFrameIndex = Mathf.Min(anim.FrameCount - 1, _previewFrameIndex + 1); _isPreviewPlaying = false; _previewTimer = 0; }
            _onionSkin = GUILayout.Toggle(_onionSkin, "Onion Skin", "Button", GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            int scrub = EditorGUILayout.IntSlider("Frame", _previewFrameIndex, 0, anim.FrameCount - 1);
            if (EditorGUI.EndChangeCheck()) { _previewFrameIndex = scrub; _previewTimer = 0; }

            _previewSpeed = EditorGUILayout.Slider("Preview Speed", _previewSpeed, 0.1f, 4f);

            EditorGUILayout.EndVertical();
        }

        // ---------------------------------------------------------------
        // 4. 遷移テスト
        // ---------------------------------------------------------------
        private void DrawTransitionSection()
        {
            if (_set == null || _set.Animations.Count < 2) return;

            EditorGUILayout.LabelField("④ 遷移（クロスフェード）テスト", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            var names = _set.Animations.Select(a => a != null ? a.AnimationName : "(missing)").ToArray();
            _fromIndex = Mathf.Clamp(_fromIndex, 0, names.Length - 1);
            _toIndex = Mathf.Clamp(_toIndex, 0, names.Length - 1);
            _fromIndex = EditorGUILayout.Popup("From", _fromIndex, names);
            _toIndex = EditorGUILayout.Popup("To", _toIndex, names);
            _testCrossFade = EditorGUILayout.Slider("Cross Fade (sec)", _testCrossFade, 0f, 2f);

            if (GUILayout.Button("テスト実行"))
            {
                _blendFromAnim = _set.Animations[_fromIndex];
                _blendToAnim = _set.Animations[_toIndex];
                _testBlendTimer = 0f;
                _isTestBlending = true;
            }

            if (_isTestBlending && _blendFromAnim != null && _blendToAnim != null)
            {
                float t = _testCrossFade <= 0f ? 1f : Mathf.Clamp01(_testBlendTimer / _testCrossFade);
                var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(false));
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

                var fromSprite = _blendFromAnim.FrameCount > 0 ? _blendFromAnim.Frames[0].sprite : null;
                var toSprite = _blendToAnim.FrameCount > 0 ? _blendToAnim.Frames[0].sprite : null;
                var c = GUI.color;
                if (fromSprite != null) { GUI.color = new Color(1f, 1f, 1f, 1f - t); DrawSpritePreviewFit(rect, fromSprite); }
                if (toSprite != null) { GUI.color = new Color(1f, 1f, 1f, t); DrawSpritePreviewFit(rect, toSprite); }
                GUI.color = c;

                EditorGUILayout.LabelField($"Blend: {t:P0}");
            }

            EditorGUILayout.EndVertical();
        }

        // ---------------------------------------------------------------
        // 5. ツール
        // ---------------------------------------------------------------
        private void DrawToolsSection()
        {
            EditorGUILayout.LabelField("⑤ ツール", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.HelpBox("Project ウィンドウで複数のスプライトを選択した状態で実行すると、名前の接頭辞ごと（例: walk_0, walk_1, run_0, run_1）にアニメーションを自動生成します。", MessageType.Info);
            using (new EditorGUI.DisabledScope(_set == null))
            {
                if (GUILayout.Button("選択中のスプライトから自動でアニメーションを作成"))
                    AutoBuildFromSelection();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_set == null || _set.Animations.Count == 0))
            {
                if (GUILayout.Button("アニメーション名の定数クラスを生成 (C#)"))
                {
                    string suggested = _set.name + "Anims";
                    string path = EditorUtility.SaveFilePanel("Save Animation Names Script", Application.dataPath, suggested, "cs");
                    if (!string.IsNullOrEmpty(path))
                    {
                        string className = Path.GetFileNameWithoutExtension(path);
                        SpriteAnimNameCodeGenerator.Generate(_set, path, className, null);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void AutoBuildFromSelection()
        {
            var sprites = Selection.objects.OfType<Sprite>().ToList();
            if (sprites.Count == 0)
            {
                EditorUtility.DisplayDialog("スプライトアニメスタジオ", "Project ウィンドウでスプライトが選択されていません。", "OK");
                return;
            }

            var groups = new Dictionary<string, List<(int order, Sprite sprite)>>();
            var regex = new Regex(@"^(.*?)[_\-\s]?(\d+)$");
            foreach (var s in sprites)
            {
                var match = regex.Match(s.name);
                string groupName = match.Success ? match.Groups[1].Value : s.name;
                int order = match.Success ? int.Parse(match.Groups[2].Value) : 0;
                if (string.IsNullOrEmpty(groupName)) groupName = s.name;

                if (!groups.TryGetValue(groupName, out var list))
                {
                    list = new List<(int, Sprite)>();
                    groups[groupName] = list;
                }
                list.Add((order, s));
            }

            foreach (var kvp in groups)
            {
                var ordered = kvp.Value.OrderBy(x => x.order).ToList();
                var anim = ScriptableObject.CreateInstance<SpriteAnimation>();
                anim.name = kvp.Key;
                anim.EditorSetName(kvp.Key);
                anim.EditorSetLoopMode(SpriteLoopMode.Loop);
                anim.EditorSetFrames(ordered.Select(o => new SpriteAnimationFrame
                {
                    sprite = o.sprite,
                    duration = 1f / Mathf.Max(1f, _bulkFps),
                }).ToArray());

                AssetDatabase.AddObjectToAsset(anim, _set);
                _set.EditorAddAnimation(anim);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(_set);
            RebuildAnimList();
            Repaint();
        }

        // ---------------------------------------------------------------
        // 共通ヘルパー
        // ---------------------------------------------------------------
        private static void DrawSpritePreview(Rect rect, Sprite sprite)
        {
            var tex = sprite.texture;
            var r = sprite.textureRect;
            var texCoords = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, texCoords);
        }

        private static void DrawSpritePreviewFit(Rect rect, Sprite sprite)
        {
            var tex = sprite.texture;
            var r = sprite.textureRect;
            float aspect = r.height > 0 ? r.width / r.height : 1f;

            Rect fit;
            if (aspect > rect.width / rect.height)
            {
                float h = rect.width / aspect;
                fit = new Rect(rect.x, rect.y + (rect.height - h) * 0.5f, rect.width, h);
            }
            else
            {
                float w = rect.height * aspect;
                fit = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, rect.height);
            }

            var texCoords = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            GUI.DrawTextureWithTexCoords(fit, tex, texCoords);
        }
    }
}
