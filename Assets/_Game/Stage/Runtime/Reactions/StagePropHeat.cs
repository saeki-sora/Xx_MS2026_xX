using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// レーザーが当たった所を赤く光らせ、当て続けると焦げ跡を残す（見た目だけ。各PCがそれぞれ計算する）。
    /// 当たりの検出は <see cref="StageLaserHeatScanner"/> が全レーザーをまとめて見て、ここへ知らせる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StagePropRenderer))]
    public sealed class StagePropHeat : MonoBehaviour
    {
        private const float MergeDistance = 0.3f;

        private readonly HeatPointBuffer _buffer = new HeatPointBuffer(StageShaderIds.MaxHeatPoints);
        private StageProp _prop;
        private StagePropRenderer _renderer;
        private bool _hadPoints;

        public HeatPointBuffer Buffer => _buffer;

        /// <summary>レーザーがこのフレーム当たっている（StageLaserHeatScanner から呼ばれる）。</summary>
        public void ReceiveLaser(Vector3 point, float thicknessMeters, float deltaTime)
        {
            if (_prop == null || !_prop.look.heatReactive)
            {
                return;
            }

            var profile = StageLookProfile.Current;
            var scale = _prop.look.heatScale;
            var radius = profile.heatRadius + thicknessMeters * profile.heatRadiusPerThickness;
            _buffer.Add(point, profile.glowPerSecond * scale * deltaTime, radius, MergeDistance + radius * 0.5f);
        }

        /// <summary>焦げ跡も含めて消す（再生したとき・テスト用）。</summary>
        public void ClearMarks()
        {
            _buffer.Clear();
            WriteLook();
        }

        private void Awake()
        {
            _prop = GetComponent<StageProp>();
            _renderer = GetComponent<StagePropRenderer>();
        }

        private void Update()
        {
            if (_buffer.Count == 0 && !_hadPoints)
            {
                return;
            }

            var profile = StageLookProfile.Current;
            var scorchRate = profile.scorchPerSecond * (_prop != null ? _prop.look.heatScale : 1f);
            _buffer.Tick(Time.deltaTime, profile.coolPerSecond, scorchRate);
            WriteLook();
        }

        private void WriteLook()
        {
            var look = _renderer.Look;
            _buffer.CopyTo(look.HeatPoints, look.HeatValues);
            look.HeatCount = _buffer.Count;
            _hadPoints = _buffer.Count > 0;
            _renderer.MarkDirty();
        }
    }
}
