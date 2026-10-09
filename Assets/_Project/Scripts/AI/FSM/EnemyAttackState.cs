using UnityEngine;

namespace Game.AI
{
    public class EnemyAttackState : EnemyState
    {
        private float _enterTime;
        private bool _executed;

        public override void Enter(EnemyBrain b)
        {
            _enterTime = Time.time;
            _executed = false;
            b.RegisterAttackUsed();
            b.AnimatorBridge?.PlayState("Attack", 1);
        }

        public override void Tick(EnemyBrain b, float dt)
        {
            if (!_executed && Time.time - _enterTime > 0.3f) _executed = true;

            if (Time.time - _enterTime > 0.9f)
            {
                // MODIFICATO: rilascia il token al CombatDirector
                b.ReleaseAttackToken();
                b.SetAttackToken(false);
                b.ChangeState(b.CombatState);
            }
        }
    }
}