using System.Collections;
using UnityEngine;
using Game.Life;
using Game.Animation;

namespace Game.Combat
{
    /// <summary>
    /// Syncs attacker and victim animations for a critical execution.
    /// Uses SyncMarker approach: positions victim, then plays both clips at t=0.
    /// </summary>
    public class CriticalExecutionController : MonoBehaviour
    {
        [SerializeField] private AnimatorBridge attackerBridge;
        [SerializeField] private string attackerStateName = "Critical_Attacker";
        [SerializeField] private string victimStateName = "Critical_Victim";
        [SerializeField] private float executionDuration = 2f;

        [Header("Placement")]
        [SerializeField] private float victimForwardOffset = 0.8f;
        [SerializeField] private bool snapVictimFacingAttacker = true;

        public IEnumerator Execute(AbstractLifeController victim)
        {
            if (victim == null) yield break;
            var victimBridge = victim.GetComponent<AnimatorBridge>();
            var victimLife = victim as AbstractLifeController;

            // Freeze both
            victimLife.IsInvulnerable = true;
            var victimRb = victim.GetComponent<Rigidbody>();
            var attackerRb = GetComponent<Rigidbody>();
            if (victimRb != null) victimRb.isKinematic = true;
            if (attackerRb != null) attackerRb.isKinematic = true;

            // Place victim
            Vector3 targetPos = transform.position + transform.forward * victimForwardOffset;
            victim.transform.position = targetPos;
            if (snapVictimFacingAttacker)
                victim.transform.rotation = Quaternion.LookRotation(-transform.forward);

            // Play both at normalizedTime 0
            attackerBridge?.PlayState(attackerStateName, 2, 0f);
            victimBridge?.PlayState(victimStateName, 2, 0f);

            // Wait
            yield return new WaitForSeconds(executionDuration);

            // Apply damage
            victim.ApplyDamage(new Core.DamageInfo
            {
                isCritical = true,
                source = gameObject,
                amount = 0,
                ignoreInvulnerability = true
            });

            // Unfreeze
            victimLife.IsInvulnerable = false;
            if (victimRb != null) victimRb.isKinematic = false;
            if (attackerRb != null) attackerRb.isKinematic = false;
        }
    }
}