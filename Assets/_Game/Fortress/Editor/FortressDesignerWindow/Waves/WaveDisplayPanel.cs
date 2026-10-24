using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの「全体」ページの下の2つの欄（2026-10-24、ユーザー依頼「出る順番や秒数を調整したい」）。
    /// ・表示のタイミング（<see cref="WaveDisplaySettings"/>）: GO! の秒数・あと少し！・残り時間の警告・赤い線の出方・初めての人向けの表示
    /// ・合間のイベント（歯みがきタイム <see cref="BrushingEvent"/>）の秒数・回数・ごほうび
    /// どちらも別のファイルなので、ほかのステージと共有している（そう書いて見せる）。HUD を作り直しても消えない。
    /// </summary>
    public sealed class WaveDisplayPanel
    {
        private const string DefaultDisplayPath = "Assets/_Game/Fortress/Presets/Waves/WaveDisplay_Default.asset";

        private static readonly (string path, string label)[] TimingFields =
        {
            ("goSeconds", "「GO!」を出す秒数（0 で出さない）"),
            ("nearGoalKills", "「あと少し！」を出す残りの数（0 で出さない）"),
            ("timeWarningSeconds", "残り時間の警告を出す秒数（0 で出さない）"),
            ("routeShowSeconds", "赤い線が出てくる秒数"),
            ("routeFadeSeconds", "ウェーブが始まってから赤い線が消える秒数"),
            ("routeArrowSpacing", "線の上の矢印の間隔(m)"),
            ("routeArrowSpeed", "線の上の矢印の速さ(m/秒)"),
            ("routeRebuildSeconds", "発表の間に道を引き直す間隔(秒)"),
            ("showEdgeMarkers", "画面の外から来る敵を、ふちの矢印で知らせる"),
            ("firstHintWaves", "初めての人向けの表示を出すウェーブ数（1 = WAVE 1 だけ）"),
            ("showYouMarker", "「YOU」の矢印を出す"),
            ("showGripHint", "握り方のヒントを出す"),
            ("showNewEnemyCards", "「NEW ENEMY」カードを出す"),
            ("syncShowSeconds", "「シンクロ！」を出しておく秒数"),
        };

        private static readonly (string path, string label)[] BrushingFields =
        {
            ("introSeconds", "はじめの説明(秒)"),
            ("brushSeconds", "みがく時間(秒)"),
            ("resultSeconds", "結果を見せる時間(秒)"),
            ("pulsesPerPlayer", "1人あたりの必要な「にぎにぎ」の回数"),
            ("pressThreshold", "「1回」と数える握りの強さ"),
            ("releaseThreshold", "次の1回のために下げる握りの強さ"),
            ("healFraction", "100% のときの回復（最大HPの割合）"),
            ("coatingFraction", "コーティングの上限（最大HPの割合）"),
            ("syncWindowSeconds", "シンクロとみなす秒数"),
            ("syncBonusPulses", "シンクロ1回のボーナスの回数"),
            ("syncCooldownSeconds", "次のシンクロまでの最短(秒)"),
        };

        private const double UsersCacheSeconds = 2.0;

        private static readonly Dictionary<Object, List<string>> UsersCache = new Dictionary<Object, List<string>>();
        private static double _usersCachedAt = -1;

        private readonly Dictionary<Object, SerializedObject> _serialized = new Dictionary<Object, SerializedObject>();
        private bool _timingFoldout = true;
        private bool _eventsFoldout = true;
        private bool _effectsFoldout;

        public void Draw(WavePlan plan, SerializedObject serializedPlan)
        {
            DrawTiming(plan, serializedPlan);
            EditorGUILayout.Space(4);
            DrawEvents(plan);
        }

        // ───────── 表示のタイミング ─────────

        private void DrawTiming(WavePlan plan, SerializedObject serializedPlan)
        {
            _timingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_timingFoldout, "表示のタイミング（GO!・あと少し！・赤い線・初めての人向け）");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!_timingFoldout)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "出る順番は「発表（WAVE n・目標・赤い線・NEW ENEMY）→ 最後に 3・2・1 → GO! → 戦い → WAVE CLEAR!（敵が逃げる）→ 合間 → 次の発表」。" +
                    "発表の長さ・3・2・1・WAVE CLEAR! の秒数は上の「始まり・発表」「ウェーブクリア → 撤退」で、ここではそれ以外を変えます。" +
                    "出てくる動き（フェード・弾み）の長さや位置は UIスタジオで Hud を直します（「ウェーブの表示を入れる」を押し直すと戻ります）。",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.PropertyField(serializedPlan.FindProperty("display"),
                    new GUIContent("表示の設定ファイル", "全部のステージで同じファイルを使ってよい。空なら既定の値。"));
                if (plan.display == null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox("設定ファイルが空なので、既定の値で動いています。変えるにはファイルを入れてください。", MessageType.Info);
                        if (GUILayout.Button("既定のファイルを入れる", GUILayout.Width(160), GUILayout.Height(38)))
                        {
                            serializedPlan.ApplyModifiedProperties();
                            Undo.RecordObject(plan, "表示の設定を入れる");
                            plan.display = LoadOrCreateDefault();
                            EditorUtility.SetDirty(plan);
                            serializedPlan.Update();
                        }
                    }

                    return;
                }

                var users = PlansUsing(plan.display);
                if (users.Count > 1)
                {
                    EditorGUILayout.LabelField($"このファイル（{plan.display.name}）は {string.Join("・", users)} で共有しています。直すと全部に効きます。", EditorStyles.wordWrappedMiniLabel);
                }

                var so = Serialized(plan.display);
                so.Update();
                var timing = so.FindProperty("timing");
                foreach (var (path, label) in TimingFields)
                {
                    var property = timing.FindPropertyRelative(path);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
                    }
                }

                so.ApplyModifiedProperties();
            }
        }

        // ───────── 合間のイベント（歯みがきタイム） ─────────

        private void DrawEvents(WavePlan plan)
        {
            var events = plan.waves.Take(Mathf.Max(0, plan.Count - 1))
                .Select(w => w?.intervalEvent)
                .Where(e => e != null)
                .Distinct()
                .ToList();
            _eventsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_eventsFoldout, $"合間のイベント（{(events.Count == 0 ? "なし" : string.Join("・", events.Select(e => e.DisplayName)))}）");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!_eventsFoldout)
            {
                return;
            }

            if (events.Count == 0)
            {
                EditorGUILayout.HelpBox("どのウェーブの後にも合間のイベントがありません（休むだけ）。各ウェーブのページの「このウェーブの後」で選べます。", MessageType.Info);
                return;
            }

            foreach (var ev in events)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var used = Enumerable.Range(0, plan.Count - 1).Where(i => plan.waves[i]?.intervalEvent == ev).Select(i => $"WAVE {i + 1}");
                    EditorGUILayout.LabelField($"{ev.DisplayName}（{ev.name}）: {string.Join("・", used)} の後 ・ 全部で {ev.DurationSeconds:0.#}秒", EditorStyles.boldLabel);
                    var users = PlansUsing(ev);
                    if (users.Count > 1)
                    {
                        EditorGUILayout.LabelField($"このファイルは {string.Join("・", users)} で共有しています。直すと全部に効きます。", EditorStyles.wordWrappedMiniLabel);
                    }

                    var so = Serialized(ev);
                    so.Update();
                    if (ev is BrushingEvent)
                    {
                        foreach (var (path, label) in BrushingFields)
                        {
                            var property = so.FindProperty(path);
                            if (property != null)
                            {
                                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
                            }
                        }

                        _effectsFoldout = EditorGUILayout.Foldout(_effectsFoldout, "演出（始まり・にぎにぎ・シンクロ・結果）", true);
                        if (_effectsFoldout)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                EffectField(so, "onStart", "始まり");
                                EffectField(so, "onPulse", "にぎにぎ1回（自分のPCだけ）");
                                EffectField(so, "onSync", "シンクロ");
                                EffectField(so, "onResult", "結果");
                            }
                        }
                    }
                    else
                    {
                        // ほかの種類のイベントは、そのまま全部の欄を出す。
                        var iterator = so.GetIterator();
                        for (var enter = true; iterator.NextVisible(enter); enter = false)
                        {
                            if (iterator.propertyPath != "m_Script")
                            {
                                EditorGUILayout.PropertyField(iterator, true);
                            }
                        }
                    }

                    so.ApplyModifiedProperties();
                }
            }
        }

        private static void EffectField(SerializedObject so, string path, string label)
        {
            var property = so.FindProperty(path);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }

        private SerializedObject Serialized(Object target)
        {
            if (!_serialized.TryGetValue(target, out var so) || so.targetObject == null)
            {
                so = new SerializedObject(target);
                _serialized[target] = so;
            }

            return so;
        }

        // このファイルを使っている計画（ファイル名）。共有していることを知らせる用（画面は 0.15 秒ごとに描き直すので、2 秒覚えておく）。
        private static List<string> PlansUsing(Object asset)
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _usersCachedAt > UsersCacheSeconds)
            {
                UsersCache.Clear();
                _usersCachedAt = now;
            }

            if (UsersCache.TryGetValue(asset, out var cached))
            {
                return cached;
            }

            var result = new List<string>();
            UsersCache[asset] = result;
            foreach (var guid in AssetDatabase.FindAssets("t:WavePlan"))
            {
                var plan = AssetDatabase.LoadAssetAtPath<WavePlan>(AssetDatabase.GUIDToAssetPath(guid));
                if (plan == null)
                {
                    continue;
                }

                if (plan.display == asset || plan.waves.Any(w => w != null && w.intervalEvent == asset))
                {
                    result.Add(plan.name);
                }
            }

            return result;
        }

        /// <summary>既定の表示の設定（無ければ作る）。</summary>
        public static WaveDisplaySettings LoadOrCreateDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WaveDisplaySettings>(DefaultDisplayPath);
            if (existing != null)
            {
                return existing;
            }

            WavePlanEditing.EnsureFolder(System.IO.Path.GetDirectoryName(DefaultDisplayPath)?.Replace('\\', '/'));
            var created = ScriptableObject.CreateInstance<WaveDisplaySettings>();
            AssetDatabase.CreateAsset(created, DefaultDisplayPath);
            AssetDatabase.SaveAssets();
            return created;
        }
    }
}
