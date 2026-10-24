using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 「4人のだれかの画面に映っているか」（Unity に頼らない計算。テストあり）。
    /// このゲームは視点がプレイヤーごとにずれている（回っている人もいる）ので、撤退した敵を消してよいのは、全員の画面から出たとき。
    /// 視点はステージの視点（<see cref="CameraViewPreset"/> の P1〜P4）から計算する（Host は全員の視点を知っている。カメラは要らない）。
    /// </summary>
    public sealed class WaveViewCoverage
    {
        private readonly List<CameraViewSettings> _views = new List<CameraViewSettings>();
        private float _aspect = 16f / 9f;

        public int ViewCount => _views.Count;

        /// <summary>視点を入れ直す（プリセットの P1〜P4。プリセットが無ければ「全部映っている」扱いにならないよう空）。</summary>
        public void Set(CameraViewPreset preset, float aspect)
        {
            _views.Clear();
            _aspect = Mathf.Max(0.1f, aspect);
            if (preset == null || preset.players == null)
            {
                return;
            }

            foreach (var view in preset.players)
            {
                _views.Add(view);
            }
        }

        /// <summary>テスト用: 視点を直接入れる。</summary>
        public void Set(IEnumerable<CameraViewSettings> views, float aspect)
        {
            _views.Clear();
            _views.AddRange(views);
            _aspect = Mathf.Max(0.1f, aspect);
        }

        /// <summary>
        /// point（地面の上）が、だれかの画面に映っているか。marginMeters だけ画面の外まで「映っている」に含める（体の大きい敵が端からはみ出さないように）。
        /// 視点が1つも無ければ常に false。
        /// </summary>
        public bool IsVisible(Vector2 point, float marginMeters)
        {
            foreach (var view in _views)
            {
                if (!CameraViewMath.TryWorldToViewport(view, _aspect, new Vector3(point.x, point.y, 0f), out var viewport))
                {
                    continue;
                }

                // 余白を画面の割合に直す（縦の映る広さで割る）。
                var margin = marginMeters / Mathf.Max(0.01f, view.VisibleHalfHeight * 2f);
                var marginX = margin / _aspect;
                if (viewport.x >= -marginX && viewport.x <= 1f + marginX && viewport.y >= -margin && viewport.y <= 1f + margin)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
