using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Runtime hook for AnimationTunerWindow. Exposes speed scaling and phase overrides.
    /// </summary>
    public class AnimationTuner : MonoBehaviour
    {
        [SerializeField] private AnimatorBridge bridge;
        [SerializeField] private float globalSpeed = 1f;
        [SerializeField] private bool isRealTime = false;

        public AnimatorBridge Bridge => bridge;
        public float GlobalSpeed => globalSpeed;
        public bool IsRealTime => isRealTime;

        private void Awake()
        {
            if (bridge == null) bridge = GetComponent<AnimatorBridge>();
            SetGlobalSpeed(globalSpeed);
        }

        public void SetGlobalSpeed(float speed)
        {
            globalSpeed = Mathf.Max(0.01f, speed);
            if (bridge != null && bridge.Animator != null)
                bridge.Animator.speed = globalSpeed;
        }

        public void SetRealTime(bool value) { isRealTime = value; }

        public void SetPhase(string stateName, float prepEnd, float activeEnd)
        {
            if (bridge == null || bridge.PhaseProfile == null) return;
            var p = bridge.PhaseProfile.Get(stateName);
            if (p == null) { p = new AnimationPhaseProfile.StatePhases { stateName = stateName }; bridge.PhaseProfile.states.Add(p); }
            p.prepEnd = Mathf.Clamp01(prepEnd);
            p.activeEnd = Mathf.Clamp01(activeEnd);
        }
    }
}