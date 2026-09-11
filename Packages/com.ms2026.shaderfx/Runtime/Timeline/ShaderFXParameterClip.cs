using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace MS2026.ShaderFX.Timeline
{
    public sealed class ShaderFXParameterClip : PlayableAsset, ITimelineClipAsset
    {
        public ShaderFXParameterBehaviour template = new();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<ShaderFXParameterBehaviour>.Create(graph, template);
        }
    }
}
