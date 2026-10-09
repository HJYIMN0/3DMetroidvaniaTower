using System.Collections.Generic;
using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Per-state timing info for attack/jump phases.
    /// Values are normalizedTime within the state's clip.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Animation/Phase Profile")]
    public class AnimationPhaseProfile : ScriptableObject
    {
        [System.Serializable]
        public class StatePhases
        {
            public string stateName;
            [Range(0f, 1f)] public float prepEnd = 0.25f;
            [Range(0f, 1f)] public float activeEnd = 0.55f;
            // recovery is [activeEnd, 1]
        }

        public List<StatePhases> states = new();

        public StatePhases Get(string stateName)
        {
            for (int i = 0; i < states.Count; i++)
                if (states[i].stateName == stateName) return states[i];
            return null;
        }
    }
}