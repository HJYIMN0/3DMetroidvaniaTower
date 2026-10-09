using System;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    public class DodgeController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float dodgeDuration = 0.5f;
        [SerializeField] private float iFrameStartDelay = 0.05f;
        [SerializeField] private float iFrameDuration = 0.3f;

        [Header("Motion")]
        [SerializeField] private float dodgeDistance = 4f;
        [SerializeField] private AnimationCurve dodgeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Se true, azzera la velocità verticale durante la schivata.")]
        [SerializeField] private bool cancelVerticalVelocity = false;

        private float _dodgeStartTime;
        private Vector3 _dodgeDirection;
        private bool _isDodging;

        public bool IsDodging => _isDodging;
        public bool IsInvulnerable { get; private set; }
        public bool CancelVerticalVelocity => cancelVerticalVelocity;
        public Vector3 DodgeDirection => _dodgeDirection;
        public float DodgeDistance => dodgeDistance;

        public event Action OnDodgeStart;
        public event Action OnDodgeEnd;

        public void BeginDodge(Vector3 direction, Life.AbstractLifeController life)
        {
            _isDodging = true;
            _dodgeStartTime = Time.time;
            _dodgeDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            if (_dodgeDirection.sqrMagnitude < 0.0001f) _dodgeDirection = -transform.forward;
            PlayerInputState.IsDodging = true;
            OnDodgeStart?.Invoke();
        }

        /// <summary>
        /// Horizontal velocity (world space) to apply this frame.
        /// Sampled from the curve so 'dodgeDistance' actually controls the total travel.
        /// </summary>
        public Vector3 GetCurrentVelocity()
        {
            if (!_isDodging) return Vector3.zero;

            float t = Mathf.Clamp01((Time.time - _dodgeStartTime) / dodgeDuration);

            // Numerical derivative of the position curve -> instantaneous speed
            const float delta = 0.01f;
            float t2 = Mathf.Clamp01(t + delta);
            float distNow = dodgeCurve.Evaluate(t);
            float distNext = dodgeCurve.Evaluate(t2);
            float speed = (distNext - distNow) / delta * (dodgeDistance / dodgeDuration);

            return _dodgeDirection * speed;
        }

        private void Update()
        {
            if (!_isDodging) return;

            float t = Time.time - _dodgeStartTime;
            if (t >= dodgeDuration)
            {
                _isDodging = false;
                IsInvulnerable = false;
                PlayerInputState.IsDodging = false;
                OnDodgeEnd?.Invoke();
                return;
            }

            IsInvulnerable = t >= iFrameStartDelay && t <= iFrameStartDelay + iFrameDuration;
        }
    }
}