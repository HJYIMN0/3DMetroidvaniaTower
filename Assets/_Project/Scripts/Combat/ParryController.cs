using Game.Core;
using Game.Life;
using System;
using UnityEngine;

namespace Game.Combat
{
    public class ParryController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float parryWindowDuration = 0.25f;

        [Header("Direction")]
        [SerializeField] private bool isOmniDirectionalParry = true;
        [SerializeField] private float parryAngleTolerance = 90f;

        [Header("Effect")]
        [SerializeField] private int destabilizationOnParry = 30;

        public bool IsParrying { get; private set; }
        private float _parryEndTime;

        public event Action<GameObject> OnParrySuccess;

        public bool IsOmniDirectionalParry
        {
            get => isOmniDirectionalParry;
            set => isOmniDirectionalParry = value;
        }

        public void BeginParry()
        {
            IsParrying = true;
            _parryEndTime = Time.time + parryWindowDuration;
            PlayerInputState.IsParrying = true;
        }

        public void EndParry()
        {
            IsParrying = false;
            PlayerInputState.IsParrying = false;
        }

        private void Update()
        {
            if (IsParrying && Time.time >= _parryEndTime) EndParry();
        }

        /// <summary>
        /// Called by WeaponHitboxRaycaster when the parrier is hit.
        /// </summary>
        public bool TryParry(GameObject attacker, Vector3 incomingDirection)
        {
            if (!IsParrying) return false;

            if (!isOmniDirectionalParry)
            {
                float angle = Vector3.Angle(transform.forward, -incomingDirection.normalized);
                if (angle > parryAngleTolerance) return false;
            }

            var attackerLife = attacker.GetComponentInParent<AbstractLifeController>();
            if (attackerLife != null) attackerLife.AddPose(destabilizationOnParry);

            OnParrySuccess?.Invoke(attacker);
            EndParry();
            return true;
        }
    }
}