using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Serializable wall jump tuning shared by WallRunModule and WallSlideModule.
    /// The jump ALWAYS leaves the wall. Vertical: a mandatory launch elevation angle (not steerable).
    /// Horizontal: direction opposite to the wall, steerable by the move input inside a control zone
    /// (max angle to the left / to the right). For controlLockTime seconds after the jump, LocomotionModule keeps
    /// the horizontal direction inside that zone, so holding the stick toward the wall cannot pull you back in.
    /// </summary>
    [System.Serializable]
    public class WallJumpSettings
    {
        [Header("Launch")]
        [Tooltip("Velocità totale (m/s) del lancio.")]
        [Min(0f)] public float launchSpeed = 9.5f;

        [Tooltip("Angolo verticale OBBLIGATORIO del lancio, in gradi sopra l'orizzontale. Non dipende dall'input. " +
                 "Alto (75) = sali molto e resti vicino alla parete (scavalcare); basso (45) = vai più lontano.")]
        [Range(5f, 89f)] public float elevationAngle = 60f;

        [Tooltip("Velocità (m/s) mantenuta lungo la parete. Solo per il wall run; per lo slide resta 0.")]
        [Min(0f)] public float alongSpeed = 0f;

        [Header("Control zone (rispetto alla direzione opposta alla parete)")]
        [Tooltip("Gradi massimi verso SINISTRA (guardando via dalla parete). Max 89: non può mai puntare verso la parete.")]
        [Range(0f, 89f)] public float maxAngleLeft = 45f;

        [Tooltip("Gradi massimi verso DESTRA (guardando via dalla parete). Max 89.")]
        [Range(0f, 89f)] public float maxAngleRight = 45f;

        [Header("Commit")]
        [Tooltip("Secondi dopo il salto in cui la direzione orizzontale resta dentro la zona di controllo e la velocità orizzontale viene mantenuta " +
                 "(non si può tornare contro la parete). 0 = nessun blocco. Basso (0.15) = ti riagganci prima.")]
        [Min(0f)] public float controlLockTime = 0.3f;

        /// <summary>
        /// Launch velocity. wallNormal points away from the wall; steerInput is the world-space move input.
        /// </summary>
        public Vector3 Compute(Vector3 wallNormal, Vector3 alongDir, Vector3 steerInput)
        {
            wallNormal.y = 0f;
            wallNormal.Normalize();
            alongDir.y = 0f;
            steerInput.y = 0f;

            Vector3 away = wallNormal;
            if (steerInput.sqrMagnitude > 0.01f)
            {
                // positive angle = clockwise seen from above = to the right when looking away from the wall
                float angle = Vector3.SignedAngle(wallNormal, steerInput.normalized, Vector3.up);
                angle = Mathf.Clamp(angle, -maxAngleLeft, maxAngleRight);
                away = Quaternion.AngleAxis(angle, Vector3.up) * wallNormal;
            }

            float elevation = elevationAngle * Mathf.Deg2Rad;
            Vector3 horizontal = away * (launchSpeed * Mathf.Cos(elevation)) + alongDir * alongSpeed;
            float vertical = launchSpeed * Mathf.Sin(elevation);
            return horizontal + Vector3.up * vertical;
        }

        /// <summary>Starts the post-jump heading lock (read by LocomotionModule). Call right after setting the launch velocity.</summary>
        public void ApplyHeadingLock(MovementContext ctx, Vector3 wallNormal)
        {
            if (controlLockTime <= 0f) return;
            wallNormal.y = 0f;
            wallNormal.Normalize();
            float centerDeg = Mathf.Atan2(wallNormal.x, wallNormal.z) * Mathf.Rad2Deg;
            ctx.LockHeading(controlLockTime, centerDeg, maxAngleLeft, maxAngleRight);
        }
    }
}