using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯のコア（<see cref="CoreToothModel"/> の下の3Dモデル）を HP に合わせて変える（見た目だけ）。
    /// HP が減るほど黄ばみのしみが広がり、ふちの光が赤く速く脈打ち、ひびが入る。ダメージの瞬間は白く光る。
    /// 値は毎フレーム MaterialPropertyBlock で歯の材質（MS2026/Fortress/CoreTooth）に入れる。
    /// HP は Host の値が Client にも届く（CoreCrystalController.ApplyReplicatedHealth）ので、通信せずに全員の画面で同じように変わる。
    /// 編集中も動かして、ステージ背景スタジオ・要塞デザイナーの「HP を試す」（<see cref="CoreDangerLook.PreviewHealth01"/>）を見られるようにする。
    /// 向き: どの席から見ても 1P の席と同じ見え方になるように、入れ物をカメラの画面の回転だけ回す（<see cref="CoreToothModel.sameViewForEveryone"/>）。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreCrystalController))]
    [DefaultExecutionOrder(1010)] // カメラのリグ（1000）がそのフレームの向きを決めたあとに、歯の向きを合わせる。
    public sealed class CoreToothLook : MonoBehaviour
    {
        private static readonly int StateId = Shader.PropertyToID("_ToothState");
        private static readonly int LookId = Shader.PropertyToID("_ToothLook");
        private static readonly int NoiseId = Shader.PropertyToID("_ToothNoise");
        private static readonly int PulseId = Shader.PropertyToID("_ToothPulse");
        private static readonly int FrameId = Shader.PropertyToID("_ToothFrame");
        private static readonly int UpId = Shader.PropertyToID("_ToothUp");
        private static readonly int GlowColorId = Shader.PropertyToID("_ToothGlowColor");
        private static readonly int HurtColorId = Shader.PropertyToID("_ToothHurtColor");
        private static readonly int CrackColorId = Shader.PropertyToID("_ToothCrackColor");
        private static readonly int InkColorId = Shader.PropertyToID("_ToothInkColor");
        private static readonly int StainColorId = Shader.PropertyToID("_ToothStainColor");
        private static readonly int StainDeepColorId = Shader.PropertyToID("_ToothStainDeepColor");

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private CoreCrystalController _core;
        private CoreDirtVisual _dirt;
        private CoreToothModel _model;
        private MaterialPropertyBlock _block;
        private float _lastHealth = -1f;
        private float _lastHitTime = -100f;

        /// <summary>ダメージを受けたときと同じ白い光を出す（試し用）。</summary>
        public void PlayHit()
        {
            _lastHitTime = Now;
        }

        /// <summary>歯のモデルを置き換えたあとに呼ぶ（見た目の部品を集め直す）。</summary>
        public void Rebind()
        {
            _model = GetComponentInChildren<CoreToothModel>(true);
            if (_model != null)
            {
                _model.CollectRenderers(_renderers);
            }
            else
            {
                _renderers.Clear();
            }
        }

        /// <summary>
        /// 編集中のプレビュー（ステージ背景スタジオの「ゲームと同じ見え方で見る」）が見ている席の画面の回転（度）。
        /// 見ていないときは NaN（編集中は歯を回さない＝シーンに回した値を残さない）。
        /// </summary>
        public static float EditorPreviewRoll { get; set; } = float.NaN;

        /// <summary>今このPCのカメラの画面の回転（度。1P の席と全体は 0）。揺れなどの一時的な演出は含めない。</summary>
        public static float ViewRoll()
        {
            if (Application.isPlaying)
            {
                var rig = Cameras.FortressCameraRig.Active;
                return rig != null ? rig.BaseView.rollDegrees : 0f;
            }

            return float.IsNaN(EditorPreviewRoll) ? 0f : EditorPreviewRoll;
        }

        /// <summary>シーンの中の歯を、今のカメラの向きに合わせて すぐ回し直す（プレビューの切り替え・保存の前に使う）。</summary>
        public static void RefreshFacingAll()
        {
            foreach (var look in FindObjectsByType<CoreToothLook>(FindObjectsSortMode.None))
            {
                look.ApplyFacing();
            }
        }

        private static float Now => Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;

        private void OnEnable()
        {
            _core = GetComponent<CoreCrystalController>();
            _dirt = GetComponent<CoreDirtVisual>();
            _block ??= new MaterialPropertyBlock();
            _core.OnHealthChanged += HandleHealthChanged;
            Rebind();
        }

        private void OnDisable()
        {
            if (_core != null)
            {
                _core.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastHealth >= 0f && current < _lastHealth - 0.0001f)
            {
                _lastHitTime = Now;
            }

            _lastHealth = current;
        }

        private void LateUpdate()
        {
            if (_model == null || _renderers.Count == 0 || _renderers[0] == null)
            {
                Rebind();
                if (_renderers.Count == 0)
                {
                    return;
                }
            }

            ApplyFacing();

            var gem = _core.gemLook ?? new CoreGemLookSettings();
            var tooth = _core.toothLook ?? new CoreToothLookSettings();
            var health01 = Health01();
            var on = tooth.enabled;
            var damage = on ? CoreGemMath.Damage(health01) : 0f;
            var crack = on ? CoreGemMath.CrackAmount(health01, gem.crackStartHealth01) : 0f;
            var flash = on ? CoreGemMath.HitFlash(Now - _lastHitTime, gem.hitFlashSeconds) : 0f;
            var dirt = on ? (_dirt != null ? _dirt.DirtAt(health01) : 1f - health01) : 0f;

            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_block);
                _block.SetVector(StateId, new Vector4(health01, damage, crack, flash));
                _block.SetVector(LookId, new Vector4(dirt, on ? tooth.rimStrength : 0f, tooth.rimWidth, tooth.rimSoftness));
                _block.SetVector(NoiseId, new Vector4(tooth.stainScale, tooth.stainFromRoot, tooth.crackScale, tooth.crackWidth));
                _block.SetVector(PulseId, new Vector4(gem.calmPulsePerSecond, gem.hurtPulsePerSecond, 0f, 0f));
                SetFrame(renderer);
                _block.SetColor(GlowColorId, gem.glowColor);
                _block.SetColor(HurtColorId, gem.hurtColor);
                _block.SetColor(CrackColorId, gem.crackColor);
                _block.SetColor(InkColorId, tooth.inkColor);
                _block.SetColor(StainColorId, tooth.stainColor);
                _block.SetColor(StainDeepColorId, tooth.stainDeepColor);
                renderer.SetPropertyBlock(_block);
            }
        }

        // 全員が 1P の席と同じ向きで歯を見るように、入れ物をカメラの画面の回転だけ回す（各PCが自分のカメラで。席替えにもついていく）。
        // 置き方（大きさ・傾き・ずれ）は入れ物の中のモデルに入っているので、そのまま一緒に回る。
        private void ApplyFacing()
        {
            if (_model == null)
            {
                Rebind();
                if (_model == null)
                {
                    return;
                }
            }

            var holder = _model.transform;
            var roll = _model.sameViewForEveryone ? ViewRoll() : 0f;
            var parentRotation = holder.parent != null ? holder.parent.rotation : Quaternion.identity;
            var local = CoreToothFit.FacingLocal(parentRotation, roll);
            if (Quaternion.Angle(holder.localRotation, local) > 0.01f)
            {
                holder.localRotation = local;
            }
        }

        // 編集中は HP が入っていない（0）ので、「HP を試す」が無ければ満タンとして見せる。
        private float Health01()
        {
            if (CoreDangerLook.PreviewHealth01 >= 0f)
            {
                return Mathf.Clamp01(CoreDangerLook.PreviewHealth01);
            }

            if (!Application.isPlaying)
            {
                return 1f;
            }

            return _core.maxHealth > 0f ? Mathf.Clamp01(_core.CurrentHealth / _core.maxHealth) : 1f;
        }

        // 模様（しみ・ひび）をモデルの大きさにそろえ、「根元」をゲームの床の側（+Z）にする。
        private void SetFrame(Renderer renderer)
        {
            Bounds bounds;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                bounds = skinned.localBounds;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
            {
                bounds = filter.sharedMesh.bounds;
            }
            else
            {
                return;
            }

            var up = renderer.transform.InverseTransformDirection(Vector3.back).normalized;
            CoreToothFit.PatternFrame(bounds, up, out var center, out var invHeight);
            _block.SetVector(FrameId, new Vector4(center.x, center.y, center.z, invHeight));
            _block.SetVector(UpId, new Vector4(up.x, up.y, up.z, 0f));
        }
    }
}
