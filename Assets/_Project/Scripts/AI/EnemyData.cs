using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.AI
{
    [CreateAssetMenu(menuName = "Game/AI/Enemy Data", fileName = "EnemyData_")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyName = "Enemy";

        [Header("Combat Profiles")]
        public EnemyMovementProfile movementProfile;
        public EnemyCombatProfile combatProfile;

        [Header("Abilities (polymorphic, hot-swappable)")]
        [SerializeReference] public List<EnemyAbility> abilities = new();

        [Header("Combo")]
        public ComboDefinition attackCombo;

        [Header("Animator")]
        public RuntimeAnimatorController animatorController;
    }
}