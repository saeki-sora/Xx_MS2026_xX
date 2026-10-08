using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>役割（床・壁・壊せる壁・遅くなる地帯・飾り）の選択と、役割ごとの追加設定。</summary>
    public sealed class PropRoleSection : PropSection
    {
        private System.Action _refreshSegment;
        private VisualElement _explain;
        private VisualElement _extra;
        private StagePropRole _builtRole;

        // 項目名は要塞デザイナーの破壊可能物タブ（DestructibleSettingsDrawer）とそろえる。3Dの背景で意味のある物だけ出す。
        private static readonly (string title, (string path, string label)[] fields)[] DestructibleFields =
        {
            ("耐久・再生", new[]
            {
                ("settings.durability.maxHealth", "耐久値"),
                ("settings.durability.damageMultiplier", "受けるダメージの倍率"),
                ("settings.durability.selfRepairPerSecond", "自己修復の速さ(耐久/秒)"),
                ("settings.durability.selfRepairDelaySeconds", "自己修復が始まるまで(秒)"),
                ("settings.durability.regenerates", "壊れても再生する"),
                ("settings.durability.regenDelaySeconds", "再生までの時間(秒)"),
                ("settings.durability.regenHealthRatio", "再生時の耐久の割合")
            }),
            ("演出（エフェクト・効果音）", new[]
            {
                ("settings.feedback.onHit", "被ダメージ時"),
                ("settings.feedback.hitInterval", "被ダメージ演出の間隔(秒)"),
                ("settings.feedback.onDestroyed", "破壊されたとき"),
                ("settings.feedback.onRegenerated", "再生したとき"),
                ("settings.feedback.placeholderDebris", "仮の破片を出す（破壊時の演出が空のとき）")
            }),
            ("耐久ゲージ・壊れた後", new[]
            {
                ("settings.visual.healthBar.mode", "耐久ゲージの表示"),
                ("settings.visual.healthBar.offsetY", "ゲージの高さ位置"),
                ("settings.collision.leavesRubble", "壊れた後に瓦礫（通行コスト地帯）を残す"),
                ("settings.collision.rubbleSpeedMultiplier", "瓦礫の上の移動速度倍率")
            })
        };

        public PropRoleSection() : base("役割", "この物がゲームで果たす役割。敵の通り方とレーザーへの影響が変わります。")
        {
        }

        protected override void Build(StageProp prop)
        {
            var options = new (StagePropRole, string, string, string)[StageRoleStyle.All.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var role = StageRoleStyle.All[i];
                options[i] = (role, StageRoleStyle.Icon(role), role.DisplayName(), StageRoleStyle.Explain(role));
            }

            Body.Add(StudioUi.Segment(options, () => Prop != null ? Prop.role : StagePropRole.Wall, ChangeRole, out _refreshSegment));
            _explain = StudioUi.Note(StageRoleStyle.Explain(prop.role));
            Body.Add(_explain);
            _extra = new VisualElement();
            Body.Add(_extra);
            BuildExtra(prop);
        }

        public override void Refresh()
        {
            if (Prop == null)
            {
                return;
            }

            _refreshSegment?.Invoke();
            if (_builtRole != Prop.role)
            {
                StudioUi.SetNote(_explain, StageRoleStyle.Explain(Prop.role), NoteKind.Info);
                BuildExtra(Prop);
            }
        }

        private void ChangeRole(StagePropRole role)
        {
            if (Prop == null || Prop.role == role)
            {
                return;
            }

            if (!StagePropComposer.SetRole(Prop, role, message => EditorUtility.DisplayDialog("役割を変えられません", message, "OK")))
            {
                return;
            }

            if (role.NeedsFootprint() && !Prop.footprint.HasShape)
            {
                StagePropComposer.RebuildFootprint(Prop);
            }

            // 床は透けない・影を作らない専用のマテリアルにする（専用シェーダーを使っている物だけ）。
            if (StageMaterialConverter.CountUnconverted(Prop) == 0)
            {
                var root = StageSceneService.FindRoot();
                StageMaterialConverter.Convert(Prop, StageAssetFactory.FolderOf(root != null ? root.current : null));
            }

            Refresh();
        }

        private void BuildExtra(StageProp prop)
        {
            _builtRole = prop.role;
            _extra.Clear();
            _extra.Unbind();
            var so = new SerializedObject(prop);
            switch (prop.role)
            {
                case StagePropRole.Wall:
                    _extra.Add(Field(so, "laserPassesThrough", "レーザーは素通り"));
                    break;
                case StagePropRole.SlowZone:
                    _extra.Add(Field(so, "slowZoneSpeed", "敵の速さの倍率"));
                    _extra.Add(Field(so, "slowZoneAvoidance", "敵が避けたがる強さ"));
                    break;
                case StagePropRole.DestructibleWall:
                    BuildDestructible(prop);
                    break;
            }

            _extra.Bind(so);
        }

        /// <summary>壊せる壁の耐久・再生・演出（破壊可能物と同じ設定をここでも触れるようにする）。</summary>
        private void BuildDestructible(StageProp prop)
        {
            var obstacle = prop.GetComponent<DestructibleObstacle>();
            if (obstacle == null)
            {
                _extra.Add(StudioUi.Note("壊せる壁の部品がありません。役割をもう一度選び直してください。", NoteKind.Error));
                return;
            }

            var container = new VisualElement();
            var so = new SerializedObject(obstacle);
            foreach (var (title, fields) in DestructibleFields)
            {
                container.Add(StudioUi.Styled(new Label(title), "sk-section-title"));
                foreach (var (path, label) in fields)
                {
                    container.Add(Field(so, path, label));
                }
            }

            container.Bind(so);
            _extra.Add(container);

            _extra.Add(StudioUi.Row(
                StudioUi.Button("要塞デザイナーで詳しく", () => EditorApplication.ExecuteMenuItem("Tools/要塞/要塞デザイナーを開く"),
                    "ドロップ・連動（まとめて壊れる）・スマッシュボールなどは、要塞デザイナーの「破壊可能物」タブで設定します。", small: true),
                StudioUi.Button("壊してみる", () => obstacle.DestroyNow(), "Play中に、この壁を壊します（ネット対戦ではHostで押してください）。", small: true),
                StudioUi.Button("直す", () => obstacle.Regenerate(), "Play中に、この壁を元に戻します。", small: true)));
        }
    }
}
