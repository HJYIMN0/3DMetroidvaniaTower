using System.Collections.Generic;
using UnityEngine;
using Game.Input;

namespace Game.Combat
{
    [CreateAssetMenu(menuName = "Game/Combat/Combo Definition", fileName = "Combo_")]
    public class ComboDefinition : ScriptableObject
    {
        public string comboName = "New Combo";
        public List<ComboStep> steps = new();

        /// <summary>
        /// If true, input windows are expressed as normalizedTime of the current animation.
        /// If false, they are expressed in absolute seconds.
        /// </summary>
        public bool useNormalizedTime = true;

        public ComboStep GetStep(int index)
        {
            if (index < 0 || index >= steps.Count) return null;
            return steps[index];
        }
    }
}