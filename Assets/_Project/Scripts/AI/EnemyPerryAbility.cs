using UnityEngine;

namespace Game.AI
{
    [CreateAssetMenu(menuName = "Game/AI/Abilities/Parry")]
    public class EnemyParryAbility : EnemyAbility
    {
        public string animatorState = "Parry";
        public float parryWindowDuration = 0.3f;

        public override bool CanExecute(EnemyBrain brain)
        {
            return brain.Target != null;
        }

        public override void Execute(EnemyBrain brain)
        {
            brain.Parry?.BeginParry();
            brain.AnimatorBridge?.PlayState(animatorState, 1);
        }
    }
}