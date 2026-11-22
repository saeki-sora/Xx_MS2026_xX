using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 調整ウィンドウの状態: 設定ファイル・いま調整している材質（ふだんは「共通の設定」）・トゥーンになっていない材質。
    /// 欄（見た目・物ごと・準備）はここを通して値を書き、変わったことをイベントで知る。
    /// </summary>
    public sealed class ToonContext
    {
        private Material _target;
        private readonly List<Material> _missing = new List<Material>();

        public event Action ValuesChanged;
        public event Action TargetChanged;
        public event Action TableChanged;

        public ObjectToonProfile Profile => ObjectToonMaterials.LoadProfile();

        public Material Common => Profile != null ? Profile.commonSettings : null;

        /// <summary>いま調整している材質（消されていたら共通の設定に戻る）。</summary>
        public Material Target
        {
            get
            {
                if (_target == null)
                {
                    _target = Common;
                    TargetLabel = CommonLabel;
                }

                return _target;
            }
        }

        public const string CommonLabel = "全部の物（共通の設定）";

        public string TargetLabel { get; private set; } = CommonLabel;

        public bool IsCommon => Target != null && Target == Common;

        /// <summary>変えた所を比べる相手。共通の設定を調整中は null（シェーダーの初期値と比べる）。</summary>
        public Material Baseline => IsCommon ? null : Common;

        public IReadOnlyList<Material> Missing => _missing;

        public void UseCommon()
        {
            _target = Common;
            TargetLabel = CommonLabel;
            TargetChanged?.Invoke();
        }

        /// <summary>材質ごとの表の1行を調整する（その材質を使う物すべて）。共通に合わせるのはやめる。</summary>
        public void UseEntry(ObjectToonProfile.Entry entry)
        {
            if (entry == null || entry.toon == null)
            {
                return;
            }

            if (entry.useCommon)
            {
                Undo.RecordObject(Profile, "この材質の物を調整");
                entry.useCommon = false;
                EditorUtility.SetDirty(Profile);
            }

            _target = entry.toon;
            TargetLabel = "「" + entry.original.name + "」の物すべて";
            TargetChanged?.Invoke();
            TableChanged?.Invoke();
        }

        /// <summary>1つの物だけの材質を調整する。</summary>
        public void UseOverride(ObjectToonProfile.ObjectOverride item)
        {
            if (item == null || item.toon == null)
            {
                return;
            }

            _target = item.toon;
            var name = item.path.Substring(item.path.LastIndexOf('/') + 1);
            TargetLabel = "「" + name + "」だけ";
            TargetChanged?.Invoke();
        }

        /// <summary>調整中の材質の値を変える（Undo に残し、共通の設定なら仲間に配り、すぐ描き直す）。</summary>
        public void Write(Action<Material> change, string undoName)
        {
            var material = Target;
            if (material == null)
            {
                return;
            }

            Undo.RecordObject(material, undoName);
            change(material);
            EditorUtility.SetDirty(material);
            AfterMaterialChanged(material);
        }

        /// <summary>材質の値がどこかで変わったあとに呼ぶ（輪郭線のパス・共通の配布・描き直し）。</summary>
        public void AfterMaterialChanged(Material material)
        {
            ObjectToonMaterials.SyncOutlinePass(material);
            ObjectToonMaterials.PushIfCommon(Profile, material);
            ObjectToonMaterials.RepaintViews();
            ValuesChanged?.Invoke();
        }

        /// <summary>Undo・ほかの画面で値が変わったとき（共通の設定を配り直して、表示を合わせる）。</summary>
        public void OnExternalChange()
        {
            var profile = Profile;
            if (profile != null)
            {
                ObjectToonMaterials.PushCommon(profile.commonSettings, profile.entries);
                ObjectToonMaterials.PushCommon(profile.spriteCommonSettings, profile.spriteEntries);
            }

            ValuesChanged?.Invoke();
            TableChanged?.Invoke();
        }

        /// <summary>表（材質ごと・物ごと・シーン）を変えたあとに呼ぶ。</summary>
        public void NotifyTableChanged()
        {
            EditorUtility.SetDirty(Profile);
            ObjectToonEditPreview.Refresh();
            TableChanged?.Invoke();
        }

        /// <summary>対象のシーンが使っているのに、まだ表に無いステージの物の材質を調べ直す（保存されたシーンのファイルから）。</summary>
        public void RefreshMissing()
        {
            _missing.Clear();
            var profile = Profile;
            if (profile == null)
            {
                return;
            }

            foreach (var sceneName in profile.targetScenes)
            {
                var scenePath = ObjectToonMaterials.FindScenePath(sceneName);
                if (scenePath == null)
                {
                    continue;
                }

                foreach (var material in ObjectToonMaterials.FindSourceMaterials(scenePath, sprite: false))
                {
                    if (material.shader.name == ObjectToonMaterials.SourceShaderNames[0] &&
                        profile.FindByOriginal(profile.entries, material) == null && !_missing.Contains(material))
                    {
                        _missing.Add(material);
                    }
                }
            }

            TableChanged?.Invoke();
        }

        /// <summary>シーンの材質を探してトゥーン材質を作り、結果を知らせる。</summary>
        public void AddSceneMaterials(bool sprite)
        {
            var report = ObjectToonMaterials.AddSceneMaterials(Profile, sprite);
            RefreshMissing();
            NotifyTableChanged();
            EditorUtility.DisplayDialog("3Dオブジェクト トゥーン", report, "OK");
        }
    }
}
