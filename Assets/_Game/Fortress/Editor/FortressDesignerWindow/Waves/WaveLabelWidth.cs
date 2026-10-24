using System;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 欄の名前（左の文字）の幅を一時的に広げる（日本語の長い名前が途中で切れないように）。using で囲んだ間だけ効く。
    /// 幅はウィンドウの幅の割合で決め、狭いウィンドウでも値の欄が残るように上限・下限を付ける。
    /// </summary>
    public readonly struct WaveLabelWidth : IDisposable
    {
        private readonly float _previous;

        public WaveLabelWidth(float ratio = 0.55f, float min = 200f, float max = 340f)
        {
            _previous = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * ratio, min, max);
        }

        public void Dispose()
        {
            EditorGUIUtility.labelWidth = _previous;
        }
    }
}
