using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆をGPUインスタンシングで描く。全敵のデータを1つのバッファに置き、
    /// 敵の種類ごとに1回だけ描画を呼ぶ（種類が5つなら描画呼び出しは5回）。
    /// 背景（ステージ背景スタジオ）が「隠れた敵を影で見せる」をONにしている間だけ、種類ごとにもう1回、
    /// 背景の裏に隠れた部分を単色の影で描く（グローバル値 _FortressSilhouetteEnabled が0より大きいとき。既定は0で描かない）。
    /// </summary>
    public sealed class SwarmRenderer : IDisposable
    {
        private const string ShaderResourcePath = "Fortress/SwarmSprite";
        private const string SilhouetteShaderResourcePath = "Fortress/SwarmSilhouette";
        private const int InstanceStride = 32;

        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int InstanceOffsetId = Shader.PropertyToID("_InstanceOffset");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColsId = Shader.PropertyToID("_Cols");
        private static readonly int RowsId = Shader.PropertyToID("_Rows");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ZWriteId = Shader.PropertyToID("_SwarmZWrite");
        private static readonly int SilhouetteEnabledId = Shader.PropertyToID("_FortressSilhouetteEnabled");

        private readonly GraphicsBuffer _buffer;
        private readonly Mesh _quad;
        private readonly Shader _shader;
        private readonly Shader _silhouetteShader;
        private readonly List<TypeVisual> _visuals = new List<TypeVisual>();
        private bool _missingShaderLogged;

        public int LastBatchCount { get; private set; }

        private sealed class TypeVisual
        {
            public Material material;
            public Material silhouette;
            public Texture2D generated;
            public int key;
        }

        public SwarmRenderer(int capacity)
        {
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, InstanceStride);
            _quad = BuildQuad();
            _shader = Resources.Load<Shader>(ShaderResourcePath);
            if (_shader == null)
            {
                _shader = Shader.Find("MS2026/Fortress/SwarmSprite");
            }

            _silhouetteShader = Resources.Load<Shader>(SilhouetteShaderResourcePath);
        }

        public void Render(
            NativeArray<SwarmInstance> instances,
            int total,
            NativeArray<int> typeStart,
            IReadOnlyList<EnemyTypeDefinition> types,
            SwarmSettings settings)
        {
            LastBatchCount = 0;
            if (total <= 0)
            {
                return;
            }

            if (_shader == null)
            {
                if (!_missingShaderLogged)
                {
                    _missingShaderLogged = true;
                    Debug.LogError("[Swarm] シェーダー 'Fortress/SwarmSprite' が見つかりません。");
                }

                return;
            }

            _buffer.SetData(instances, 0, 0, total);

            var bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            var drawSilhouettes = _silhouetteShader != null && Shader.GetGlobalFloat(SilhouetteEnabledId) > 0f;
            for (var t = 0; t < types.Count; t++)
            {
                var start = typeStart[t];
                var count = typeStart[t + 1] - start;
                if (count <= 0)
                {
                    continue;
                }

                var material = PrepareMaterial(t, types[t], settings);
                material.SetFloat(InstanceOffsetId, start);

                var renderParams = new RenderParams(material)
                {
                    worldBounds = bounds,
                    layer = 0,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };

                Graphics.RenderMeshPrimitives(renderParams, _quad, 0, count);
                LastBatchCount++;

                if (drawSilhouettes)
                {
                    renderParams.material = PrepareSilhouette(t, material);
                    Graphics.RenderMeshPrimitives(renderParams, _quad, 0, count);
                    LastBatchCount++;
                }
            }
        }

        private Material PrepareMaterial(int typeIndex, EnemyTypeDefinition definition, SwarmSettings settings)
        {
            while (_visuals.Count <= typeIndex)
            {
                _visuals.Add(new TypeVisual());
            }

            var visual = _visuals[typeIndex];
            if (visual.material == null)
            {
                visual.material = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            var swarm = definition.swarm;
            var frames = Mathf.Max(1, swarm.frameCount);
            var directions = Mathf.Max(1, swarm.directionCount);

            var sheet = swarm.spriteSheet;
            if (sheet == null)
            {
                var key = MakeKey(definition.placeholderColor, frames, directions);
                if (visual.generated == null || visual.key != key)
                {
                    DestroyObject(visual.generated);
                    visual.generated = SwarmPlaceholderSheet.Create(definition.placeholderColor, frames, directions);
                    visual.key = key;
                }

                sheet = visual.generated;
            }

            var material = visual.material;
            material.renderQueue = settings.renderQueue;
            material.SetBuffer(InstancesId, _buffer);
            material.SetTexture(MainTexId, sheet);
            material.SetFloat(ColsId, frames);
            material.SetFloat(RowsId, directions);
            material.SetColor(TintId, Color.white);

            // 絵を立たせているときだけ深度を書く(寝ているときは従来通り書かない)。
            var standing = Shader.GetGlobalFloat(Billboards.BillboardShaderGlobals.SwarmStandId) > 0f;
            material.SetFloat(ZWriteId, standing ? 1f : 0f);
            return material;
        }

        /// <summary>影用のマテリアルを、本体と同じ絵・同じ並び・同じ範囲にそろえる（本体の後に描く）。</summary>
        private Material PrepareSilhouette(int typeIndex, Material main)
        {
            var visual = _visuals[typeIndex];
            if (visual.silhouette == null)
            {
                visual.silhouette = new Material(_silhouetteShader) { hideFlags = HideFlags.HideAndDontSave };
            }

            var silhouette = visual.silhouette;
            silhouette.renderQueue = main.renderQueue + 1;
            silhouette.SetBuffer(InstancesId, _buffer);
            silhouette.SetTexture(MainTexId, main.GetTexture(MainTexId));
            silhouette.SetFloat(ColsId, main.GetFloat(ColsId));
            silhouette.SetFloat(RowsId, main.GetFloat(RowsId));
            silhouette.SetColor(TintId, main.GetColor(TintId));
            silhouette.SetFloat(InstanceOffsetId, main.GetFloat(InstanceOffsetId));
            return silhouette;
        }

        private static int MakeKey(Color color, int frames, int directions)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + color.GetHashCode();
                hash = hash * 31 + frames;
                hash = hash * 31 + directions;
                return hash;
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh
            {
                name = "Fortress_SwarmQuad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        public void Dispose()
        {
            _buffer?.Release();

            foreach (var visual in _visuals)
            {
                DestroyObject(visual.material);
                DestroyObject(visual.silhouette);
                DestroyObject(visual.generated);
            }

            _visuals.Clear();
            DestroyObject(_quad);
        }
    }
}
