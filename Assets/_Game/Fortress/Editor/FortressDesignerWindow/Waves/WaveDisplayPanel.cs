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

        // 欄はデータの並び順（見出しはデータ側の [Header] がそのまま出る。ここで書くと二重になる）。
        // 上から「試合の始まり → 発表の中の順番 → GO → 戦い → 赤い線 → 歯みがき」。
        private static readonly (string path, string label)[] TimingFields =
        {
            ("hintSeconds", "握り方のヒントだけを出す秒数（0 ですぐ発表）"),
            ("showYouMarker", "「YOU」の矢印を出す"),
            ("showGripHint", "握り方のヒントを出す"),
            ("bannerDelay", "「WAVE n／目標撃破数」"),
            ("routeDelay", "敵のルート（赤い線・「！」・ふちの矢印）"),
            ("newEnemyDelay", "「NEW ENEMY」カード"),
            ("showNewEnemyCards", "「NEW ENEMY」カードを出す"),
            ("showAnnounceText", "発表の一言（「ボス出現！」など）を出す"),
            ("showRouteHint", "「赤い線 ＝ 敵の通り道」を出す"),
            ("goSeconds", "「GO!」を出す秒数（0 で出さない）"),
            ("nearGoalKills", "「あと少し！」を出す残りの数（0 で出さない）"),
            ("timeWarningSeconds", "残り時間の警告を出す秒数（0 で出さない）"),
            (null, "敵のルート（赤い線）"),
            ("routeShowSeconds", "赤い線が出てくる秒数"),
            ("routeFadeSeconds", "ウェーブが始まってから赤い線が消える秒数"),
            ("routeArrowSpacing", "線の上の矢印の間隔(m)"),
            ("routeArrowSpeed", "線の上の矢印の速さ(m/秒)"),
            ("routeRebuildSeconds", "発表の間に道を引き直す間隔(秒)"),
            ("showEdgeMarkers", "画面の外から来る敵を、ふちの矢印で知らせる"),
            ("showEdgeMarkerCounts", "ふちの矢印の横に、来る数（×40 など）を出す"),
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
                    "発表の長さ・最後の 3・2・1・WAVE CLEAR! の秒数は上の「始まり・発表」「ウェーブクリア → 撤退」で、ここではそれ以外を変えます。" +
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

                DrawOrder(plan, plan.display.timing);
                var so = Serialized(plan.display);
                so.Update();
                var timing = so.FindProperty("timing");
                using (new WaveLabelWidth())
                {
                    foreach (var (path, label) in TimingFields)
                    {
                        var property = timing.FindPropertyRelative(path);
                        if (property != null)
                        {
                            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
                        }
                    }
                }

                so.ApplyModifiedProperties();
            }
        }

        // 今の設定での出る順番を、時間の順に1行ずつ見せる（数字を変えるとすぐ並びが変わる）。
        private static void DrawOrder(WavePlan plan, WaveDisplayTiming t)
        {
            var steps = new List<(float time, string text)>();
            var announce = Mathf.Max(0.5f, plan.announceSeconds);
            if (t.bannerDelay < announce)
            {
                steps.Add((t.bannerDelay, "WAVE n／目標撃破数" + (t.showAnnounceText ? "＋一言" : "")));
            }

            if (plan.showRoutes && t.routeDelay < announce)
            {
                steps.Add((t.routeDelay, "赤い線" + (t.showRouteHint ? "＋「赤い線＝敵の通り道」" : "")));
            }

            if (t.showNewEnemyCards && t.newEnemyDelay < announce)
            {
                steps.Add((t.newEnemyDelay, "NEW ENEMY（初めて出る敵がいるとき）"));
            }

            if (plan.countdownSeconds > 0f)
            {
                steps.Add((Mathf.Max(0f, announce - plan.countdownSeconds), "3・2・1"));
            }

            var line = string.Join(" → ", steps.OrderBy(x => x.time).Select(x => $"{x.time:0.#}秒 {x.text}"));
            var start = t.hintSeconds > 0f && (t.showGripHint || t.showYouMarker) ? $"握り方のヒント {t.hintSeconds:0.#}秒 → " : "";
            EditorGUILayout.HelpBox(
                $"出る順番（今の設定）: 準備待ち → {start}発表 {announce:0.#}秒［{line}］→ GO!{(t.goSeconds > 0f ? $" {t.goSeconds:0.#}秒" : "（出さない）")} → 戦い → WAVE CLEAR!（敵が逃げる）→ 合間 → 次の発表\n" +
                "握り方のヒントは試合の始まりだけ（WAVE 2 からは発表から）。",
                MessageType.None);
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
                    using var width = new WaveLabelWidth();
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

                        _effectsFoldout = EditorGUILayout.Foldout(_effectsFoldout, "演出（各PCで鳴らす。位置はコア）", true);
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
