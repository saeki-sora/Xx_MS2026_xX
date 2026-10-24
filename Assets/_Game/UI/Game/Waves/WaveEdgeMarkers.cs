using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 次のウェーブで敵が湧く場所のうち、このPCの画面の外にある所を、画面のふちの矢印で知らせる（2026-10-24、初めての人向け）。
    /// 視点はPCごとに違う（回っている人もいる）ので、各PCが自分のカメラで計算する。赤い線（<see cref="WaveRoutes"/>）と同じ間だけ、同じ濃さで出る。
    /// HUD の全面に広げた RectTransform に付ける（UIスタジオの「ウェーブの表示を入れる」が作る）。
    /// </summary>
    [AddComponentMenu("UI Studio/画面の外から来る敵の矢印 (WaveEdgeMarkers)")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class WaveEdgeMarkers : MonoBehaviour
    {
        [Tooltip("矢印の絵（上向き）。")]
        public Sprite arrow;

        [Tooltip("矢印の色。")]
        public Color color = new Color(1f, 0.25f, 0.3f, 1f);

        [Tooltip("画面のふちから内側へ離す量（HUD の基準 1920×1080 での px）。")]
        [Min(0f)]
        public float margin = 80f;

        [Tooltip("矢印の大きさ（px）。")]
        [Min(8f)]
        public float size = 84f;

        [Tooltip("数の文字の大きさ。")]
        [Min(8)]
        public int fontSize = 34;

        private readonly List<Image> _arrows = new List<Image>();
        private readonly List<Text> _labels = new List<Text>();
        private RectTransform _rect;
        private Font _font;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void LateUpdate()
        {
            var alpha = WaveDisplayTiming.Current.showEdgeMarkers ? WaveRoutes.Alpha : 0f;
            var spawns = WaveRoutes.Spawns;
            var rig = FortressCameraRig.Active;
            var camera = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            var used = 0;
            if (alpha > 0f && camera != null)
            {
                var half = _rect.rect.size * 0.5f;
                var limit = new Vector2(Mathf.Max(1f, half.x - margin), Mathf.Max(1f, half.y - margin));
                foreach (var spawn in spawns)
                {
                    var viewport = camera.WorldToViewportPoint(new Vector3(spawn.Position.x, spawn.Position.y, 0f));
                    var behind = viewport.z < 0f;
                    var inside = !behind && viewport.x > 0.03f && viewport.x < 0.97f && viewport.y > 0.03f && viewport.y < 0.97f;
                    if (inside)
                    {
                        continue;
                    }

                    var offset = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                    if (behind)
                    {
                        offset = -offset;
                    }

                    if (offset.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }

                    // 画面の中心からその方向へ伸ばし、ふちで止める。
                    var direction = new Vector2(offset.x * half.x, offset.y * half.y).normalized;
                    var scale = Mathf.Min(limit.x / Mathf.Max(1e-4f, Mathf.Abs(direction.x)), limit.y / Mathf.Max(1e-4f, Mathf.Abs(direction.y)));
                    var position = direction * scale;
                    Place(used++, position, direction, spawn.Enemies, alpha);
                }
            }

            for (var i = used; i < _arrows.Count; i++)
            {
                _arrows[i].enabled = false;
                _labels[i].enabled = false;
            }
        }

        private void Place(int index, Vector2 position, Vector2 direction, int enemies, float alpha)
        {
            while (_arrows.Count <= index)
            {
                Create();
            }

            var pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 9f);
            var image = _arrows[index];
            image.enabled = true;
            image.color = new Color(color.r, color.g, color.b, color.a * alpha);
            var rect = image.rectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * size;
            rect.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));
            rect.localScale = Vector3.one * pulse;

            var label = _labels[index];
            label.enabled = enemies > 0 && WaveDisplayTiming.Current.showEdgeMarkerCounts;
            label.text = $"×{enemies}";
            label.color = new Color(1f, 1f, 1f, alpha);
            label.rectTransform.anchoredPosition = position - direction * (size * 0.95f);
        }

        private void Create()
        {
            var go = new GameObject("Arrow", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var image = go.AddComponent<Image>();
            image.sprite = arrow;
            image.raycastTarget = false;
            _arrows.Add(image);

            var labelGo = new GameObject("Count", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            var label = labelGo.AddComponent<Text>();
            label.font = _font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.BoldAndItalic;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(160f, 50f);
            var outline = labelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.06f, 0.08f, 0.2f);
            outline.effectDistance = new Vector2(2f, -2f);
            _labels.Add(label);
        }
    }
}
