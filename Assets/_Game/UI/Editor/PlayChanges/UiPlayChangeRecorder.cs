using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// Play 中に UI の値を変えたら（インスペクター・シーンビュー・UIスタジオのどこからでも）記録し、
    /// Play を止めたときに「残す／一覧で選ぶ／捨てる」をたずねる。
    /// Unity の「元に戻す（Undo）」に記録される変更を拾うので、ゲームのプログラムが動かした値（動き・ゲージ・文字）は入らない。
    /// UIスタジオを開いていなくても動く。
    /// </summary>
    [InitializeOnLoad]
    public static class UiPlayChangeRecorder
    {
        private const string PrefEnabled = "MS2026.UiStudio.RecordPlayChanges";
        private const string SessionRecorded = "MS2026.UiStudio.PlayChangesRecorded";

        // Play 中だけ: 変更の鍵 → いま動いている物（止める直前に最後の値を読み直すため）。
        private static readonly Dictionary<string, Object> LiveTargets = new Dictionary<string, Object>();

        static UiPlayChangeRecorder()
        {
            Undo.postprocessModifications += OnModifications;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Play 中の変更を記録するか（パソコンごとの設定。最初は ON）。</summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefEnabled, true);
            set => EditorPrefs.SetBool(PrefEnabled, value);
        }

        /// <summary>いま記録しているか（Play 中で ON）。</summary>
        public static bool IsRecording => EditorApplication.isPlaying && Enabled;

        /// <summary>Play 中なら、その変更が入った今動いている物（無ければ null）。</summary>
        public static Object LiveTarget(UiPlayChange change) =>
            EditorApplication.isPlaying && change != null && LiveTargets.TryGetValue(change.Key, out var live) && live != null ? live : null;

        private static UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            if (!IsRecording || modifications == null)
            {
                return modifications;
            }

            try
            {
                // 名前の変更を先に見て、Play 前の名前を控える（同じ回の他の変更の道のりを Play 前の名前で作るため）。
                foreach (var modification in modifications)
                {
                    var current = modification.currentValue;
                    if (current != null && current.target is GameObject go && current.propertyPath == "m_Name")
                    {
                        UiPlayLocator.RememberOriginalName(go, modification.previousValue?.value ?? go.name);
                    }
                }

                var recorded = false;
                foreach (var modification in modifications)
                {
                    recorded |= Record(modification);
                }

                if (recorded)
                {
                    SessionState.SetBool(SessionRecorded, true);
                    UiPlayChangeStore.Touch();
                }
            }
            catch (Exception e)
            {
                // 記録に失敗しても、Unity の変更そのものは止めない。
                Debug.LogException(e);
            }

            return modifications;
        }

        private static bool Record(UndoPropertyModification modification)
        {
            var current = modification.currentValue;
            if (current?.target == null || string.IsNullOrEmpty(current.propertyPath) || IsRuntimeHelper(current.target))
            {
                return false;
            }

            if (!UiPlayLocator.TryLocate(current.target, out var target, out var problem))
            {
                return false;
            }

            var change = new UiPlayChange
            {
                target = target,
                propertyPath = current.propertyPath,
                value = current.value ?? "",
                after = Describe(current),
                problem = problem
            };

            var reference = current.objectReference;
            if (reference != null)
            {
                if (EditorUtility.IsPersistent(reference))
                {
                    change.objectValue = reference;
                }
                else if (UiPlayLocator.TryLocate(reference, out var referenceTarget, out var referenceProblem) &&
                         string.IsNullOrEmpty(referenceProblem) && referenceTarget.AnchorKey == target.AnchorKey)
                {
                    change.referencesLocal = true;
                    change.reference = referenceTarget;
                }
                else if (string.IsNullOrEmpty(change.problem))
                {
                    change.problem = "Play 中だけある物か、別の画面の物を指しているので残せません。";
                }
            }

            if (!UiPlayChangeStore.Contains(change.Key))
            {
                change.before = Describe(modification.previousValue);
                change.beforeValue = modification.previousValue?.value ?? "";
                change.beforeObject = modification.previousValue?.objectReference;
                change.label = LabelOf(current.target, current.propertyPath);
            }

            LiveTargets[change.Key] = current.target;
            UiPlayChangeStore.Merge(change);
            return true;
        }

        private static bool IsRuntimeHelper(Object target)
        {
            var go = target is Component component ? component.gameObject : target as GameObject;
            return go != null && (go.hideFlags & HideFlags.DontSave) != 0;
        }

        private static string Describe(PropertyModification modification)
        {
            if (modification == null)
            {
                return "";
            }

            if (modification.objectReference != null)
            {
                return modification.objectReference.name;
            }

            return modification.value ?? "";
        }

        /// <summary>欄の道のりを、インスペクターと同じ表示名でつなぐ（「Anchored Position › X」「Entries › 1番目 › Delay」）。</summary>
        private static string LabelOf(Object target, string path)
        {
            switch (path)
            {
                case "m_Name":
                    return "名前";
                case "m_IsActive":
                    return "表示（ON/OFF）";
            }

            var parts = new List<string>();
            using (var so = new SerializedObject(target))
            {
                var segments = path.Split('.');
                var prefix = "";
                for (var i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    if (segment == "Array" && i + 1 < segments.Length)
                    {
                        var next = segments[i + 1];
                        if (next == "size")
                        {
                            parts.Add("数");
                            break;
                        }

                        if (next.StartsWith("data[", StringComparison.Ordinal))
                        {
                            prefix += ".Array." + next;
                            var number = next.Substring(5).TrimEnd(']');
                            parts.Add(int.TryParse(number, out var n) ? $"{n + 1}番目" : number);
                            i++;
                            continue;
                        }
                    }

                    prefix = prefix.Length == 0 ? segment : prefix + "." + segment;
                    var property = so.FindProperty(prefix);
                    parts.Add(property != null ? property.displayName : ObjectNames.NicifyVariableName(segment));
                }
            }

            return string.Join(" › ", parts);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    UiPlayLocator.ResetNames();
                    LiveTargets.Clear();
                    SessionState.SetBool(SessionRecorded, false);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    ReadFinalValues();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    UiPlayLocator.ResetNames();
                    LiveTargets.Clear();
                    if (SessionState.GetBool(SessionRecorded, false))
                    {
                        SessionState.SetBool(SessionRecorded, false);
                        // シーンが読み直されて落ち着いてからたずねる。
                        EditorApplication.delayCall += AskAfterPlay;
                    }

                    break;
            }
        }

        /// <summary>
        /// Play が終わる直前（物がまだある）に、記録した欄の最後の値を読み直す。
        /// Play 中に元に戻した（Ctrl+Z）変更は、Unity が知らせてくれないのでここで見つけて外す。
        /// </summary>
        private static void ReadFinalValues()
        {
            if (LiveTargets.Count == 0)
            {
                return;
            }

            var reverted = new List<UiPlayChange>();
            foreach (var change in UiPlayChangeStore.Items)
            {
                if (!LiveTargets.TryGetValue(change.Key, out var live) || live == null)
                {
                    continue;
                }

                try
                {
                    using (var so = new SerializedObject(live))
                    {
                        var property = so.FindProperty(change.propertyPath);
                        if (property == null)
                        {
                            // 配列の要素を足して、また消したときなど。欄そのものが無くなっている。
                            reverted.Add(change);
                            continue;
                        }

                        if (!UiPlayValueIo.TryRead(property, out var text, out var reference))
                        {
                            continue;
                        }

                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (reference == change.beforeObject)
                            {
                                reverted.Add(change);
                            }

                            continue;
                        }

                        if (UiPlayValueText.Same(text, change.beforeValue))
                        {
                            reverted.Add(change);
                            continue;
                        }

                        change.value = text;
                        change.after = text;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (reverted.Count > 0)
            {
                UiPlayChangeStore.RemoveAll(reverted.Contains);
            }
            else
            {
                UiPlayChangeStore.Touch();
            }
        }

        /// <summary>Play を止めたあと「残す／一覧で選ぶ／捨てる」をたずねる。</summary>
        private static void AskAfterPlay()
        {
            var total = UiPlayChangeStore.Count;
            if (total == 0)
            {
                return;
            }

            var applicable = UiPlayChangeStore.ApplicableCount;
            if (applicable == 0)
            {
                if (EditorUtility.DisplayDialog("Play 中に変えた UI の値",
                        $"Play 中に UI の値を {total} か所変えましたが、どれも残せない変更でした（Play 中に作られた部品など）。\n理由は UIスタジオの「Play中の変更」で見られます。",
                        "一覧を見る", "捨てる"))
                {
                    UiStudioWindow.OpenPage<UiPlayChangesPage>();
                }
                else
                {
                    UiPlayChangeStore.Clear();
                }

                return;
            }

            var message = $"Play 中に UI の値を {applicable} か所変えました。\n書き戻し先: {Destinations()}\n\n" +
                          "・残す … Prefab／シーンに書き戻します（シーンは保存すると確定）\n" +
                          "・一覧で選ぶ … UIスタジオの「Play中の変更」で1つずつ選べます\n" +
                          "・捨てる … Play 前のままにします";
            if (total > applicable)
            {
                message += $"\n\n（ほかに残せない変更が {total - applicable} か所あります。一覧で理由を見られます）";
            }

            // ×・Esc で閉じたときは「一覧で選ぶ」（何も消さない）になるよう、キャンセル側に置く。
            switch (EditorUtility.DisplayDialogComplex("Play 中に変えた UI の値", message, "残す", "一覧で選ぶ", "捨てる"))
            {
                case 0:
                    var report = UiPlayChangeApplier.ApplyKept();
                    report.Log();
                    if (report.Failed > 0 || UiPlayChangeStore.Count > 0)
                    {
                        UiStudioWindow.OpenPage<UiPlayChangesPage>();
                    }

                    break;
                case 2:
                    UiPlayChangeStore.Clear();
                    break;
                default:
                    UiStudioWindow.OpenPage<UiPlayChangesPage>();
                    break;
            }
        }

        private static string Destinations()
        {
            var names = new List<string>();
            foreach (var group in UiPlayChangeList.GroupByDestination(UiPlayChangeStore.Items))
            {
                var name = Path.GetFileName(group[0].target.assetPath);
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count > 4)
            {
                return string.Join("、", names.GetRange(0, 4)) + $" ほか {names.Count - 4} 個";
            }

            return names.Count > 0 ? string.Join("、", names) : "（なし）";
        }
    }
}
