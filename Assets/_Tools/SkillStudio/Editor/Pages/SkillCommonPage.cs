using MS2026.Skills;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 共通のページ: 4つのスキルの一覧（溜まり方のまとめ・開くボタン）と、Play 中に全員をまとめて試すボタン。
    /// </summary>
    public sealed class SkillCommonPage : IStudioPage
    {
        private readonly SkillStudioContext _context;
        private readonly StudioShell _shell;
        private readonly System.Func<SkillColor, IStudioPage> _pageOf;
        private VisualElement _root;
        private VisualElement _cards;
        private VisualElement _playRow;
        private string _lastSignature;

        public SkillCommonPage(SkillStudioContext context, StudioShell shell, System.Func<SkillColor, IStudioPage> pageOf)
        {
            _context = context;
            _shell = shell;
            _pageOf = pageOf;
        }

        public string Icon => "共";

        public string Label => "共通";

        public string Tooltip => "4つのスキルの一覧と、全員まとめての試し撃ち。";

        public int Badge => 0;

        public bool BadgeIsWarning => false;

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.Add(StudioUi.PageTitle("スキル調整スタジオ"));
            _root.Add(StudioUi.Lead("左で色を選ぶと、そのキャラのスキルの「レベルデザイン」と「見た目」を調整できます。値は設定ファイルにすぐ入り、Play 中ならその場で変わります。"));

            _playRow = StudioUi.Row(
                StudioUi.Button("全員を使えるようにする", () => ForEachSlot(s => s.GrantCharge()), "Play 中: 全員のスキルを溜まった状態にする。", small: true),
                StudioUi.Button("全員今すぐ撃つ", () => ForEachSlot(SkillStudioContext.FireNow), "Play 中: 全員のスキルを今すぐ撃つ。", primary: true, small: true),
                StudioUi.Button("全員止める", () => ForEachSlot(s => s.CancelActive()), "Play 中: 撃っている最中のスキルを全部止める。", small: true));
            _playRow.style.marginTop = 8;
            _playRow.style.marginBottom = 10;
            _root.Add(_playRow);

            _root.Add(StudioUi.Section("4つのスキル"));
            _cards = new VisualElement();
            _root.Add(_cards);
            return _root;
        }

        public void Refresh()
        {
            if (_root == null)
            {
                return;
            }

            _playRow.SetEnabled(Application.isPlaying);
            var signature = "";
            foreach (var color in SkillColors.All)
            {
                var skill = _context.SkillOf(color);
                signature += skill != null ? $"{skill.GetInstanceID()}:{skill.chargeBySmashBall}:{skill.chargeByKills}:{skill.killsToCharge}:{skill.countOwnSkillKills};" : "-;";
            }

            if (signature == _lastSignature)
            {
                return;
            }

            _lastSignature = signature;
            _cards.Clear();
            foreach (var color in SkillColors.All)
            {
                var target = color;
                var skill = _context.SkillOf(color);
                var body = skill == null
                    ? "設定ファイルが見つかりません。"
                    : $"{skill.DisplayName}\n溜まり方: {ChargeSummary(skill)}";
                var card = StudioUi.ClickCard($"{SkillColors.Label(color)}のスキル", body, () => _shell.Show(_pageOf(target)),
                    "押すとこのスキルのページを開く。");
                card.style.borderLeftWidth = 4;
                card.style.borderLeftColor = SkillColors.Tint(color);
                card.style.marginBottom = 6;
                _cards.Add(card);
            }
        }

        private static string ChargeSummary(CharacterSkill skill)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (skill.chargeByKills)
            {
                parts.Add($"敵を{skill.killsToCharge}体倒す" + (skill.countOwnSkillKills ? "（スキルで倒した分も数える）" : "（スキルで倒した分は数えない）"));
            }

            if (skill.chargeBySmashBall)
            {
                parts.Add("スマッシュボールを割る");
            }

            return parts.Count > 0 ? string.Join(" ／ ", parts) : "溜まらない（どちらも OFF）";
        }

        private static void ForEachSlot(System.Action<PlayerSkillSlot> action)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (var slot in PlayerSkillSlot.All)
            {
                if (slot != null)
                {
                    action(slot);
                }
            }
        }
    }
}
