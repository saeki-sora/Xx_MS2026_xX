using System;
using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// スタジオ上の帯の右側: ステージの選択／新規作成／シーンに出す／ステージに保存／シーンビューの表示切り替え。
    /// </summary>
    public sealed class StageStudioHeader : VisualElement
    {
        private readonly StageStudioContext _context;
        private readonly Action _onStageChanged;
        private readonly PopupField<StageSet> _picker;
        private readonly Label _status;
        private readonly Button _place;
        private readonly Button _save;
        private readonly Toggle _footprints;
        private readonly Toggle _gaps;
        private List<StageSet> _stages = new List<StageSet>();
        private double _nextStageScan;

        public StageStudioHeader(StageStudioContext context, Action onStageChanged)
        {
            _context = context;
            _onStageChanged = onStageChanged;
            AddToClassList("sk-row");

            _footprints = new Toggle("輪郭") { tooltip = "シーンビューに、敵が通れない範囲（オレンジ）・遅くなる地帯（水色）を表示します。" };
            _footprints.RegisterValueChangedCallback(e => _context.ShowFootprints = e.newValue);
            _gaps = new Toggle("すき間") { tooltip = "シーンビューに、背景どうしのすき間の幅を表示します（赤＝敵が通れない、黄＝敵より狭い、緑＝通れる）。" };
            _gaps.RegisterValueChangedCallback(e => _context.ShowGaps = e.newValue);
            _footprints.style.marginRight = 8;
            _gaps.style.marginRight = 12;
            Add(_footprints);
            Add(_gaps);

            _picker = new PopupField<StageSet>(new List<StageSet> { null }, 0, FormatStage, FormatStage)
            {
                tooltip = "編集するステージ。選ぶと「シーンに出す」でシーンの背景が入れ替わります。"
            };
            _picker.style.minWidth = 170;
            _picker.RegisterValueChangedCallback(e => OnPicked(e.newValue));
            Add(_picker);

            Add(StudioUi.Button("＋ 新しいステージ", CreateStage, "新しい空のステージ（StageSet と Prefab）を作り、シーンに出します。", small: true));

            _status = StudioUi.Chip("", ChipKind.Plain);
            _status.style.marginLeft = 8;
            Add(_status);

            _place = StudioUi.Button("シーンに出す", PlaceSelected, "選んでいるステージの背景をシーンに置きます（前の背景はシーンから外れます。ステージのPrefabは消えません）。", primary: true, small: true);
            _save = StudioUi.Button("ステージに保存", Save, "シーンで直した背景を、ステージ（Prefab）へ書き戻します。シーンを保存するだけでは入りません。", small: true);
            Add(_place);
            Add(_save);
        }

        public void Refresh()
        {
            if (EditorApplication.timeSinceStartup >= _nextStageScan)
            {
                _nextStageScan = EditorApplication.timeSinceStartup + 2.0;
                _stages = StageAssetFactory.FindAllStageSets();
                var choices = new List<StageSet>(_stages);
                if (choices.Count == 0)
                {
                    choices.Add(null);
                }

                _picker.choices = choices;
            }

            var current = _context.Stage;
            if (_picker.value != current && (current != null || _stages.Count == 0))
            {
                _picker.SetValueWithoutNotify(current);
            }

            _footprints.SetValueWithoutNotify(_context.ShowFootprints);
            _gaps.SetValueWithoutNotify(_context.ShowGaps);

            var root = _context.Root;
            var placed = root != null && root.instance != null && root.current == _picker.value;
            var unsaved = StageSceneService.HasUnsavedChanges(root);
            _status.text = root == null ? "置き場所なし" : !placed ? "シーンに出ていません" : unsaved ? "未保存の変更あり" : "シーンに出ています";
            StudioUi.SetChipKind(_status, root == null ? ChipKind.Error : !placed ? ChipKind.Warn : unsaved ? ChipKind.Accent : ChipKind.Ok);
            _status.tooltip = unsaved
                ? "シーンの背景にステージ（Prefab）へ保存していない変更があります。「ステージに保存」で書き戻します。"
                : "今のシーンの背景の状態です。";

            _place.SetEnabled(!Application.isPlaying && _picker.value != null && !placed);
            _save.SetEnabled(!Application.isPlaying && unsaved);
        }

        private void OnPicked(StageSet stage)
        {
            if (stage != null && _context.Root != null && _context.Root.instance == null)
            {
                PlaceSelected();
            }
        }

        private void PlaceSelected()
        {
            var stage = _picker.value;
            if (stage == null)
            {
                return;
            }

            var root = _context.Root != null ? _context.Root : StageSceneService.CreateRoot();
            if (StageSceneService.HasUnsavedChanges(root) &&
                !EditorUtility.DisplayDialog("ステージを入れ替えます",
                    $"今の「{(root.current != null ? root.current.Label : "")}」に、ステージへ保存していない変更があります。\n入れ替えると、その変更はシーンから消えます。", "入れ替える", "やめる"))
            {
                return;
            }

            StageSceneService.PlaceStage(root, stage);
            _context.RecheckNow(); // 作ったばかりの置き場所を、ページを作り直す前に読み直す
            _onStageChanged?.Invoke();
        }

        private void CreateStage()
        {
            var name = $"ステージ{StageAssetFactory.FindAllStageSets().Count + 1}";
            var stage = StageAssetFactory.CreateStage(name);
            _nextStageScan = 0;
            Refresh();
            _picker.SetValueWithoutNotify(stage);
            var root = _context.Root != null ? _context.Root : StageSceneService.CreateRoot();
            StageSceneService.PlaceStage(root, stage);
            _context.RecheckNow();
            EditorGUIUtility.PingObject(stage);
            _onStageChanged?.Invoke();
        }

        private void Save()
        {
            if (StageSceneService.SaveToPrefab(_context.Root))
            {
                _context.RecheckNow();
            }
        }

        private static string FormatStage(StageSet stage) => stage != null ? stage.Label : "（ステージがありません）";
    }
}
