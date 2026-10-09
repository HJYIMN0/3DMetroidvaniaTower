using UnityEngine;

namespace Game.AI
{
    public class EnemyStunnedState : EnemyState
    {
        public override void Enter(EnemyBrain b)
        {
            b.AnimatorBridge?.PlayState("Stun", 2);
            if (b.Agent != null && b.Agent.isOnNavMesh) b.Agent.isStopped = true;
        }

        public override void Exit(EnemyBrain b)
        {
            if (b.Agent != null && b.Agent.isOnNavMesh) b.Agent.isStopped = false;
        }
    }
}