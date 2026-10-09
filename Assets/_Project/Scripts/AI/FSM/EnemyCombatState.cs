using UnityEngine;

namespace Game.AI
{
    public class EnemyCombatState : EnemyState
    {
        private float _stateEnterTime;

        public override void Enter(EnemyBrain b)
        {
            _stateEnterTime = Time.time;
        }

        public override void Tick(EnemyBrain b, float dt)
        {
            if (b.Target == null) { b.ChangeState(b.IdleState); return; }

            // Reaction delay window
            if (Time.time - _stateEnterTime < b.Data.combatProfile.reactionDelay) return;

            float d = Vector3.Distance(b.transform.position, b.Target.position);
            if (d > b.Data.combatProfile.attackRange + 0.5f)
            {
                b.ChangeState(b.ChaseState);
                return;
            }

            float attackScore = ScoreAttack(b, d);
            float parryScore = ScoreParry(b, d);


            if (parryScore > attackScore && b.CanUseParry())
            {
                b.ChangeState(b.ParryState);
            }
            else if (attackScore > 0f && b.CanAttack())
            {
                if (b.TryAttack())
                {
                    b.SetAttackToken(true);
                    b.ChangeState(b.AttackState);
                }
                // else: in coda, resta in CombatState e ritenta al prossimo frame
            }
        }

        private float ScoreAttack(EnemyBrain b, float d)
        {
            var p = b.Data.combatProfile;
            float proximity = Mathf.Clamp01(1f - (d / p.attackRange));
            float bias = p.attackBias;
            float rnd = Random.Range(-p.randomness, p.randomness);
            return proximity * bias + rnd;
        }

        private float ScoreParry(EnemyBrain b, float d)
        {
            var p = b.Data.combatProfile;
            // Input reading with delay: was the player attacking recently?
            float since = Time.time - Core.PlayerInputState.LastAttackInputTime;
            if (since > p.reactionDelay || since < 0) return 0f;

            float rnd = Random.Range(0f, 1f);
            if (rnd > p.parryReactionProbability) return 0f;

            float proximity = Mathf.Clamp01(1f - (d / p.parryRange));
            return proximity * 1.2f; // slightly favors parry when reading is active
        }
    }
}