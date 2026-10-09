using UnityEngine;
using Game.Input;

namespace Game.Combat
{
    /// <summary>
    /// Evaluates combo progression. Pure logic, no animation calls.
    /// The CombatManager decides what to do with the returned step.
    /// </summary>
    public class ComboController
    {
        private ComboDefinition _current;
        private int _stepIndex;
        private float _stepStartTime;

        public ComboDefinition CurrentCombo => _current;
        public int CurrentStepIndex => _stepIndex;
        public ComboStep CurrentStep => _current != null ? _current.GetStep(_stepIndex) : null;
        public bool IsInCombo => _current != null && _stepIndex >= 0;

        /// <summary>Start a new combo from step 0.</summary>
        public ComboStep StartCombo(ComboDefinition def)
        {
            _current = def;
            _stepIndex = 0;
            _stepStartTime = Time.time;
            return CurrentStep;
        }

        /// <summary>
        /// Try to advance the combo with a new input.
        /// Returns the new step if the input is valid, or null.
        /// </summary>
        public ComboStep TryAdvance(InputCommandType input)
        {
            if (_current == null) return null;
            var step = CurrentStep;
            if (step == null) return null;
            if (step.requiredInput != input) return null;

            if (!IsInWindow()) return null;

            int next = _stepIndex + 1;
            if (next >= _current.steps.Count)
            {
                // Combo finished — loop back or end
                Reset();
                return null;
            }
            _stepIndex = next;
            _stepStartTime = Time.time;
            return CurrentStep;
        }

        /// <summary>Must be called when the animation normalized time is known.</summary>
        public bool IsInWindow(float currentNormalizedTime = -1f)
        {
            var step = CurrentStep;
            if (step == null) return false;

            if (_current.useNormalizedTime && currentNormalizedTime >= 0f)
                return currentNormalizedTime >= step.inputWindowStart && currentNormalizedTime <= step.inputWindowEnd;

            float elapsed = Time.time - _stepStartTime;
            return elapsed >= step.inputWindowStart && elapsed <= step.inputWindowEnd;
        }

        public void Reset()
        {
            _current = null;
            _stepIndex = -1;
        }
    }
}