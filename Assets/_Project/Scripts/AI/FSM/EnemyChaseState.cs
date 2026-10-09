using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    public class EnemyChaseState : EnemyState
    {
        public override void Enter(EnemyBrain b)
        {
            if (b.Agent != null && b.Agent.isOnNavMesh)
            {
                b.Agent.isStopped = false;
                b.Agent.speed = b.Data.movementProfile.chaseSpeed;
            }
        }

        public override void Tick(EnemyBrain b, float dt)
        {
            if (b.Target == null) { b.ChangeState(b.IdleState); return; }

            float d = Vector3.Distance(b.transform.position, b.Target.position);
            if (d <= b.Data.combatProfile.attackRange)
            {
                b.ChangeState(b.CombatState);
                return;
            }

            if (b.Agent != null && b.Agent.isOnNavMesh)
                b.Agent.SetDestination(b.Target.position);
        }

        public override void Exit(EnemyBrain b)
        {
            if (b.Agent != null && b.Agent.isOnNavMesh) b.Agent.isStopped = true;
        }
    }
}