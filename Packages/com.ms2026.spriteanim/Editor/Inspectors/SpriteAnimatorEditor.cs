using System;
using UnityEditor;
using UnityEngine;

namespace MS2026.SpriteAnim.Editor
{
    [CustomEditor(typeof(SpriteAnimator))]
    public class SpriteAnimatorEditor : UnityEditor.Editor
    {
        private SerializedProperty _set;
        private SerializedProperty _defaultAnim;
        private SerializedProperty _playOnEnable;
        private SerializedProperty _useUnscaledTime;
        private SerializedProperty _randomizeStart;
        private SerializedProperty _speed;
        private SerializedProperty _targetSR;
        private SerializedProperty _targetImg;
        private SerializedProperty _onStarted;
        private SerializedProperty _onCompleted;
        private SerializedProperty _onLooped;
        private SerializedProperty _onFrameEvent;

        private string[] _names = Array.Empty<string>();
        private int _testSelection;

        private void OnEnable()
        {
            _set = serializedObject.FindProperty("animationSet");
            _defaultAnim = serializedObject.FindProperty("defaultAnimation");
            _playOnEnable = serializedObject.FindProperty("playOnEnable");
            _useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            _randomizeStart = serializedObject.FindProperty("randomizeStartFrame");
            _speed = serializedObject.FindProperty("speed");
            _targetSR = serializedObject.FindProperty("targetSpriteRenderer");
            _targetImg = serializedObject.FindProperty("targetImage");
            _onStarted = serializedObject.FindProperty("onAnimationStarted");
            _onCompleted = serializedObject.FindProperty("onAnimationCompleted");
            _onLooped = serializedObject.FindProperty("onAnimationLooped");
            _onFrameEvent = serializedObject.FindProperty("onFrameEvent");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_set);
            RefreshNames(_set.objectReferenceValue as SpriteAnimationSet);

            if (_names.Length > 0)
            {
                int current = Mathf.Max(0, Array.IndexOf(_names, _defaultAnim.stringValue));
                int picked = EditorGUILayout.Popup("Default Animation", current, _names);
                _defaultAnim.stringValue = _names[picked];
            }
            else
            {
                EditorGUILayout.PropertyField(_defaultAnim);
            }

            EditorGUILayout.PropertyField(_playOnEnable);
            EditorGUILayout.PropertyField(_useUnscaledTime);
            EditorGUILayout.PropertyField(_randomizeStart);
            EditorGUILayout.PropertyField(_speed);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target (未指定なら自動検出)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_targetSR);
            EditorGUILayout.PropertyField(_targetImg);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_onStarted);
            EditorGUILayout.PropertyField(_onCompleted);
            EditorGUILayout.PropertyField(_onLooped);
            EditorGUILayout.PropertyField(_onFrameEvent);

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying && _names.Length > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Play Mode テスト", EditorStyles.boldLabel);
                var animator = (SpriteAnimator)target;

                EditorGUILayout.BeginHorizontal();
                _testSelection = Mathf.Clamp(_testSelection, 0, _names.Length - 1);
                _testSelection = EditorGUILayout.Popup(_testSelection, _names);
                if (GUILayout.Button("Play", GUILayout.Width(60))) animator.Play(_names[_testSelection]);
                if (GUILayout.Button("Stop", GUILayout.Width(60))) animator.Stop();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("再生中", animator.CurrentAnimationName ?? "-");
            }
        }

        private void RefreshNames(SpriteAnimationSet set)
        {
            if (set == null)
            {
                _names = Array.Empty<string>();
                return;
            }

            var list = set.Animations;
            _names = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                _names[i] = list[i] != null ? list[i].AnimationName : "(missing)";
        }
    }
}
