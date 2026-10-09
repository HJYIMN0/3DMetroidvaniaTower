using UnityEngine;

namespace Game.AI
{
    [CreateAssetMenu(menuName = "Game/AI/Enemy Movement Profile")]
    public class EnemyMovementProfile : ScriptableObject
    {
        public float walkSpeed = 2f;
        public float chaseSpeed = 4f;
        public float stoppingDistance = 1.5f;
        public float turnSpeed = 360f;
    }
}