using UnityEngine;

namespace Game.AI
{
    [CreateAssetMenu(menuName = "Game/AI/Abilities/Melee Attack")]
    public class EnemyMeleeAttackAbility : EnemyAbility
    {
        public string animatorState = "Attack";
        public float damageMultiplier = 1f;
        public float range = 2f;

        public override bool CanExecute(EnemyBrain brain)
        {
            if (brain.Target == null) return false;
            float d = Vector3.Distance(brain.transform.position, brain.Target.position);
            return d <= range;
        }

        public override void Execute(EnemyBrain brain)
        {
            brain.AnimatorBridge?.PlayState(animatorState, 1);
        }
    }
}