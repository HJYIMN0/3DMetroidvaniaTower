using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// Raycast-based hit detection for melee weapons.
    /// Activated during the "active" phase of an attack animation.
    /// Dedups targets per swing.
    /// </summary>
    public class WeaponHitboxRaycaster : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private Transform rayTip;      // optional: if set, ray goes origin -> tip

        [Header("Raycast")]
        [SerializeField] private float rayLength = 1.2f;
        [SerializeField] private float sphereRadius = 0.15f;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Damage")]
        [SerializeField] private int baseDamage = 10;
        [SerializeField] private int basePoseDamage = 15;

        [Header("Runtime")]
        [SerializeField] private bool isActive;
        [SerializeField] private float currentDamageMultiplier = 1f;

        private readonly HashSet<IDamageable> _hitThisSwing = new();
        private readonly RaycastHit[] _buffer = new RaycastHit[16];

        public event Action<RaycastHit, IDamageable> OnHit;

        public void Activate(float damageMultiplier = 1f)
        {
            isActive = true;
            currentDamageMultiplier = damageMultiplier;
            _hitThisSwing.Clear();
        }

        public void Deactivate()
        {
            isActive = false;
        }

        private void Update()
        {
            if (!isActive) return;

            Vector3 origin = rayOrigin != null ? rayOrigin.position : transform.position;
            Vector3 dir;
            float len;

            if (rayTip != null)
            {
                Vector3 delta = rayTip.position - origin;
                len = delta.magnitude;
                dir = len > 0.0001f ? delta / len : transform.forward;
            }
            else
            {
                dir = transform.forward;
                len = rayLength;
            }

            int count = Physics.SphereCastNonAlloc(origin, sphereRadius, dir, _buffer, len, targetMask, triggerInteraction);
            for (int i = 0; i < count; i++)
            {
                var hit = _buffer[i];
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg == null) continue;
                if (_hitThisSwing.Contains(dmg)) continue;

                _hitThisSwing.Add(dmg);

                var info = DamageInfo.Basic(
                    Mathf.RoundToInt(baseDamage * currentDamageMultiplier),
                    gameObject,
                    hit.point,
                    hit.normal);
                info.poseDamage = basePoseDamage;

                dmg.ApplyDamage(info);
                OnHit?.Invoke(hit, dmg);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (rayOrigin == null) return;
            Vector3 origin = rayOrigin.position;
            Vector3 dir = rayTip != null ? (rayTip.position - origin).normalized : transform.forward;
            float len = rayTip != null ? (rayTip.position - origin).magnitude : rayLength;
            Gizmos.color = isActive ? Color.red : Color.yellow;
            Gizmos.DrawLine(origin, origin + dir * len);
            Gizmos.DrawWireSphere(origin + dir * len, sphereRadius);
        }
    }
}