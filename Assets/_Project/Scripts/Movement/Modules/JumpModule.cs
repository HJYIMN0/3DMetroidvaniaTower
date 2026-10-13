using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Jump + vertical arc (gravity). Passive module. The arc is defined by two designer-friendly numbers:
    /// jumpHeight (meters) and timeToApex (seconds); gravity and takeoff speed are derived:
    /// g = 2h / t^2, v0 = 2h / t. Falling, jump-cut (variable height) and apex hang are separate multipliers.
    /// </summary>
    public class JumpModule : MovementModule
    {
        [Header("Jump arc")]
        [Tooltip("Altezza massima (m) del salto tenendo premuto il tasto. Misura reale: HUD 'Last jump peak'.")]
        [SerializeField, Min(0.05f)] private float jumpHeight = 2.2f;
        [Tooltip("Secondi per raggiungere l'apice. Più basso = salto più scattante (gravità più forte).")]
        [SerializeField, Min(0.05f)] private float timeToApex = 0.4f;

        [Header("Gravity shaping")]
        [Tooltip("Moltiplicatore di gravità in discesa. >1 = si cade più in fretta di quanto si sale.")]
        [SerializeField, Min(0.1f)] private float fallGravityMultiplier = 1.6f;
        [Tooltip("Moltiplicatore di gravità in salita se il tasto è rilasciato (salto variabile). 1 = salto sempre a piena altezza.")]
        [SerializeField, Min(0.1f)] private float jumpCutGravityMultiplier = 3f;
        [Tooltip("Sotto questa |velocità verticale| (m/s) si applica la gravità d'apice.")]
        [SerializeField, Min(0f)] private float apexThreshold = 1f;
        [Tooltip("Moltiplicatore di gravità vicino all'apice. <1 = 'hang time' (sospensione). 1 = disattivato.")]
        [SerializeField, Min(0.1f)] private float apexGravityMultiplier = 0.6f;
        [Tooltip("Velocità massima di caduta (m/s).")]
        [SerializeField, Min(0f)] private float maxFallSpeed = 25f;

        [Header("Forgiveness")]
        [Tooltip("Coyote time (s): per quanto tempo dopo aver lasciato il bordo il salto da terra è ancora valido. " +
                 "Parte dal momento in cui il terreno non viene più rilevato e si confronta col momento ESATTO in cui premi il tasto " +
                 "(precisione al frame, non a scatti di 0.02 s). Verifica il valore reale nell'HUD: 'last coyote jump delay'. " +
                 "Il momento in cui il terreno 'sparisce' dipende anche da GroundDetector (Check Distance, Radius Shrink).")]
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [Tooltip("Secondi per cui un salto premuto in anticipo viene ricordato (es. poco prima di atterrare).")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
        [Tooltip("Dopo un salto, per questi secondi il contatto col terreno viene ignorato (evita doppi salti dallo stesso suolo).")]
        [SerializeField, Min(0f)] private float ignoreGroundAfterJump = 0.1f;

        [Header("Air jumps")]
        [Tooltip("Quanti salti in aria hai dopo aver toccato terra. 0 = solo quelli concessi dal contatto con le pareti.")]
        [SerializeField, Min(0)] private int startAirJumpsOnGround = 0;
        [Tooltip("Massimo di salti in aria accumulabili.")]
        [SerializeField, Min(0)] private int maxAirJumps = 1;
        [Tooltip("Altezza (m) del salto in aria.")]
        [SerializeField, Min(0.05f)] private float airJumpHeight = 1.6f;

        [Header("Derived (read-only, calcolati)")]
        [Tooltip("Gravità in salita (m/s²). Calcolata da jumpHeight e timeToApex.")]
        [SerializeField] private float derivedGravity;
        [Tooltip("Velocità iniziale del salto (m/s). Calcolata.")]
        [SerializeField] private float derivedJumpVelocity;
        [Tooltip("Durata totale (s) di un salto su terreno piano, senza jump-cut né apex hang. Calcolata.")]
        [SerializeField] private float derivedAirTime;

        private float _airborneSince = float.PositiveInfinity;   // time of the first tick without ground (Infinity = grounded / not armed)
        private bool _coyoteArmed;                                // true after touching ground, false after a jump
        private float _lastCoyoteDelay = -1f;
        private float _lastJumpTime = -999f;
        private bool _jumpRising;   // true after a jump made by THIS module, until vy <= 0 (jump-cut applies only then)
        private int _airJumpsLeft;

        public override bool Exclusive => false;
        protected override int DefaultPriority => 60;

        /// <summary>Rising gravity (m/s², positive) derived from jumpHeight and timeToApex.</summary>
        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);

        /// <summary>Seconds of coyote window left (0 when grounded or expired).</summary>
        public float CoyoteRemaining =>
            (_coyoteArmed && !float.IsInfinity(_airborneSince))
                ? Mathf.Max(0f, coyoteTime - (Time.time - _airborneSince))
                : 0f;

        /// <summary>Delay between leaving the ground and pressing jump in the last coyote jump (-1 = none yet).</summary>
        public float LastCoyoteDelay => _lastCoyoteDelay;

        public override string DebugState => rb != null
            ? $"vy={rb.linearVelocity.y:F2} airJumpsLeft={_airJumpsLeft} g={Gravity:F1} coyote={CoyoteRemaining:F3}s"
            : "n/a";

        private void OnValidate() { Recalculate(); }

        private void Recalculate()
        {
            derivedGravity = Gravity;
            derivedJumpVelocity = 2f * jumpHeight / timeToApex;
            derivedAirTime = timeToApex * (1f + 1f / Mathf.Sqrt(Mathf.Max(0.01f, fallGravityMultiplier)));
        }

        public override bool CanExecute(MovementContext ctx) { return true; }

        public override void Tick(MovementContext ctx, float dt)
        {
            float g = Gravity;
            Vector3 vel = rb.linearVelocity;
            float vy = vel.y;
            float now = Time.time;

            bool justJumped = now - _lastJumpTime < ignoreGroundAfterJump;
            bool grounded = ctx.IsGrounded && !justJumped;

            if (grounded)
            {
                _coyoteArmed = true;
                _airborneSince = float.PositiveInfinity;
                _airJumpsLeft = startAirJumpsOnGround;
            }
            else if (_coyoteArmed && float.IsPositiveInfinity(_airborneSince))
            {
                _airborneSince = now;   // first tick without ground: the coyote window starts here
            }

            _airJumpsLeft = Mathf.Min(_airJumpsLeft + ctx.PendingAirJumpGrants, maxAirJumps);
            ctx.PendingAirJumpGrants = 0;

            // ---- jump decision (buffer + coyote + air jumps) ----
            bool bufferOk = ctx.Jump.Peek(jumpBufferTime);

            // Coyote is judged on the moment the button was PRESSED (frame precision), not on the fixed tick that
            // happens to execute the jump (0.02 s steps). While grounded _airborneSince is +Infinity, so this is always true.
            bool coyoteOk = _coyoteArmed && ctx.Jump.time - _airborneSince <= coyoteTime;

            if (bufferOk && coyoteOk)
            {
                if (!float.IsInfinity(_airborneSince)) _lastCoyoteDelay = Mathf.Max(0f, ctx.Jump.time - _airborneSince);
                vy = TakeoffVelocity(jumpHeight, g, dt);
                _lastJumpTime = now;
                _jumpRising = true;
                _coyoteArmed = false;   // one ground jump per ground contact
                ctx.Jump.Clear();
            }
            else if (bufferOk && _airJumpsLeft > 0)
            {
                vy = TakeoffVelocity(airJumpHeight, g, dt);
                _lastJumpTime = now;
                _jumpRising = true;
                _airJumpsLeft--;
                ctx.Jump.Clear();
            }

            if (vy <= 0f) _jumpRising = false;

            // ---- gravity ----
            if (grounded && vy <= 0f)
            {
                vy = 0f;
            }
            else
            {
                float mult = vy > 0f
                    ? ((ctx.JumpHeld || !_jumpRising) ? 1f : jumpCutGravityMultiplier)
                    : fallGravityMultiplier;
                if (Mathf.Abs(vy) < apexThreshold) mult *= apexGravityMultiplier;

                vy -= g * mult * dt;
                if (vy < -maxFallSpeed) vy = -maxFallSpeed;
            }

            rb.linearVelocity = new Vector3(vel.x, vy, vel.z);
            ctx.VerticalHandled = true;
        }

        // v0 = sqrt(2 g h). The extra half step compensates the semi-implicit Euler integration,
        // so the real apex matches jumpHeight within a few millimeters instead of ~4% lower.
        private static float TakeoffVelocity(float height, float g, float dt)
        {
            return Mathf.Sqrt(2f * g * height) + 0.5f * g * dt;
        }

        [ContextMenu("Debug/Print Derived Values")]
        private void PrintDerived()
        {
            Recalculate();
            Debug.Log($"[JumpModule] g={derivedGravity:F2} m/s² v0={derivedJumpVelocity:F2} m/s " +
                      $"flatAirTime={derivedAirTime:F2} s fallG={derivedGravity * fallGravityMultiplier:F2} m/s²");
        }

        protected override void OnDrawGizmosSelected()
        {
            Vector3 p = transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(p + Vector3.up * jumpHeight, new Vector3(1f, 0.02f, 1f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(p + Vector3.up * airJumpHeight, new Vector3(0.6f, 0.02f, 0.6f));
        }
    }
}