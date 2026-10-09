using UnityEngine;

namespace Game.AI
{
    [CreateAssetMenu(menuName = "Game/AI/Enemy Combat Profile")]
    public class EnemyCombatProfile : ScriptableObject
    {
        [Header("Ranges")]
        public float attackRange = 2f;
        public float parryRange = 2f;

        [Header("Cooldowns")]
        public float attackCooldown = 1.5f;
        public float parryCooldown = 2f;

        [Header("AI Bias")]
        [Range(0f, 1f)] public float attackBias = 0.5f;
        [Range(0f, 1f)] public float parryReactionProbability = 0.4f;
        [Range(0f, 1f)] public float randomness = 0.2f;

        [Header("Input Reading")]
        public float reactionDelay = 0.15f;
    }
}