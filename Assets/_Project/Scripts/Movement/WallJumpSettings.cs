using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Serializable wall jump tuning shared by WallRunModule and WallSlideModule.
    /// The jump ALWAYS leaves the wall. Horizontal direction: the wall normal snapped to the nearest of the player's
    /// four local axes (forward, back, right, left; relative to the PLAYER, not the camera). Vertical: a mandatory
    /// launch elevation angle. After the jump LocomotionModule keeps that direction (inside the optional left/right
    /// zone) while you rise, so you cannot steer back into the wall and slide up along it.
    /// </summary>
    [System.Serializable]
    public class WallJumpSettings
    {
        [Header("Launch")]
        [Tooltip("Velocità totale (m/s) del lancio.")]
        [Min(0f)] public float launchSpeed = 9.5f;

        [Tooltip("Angolo verticale OBBLIGATORIO del lancio, in gradi sopra l'orizzontale. Non dipende dall'input. Max 80.")]
        [Range(5f, 80f)] public float elevationAngle = 60f;

        [Tooltip("Velocità orizzontale MINIMA (m/s) in allontanamento dalla parete. Se l'angolo è molto alto la componente orizzontale " +
                 "sarebbe quasi zero e il player salirebbe strisciando lungo il muro: questo valore lo impedisce.")]
        [Min(0f)] public float minAwaySpeed = 3f;

        [Tooltip("Velocità (m/s) mantenuta lungo la parete. Solo per il wall run; per lo slide resta 0.")]
        [Min(0f)] public float alongSpeed = 0f;

        [Header("Direction")]
        [Tooltip("true = la direzione orizzontale è la normale della parete agganciata all'asse più vicino del PLAYER " +
                 "(avanti, indietro, destra, sinistra: 4 direzioni -x, x, z, -z). false = normale esatta della parete.")]
        public bool snapToPlayerAxes = true;

        [Tooltip("Gradi massimi verso SINISTRA (guardando via dalla parete) con cui l'input può inclinare il salto. 0 = nessuna inclinazione. Max 89.")]
        [Range(0f, 89f)] public float maxAngleLeft = 0f;

        [Tooltip("Gradi massimi verso DESTRA (guardando via dalla parete). 0 = nessuna inclinazione. Max 89.")]
        [Range(0f, 89f)] public float maxAngleRight = 0f;

        [Header("Commit")]
        [Tooltip("Secondi minimi dopo il salto in cui la direzione orizzontale resta bloccata (zona sopra) e la velocità orizzontale viene mantenuta. 0 = nessun blocco.")]
        [Min(0f)] public float controlLockTime = 0.35f;

        [Tooltip("Il blocco dura almeno finché stai salendo (fino all'apice). Impedisce di tornare contro la parete mentre sali.")]
        public bool lockUntilApex = true;

        /// <summary>
        /// Horizontal direction away from the wall. wallNormal points away from the wall.
        /// With snapToPlayerAxes it returns the player's local axis (forward/back/right/left) closest to the normal:
        /// the angle to the normal is at most 45 degrees, so it never points into the wall.
        /// </summary>
        public Vector3 GetAwayDirection(Vector3 wallNormal, Transform player)
        {
            wallNormal.y = 0f;
            wallNormal.Normalize();
            if (!snapToPlayerAxes || player == null) return wallNormal;

            Vector3 f = player.forward; f.y = 0f; f.Normalize();
            Vector3 r = player.right; r.y = 0f; r.Normalize();
            if (f.sqrMagnitude < 0.0001f || r.sqrMagnitude < 0.0001f) return wallNormal;

            Vector3 best = f;
            float bestDot = Vector3.Dot(wallNormal, f);
            float d;
            d = Vector3.Dot(wallNormal, -f); if (d > bestDot) { bestDot = d; best = -f; }
            d = Vector3.Dot(wallNormal, r); if (d > bestDot) { bestDot = d; best = r; }
            d = Vector3.Dot(wallNormal, -r); if (d > bestDot) { best = -r; }
            return best;
        }

        /// <summary>Launch velocity. awayDir comes from GetAwayDirection; steerInput is the world-space move input.</summary>
        public Vector3 Compute(Vector3 awayDir, Vector3 alongDir, Vector3 steerInput)
        {
            awayDir.y = 0f;
            awayDir.Normalize();
            alongDir.y = 0f;
            steerInput.y = 0f;

            Vector3 away = awayDir;
            if ((maxAngleLeft > 0f || maxAngleRight > 0f) && steerInput.sqrMagnitude > 0.01f)
            {
                // positive angle = clockwise seen from above = to the right when looking away from the wall
                float angle = Vector3.SignedAngle(awayDir, steerInput.normalized, Vector3.up);
                angle = Mathf.Clamp(angle, -maxAngleLeft, maxAngleRight);
                away = Quaternion.AngleAxis(angle, Vector3.up) * awayDir;
            }

            float elevation = elevationAngle * Mathf.Deg2Rad;
            float horizontalSpeed = Mathf.Max(launchSpeed * Mathf.Cos(elevation), minAwaySpeed);
            float verticalSpeed = launchSpeed * Mathf.Sin(elevation);
            return away * horizontalSpeed + alongDir * alongSpeed + Vector3.up * verticalSpeed;
        }

        /// <summary>Starts the post-jump heading lock (read by LocomotionModule). Call right after setting the launch velocity.</summary>
        public void ApplyHeadingLock(MovementContext ctx, Vector3 awayDir)
        {
            if (controlLockTime <= 0f && !lockUntilApex) return;
            awayDir.y = 0f;
            awayDir.Normalize();
            float centerDeg = Mathf.Atan2(awayDir.x, awayDir.z) * Mathf.Rad2Deg;
            ctx.LockHeading(controlLockTime, centerDeg, maxAngleLeft, maxAngleRight, lockUntilApex);
        }
    }
}