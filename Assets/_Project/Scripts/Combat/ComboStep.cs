using UnityEngine;
using Game.Input;

namespace Game.Combat
{
    [System.Serializable]
    public class ComboStep
    {
        public string label = "Step";
        public InputCommandType requiredInput = InputCommandType.Attack;

        [Tooltip("Animator state name to play for this step.")]
        public string animatorStateName;

        [Tooltip("Start of the input window (relative to previous step start).")]
        public float inputWindowStart = 0.15f;

        [Tooltip("End of the input window.")]
        public float inputWindowEnd = 0.65f;

        [Tooltip("Damage multiplier applied to base weapon damage.")]
        public float damageMultiplier = 1f;

        [Tooltip("Can this step be canceled into a dodge?")]
        public bool allowCancelIntoDodge = true;

        [Tooltip("Can this step be canceled into a parry?")]
        public bool allowCancelIntoParry = true;
    }
}