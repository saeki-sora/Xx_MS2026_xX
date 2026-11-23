using System.Linq;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>音のファイルを落とせるようにする（行・「はじめに」の置き場所）。Project の中のファイルも、エクスプローラーのファイルも落とせる。</summary>
    public static class WavDrop
    {
        public static void Attach(VisualElement target, SoundStudioContext context, SoundSlot slot)
        {
            target.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (FirstSoundPath() == null)
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                target.AddToClassList("ss-drop--over");
            });
            target.RegisterCallback<DragLeaveEvent>(_ => target.RemoveFromClassList("ss-drop--over"));
            target.RegisterCallback<DragPerformEvent>(_ =>
            {
                target.RemoveFromClassList("ss-drop--over");
                var path = FirstSoundPath();
                if (path == null)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                SeImporter.Import(context, System.IO.Path.GetFullPath(path), slot);
            });
        }

        private static string FirstSoundPath()
        {
            return DragAndDrop.paths?.FirstOrDefault(SeImportNaming.IsSoundFile);
        }
    }

    /// <summary>「はじめに」の素材の置き場所（落とすと登録だけする）。</summary>
    public sealed class SoundDropZone : VisualElement
    {
        public SoundDropZone(SoundStudioContext context, SoundSlot slot)
        {
            AddToClassList("ss-drop");
            tooltip = "wav / mp3 / ogg / aiff をここに落とすと、D-Drive に新しい音として登録する（欄には入れない）。欄に入れたいときは、その行に落とす。";
            Add(StudioUi.Styled(new Label("ここに音のファイルを落とすと、D-Drive に登録します"), "ss-drop-label"));
            WavDrop.Attach(this, context, slot);
        }
    }
}
