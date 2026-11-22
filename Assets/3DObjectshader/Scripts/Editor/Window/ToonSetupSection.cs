using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 準備の欄（作る人向け・最初は閉じる）: ゲーム中にトゥーンにするか・対象のシーン・材質探し・2Dの絵。
    /// デザイナーさんがふだん触らない物をここにまとめる（資料 T2・T8）。
    /// </summary>
    public sealed class ToonSetupSection : Foldout
    {
        private const string OpenKey = "MS2026.ObjectToon.Window.SetupOpen";

        private readonly ToonContext _context;
        private readonly VisualElement _body;
        private MaterialEditor _spriteEditor;

        public ToonSetupSection(ToonContext context)
        {
            _context = context;
            text = "準備（作る人向け）";
            tooltip = "ゲーム中にトゥーンにするか・対象のシーン・材質探し・2Dの絵。ふだんは触らなくて大丈夫です。";
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

            _body = new VisualElement();
            Add(_body);
            context.TableChanged += Rebuild;
            Rebuild();
        }

        public void Dispose()
        {
            if (_spriteEditor != null)
            {
                Object.DestroyImmediate(_spriteEditor);
                _spriteEditor = null;
            }
        }

        private void Rebuild()
        {
            _body.Clear();
            var profile = _context.Profile;
            if (profile == null)
            {
                return;
            }

            _body.Add(ProfileToggle("ゲームを動かしたときにトゥーンにする", profile.enabledInPlay,
                "オフにすると、再生しても元の材質のままになります（見比べたいとき用）。",
                on => profile.enabledInPlay = on));

            _body.Add(StudioUi.Section("3Dの物をトゥーンにするシーン"));
            _body.Add(SceneList(profile, profile.targetScenes));
            _body.Add(StudioUi.Button("シーンの材質を探してトゥーン材質を作る（3D）", () => _context.AddSceneMaterials(sprite: false),
                "上のシーンが使っている材質を探して、まだ無い物のトゥーン材質を作ります。シーンを保存してから押してください。"));

            _body.Add(StudioUi.Section("2Dの絵"));
            _body.Add(StudioUi.Note("SpriteRenderer の絵（砲台・コア・壊せる物など）を、3Dの物のトゥーンに合わせます。群れの敵は別の描き方なので変わりません。" +
                                    "輪郭線は絵の四角の中にしか描けないので、端まで色がある絵は Sprite の Mesh Type を Full Rect にして余白をあけてください。", NoteKind.Info));
            _body.Add(ProfileToggle("2Dの絵もトゥーンにする", profile.spriteEnabled, "オンにすると、下のシーンの SpriteRenderer もトゥーンにします。",
                on => profile.spriteEnabled = on));
            _body.Add(SceneList(profile, profile.spriteScenes));
            _body.Add(StudioUi.Button("シーンの絵の材質を探してトゥーン材質を作る（2D）", () => _context.AddSceneMaterials(sprite: true),
                "上のシーンが使っている絵の材質を探して、まだ無い物のトゥーン材質を作ります。"));

            if (profile.spriteCommonSettings != null)
            {
                var fold = new Foldout { text = "2Dの絵の共通の値", value = false };
                fold.AddToClassList("tw-fold");
                fold.Add(new IMGUIContainer(DrawSpriteCommon));
                _body.Add(fold);
            }
        }

        private void DrawSpriteCommon()
        {
            var profile = _context.Profile;
            var material = profile != null ? profile.spriteCommonSettings : null;
            if (material == null)
            {
                return;
            }

            if (_spriteEditor == null || _spriteEditor.target != material)
            {
                Dispose();
                _spriteEditor = (MaterialEditor)Editor.CreateEditor(material, typeof(MaterialEditor));
            }

            if (_spriteEditor.PropertiesGUI())
            {
                ObjectToonMaterials.PushCommon(material, profile.spriteEntries);
            }
        }

        private VisualElement ProfileToggle(string label, bool current, string help, System.Action<bool> apply)
        {
            var toggle = new Toggle(label) { value = current, tooltip = help };
            toggle.AddToClassList("tw-setup-toggle");
            toggle.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_context.Profile, label);
                apply(e.newValue);
                _context.NotifyTableChanged();
            });
            return toggle;
        }

        private VisualElement SceneList(ObjectToonProfile profile, List<string> scenes)
        {
            var box = new VisualElement();
            for (int i = 0; i < scenes.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("tw-item");
                var field = new TextField { value = scenes[i], isDelayed = true, tooltip = "シーンの名前（ファイル名から .unity を除いた物）" };
                field.AddToClassList("tw-item-name");
                field.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(profile, "トゥーンにするシーン");
                    scenes[index] = e.newValue.Trim();
                    _context.NotifyTableChanged();
                    _context.RefreshMissing();
                });
                row.Add(field);
                row.Add(StudioUi.Button("外す", () =>
                {
                    Undo.RecordObject(profile, "トゥーンにするシーン");
                    scenes.RemoveAt(index);
                    _context.NotifyTableChanged();
                    _context.RefreshMissing();
                }, "このシーンを外します。", small: true));
                box.Add(row);
            }

            var active = EditorSceneManager.GetActiveScene().name;
            box.Add(StudioUi.Button("今のシーン（" + active + "）を追加", () =>
            {
                if (scenes.Contains(active))
                {
                    return;
                }

                Undo.RecordObject(profile, "トゥーンにするシーン");
                scenes.Add(active);
                _context.NotifyTableChanged();
                _context.RefreshMissing();
            }, "いま開いているシーンを追加します。", small: true));
            return box;
        }
    }
}
