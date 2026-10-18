using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// ビリビリ（稲妻）の線をまとめて出す係（<see cref="RedLaserVisuals"/> が1つ持つ）。
    /// LineRenderer をあらかじめ決まった数だけ作っておき、使い回す（毎フレームの生成・破棄なし）。
    /// 稲妻は生きている間、毎フレーム形を作り直すので、ジジジッと震えて見える。
    /// </summary>
    internal sealed class RedLaserLightning
    {
        private const int MaxPoints = 18;

        private readonly Bolt[] _bolts;
        private readonly Vector3[] _points = new Vector3[MaxPoints];
        private int _next;

        public RedLaserLightning(Transform parent, Material material, int count, int sortingOrder)
        {
            _bolts = new Bolt[Mathf.Max(1, count)];
            for (var i = 0; i < _bolts.Length; i++)
            {
                var go = new GameObject("Bolt" + i) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(parent, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.textureMode = LineTextureMode.Stretch;
                line.numCapVertices = 2;
                line.numCornerVertices = 1;
                line.sharedMaterial = material;
                line.sortingOrder = sortingOrder;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                line.enabled = false;
                _bolts[i] = new Bolt { Line = line };
            }
        }

        public int SortingOrder
        {
            set
            {
                for (var i = 0; i < _bolts.Length; i++)
                {
                    _bolts[i].Line.sortingOrder = value;
                }
            }
        }

        /// <summary>
        /// 稲妻を1本出す。空きが無ければ一番古いものを使い回す。
        /// </summary>
        /// <param name="jitter">ギザギザの大きさ（線の長さに対する割合）。</param>
        /// <param name="branchChance">途中から枝分かれした短い稲妻を1本足す確率（0〜1）。</param>
        public void Spawn(Vector2 from, Vector2 to, float z, float width, float life, Color color, float jitter, float branchChance)
        {
            SpawnOne(from, to, z, width, life, color, jitter);
            if (branchChance > 0f && Random.value < branchChance)
            {
                // 途中の点から、横にそれる短い枝。
                var t = Random.Range(0.3f, 0.7f);
                var start = Vector2.Lerp(from, to, t);
                var delta = to - from;
                var side = new Vector2(-delta.y, delta.x) * Random.Range(-0.45f, 0.45f);
                var end = start + delta * Random.Range(0.25f, 0.45f) + side;
                SpawnOne(start, end, z, width * 0.6f, life * 0.8f, color, jitter);
            }
        }

        /// <summary>毎フレーム呼ぶ。生きている稲妻の形を作り直し、寿命が来たら消す。</summary>
        public void Tick(float dt)
        {
            for (var i = 0; i < _bolts.Length; i++)
            {
                var bolt = _bolts[i];
                if (!bolt.Alive)
                {
                    continue;
                }

                bolt.Age += dt;
                if (bolt.Age >= bolt.Life)
                {
                    bolt.Alive = false;
                    bolt.Line.enabled = false;
                    continue;
                }

                // ちらちら点滅しながら消えていく。
                var fade = 1f - bolt.Age / bolt.Life;
                var flicker = Random.value < 0.25f ? 0.35f : 1f;
                var color = bolt.Color;
                color.a *= fade * flicker;
                bolt.Line.startColor = color;
                bolt.Line.endColor = color;
                Rebuild(bolt);
            }
        }

        public void HideAll()
        {
            for (var i = 0; i < _bolts.Length; i++)
            {
                _bolts[i].Alive = false;
                _bolts[i].Line.enabled = false;
            }
        }

        private void SpawnOne(Vector2 from, Vector2 to, float z, float width, float life, Color color, float jitter)
        {
            var bolt = _bolts[_next];
            _next = (_next + 1) % _bolts.Length;
            bolt.From = from;
            bolt.To = to;
            bolt.Z = z;
            bolt.Life = Mathf.Max(0.02f, life);
            bolt.Age = 0f;
            bolt.Color = color;
            bolt.Jitter = jitter;
            bolt.Alive = true;
            bolt.Line.widthMultiplier = width;
            bolt.Line.startColor = color;
            bolt.Line.endColor = color;
            bolt.Line.enabled = true;
            Rebuild(bolt);
        }

        /// <summary>始点と終点はそのまま、間の点を横にランダムにずらしてギザギザにする（真ん中ほど大きくずれる）。</summary>
        private void Rebuild(Bolt bolt)
        {
            var delta = bolt.To - bolt.From;
            var length = delta.magnitude;
            if (length < 0.001f)
            {
                bolt.Line.enabled = false;
                return;
            }

            var count = Mathf.Clamp(Mathf.CeilToInt(length / 0.22f) + 1, 4, MaxPoints);
            var perpendicular = new Vector2(-delta.y, delta.x) / length;
            var amplitude = length * bolt.Jitter;
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)(count - 1);
                var point = bolt.From + delta * t;
                if (i > 0 && i < count - 1)
                {
                    var envelope = Mathf.Sin(t * Mathf.PI);
                    point += perpendicular * (Random.Range(-1f, 1f) * amplitude * envelope);
                }

                _points[i] = new Vector3(point.x, point.y, bolt.Z);
            }

            bolt.Line.positionCount = count;
            bolt.Line.SetPositions(_points);
        }

        private sealed class Bolt
        {
            public LineRenderer Line;
            public Vector2 From;
            public Vector2 To;
            public float Z;
            public float Life;
            public float Age;
            public Color Color;
            public float Jitter;
            public bool Alive;
        }
    }
}
