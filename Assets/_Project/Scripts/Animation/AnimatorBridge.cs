using System;
using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Thin wrapper over Animator. Central point for all animation calls.
    /// </summary>
    public class AnimatorBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationPhaseProfile phaseProfile;

        public Animator Animator => animator;
        public AnimationPhaseProfile PhaseProfile => phaseProfile;

        public event Action<string, int> OnStatePlayed;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
        }

        public void PlayState(string stateName, int layer, float normalizedTime = 0f)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return;
            animator.Play(stateName, layer, normalizedTime);
            OnStatePlayed?.Invoke(stateName, layer);
        }

        public void SetFloat(string name, float value) { if (animator) animator.SetFloat(name, value); }
        public void SetBool(string name, bool value) { if (animator) animator.SetBool(name, value); }
        public void SetTrigger(string name) { if (animator) animator.SetTrigger(name); }

        public float GetNormalizedTime(int layer) => animator != null ? animator.GetCurrentAnimatorStateInfo(layer).normalizedTime % 1f : 0f;

        public void SetSpeed(float speed) { if (animator) animator.speed = speed; }

        /// <summary>Register a phase behaviour at runtime (called by StateMachineBehaviour in OnStateEnter).</summary>
        public AnimationPhaseProfile GetPhaseProfile() => phaseProfile;
    }
}