using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>Play中に演出を試し撃ちし、実際に鳴った/鳴らなかった履歴を表示する。</summary>
    public sealed class CameraFeedbackTester
    {
        private const int MaxLogEntries = 8;

        private static readonly string[] RelatedLabels = { "関係者なし", "P1", "P2", "P3", "P4" };

        private readonly List<string> _log = new List<string>();
        private CameraFeedbackDirector _subscribed;
        private int _relatedChoice = 1;

        public void Draw(CameraTabContext context)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("試し撃ち", EditorStyles.miniBoldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Modeに入ると、ここから演出を試し撃ちできます。視点をP1〜P4に切り替えながら撃つと、画面ごとの出し分けを確認できます。", MessageType.Info);
                Unsubscribe();
                return;
            }

            Subscribe(context.Director);

            _relatedChoice = EditorGUILayout.Popup(new GUIContent("関係するプレイヤー", "誰が割った/誰の砲台がオーバーヒートした、の想定。"), _relatedChoice, RelatedLabels);
            var related = _relatedChoice - 1;

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (CameraFeedbackEvent eventType in Enum.GetValues(typeof(CameraFeedbackEvent)))
                {
                    if (GUILayout.Button(CameraFeedbackLabels.Event(eventType)))
                    {
                        Fire(context, eventType, related);
                    }
                }
            }

            foreach (var entry in _log)
            {
                EditorGUILayout.LabelField(entry, EditorStyles.miniLabel);
            }
        }

        private static void Fire(CameraTabContext context, CameraFeedbackEvent eventType, int related)
        {
            var points = context.Points;
            Vector2 position = points.MapCenter;
            if (ViewerIndex.IsPlayer(related) && eventType == CameraFeedbackEvent.TurretOverheated && points.TryGetTurret(related, out var turret))
            {
                position = turret;
            }

            var amount = eventType == CameraFeedbackEvent.CoreDamaged ? 10f : (float?)null;
            context.Director.Play(new CameraFeedbackRequest(eventType, related, position, amount), true);
        }

        private void Subscribe(CameraFeedbackDirector director)
        {
            if (director == _subscribed)
            {
                return;
            }

            Unsubscribe();
            _subscribed = director;
            if (_subscribed != null)
            {
                _subscribed.Played += HandlePlayed;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.Played -= HandlePlayed;
            }

            _subscribed = null;
        }

        private void HandlePlayed(CameraFeedbackRequest request, float strength)
        {
            var who = request.HasRelatedPlayer ? $"P{request.RelatedPlayer + 1}" : "関係者なし";
            var result = strength > 0f ? $"強さ {strength:0.00}" : "この画面では鳴らさない";
            _log.Insert(0, $"{Time.unscaledTime:0.0}s  {CameraFeedbackLabels.Event(request.EventType)} ({who}) → {result}");

            if (_log.Count > MaxLogEntries)
            {
                _log.RemoveAt(_log.Count - 1);
            }
        }
    }
}
