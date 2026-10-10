using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Serializable wall jump tuning shared by WallRunModule and WallSlideModule.
    /// </summary>
    [System.Serializable]
    public class WallJumpSettings
    {
        [Tooltip("Velocità verticale (m/s) data dal wall jump.")]
        [Min(0f)] public float upSpeed = 8f;

        [Tooltip("Velocità (m/s) in allontanamento dalla parete.")]
        [Min(0f)] public float awaySpeed = 5f;

        [Tooltip("Velocità (m/s) mantenuta lungo la parete (direzione di corsa).")]
        [Min(0f)] public float alongSpeed = 3f;

        /// <summary>Returns the launch velocity. wallNormal points away from the wall.</summary>
        public Vector3 Compute(Vector3 wallNormal, Vector3 alongDir)
        {
            wallNormal.y = 0f;
            wallNormal.Normalize();
            alongDir.y = 0f;
            return wallNormal * awaySpeed + alongDir * alongSpeed + Vector3.up * upSpeed;
        }
    }
}