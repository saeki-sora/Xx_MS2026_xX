using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 物ごとの欄: シーンで選んでいる物のカード（この材質の物を調整／この物だけ調整／鏡にする）と、材質の一覧。
    /// 「調整」を押すと、上の見た目の欄がその材質の値を動かすようになる（窓の上の「調整中」の帯で分かる）。
    /// </summary>
    public sealed class ToonObjectsSection : Foldout
    {
        private const string OpenKey = "MS2026.ObjectToon.Window.ObjectsOpen";
        private const int MaxRenderers = 6;

        private readonly ToonContext _context;
        private readonly VisualElement _selected;
        private readonly VisualElement _list;

        public ToonObjectsSection(ToonContext context)
        {
            _context = context;
            AddToClassList("tw-fold");
            AddToClassList("tw-fold--big");
            value = EditorPrefs.GetBool(OpenKey, false);
            this.RegisterValueChangedCallback(e =>
            {
                if (e.target == this)
                {
                    EditorPrefs.SetBool(OpenKey, e.newValue);
                }
            });

            _selected = new VisualElement();
            Add(_selected);
            Add(StudioUi.Section("材質の一覧", "ステージで使っている材質ごとのトゥーン材質です。左のスイッチを切ると、その材質は元のまま（トゥーンにしない）。"));
            _list = new VisualElement();
            Add(_list);

            context.TableChanged += Rebuild;
            context.TargetChanged += Rebuild;
            Rebuild();
        }

        public void Rebuild()
        {
            var profile = _context.Profile;
            _selected.Clear();
            _list.Clear();
            if (profile == null)
            {
                return;
            }

            BuildSelected(profile);
            BuildList(profile);
        }

        // ───────── 選んでいる物 ─────────

        private void BuildSelected(ObjectToonProfile profile)
        {
            var go = Selection.activeGameObject;
            text = "物ごと" + (go != null && go.scene.IsValid() ? "　（選んでいる物: " + go.name + "）" : "");

            if (go == null || !go.scene.IsValid() || !go.scene.isLoaded)
            {
                _selected.Add(StudioUi.Note("シーンビューかヒエラルキーで物（石けん・コップ・鏡など）をクリックすると、ここにその物が出ます。", NoteKind.Info));
                return;
            }

            var renderers = new List<Renderer>();
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    renderers.Add(renderer);
                }
            }

            if (renderers.Count == 0)
            {
                _selected.Add(StudioUi.Note("「" + go.name + "」には3Dの描画部品（MeshRenderer）がありません。", NoteKind.Info));
                return;
            }

            if (!profile.targetScenes.Contains(go.scene.name))
            {
                _selected.Add(StudioUi.Note("このシーン（" + go.scene.name + "）はトゥーンにするシーンに入っていません。いちばん下の「準備」で追加できます。", NoteKind.Warn));
            }

            if (renderers.Count > MaxRenderers)
            {
                _selected.Add(StudioUi.Note("中に描画する物が " + renderers.Count + " 個あります。最初の " + MaxRenderers + " 個だけ出しています（子の物を直接選ぶと出ます）。", NoteKind.Info));
                renderers.RemoveRange(MaxRenderers, renderers.Count - MaxRenderers);
            }

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    _selected.Add(BuildSlotCard(profile, renderer, slot, materials[slot], materials.Length > 1));
                }
            }
        }

        private VisualElement BuildSlotCard(ObjectToonProfile profile, Renderer renderer, int slot, Material current, bool showSlot)
        {
            var sceneName = renderer.gameObject.scene.name;
            var path = ObjectToonApplier.GetPath(renderer.transform);
            var own = profile.FindOverride(sceneName, path, slot);
            var original = ResolveOriginal(profile, own, current);
            var entry = original != null ? profile.FindByOriginal(profile.entries, original) : null;

            var card = StudioUi.Card(renderer.name + (showSlot ? "（材質 " + slot + "）" : ""));
            card.AddToClassList("tw-card");
            var status = new Label();
            status.AddToClassList("tw-card-status");
            card.Add(status);
            var buttons = new VisualElement();
            buttons.AddToClassList("tw-buttons");
            card.Add(buttons);

            if (own != null && own.toon != null)
            {
                status.text = "この物だけの材質で調整しています。";
                MarkTarget(card, own.toon);
                buttons.Add(StudioUi.Button("この物を調整", () => _context.UseOverride(own), "上の項目で、この物だけの値を動かします。", primary: true, small: true));
                buttons.Add(StudioUi.Button("鏡にする", () => ApplyMirror(own.toon), "空色の映り込みと斜めの光の筋を付けます（鏡・金属用）。", small: true));
                buttons.Add(StudioUi.Button("この物だけをやめる", () => RemoveOverride(profile, own), "この物だけの調整をやめて、材質ごとの設定に戻します（作った材質のファイルは残します）。", small: true));
                return card;
            }

            if (entry != null && entry.toon != null)
            {
                if (!entry.enabled)
                {
                    status.text = "トゥーンにしていません（材質: " + original.name + "）。";
                    buttons.Add(StudioUi.Button("トゥーンにする", () => SetEnabled(profile, entry, true), "この材質をトゥーンにします。", primary: true, small: true));
                    return card;
                }

                status.text = (entry.useCommon ? "共通の設定に合わせています" : "この材質だけの値です") + "（材質: " + original.name + "）。";
                MarkTarget(card, entry.toon);
                buttons.Add(StudioUi.Button("この材質の物を調整", () => _context.UseEntry(entry),
                    "同じ材質を使っている物すべてを、上の項目で調整します（共通の設定に合わせるのはやめます）。", primary: true, small: true));
                buttons.Add(StudioUi.Button("この物だけ調整", () => CreateOverride(profile, sceneName, path, slot, original, entry),
                    "この物専用の材質を作って、ほかの同じ材質の物とは別に調整します。", small: true));
                buttons.Add(StudioUi.Button("鏡にする", () => ApplyMirror(entry.toon), "空色の映り込みと斜めの光の筋を付けます（鏡・金属用）。", small: true));
                if (ObjectToonMaterials.LooksLikeMirror(original) && entry.toon.GetFloat("_ReflectOn") < 0.5f)
                {
                    card.Add(StudioUi.Note("元の材質は鏡のようです。真っ白に見えるときは「鏡にする」を押してください。", NoteKind.Info));
                }

                return card;
            }

            if (original != null && ObjectToonMaterials.IsSourceCandidate(original, false))
            {
                status.text = "まだトゥーンになっていません（材質: " + original.name + "）。";
                buttons.Add(StudioUi.Button("追加", () => _context.AddSceneMaterials(sprite: false),
                    "対象のシーンの材質を探して、トゥーン材質を作ります（シーンを保存してから押してください）。", primary: true, small: true));
                return card;
            }

            status.text = "この材質はトゥーンの対象外です（" + (original != null && original.shader != null ? original.shader.name : "材質なし") + "）。";
            return card;
        }

        private void MarkTarget(VisualElement card, Material material)
        {
            card.EnableInClassList("tw-card--target", material == _context.Target);
        }

        private static Material ResolveOriginal(ObjectToonProfile profile, ObjectToonProfile.ObjectOverride own, Material current)
        {
            if (own != null && own.original != null)
            {
                return own.original;
            }

            var entry = current != null ? profile.FindByToon(current) : null;
            return entry != null ? entry.original : current;
        }

        private void CreateOverride(ObjectToonProfile profile, string sceneName, string path, int slot, Material original, ObjectToonProfile.Entry entry)
        {
            var objectName = path.Substring(path.LastIndexOf('/') + 1);
            var toon = ObjectToonMaterials.CreateObjectMaterial(entry != null ? entry.toon : null, original, sceneName + "_" + objectName, slot);
            Undo.RecordObject(profile, "この物だけ調整する");
            var item = new ObjectToonProfile.ObjectOverride { scene = sceneName, path = path, slot = slot, original = original, toon = toon };
            profile.objectOverrides.Add(item);
            AssetDatabase.SaveAssets();
            _context.NotifyTableChanged();
            _context.UseOverride(item);
        }

        private void RemoveOverride(ObjectToonProfile profile, ObjectToonProfile.ObjectOverride own)
        {
            if (!EditorUtility.DisplayDialog("3Dオブジェクト トゥーン", "この物だけの調整をやめて、材質ごとの設定に戻しますか？\n（作った材質のファイルは消さずに残します）", "やめる", "キャンセル"))
            {
                return;
            }

            bool wasTarget = own.toon == _context.Target;
            Undo.RecordObject(profile, "この物だけをやめる");
            profile.objectOverrides.Remove(own);
            _context.NotifyTableChanged();
            if (wasTarget)
            {
                _context.UseCommon();
            }
        }

        private void ApplyMirror(Material toon)
        {
            Undo.RecordObject(toon, "鏡のおすすめの値");
            ObjectToonMaterials.ApplyMirrorPreset(toon);
            _context.AfterMaterialChanged(toon);
        }

        private void SetEnabled(ObjectToonProfile profile, ObjectToonProfile.Entry entry, bool on)
        {
            Undo.RecordObject(profile, "トゥーンにする");
            entry.enabled = on;
            _context.NotifyTableChanged();
        }

        // ───────── 材質の一覧 ─────────

        private void BuildList(ObjectToonProfile profile)
        {
            if (profile.entries.Count == 0 && profile.objectOverrides.Count == 0)
            {
                _list.Add(StudioUi.Note("まだありません。いちばん下の「準備」の「シーンの材質を探してトゥーン材質を作る」を押してください。", NoteKind.Info));
            }

            foreach (var entry in profile.entries)
            {
                if (entry != null && entry.original != null)
                {
                    _list.Add(BuildEntryRow(profile, entry));
                }
            }

            foreach (var material in _context.Missing)
            {
                var row = ListRow(material.name, "トゥーンになっていない", ChipKind.Warn);
                row.Add(StudioUi.Button("追加", () => _context.AddSceneMaterials(sprite: false), "対象のシーンの材質を探して、トゥーン材質を作ります。", small: true));
                _list.Add(row);
            }

            if (profile.objectOverrides.Count > 0)
            {
                _list.Add(StudioUi.Section("この物だけの材質（" + profile.objectOverrides.Count + "）"));
            }

            foreach (var item in profile.objectOverrides)
            {
                if (item == null)
                {
                    continue;
                }

                var name = item.path.Substring(item.path.LastIndexOf('/') + 1);
                var row = ListRow(name, item.enabled ? "この物だけ" : "オフ", ChipKind.Info);
                row.tooltip = item.scene + " / " + item.path + "  [材質 " + item.slot + "]";
                row.EnableInClassList("tw-item--target", item.toon != null && item.toon == _context.Target);
                row.Add(StudioUi.Button("選ぶ", () => SelectByPath(item), "シーンでこの物を選びます。", small: true));
                row.Add(StudioUi.Button("調整", () => _context.UseOverride(item), "上の項目で、この物だけの値を動かします。", small: true));
                _list.Add(row);
            }
        }

        private VisualElement BuildEntryRow(ObjectToonProfile profile, ObjectToonProfile.Entry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("tw-item");
            row.EnableInClassList("tw-item--target", entry.toon != null && entry.toon == _context.Target);

            var toggle = new Toggle { value = entry.enabled, tooltip = "オンでトゥーンにする / オフで元の材質のまま" };
            toggle.RegisterValueChangedCallback(e => SetEnabled(profile, entry, e.newValue));
            row.Add(toggle);

            var name = new Label(entry.original.name);
            name.AddToClassList("tw-item-name");
            row.Add(name);

            if (entry.toon == null)
            {
                row.Add(StudioUi.Chip("材質が無い", ChipKind.Error, "トゥーン材質のファイルが見つかりません。"));
                row.Add(StudioUi.Button("作り直す", () => RecreateToon(profile, entry), "トゥーン材質を作り直します。", small: true));
                return row;
            }

            if (!entry.enabled)
            {
                row.Add(StudioUi.Chip("オフ", ChipKind.Plain, "この材質はトゥーンにしていません。"));
            }
            else if (entry.useCommon)
            {
                row.Add(StudioUi.Chip("共通", ChipKind.Ok, "共通の設定に合わせています。"));
            }
            else
            {
                row.Add(StudioUi.Chip("この材質だけ", ChipKind.Accent, "共通の設定とは別の値です。"));
                row.Add(StudioUi.Button("共通に戻す", () => BackToCommon(profile, entry), "この材質を、共通の設定に合わせ直します（この材質だけの値は上書きされます）。", small: true));
            }

            row.Add(StudioUi.Button("選ぶ", () => SelectUsers(entry), "シーンで、この材質を使っている物を選びます。", small: true));
            row.Add(StudioUi.Button("調整", () => _context.UseEntry(entry), "この材質の物すべてを、上の項目で調整します。", small: true));
            return row;
        }

        private static VisualElement ListRow(string name, string chip, ChipKind kind)
        {
            var row = new VisualElement();
            row.AddToClassList("tw-item");
            var label = new Label(name);
            label.AddToClassList("tw-item-name");
            row.Add(label);
            row.Add(StudioUi.Chip(chip, kind));
            return row;
        }

        private void BackToCommon(ObjectToonProfile profile, ObjectToonProfile.Entry entry)
        {
            Undo.RecordObject(profile, "共通に合わせる");
            Undo.RecordObject(entry.toon, "共通に合わせる");
            entry.useCommon = true;
            ObjectToonMaterials.CopyCommon(profile.commonSettings, entry.toon);
            _context.NotifyTableChanged();
            _context.AfterMaterialChanged(entry.toon);
        }

        private void RecreateToon(ObjectToonProfile profile, ObjectToonProfile.Entry entry)
        {
            Undo.RecordObject(profile, "トゥーン材質を作る");
            entry.toon = ObjectToonMaterials.CreateToon(entry.original, profile.commonSettings);
            AssetDatabase.SaveAssets();
            _context.NotifyTableChanged();
        }

        // ───────── シーンで選ぶ ─────────

        private static void SelectUsers(ObjectToonProfile.Entry entry)
        {
            var users = new List<Object>();
            ForEachLoadedRenderer(renderer =>
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && (material == entry.original || material == entry.toon))
                    {
                        users.Add(renderer.gameObject);
                        break;
                    }
                }
            });

            SelectOrTell(users, "開いているシーンに、この材質を使っている物はありません（対象のシーンを開いてから押してください）。");
        }

        private static void SelectByPath(ObjectToonProfile.ObjectOverride item)
        {
            var users = new List<Object>();
            ForEachLoadedRenderer(renderer =>
            {
                if (renderer.gameObject.scene.name == item.scene && ObjectToonApplier.GetPath(renderer.transform) == item.path)
                {
                    users.Add(renderer.gameObject);
                }
            });

            SelectOrTell(users, "その物が見つかりません。シーン「" + item.scene + "」を開いているか、物の名前や並びが変わっていないか確認してください。");
        }

        private static void ForEachLoadedRenderer(System.Action<Renderer> action)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        action(renderer);
                    }
                }
            }
        }

        private static void SelectOrTell(List<Object> users, string message)
        {
            if (users.Count == 0)
            {
                EditorUtility.DisplayDialog("3Dオブジェクト トゥーン", message, "OK");
                return;
            }

            Selection.objects = users.ToArray();
            EditorGUIUtility.PingObject(users[0]);
        }
    }
}
