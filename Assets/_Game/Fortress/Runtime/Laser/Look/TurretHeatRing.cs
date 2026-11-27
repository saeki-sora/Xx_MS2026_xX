using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 砲台のまわりの床に出す熱のゲージの輪（2026-11-27 案A）。<see cref="LaserTurretLook"/> が持って毎フレーム動かす。
    /// 12時の所から時計回りに溜まり、色は芯と同じ（<see cref="LaserHeatLookSettings.coreColors"/>）。
    /// オーバーヒート中は灰色になり、止まっている残り時間ぶんが減っていく。線は2本（下地と溜まった所）の LineRenderer で、
    /// 照準線（<see cref="TurretAimIndicator"/>）と同じく Sprites/Default の線で、床の面（XY）に沿って描く。シーンには保存しない。
    /// </summary>
    internal sealed class TurretHeatRing
    {
        private const int FullSegments = 64;

        // 床とちらつかないよう、少しだけカメラ側（-Z）に置く（カメラは +Z を向いている）。
        private const float LiftTowardCamera = 0.01f;

        private readonly Transform _root;
        private readonly LineRenderer _track;
        private readonly LineRenderer _fill;
        private readonly Material _material;
        private readonly Vector3[] _points = new Vector3[FullSegments + 1];
        private float _visibility;

        public TurretHeatRing(Transform owner)
        {
            var go = new GameObject("HeatRing") { hideFlags = HideFlags.DontSave };
            _root = go.transform;
            _root.SetParent(owner, false);

            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            // 下地を先に（奥に）、溜まった所を上に描く。砲台の絵（0）と足元の影（-1）より手前に描く
            // （輪は影より外にあるので絵には重ならない。後ろにすると影に隠れて見えなくなる）。
            _track = CreateLine("Track", true, 1);
            _fill = CreateLine("Fill", false, 2);
            SetVisible(false);
        }

        /// <param name="blink">点滅の明るさ（1＝いつも通り）。</param>
        public void Update(LaserHeatLookSettings settings, Vector3 center, float heat01, bool overheated,
            float silenceRemaining, float silenceDuration, float blink, float deltaTime)
        {
            var wanted = settings != null && settings.enabled && settings.showRing
                         && LaserHeatLookMath.RingWanted(overheated, heat01, settings.ringShowFrom);
            var speed = settings != null ? settings.ringFadeSpeed : 6f;
            _visibility = Mathf.MoveTowards(_visibility, wanted ? 1f : 0f, speed * Mathf.Max(0f, deltaTime));
            if (_visibility <= 0f || settings == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _root.SetPositionAndRotation(new Vector3(center.x, center.y, center.z - LiftTowardCamera), Quaternion.identity);

            var radius = settings.ringRadius;
            var width = settings.ringWidth;

            _track.widthMultiplier = width * Mathf.Max(1f, settings.ringTrackWidthScale);
            _track.positionCount = FullSegments;
            for (var i = 0; i < FullSegments; i++)
            {
                _track.SetPosition(i, PointOnRing(radius, i / (float)FullSegments));
            }

            var trackColor = settings.ringTrackColor;
            trackColor.a *= _visibility;
            _track.startColor = trackColor;
            _track.endColor = trackColor;

            var fill = LaserHeatLookMath.RingFill(overheated, heat01, silenceRemaining, silenceDuration);
            if (fill <= 0.001f)
            {
                _fill.enabled = false;
                return;
            }

            _fill.enabled = true;
            var segments = Mathf.Max(2, Mathf.CeilToInt(FullSegments * fill));
            for (var i = 0; i <= segments; i++)
            {
                _points[i] = PointOnRing(radius, fill * i / segments);
            }

            _fill.widthMultiplier = width;
            _fill.positionCount = segments + 1;
            _fill.SetPositions(_points);

            Color color;
            if (overheated)
            {
                color = settings.overheatRingColor;
            }
            else
            {
                // 熱が少ないうちは白すぎて床に溶けるので、黄色寄りから始める。
                color = settings.coreColors.Evaluate(Mathf.Max(0.35f, heat01));
                color.a = 0.95f * Mathf.Clamp01(blink);
            }

            color.a *= _visibility;
            _fill.startColor = color;
            _fill.endColor = color;
        }

        public void Hide()
        {
            _visibility = 0f;
            SetVisible(false);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        // 0 が 12時、1 で一周（時計回り）。
        private Vector3 PointOnRing(float radius, float t)
        {
            var angle = Mathf.PI * 0.5f - t * Mathf.PI * 2f;
            var center = _root.position;
            return new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, center.z);
        }

        private LineRenderer CreateLine(string name, bool loop, int sortingOrder)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            // 照準線と同じくカメラの方を向ける（カメラは床をほぼ真上から見ているので、床に描いたように見える）。
            // TransformZ にすると、線の太さが奥行きの向きに出て真横から見た板になり、見えなくなる。
            line.alignment = LineAlignment.View;
            line.numCapVertices = loop ? 0 : 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = sortingOrder;
            if (_material != null)
            {
                line.sharedMaterial = _material;
            }

            return line;
        }

        private void SetVisible(bool visible)
        {
            if (_track.enabled != visible)
            {
                _track.enabled = visible;
            }

            if (!visible && _fill.enabled)
            {
                _fill.enabled = false;
            }
        }
    }
}
