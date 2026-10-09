using UnityEngine;

namespace Game.AI
{
    public class EnemyIdleState : EnemyState
    {
        public override void Tick(EnemyBrain b, float dt)
        {
            if (b.Target != null)
            {
                float d = Vector3.Distance(b.transform.position, b.Target.position);
                if (d <= b.Data.combatProfile.attackRange + 5f)
                    b.ChangeState(b.ChaseState);
            }
        }
    }
}