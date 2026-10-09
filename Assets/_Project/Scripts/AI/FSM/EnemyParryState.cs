using UnityEngine;

namespace Game.AI
{
    public class EnemyParryState : EnemyState
    {
        private float _enterTime;

        public override void Enter(EnemyBrain b)
        {
            _enterTime = Time.time;
            b.Parry?.BeginParry();
            b.AnimatorBridge?.PlayState("Parry", 1);
            b.RegisterParryUsed();
        }

        public override void Tick(EnemyBrain b, float dt)
        {
            if (Time.time - _enterTime > 0.6f)
                b.ChangeState(b.CombatState);
        }
    }
}