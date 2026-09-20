using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブ内の沸き位置エディタ。位置の数値編集、追加/複製/削除、ID変更（ウェーブ参照も追従）、
    /// リング状の自動配置、グリッドスナップ、Sceneビュー上でのドラッグ編集をまとめて提供する。
    /// </summary>
    public sealed class SpawnPointsPanel
    {
        private bool _foldout = true;

        private bool _snapEnabled;
        private float _gridSize = 0.5f;

        private int _ringCount = 8;
        private float _ringRadius = 10f;
        private Vector2 _ringCenter = Vector2.zero;
        private float _ringStartAngle;
        private bool _ringReplaceExisting = true;

        public void Draw(EnemySpawnDirector director, EnemyWaveConfig wave)
        {
            var points = FindAllPoints();

            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, $"沸き位置の編集（{points.Length}箇所）");
            if (_foldout)
            {
                DrawContents(director, wave, points);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawContents(EnemySpawnDirector director, EnemyWaveConfig wave, EnemySpawnPoint[] points)
        {
            EditorGUILayout.HelpBox(
                "敵が湧く地点そのものの位置とIDを編集します。IDを変えると、このウェーブの参照も自動で追従します。" +
                "「ドラッグ編集」をONにするとSceneビュー上で直接動かせます。",
                MessageType.None);

            DrawSceneToggles();
            EditorGUILayout.Space(4);

            var usage = CountEnemiesPerPoint(wave);
            var duplicatedIds = points.GroupBy(p => p.ResolvedId).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

            foreach (var point in points)
            {
                DrawRow(director, wave, point, usage, duplicatedIds);
            }

            if (points.Length == 0)
            {
                EditorGUILayout.HelpBox("沸き位置がありません。「＋ 追加」か「リング状に配置」で作成してください。", MessageType.Warning);
            }

            if (duplicatedIds.Count > 0)
            {
                EditorGUILayout.HelpBox($"IDが重複している沸き位置があります: {string.Join(", ", duplicatedIds)}", MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            DrawAddAndSnapButtons(director, points);
            EditorGUILayout.Space(4);
            DrawRingGenerator(director, wave, points);
            EditorGUILayout.Space(8);
        }

        private void DrawSceneToggles()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                SpawnPointSceneOverlay.ShowLabels = EditorGUILayout.ToggleLeft(
                    new GUIContent("Sceneにラベル表示", "SceneビューにIDを表示します。"),
                    SpawnPointSceneOverlay.ShowLabels, GUILayout.Width(140));

                SpawnPointSceneOverlay.EnableHandles = EditorGUILayout.ToggleLeft(
                    new GUIContent("ドラッグ編集", "Sceneビュー上に移動ハンドルを出し、直接動かせるようにします。"),
                    SpawnPointSceneOverlay.EnableHandles, GUILayout.Width(110));

                _snapEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent("グリッドスナップ", "ハンドルで動かしたときの座標を、指定した幅に丸めます。"),
                    _snapEnabled, GUILayout.Width(120));

                using (new EditorGUI.DisabledScope(!_snapEnabled))
                {
                    _gridSize = Mathf.Max(0.01f, EditorGUILayout.FloatField(_gridSize, GUILayout.Width(50)));
                }
            }

            SpawnPointSceneOverlay.SnapSize = _snapEnabled ? _gridSize : 0f;
        }

        private void DrawRow(
            EnemySpawnDirector director,
            EnemyWaveConfig wave,
            EnemySpawnPoint point,
            Dictionary<string, int> usage,
            HashSet<string> duplicatedIds)
        {
            var id = point.ResolvedId;
            usage.TryGetValue(id, out var enemyCount);

            var prevColor = GUI.backgroundColor;
            if (duplicatedIds.Contains(id))
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.4f);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prevColor;

                EditorGUI.BeginChangeCheck();
                var newId = EditorGUILayout.DelayedTextField(id, GUILayout.MinWidth(80));
                if (EditorGUI.EndChangeCheck())
                {
                    RenamePoint(point, newId, wave);
                }

                var position = (Vector2)point.transform.position;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("X", GUILayout.Width(12));
                var x = EditorGUILayout.FloatField(position.x, GUILayout.Width(56));
                EditorGUILayout.LabelField("Y", GUILayout.Width(12));
                var y = EditorGUILayout.FloatField(position.y, GUILayout.Width(56));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(point.transform, "Move Spawn Point");
                    point.transform.position = new Vector3(x, y, point.transform.position.z);
                }

                EditorGUILayout.LabelField(
                    enemyCount > 0 ? $"{enemyCount}体" : "未使用",
                    EditorStyles.miniLabel, GUILayout.Width(44));

                if (GUILayout.Button(new GUIContent("選択", "Sceneビューでこの沸き位置を選択して表示します。"), GUILayout.Width(40)))
                {
                    Selection.activeGameObject = point.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }

                if (GUILayout.Button(new GUIContent("複製", "少しずらした位置にコピーを作ります。"), GUILayout.Width(40)))
                {
                    CreatePoint(director, (Vector2)point.transform.position + Vector2.right, id);
                }

                if (GUILayout.Button(new GUIContent("削除", "この沸き位置を削除します。"), GUILayout.Width(40)))
                {
                    DeletePoint(director, point, enemyCount);
                }
            }

            GUI.backgroundColor = prevColor;
        }

        private void DrawAddAndSnapButtons(EnemySpawnDirector director, EnemySpawnPoint[] points)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("＋ 追加", "Sceneビューの中心に新しい沸き位置を作ります。")))
                {
                    var pivot = SceneView.lastActiveSceneView != null
                        ? (Vector2)SceneView.lastActiveSceneView.pivot
                        : Vector2.zero;
                    var created = CreatePoint(director, pivot, null);
                    Selection.activeGameObject = created.gameObject;
                }

                using (new EditorGUI.DisabledScope(points.Length == 0))
                {
                    if (GUILayout.Button(new GUIContent("全てグリッドに揃える", "全ての沸き位置を、指定したグリッド幅に丸めます。")))
                    {
                        foreach (var point in points)
                        {
                            Undo.RecordObject(point.transform, "Snap Spawn Points");
                            var p = point.transform.position;
                            var size = Mathf.Max(0.01f, _gridSize);
                            point.transform.position = new Vector3(
                                Mathf.Round(p.x / size) * size,
                                Mathf.Round(p.y / size) * size,
                                p.z);
                        }
                    }
                }
            }
        }

        private void DrawRingGenerator(EnemySpawnDirector director, EnemyWaveConfig wave, EnemySpawnPoint[] points)
        {
            EditorGUILayout.LabelField("リング状に自動配置", EditorStyles.miniBoldLabel);
            _ringCount = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("個数"), _ringCount), 1, 64);
            _ringRadius = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("半径"), _ringRadius));
            _ringCenter = EditorGUILayout.Vector2Field(new GUIContent("中心座標"), _ringCenter);
            _ringStartAngle = EditorGUILayout.FloatField(
                new GUIContent("開始角度(度)", "0度が真上(+Y)。時計回りに並べます。"), _ringStartAngle);
            _ringReplaceExisting = EditorGUILayout.ToggleLeft(
                new GUIContent("既存の沸き位置を置き換える（ID: Spawn_0, Spawn_1... を再利用）"), _ringReplaceExisting);

            if (!GUILayout.Button("リングを生成"))
            {
                return;
            }

            if (_ringReplaceExisting && points.Length > 0 &&
                !EditorUtility.DisplayDialog(
                    "既存の沸き位置を置き換えます",
                    $"現在の{points.Length}箇所を削除して作り直します。よろしいですか？（Ctrl+Zで元に戻せます）",
                    "置き換える", "キャンセル"))
            {
                return;
            }

            GenerateRing(director, points);
        }

        private void GenerateRing(EnemySpawnDirector director, EnemySpawnPoint[] existing)
        {
            var parent = existing.Length > 0 ? existing[0].transform.parent : director.transform;

            if (_ringReplaceExisting)
            {
                foreach (var point in existing)
                {
                    Undo.DestroyObjectImmediate(point.gameObject);
                }
            }

            for (var i = 0; i < _ringCount; i++)
            {
                var angle = (_ringStartAngle * Mathf.Deg2Rad) + i * Mathf.PI * 2f / _ringCount;
                var position = _ringCenter + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * _ringRadius;
                CreatePoint(director, position, "Spawn", parent);
            }

            SyncDirector(director);
        }

        private static EnemySpawnPoint CreatePoint(EnemySpawnDirector director, Vector2 position, string idBase, Transform parentOverride = null)
        {
            var existing = FindAllPoints();
            var parent = parentOverride != null
                ? parentOverride
                : existing.Length > 0 ? existing[0].transform.parent : director.transform;

            var id = MakeUniqueId(existing, idBase);
            var go = new GameObject(id);
            Undo.RegisterCreatedObjectUndo(go, "Add Spawn Point");
            Undo.SetTransformParent(go.transform, parent, "Add Spawn Point");
            go.transform.position = position;

            var point = Undo.AddComponent<EnemySpawnPoint>(go);
            point.spawnPointId = id;

            SyncDirector(director);
            return point;
        }

        private static void DeletePoint(EnemySpawnDirector director, EnemySpawnPoint point, int enemyCount)
        {
            if (enemyCount > 0 && !EditorUtility.DisplayDialog(
                    "沸き位置を削除",
                    $"'{point.ResolvedId}' はウェーブで使われています（{enemyCount}体分）。削除すると該当エントリは湧かなくなります。削除しますか？",
                    "削除", "キャンセル"))
            {
                return;
            }

            Undo.DestroyObjectImmediate(point.gameObject);
            SyncDirector(director);
        }

        private static void RenamePoint(EnemySpawnPoint point, string newId, EnemyWaveConfig wave)
        {
            if (string.IsNullOrWhiteSpace(newId))
            {
                return;
            }

            var oldId = point.ResolvedId;
            if (newId == oldId)
            {
                return;
            }

            Undo.RecordObject(point, "Rename Spawn Point");
            Undo.RecordObject(point.gameObject, "Rename Spawn Point");
            point.spawnPointId = newId;
            point.gameObject.name = newId;

            if (wave == null)
            {
                return;
            }

            Undo.RecordObject(wave, "Rename Spawn Point");
            foreach (var entry in wave.spawnEntries.Where(e => e.spawnPointId == oldId))
            {
                entry.spawnPointId = newId;
            }

            EditorUtility.SetDirty(wave);
        }

        /// <summary>Directorの参照配列を、シーン上の全EnemySpawnPointに合わせる。</summary>
        private static void SyncDirector(EnemySpawnDirector director)
        {
            Undo.RecordObject(director, "Sync Spawn Points");
            director.spawnPoints = FindAllPoints();
            EditorUtility.SetDirty(director);
        }

        private static EnemySpawnPoint[] FindAllPoints()
        {
            return Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(p => p.name, new NaturalNameComparer())
                .ToArray();
        }

        private static string MakeUniqueId(EnemySpawnPoint[] existing, string idBase)
        {
            var ids = existing.Select(p => p.ResolvedId).ToHashSet();
            var baseName = string.IsNullOrEmpty(idBase) ? "Spawn" : idBase.Split('_')[0];

            for (var i = 0; ; i++)
            {
                var candidate = $"{baseName}_{i}";
                if (!ids.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        private static Dictionary<string, int> CountEnemiesPerPoint(EnemyWaveConfig wave)
        {
            var result = new Dictionary<string, int>();
            if (wave == null)
            {
                return result;
            }

            foreach (var entry in wave.spawnEntries)
            {
                if (string.IsNullOrEmpty(entry.spawnPointId))
                {
                    continue;
                }

                result.TryGetValue(entry.spawnPointId, out var current);
                result[entry.spawnPointId] = current + entry.count;
            }

            return result;
        }

        /// <summary>Spawn_2 が Spawn_10 より前に来るような自然順ソート。</summary>
        private sealed class NaturalNameComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                var (nameA, numA) = Split(a);
                var (nameB, numB) = Split(b);
                var byName = string.CompareOrdinal(nameA, nameB);
                return byName != 0 ? byName : numA.CompareTo(numB);
            }

            private static (string name, int number) Split(string value)
            {
                var index = value.LastIndexOf('_');
                if (index >= 0 && int.TryParse(value.Substring(index + 1), out var number))
                {
                    return (value.Substring(0, index), number);
                }

                return (value, 0);
            }
        }
    }
}
