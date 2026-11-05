using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯磨き粉ビームのシェーダー（MS2026/Fortress/ToothpasteBeam）へ、毎フレームの値を渡す。
    /// LineRenderer の位置・太さ・色・マテリアルには触れず、MaterialPropertyBlock で長さ・太さ・流れ・強さだけを入れる。
    /// 別のシェーダーのマテリアルが付いている場合も、知らない値は無視されるだけなので害はない。
    /// </summary>
    internal sealed class BeamGelDriver
    {
        private static readonly int LengthId = Shader.PropertyToID("_BeamLength");
        private static readonly int WidthId = Shader.PropertyToID("_BeamWidth");
        private static readonly int FlowId = Shader.PropertyToID("_BeamFlow");
        private static readonly int PowerId = Shader.PropertyToID("_BeamPower");
        private static readonly int TipRoundId = Shader.PropertyToID("_BeamTipRound");

        // 当たる・外れるが切り替わったとき、先の丸みを一瞬で出し入れしない（約0.1秒）。
        private const float TipRoundSpeed = 10f;

        // 流れの座標が大きくなりすぎないように折り返す（模様が一瞬ずれるだけで目立たない）。
        private const float FlowWrap = 1024f;

        private readonly LaserBeamVisual _beam;
        private MaterialPropertyBlock _block;
        private float _flow;
        private float _tipRound = 1f;
        private bool _applied;

        public BeamGelDriver(LaserBeamVisual beam)
        {
            _beam = beam;
        }

        public void Update(LaserLookSettings look, LaserTurret turret, float deltaTime)
        {
            var line = _beam != null ? _beam.lineRenderer : null;
            if (line == null || !line.enabled || line.positionCount < 2)
            {
                return;
            }

            _flow += LaserLookMath.FlowSpeed(look, turret.CurrentThickness01) * deltaTime;
            if (_flow > FlowWrap)
            {
                _flow -= FlowWrap;
            }

            var length = Vector3.Distance(line.GetPosition(0), line.GetPosition(line.positionCount - 1));

            // 何にも当たらず届く距離で終わっているときだけ先を丸くする（壁などに当たっているときは、そこでぴったり止める）。
            _tipRound = Mathf.MoveTowards(_tipRound, _beam.HasImpact ? 0f : 1f, TipRoundSpeed * deltaTime);

            _block ??= new MaterialPropertyBlock();
            line.GetPropertyBlock(_block);
            _block.SetFloat(LengthId, Mathf.Max(0.01f, length));
            _block.SetFloat(WidthId, Mathf.Max(0.001f, turret.CurrentThicknessMeters));
            _block.SetFloat(FlowId, _flow);
            _block.SetFloat(PowerId, turret.CurrentThickness01);
            _block.SetFloat(TipRoundId, _tipRound);
            line.SetPropertyBlock(_block);
            _applied = true;
        }

        /// <summary>見た目の設定がOFFになったとき、入れた値を外してマテリアルの既定値に戻す。</summary>
        public void Clear()
        {
            if (!_applied)
            {
                return;
            }

            _applied = false;
            var line = _beam != null ? _beam.lineRenderer : null;
            if (line != null)
            {
                line.SetPropertyBlock(null);
            }
        }
    }
}
