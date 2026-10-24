using System.Collections.Generic;
using MS2026.CharacterSelect;
using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// 砲台にキャラの絵（<see cref="SkillTurretLook"/>）を出す（2026-10-22〜）。
    /// スキルを撃っている間（<see cref="PlayerSkillState.Active"/>）だけ発動中の絵にし、切り替わる瞬間にぽよんと弾ませ、発動中は小刻みに震わせる。
    /// <see cref="PlayerSkillInstaller"/> がスキル欄を付けるときに、砲台のキャラの絵の一覧（<see cref="TurretPortraitTable"/>）と一緒に自動で付ける（シーンの編集は要らない）。
    /// 絵は砲台の子「[SkillPortrait]」に出す。どの行を出すかは一覧の <see cref="TurretPortraitTable.Find"/>（キャラ選択の結果、無ければ席）。
    /// ・砲台と一緒に回る（既定）: 子を床に寝かせたまま砲台に付ける。砲台は狙う向きへ回る（transform.up = 狙う向き）ので、絵の上がいつも狙う向き。
    ///   出している間は、元の砲台の見た目（本体・砲身などの SpriteRenderer）を隠し、やめたら戻す。
    /// ・立てる: 子に <see cref="BillboardSprite"/> を付けてカメラの方へ立て、向きは毎フレーム「画面の上 = 絵の上」に戻す
    ///   （砲台自身の絵を立てると、砲台と一緒に回って横倒しの板のように見えるため）。
    /// 各PCが自分で表示するので通信は要らない。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class SkillTurretPortrait : MonoBehaviour
    {
        private const string ChildName = "[SkillPortrait]";
        private const int SortingOrder = 10;
        // 砲台の仮の見た目を作るツール（TurretVisualFactory）が付ける砲身の名前。
        private const string BarrelName = "Barrel";
        // 立てるとき、立たせる仕組み（ビルボード）が新しい絵を拾うまで（0.5 秒ごとに集め直す）、寝た絵が一瞬見えないように隠しておく上限。
        private const float WaitForBillboardSeconds = 1f;

        private readonly List<SpriteRenderer> _hiddenParts = new List<SpriteRenderer>();
        private PlayerSkillSlot _slot;
        private TurretPortraitTable _table;
        private SpriteRenderer _renderer;
        private bool _standing;
        private Sprite _shown;
        private float _sincePop = float.MaxValue;
        private float _sinceCreated;

        /// <summary>砲台にこの係を付ける（もう付いていれば一覧だけ差し替える）。table = 砲台のキャラの絵の一覧。</summary>
        internal static void Ensure(PlayerSkillSlot slot, TurretPortraitTable table)
        {
            if (slot == null)
            {
                return;
            }

            if (!slot.TryGetComponent(out SkillTurretPortrait portrait))
            {
                portrait = slot.gameObject.AddComponent<SkillTurretPortrait>();
            }

            portrait._slot = slot;
            portrait._table = table;
        }

        private void LateUpdate()
        {
            var look = FindLook();
            if (look == null)
            {
                Hide();
                return;
            }

            EnsureRenderer(!look.rotateWithTurret);
            var active = _slot.State == PlayerSkillState.Active;
            var wanted = active && look.activeSprite != null ? look.activeSprite : look.normalSprite;
            if (wanted != _shown)
            {
                // 最初に出したときは弾ませない（試合開始でいきなり弾まないように）。
                _sincePop = _shown != null ? 0f : float.MaxValue;
                _shown = wanted;
                _renderer.sprite = wanted;
            }
            else if (_sincePop < float.MaxValue)
            {
                _sincePop += Time.deltaTime;
            }

            _sinceCreated += Time.deltaTime;
            _renderer.enabled = !_standing || IsStanding() || _sinceCreated >= WaitForBillboardSeconds;
            SetTurretPartsHidden(look.hideTurretParts);

            // 砲台の拡大率に関係なく「高さ look.height m」で出す（子の拡大率を打ち消し、size・ずれをメートルで指定する）。
            var child = _renderer.transform;
            var parentScale = transform.lossyScale;
            var sx = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
            var sy = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
            child.localScale = new Vector3(1f / sx, 1f / sy, 1f);
            if (_standing)
            {
                var screenUp = ScreenUpAngle();
                var rotation = Quaternion.Euler(0f, 0f, screenUp + look.rotationOffset);
                child.SetPositionAndRotation(transform.position + Quaternion.Euler(0f, 0f, screenUp) * (Vector3)look.offset, rotation);
            }
            else
            {
                // 砲台の子のまま回る: 絵の上 = 砲台の上（狙う向き）。
                child.localPosition = new Vector3(look.offset.x / sx, look.offset.y / sy, 0f);
                child.localRotation = Quaternion.Euler(0f, 0f, look.rotationOffset);
            }

            var rect = wanted.rect;
            var aspect = rect.height > 0f ? rect.width / rect.height : 1f;
            _renderer.size = new Vector2(look.height * aspect, look.height) * look.ScaleAt(_sincePop, active, Time.time);
            _renderer.color = look.tint;
        }

        // 一覧から、この砲台に出す行を探す。キャラ選択を通ったときは選んだキャラで、通っていないときは「キャラ選択なしのときに出す人」で決める。
        private SkillTurretLook FindLook()
        {
            if (_slot == null || _table == null)
            {
                return null;
            }

            var hasSelection = CharacterSelectionResult.HasResult;
            var chosen = hasSelection ? CharacterSelectionResult.Get(_slot.PlayerIndex) : null;
            return _table.Find(_slot.PlayerIndex, hasSelection, chosen != null ? chosen.id : null);
        }

        private void EnsureRenderer(bool standing)
        {
            if (_renderer != null && _standing != standing)
            {
                // 置き方を変えたら作り直す（立てる用の BillboardSprite の付け外しを、ビルボードの仕組みに任せず確実にするため）。
                Destroy(_renderer.gameObject);
                _renderer = null;
                _shown = null;
            }

            if (_renderer != null)
            {
                _renderer.gameObject.SetActive(true);
                return;
            }

            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            if (standing)
            {
                go.AddComponent<BillboardSprite>();
            }

            _renderer = go.AddComponent<SpriteRenderer>();
            // 枠なしのスライス表示 = size で好きな大きさに伸ばせる（絵は Full Rect で取り込んである）。
            _renderer.drawMode = SpriteDrawMode.Sliced;
            _renderer.sortingOrder = SortingOrder;
            _renderer.enabled = !standing;
            _standing = standing;
            _sinceCreated = 0f;
        }

        // 元の砲台の見た目（砲台自身の絵と、子「Barrel」（砲身）の絵）を隠す／戻す。隠す前に消えていたものはそのまま。
        // スキルの演出など、ほかの子の絵には触らない。
        private void SetTurretPartsHidden(bool hidden)
        {
            if (!hidden)
            {
                RestoreTurretParts();
                return;
            }

            if (_hiddenParts.Count > 0)
            {
                return;
            }

            if (TryGetComponent(out SpriteRenderer body))
            {
                HidePart(body);
            }

            var barrel = transform.Find(BarrelName);
            if (barrel != null)
            {
                foreach (var part in barrel.GetComponentsInChildren<SpriteRenderer>(false))
                {
                    HidePart(part);
                }
            }
        }

        private void HidePart(SpriteRenderer part)
        {
            if (part == null || part == _renderer || !part.enabled)
            {
                return;
            }

            part.enabled = false;
            _hiddenParts.Add(part);
        }

        private void RestoreTurretParts()
        {
            foreach (var part in _hiddenParts)
            {
                if (part != null)
                {
                    part.enabled = true;
                }
            }

            _hiddenParts.Clear();
        }

        // ビルボード用のマテリアルに替わっていれば、立って見えている。
        private bool IsStanding()
        {
            var material = _renderer.sharedMaterial;
            return material != null && material.shader != null && material.shader.name.Contains("Billboard");
        }

        // このPCのカメラで「画面の上」が地面のどちらか（Z 回りの角度）。カメラが無ければ 0（ワールドの +Y が上）。
        private static float ScreenUpAngle()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return 0f;
            }

            var up = cam.transform.up;
            var ground = new Vector2(up.x, up.y);
            if (ground.sqrMagnitude < 1e-8f)
            {
                return 0f;
            }

            return Mathf.Atan2(-ground.x, ground.y) * Mathf.Rad2Deg;
        }

        private void Hide()
        {
            if (_renderer != null)
            {
                _renderer.gameObject.SetActive(false);
            }

            RestoreTurretParts();
            _shown = null;
            _sincePop = float.MaxValue;
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (_renderer != null)
            {
                Destroy(_renderer.gameObject);
            }
        }
    }
}
