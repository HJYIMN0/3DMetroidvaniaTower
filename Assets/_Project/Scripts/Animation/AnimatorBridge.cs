using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Animation
{
    public class AnimatorBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationPhaseProfile phaseProfile;

        [Header("Parameter Names (case-sensitive, must match Animator Controller)")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string verticalVelocityParam = "VerticalVelocity";
        [SerializeField] private string jumpParam = "Jump";
        [SerializeField] private string attackParam = "Attack";

        public Animator Animator => animator;
        public AnimationPhaseProfile PhaseProfile => phaseProfile;

        public event Action<string, int> OnStatePlayed;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();

            if (animator != null && Mathf.Approximately(animator.speed, 0f))
                Debug.LogWarning("[AnimatorBridge] Animator.speed = 0 in Awake — animation will not advance.", this);

            ValidateParameters();
        }

        [ContextMenu("Validate Parameters")]
        public void ValidateParameters()
        {
            if (animator == null) { Debug.LogError("[AnimatorBridge] Animator is null.", this); return; }

            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in animator.parameters) existing.Add(p.name);

            var missing = new StringBuilder();
            foreach (var n in new[] { speedParam, groundedParam, verticalVelocityParam, jumpParam, attackParam })
                if (!string.IsNullOrEmpty(n) && !existing.Contains(n))
                    missing.AppendLine($"  - \"{n}\"");

            if (missing.Length > 0)
                Debug.LogError($"[AnimatorBridge] Missing parameters on '{name}':\n{missing}", this);
        }

        // ---------- Typed API ----------
        public void SetLocomotionSpeed(float normalized01) => SetFloat(speedParam, Mathf.Clamp01(normalized01));
        public void SetGrounded(bool value) => SetBool(groundedParam, value);
        public void SetVerticalVelocity(float value) => SetFloat(verticalVelocityParam, value);
        public void TriggerJump() => SetTrigger(jumpParam);
        public void TriggerAttack() => SetTrigger(attackParam);

        // ---------- Generic API (unchanged behaviour) ----------
        public void PlayState(string stateName, int layer, float normalizedTime = 0f)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return;
            animator.Play(stateName, layer, normalizedTime);
            OnStatePlayed?.Invoke(stateName, layer);
        }

        public void SetFloat(string name, float value) { if (animator && !string.IsNullOrEmpty(name)) animator.SetFloat(name, value); }
        public void SetBool(string name, bool value) { if (animator && !string.IsNullOrEmpty(name)) animator.SetBool(name, value); }
        public void SetTrigger(string name) { if (animator && !string.IsNullOrEmpty(name)) animator.SetTrigger(name); }

        public float GetNormalizedTime(int layer) =>
            animator != null ? animator.GetCurrentAnimatorStateInfo(layer).normalizedTime % 1f : 0f;

        /// <summary>Playback speed of the Animator (NOT the "Speed" blend parameter).</summary>
        public void SetSpeed(float speed) { if (animator) animator.speed = speed; }

        public AnimationPhaseProfile GetPhaseProfile() => phaseProfile;
    }
}