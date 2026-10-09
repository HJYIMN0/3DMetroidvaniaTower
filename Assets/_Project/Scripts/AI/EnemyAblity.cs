using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Base ScriptableObject for enemy abilities. Hot-swappable.
    /// </summary>
    public abstract class EnemyAbility : ScriptableObject
    {
        public string abilityName;
        public float cooldown = 1f;

        public abstract bool CanExecute(EnemyBrain brain);
        public abstract void Execute(EnemyBrain brain);
    }
}