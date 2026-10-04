using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 出来事ごとのカメラ演出(揺れ・寄り)の設定一式。<see cref="CameraFeedbackDirector"/> に割り当てる。
    /// 出来事の種類(<see cref="CameraFeedbackEvent"/>)が増えても、既定値の項目が自動で追加される。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Camera Feedback Config", fileName = "New CameraFeedbackConfig")]
    public sealed class CameraFeedbackConfig : ScriptableObject
    {
        [Tooltip("全演出の強さの倍率。0で全て無効（酔いやすい人向けのオプションにも使える）。")]
        [Range(0f, 2f)]
        public float globalStrength = 1f;

        public List<CameraReaction> reactions = new List<CameraReaction>();

        public CameraReaction Get(CameraFeedbackEvent eventType)
        {
            foreach (var reaction in reactions)
            {
                if (reaction != null && reaction.eventType == eventType)
                {
                    return reaction;
                }
            }

            return null;
        }

        /// <summary>全ての出来事の項目が揃っているようにする。足りなければ既定値で追加しtrueを返す。</summary>
        public bool EnsureAllEvents()
        {
            var added = false;
            foreach (CameraFeedbackEvent eventType in Enum.GetValues(typeof(CameraFeedbackEvent)))
            {
                if (Get(eventType) == null)
                {
                    reactions.Add(CameraReaction.CreateDefault(eventType));
                    added = true;
                }
            }

            return added;
        }

        private void OnValidate()
        {
            reactions ??= new List<CameraReaction>();
            reactions.RemoveAll(r => r == null);
            EnsureAllEvents();
        }
    }
}
