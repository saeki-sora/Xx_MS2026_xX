using System;
using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>コアのページ: どのモデルをコアにするか、置き方（幅・向き・高さ）、元の丸い絵に戻す。</summary>
    public static class CoreModelPanel
    {
        public static VisualElement Build(CoreCrystalController core, CoreToothModel holder, Action changed)
        {
            var root = new VisualElement();
            var card = StudioUi.Card();
            card.Add(StudioUi.Section("見た目のモデル"));
            var model = holder != null && holder.transform.childCount > 0 ? holder.transform.GetChild(0).name : null;
            card.Add(StudioUi.Row(StudioUi.Chip(model != null ? $"今：3Dモデル「{model}」" : "今：元の丸い絵", model != null ? ChipKind.Ok : ChipKind.Plain)));

            var field = new ObjectField("コアにするモデル")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false,
                value = holder != null ? holder.sourceModel : null,
                tooltip = "FBX か Prefab を入れます。Maya で作った歯なら、そのまま入れて大丈夫です（向きは自動で起こします）。"
            };
            card.Add(field);
            var applyText = holder != null ? "このモデルに入れ替える" : "このモデルをコアにする";
            card.Add(StudioUi.Row(
                StudioUi.Button(applyText, () =>
                {
                    if (field.value is GameObject asset)
                    {
                        CoreToothSetup.Apply(core, asset);
                        changed();
                    }
                }, "向き・大きさ・床の高さを自動で合わせ、歯のトゥーンの材質にします（元に戻すは Ctrl+Z）。", primary: true),
                StudioUi.Spacer(),
                holder != null
                    ? StudioUi.Button("元の丸い絵に戻す", () => { CoreToothSetup.RevertToSprite(core); changed(); }, "3Dモデルを外して、元の丸い絵を出します（元に戻すは Ctrl+Z）。", small: true)
                    : new VisualElement()));
            root.Add(card);

            if (holder != null)
            {
                root.Add(BuildPlacement(holder));
            }

            return root;
        }

        private static VisualElement BuildPlacement(CoreToothModel holder)
        {
            var card = StudioUi.Card();
            card.Add(StudioUi.Section("置き方", "値を変えると、すぐにモデルを置き直します。"));
            var so = new SerializedObject(holder);
            card.Add(Field(so, "widthMeters", "幅（m）"));
            card.Add(Field(so, "yawDegrees", "床の上での向き（度）"));
            card.Add(Field(so, "mayaUp", "Maya の向きを床に合わせる"));
            card.Add(Field(so, "sinkMeters", "床にめり込ませる深さ（m）"));

            card.Add(StudioUi.Section("位置のずれと傾き", "見た目だけ動きます。敵が当たる場所（コアの位置）は変わりません。"));
            card.Add(Field(so, "offsetX", "左右のずれ（m・＋で右）"));
            card.Add(Field(so, "offsetY", "上下のずれ（m・＋で画面の上）"));
            card.Add(Field(so, "tiltForward", "前後の傾き（度・＋で上へ倒れる）"));
            card.Add(Field(so, "tiltSide", "左右の傾き（度・＋で右へ倒れる）"));

            card.Add(StudioUi.Section("どの席から見ても同じ向き", "ON なら、2P〜4P の画面でも 1P の画面と同じ向きに見えます（席替えのあとも）。上下・左右・前後は 1P の画面での向きです。"));
            card.Add(Field(so, "sameViewForEveryone", "全員 1P と同じ見え方にする"));

            card.Add(StudioUi.Row(
                StudioUi.Button("置き直す", () => CoreToothSetup.Refit(holder), "今の値で、モデルを置き直します。", small: true),
                StudioUi.Button("ずれと傾きを 0 に戻す", () =>
                {
                    Undo.RecordObject(holder, "ずれと傾きを 0 に戻す");
                    holder.offsetX = 0f;
                    holder.offsetY = 0f;
                    holder.tiltForward = 0f;
                    holder.tiltSide = 0f;
                    EditorUtility.SetDirty(holder);
                    so.Update();
                    CoreToothSetup.Refit(holder);
                }, "位置のずれと傾きだけを 0 にします（幅・向き・めり込みはそのまま）。", small: true)));
            card.Add(StudioUi.Note("スライダーの横の数字の欄には、細かい値（例: 0.05）を直接打ち込めます。幅は、まっすぐ立てたときの床の上の縦横の長い方です（回したり傾けたりしても大きさは変わりません）。今の丸い絵は幅 2.5m。当たったときの赤い輪は、歯が大きいと自動で外へ広がります。", NoteKind.Info));
            card.Bind(so);
            card.TrackSerializedObjectValue(so, _ => CoreToothSetup.Refit(holder));
            return card;
        }

        private static PropertyField Field(SerializedObject so, string path, string label)
        {
            var property = so.FindProperty(path);
            return new PropertyField(property, label) { tooltip = property?.tooltip };
        }
    }
}
