using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景オブジェクトの詳細の1区画（役割・通れない範囲・反応・モデル…）。
    /// 選ばれた物が変わると <see cref="Bind"/>、表示中は定期的に <see cref="Refresh"/> が呼ばれる。
    /// </summary>
    public abstract class PropSection
    {
        protected PropSection(string title, string tooltip)
        {
            Root = new VisualElement();
            Root.Add(StudioUi.Section(title, tooltip));
            Body = StudioUi.Card();
            Root.Add(Body);
        }

        public VisualElement Root { get; }

        protected VisualElement Body { get; }

        protected StageProp Prop { get; private set; }

        public void Bind(StageProp prop)
        {
            Prop = prop;
            Body.Clear();
            Body.Unbind();
            if (prop != null)
            {
                Build(prop);
            }
        }

        public virtual void Refresh()
        {
        }

        protected abstract void Build(StageProp prop);

        /// <summary>SerializedObject の項目を、日本語の名前と説明（ツールチップ）付きで並べる。</summary>
        protected static PropertyField Field(SerializedObject so, string path, string label)
        {
            var property = so.FindProperty(path);
            return new PropertyField(property, label) { tooltip = property?.tooltip };
        }
    }
}
