using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Runtime hub exposing the player's input state for AI input-reading.
    /// Registered by InputReader; read by EnemyBrain with a reaction delay.
    /// </summary>
    public static class PlayerInputState
    {
        public static float LastAttackInputTime = -999f;
        public static float LastParryInputTime = -999f;
        public static float LastDodgeInputTime = -999f;
        public static bool IsAttacking = false;
        public static bool IsParrying = false;
        public static bool IsDodging = false;

        public static void Reset()
        {
            LastAttackInputTime = LastParryInputTime = LastDodgeInputTime = -999f;
            IsAttacking = IsParrying = IsDodging = false;
        }
    }
}