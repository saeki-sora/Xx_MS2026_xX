using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    // ゲームの中の研修（2026-11-12）: フィールドの枠には何も描かず、本物のゲームのカメラをその枠の中だけに映す。
    // ・背景はフィールドの所だけ穴をあけて並べる
    // ・カメラの映す範囲（Camera.rect）を枠に合わせる（終わったら元に戻す）
    // ・ゲームの画面の文字や枠（この画面より下に描く Canvas）は、研修の間だけ隠す（穴から見えて重ならないように。終わったら戻す）
    public sealed partial class PracticeScreen
    {
        private Camera _viewCamera;
        private Rect _viewCameraRect;
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
        private float _nextCanvasScan;
        private RectTransform _viewHole;

        private void BuildBackgroundAround(float x, float y, float w, float h)
        {
            var texture = _cfg.background;
            void Piece(string name, float px, float py, float pw, float ph)
            {
                if (pw <= 0f || ph <= 0f)
                {
                    return;
                }

                var piece = Raw(name, _root, texture, P(px + (pw * 0.5f), py + (ph * 0.5f)), new Vector2(pw, ph));
                piece.uvRect = new Rect(px / 1920f, 1f - ((py + ph) / 1080f), pw / 1920f, ph / 1080f);
                if (texture == null)
                {
                    piece.color = new Color32(0xee, 0xe9, 0xff, 0xff);
                }
            }

            Piece("Background Top", 0f, 0f, 1920f, y);
            Piece("Background Bottom", 0f, y + h, 1920f, 1080f - (y + h));
            Piece("Background Left", 0f, y, x, h);
            Piece("Background Right", x + w, y, 1920f - (x + w), h);
            if (_cfg.halftone != null)
            {
                var a = Raw("Halftone", _root, _cfg.halftone, P(1660f, 950f), new Vector2(520f, 260f));
                a.color = new Color(1f, 1f, 1f, 0.22f);
            }
        }

        private void BuildRealField()
        {
            // 枠（中は塗らない）。絵の置き場に枠の絵があれば、それを最後に重ねる（BuildFieldFrameImage）。
            if (Art == null || Art.fieldFrame == null)
            {
                var frame = Shape("Field Frame", _root, P(FieldX + (FieldW * 0.5f), FieldY + (FieldH * 0.5f)), new Vector2(FieldW + 40f, FieldH + 40f));
                var outer = Chamfer(Vector2.zero, new Vector2(FieldW + 10f, FieldH + 10f), 30f);
                var shifted = new List<Vector2>();
                foreach (var p in outer)
                {
                    shifted.Add(p + new Vector2(7f, -7f));
                }

                frame.Stroke(shifted, 6f, Pink, true);
                frame.Stroke(outer, 10f, Ink, true);
                frame.Stroke(outer, 3f, Mint, true);
            }

            _field = MakeRect("Field", _root, FieldX, FieldY, FieldW, FieldH);
            _field.gameObject.AddComponent<RectMask2D>();
            _viewHole = MakeRect("Camera Window", _root, FieldX, FieldY, FieldW, FieldH);

            for (var i = 0; i < FloatPool; i++)
            {
                var text = Heading("Float", _field, string.Empty, 34, Vector2.zero, new Vector2(900f, 90f));
                text.enabled = false;
                _floaters[i] = new Floater { Text = text };
            }

            _holeCover = HowToPlayArt.MakeImage("Cover", _field, null, new Color32(0x1b, 0x23, 0x66, 0xff), Vector2.zero, new Vector2(FieldW, FieldH));
            _holeText = Heading("Cover Text", _field, Ui.fieldLoading, 36, Vector2.zero, new Vector2(900f, 60f));
            _burnEdge = HowToPlayArt.MakeImage("Burn Edge", _field, Vignette, new Color(Burn.r, Burn.g, Burn.b, 0f), Vector2.zero, new Vector2(FieldW, FieldH));
            _burnEdge.type = Image.Type.Simple;
            BuildFieldFrameImage();
        }

        /// <summary>絵の置き場（Resources/PracticeArtLibrary）の「絵」。</summary>
        private static PracticeArtLibrary Art => PracticeArtLibrary.Load();

        /// <summary>フィールドの枠の絵（真ん中が透明。「ためしてみてね」の札も絵の中）を、フィールドの真ん中に絵の大きさのまま重ねる。</summary>
        private void BuildFieldFrameImage()
        {
            var tex = Art != null ? Art.fieldFrame : null;
            if (tex == null)
            {
                return;
            }

            Raw("Field Frame Image", _root, tex, P(FieldX + (FieldW * 0.5f), FieldY + (FieldH * 0.5f)), new Vector2(tex.width, tex.height));
        }

        private void TickRealField(float dt)
        {
            TickFloaters();
            var a = LocalOverheated ? 0.38f + (0.14f * Mathf.Sin(_time * 14f)) : 0f;
            _burnEdge.color = new Color(Burn.r, Burn.g, Burn.b, Mathf.MoveTowards(_burnEdge.color.a, a, dt * 3f));
            _shake = Mathf.Max(0f, _shake - dt);
        }

        private void LateUpdate()
        {
            if (!_realField || _viewHole == null)
            {
                return;
            }

            // 消えていく途中はカメラを全画面へ戻していく（幕などで隠れていなくても不自然にならないように）。
            var full = _fade < 0.999f && !(_keepVisible == null || _keepVisible());
            ApplyCameraRect(full ? 1f - Mathf.Clamp01(_fade) : 0f);
            if (Time.unscaledTime >= _nextCanvasScan && !full)
            {
                _nextCanvasScan = Time.unscaledTime + 0.5f;
                HideGameCanvases();
            }
            else if (full)
            {
                RestoreGameCanvases();
            }
        }

        /// <summary>カメラの映す範囲を、フィールドの枠に合わせる（toFull = 1 で全画面）。</summary>
        /// <summary>枠に映すカメラ（練習のシーンを重ねていればそのカメラ、ゲームの中なら今のメインカメラ）。</summary>
        private Camera ViewCam => _sceneMode ? (PracticeSceneDriver.Current != null ? PracticeSceneDriver.Current.ViewCamera : null) : Camera.main;

        private void ApplyCameraRect(float toFull)
        {
            var cam = ViewCam;
            if (cam != _viewCamera)
            {
                RestoreCamera();
                _viewCamera = cam;
                if (cam != null)
                {
                    _viewCameraRect = cam.rect;
                }
            }

            if (cam == null)
            {
                return;
            }

            var corners = new Vector3[4];
            _viewHole.GetWorldCorners(corners);
            var sw = Mathf.Max(1f, Screen.width);
            var sh = Mathf.Max(1f, Screen.height);
            var rect = new Rect(corners[0].x / sw, corners[0].y / sh, (corners[2].x - corners[0].x) / sw, (corners[2].y - corners[0].y) / sh);
            var target = new Rect(
                Mathf.Lerp(rect.x, _viewCameraRect.x, toFull),
                Mathf.Lerp(rect.y, _viewCameraRect.y, toFull),
                Mathf.Lerp(rect.width, _viewCameraRect.width, toFull),
                Mathf.Lerp(rect.height, _viewCameraRect.height, toFull));
            cam.rect = target;
        }

        private void RestoreCamera()
        {
            if (_viewCamera != null)
            {
                _viewCamera.rect = _viewCameraRect;
            }

            _viewCamera = null;
        }

        /// <summary>ゲームの画面の文字や枠（この画面より下に描く Canvas）を隠す。</summary>
        private void HideGameCanvases()
        {
            var mine = _group != null ? _group.GetComponent<Canvas>() : null;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas == null || canvas == mine || !canvas.isRootCanvas || !canvas.enabled || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (canvas.sortingOrder >= _settings.sortingOrder || canvas.transform.IsChildOf(transform))
                {
                    continue;
                }

                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }

        private void RestoreGameCanvases()
        {
            foreach (var canvas in _hiddenCanvases)
            {
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }

            _hiddenCanvases.Clear();
        }

        private void EndRealField()
        {
            if (!_realField)
            {
                return;
            }

            RestoreCamera();
            RestoreGameCanvases();
        }
    }
}
