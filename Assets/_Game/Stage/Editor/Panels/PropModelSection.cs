using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>見た目のモデル: 取り込み元・取り込み直し・別のモデルへの差し替え・マテリアルの切り替え。</summary>
    public sealed class PropModelSection : PropSection
    {
        private Label _materials;

        public PropModelSection() : base("モデル", "見た目の3Dモデル。差し替えても位置・役割・設定はそのまま残り、通れない範囲だけ作り直します。")
        {
        }

        protected override void Build(StageProp prop)
        {
            var source = new ObjectField("取り込み元") { objectType = typeof(GameObject), value = prop.sourceModel, tooltip = "この物を取り込んだ元のモデル（FBXやPrefab）。" };
            source.SetEnabled(false);
            Body.Add(source);
            if (!string.IsNullOrEmpty(prop.sourceNodePath))
            {
                Body.Add(StudioUi.Styled(new Label($"モデルの中の場所: {prop.sourceNodePath}"), "sk-muted"));
            }

            Body.Add(StudioUi.Row(
                StudioUi.Button("取り込み直す", Reimport, "Mayaでモデルを直してFBXを書き出し直したら押してください。今の位置のまま、新しい形に入れ替えます。", small: true)));

            var replace = new ObjectField("差し替えるモデル") { objectType = typeof(GameObject), allowSceneObjects = false, tooltip = "別のモデル（FBX・Prefab）を選んで「差し替える」を押すと、見た目だけ入れ替わります。" };
            Body.Add(replace);
            Body.Add(StudioUi.Row(StudioUi.Button("差し替える", () => Replace(replace.value as GameObject), "上で選んだモデルに入れ替えます（元に戻すは Ctrl+Z）。", small: true)));

            Body.Add(StudioUi.Section("マテリアル"));
            _materials = StudioUi.Styled(new Label(), "sk-muted");
            Body.Add(_materials);
            Body.Add(StudioUi.Row(
                StudioUi.Button("専用シェーダーに切り替え", ConvertMaterials, "透け・焦げ・光り・溶け・敵の影が出る専用シェーダーのコピーに替えます。色やテクスチャは引き継ぎます。元のマテリアルは変わりません。", small: true),
                StudioUi.Button("元に戻す", () => StageMaterialConverter.Revert(Prop), "取り込んだときの元のマテリアルに戻します。", small: true)));
        }

        public override void Refresh()
        {
            if (Prop == null || _materials == null)
            {
                return;
            }

            var unconverted = StageMaterialConverter.CountUnconverted(Prop);
            _materials.text = unconverted == 0
                ? "すべて専用シェーダーです。"
                : $"専用シェーダーでないマテリアルが {unconverted} 個あります（透け・焦げなどが出ません）。";
        }

        private void Reimport()
        {
            if (Prop.sourceModel == null)
            {
                EditorUtility.DisplayDialog("取り込み直せません", "取り込み元のモデルがわかりません。「差し替えるモデル」から選んでください。", "OK");
                return;
            }

            var model = StageModelImporter.InstantiateNode(Prop.sourceModel, Prop.sourceNodePath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("取り込み直せません", $"モデルの中に「{Prop.sourceNodePath}」が見つかりません。Mayaで名前を変えた場合は「差し替えるモデル」から選び直してください。", "OK");
                return;
            }

            Undo.RegisterCreatedObjectUndo(model, "取り込み直す");
            StagePropComposer.ReplaceModel(Prop, model, Prop.sourceModel, Prop.sourceNodePath);
            ConvertMaterials();
        }

        private void Replace(GameObject asset)
        {
            if (asset == null)
            {
                return;
            }

            var model = StageModelImporter.InstantiateNode(asset, string.Empty);
            Undo.RegisterCreatedObjectUndo(model, "モデルを差し替え");
            StagePropComposer.ReplaceModel(Prop, model, asset, string.Empty);
            ConvertMaterials();
            Bind(Prop);
        }

        private void ConvertMaterials()
        {
            var root = StageSceneService.FindRoot();
            StageMaterialConverter.Convert(Prop, StageAssetFactory.FolderOf(root != null ? root.current : null));
        }
    }
}
