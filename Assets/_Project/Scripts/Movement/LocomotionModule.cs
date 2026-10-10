using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Horizontal movement (XZ only, never touches Y). Speed ramp, direction change and air control are
    /// separate and fully exposed. Passive module: runs every tick when no exclusive module owns the tick.
    /// Speed = runSpeed * accelerationCurve(ramp), where ramp (0..1) advances linearly in time, so
    /// timeToMaxSpeed / timeToStop stay exact whatever the curve shape is.
    /// </summary>
    public class LocomotionModule : MovementModule
    {
        [Header("Speeds")]
        [Tooltip("Velocità di camminata (m/s), usata quando la leva è sotto la soglia di corsa.")]
        [SerializeField, Min(0f)] private float walkSpeed = 3f;
        [Tooltip("Velocità massima di corsa (m/s).")]
        [SerializeField, Min(0.01f)] private float runSpeed = 6f;
        [Tooltip("Sopra questa intensità dell'input si corre. Con la tastiera l'input vale sempre 1, quindi si corre sempre.")]
        [SerializeField, Range(0f, 1f)] private float runInputThreshold = 0.75f;

        [Header("Speed ramp")]
        [Tooltip("Secondi per andare da fermo alla velocità massima.")]
        [SerializeField, Min(0f)] private float timeToMaxSpeed = 0.2f;
        [Tooltip("Secondi per andare dalla velocità massima a fermo (o per rallentare verso una velocità più bassa).")]
        [SerializeField, Min(0f)] private float timeToStop = 0.1f;
        [Tooltip("Easing della rampa. X = progresso 0..1 (nel tempo), Y = frazione della velocità massima 0..1. Lineare = rampa costante.")]
        [SerializeField] private AnimationCurve accelerationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Direction change")]
        [Tooltip("Secondi per girare di 180° senza passare da zero. 0 = cambio istantaneo.")]
        [SerializeField, Min(0f)] private float timeToChangeDirection = 0.1f;

        [Header("Air")]
        [Tooltip("Controllo in aria. 1 = come a terra, 0 = nessun controllo (inerzia pura).")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.8f;

        [Header("Runtime (read-only)")]
        [SerializeField] private float currentSpeed;
        [SerializeField] private float ramp;
        [SerializeField] private float headingDegrees;

        private float _lastTickTime = -999f;

        public override bool Exclusive => false;
        protected override int DefaultPriority => 10;

        public float CurrentSpeed => currentSpeed;
        public float HeadingDegrees => headingDegrees;
        public override string DebugState => $"speed={currentSpeed:F2} ramp={ramp:F2} heading={headingDegrees:F0}";

        public override bool CanExecute(MovementContext ctx) { return true; }

        public override void Tick(MovementContext ctx, float dt)
        {
            // If an exclusive module owned the previous ticks, our internal state is stale: re-sync from the Rigidbody.
            if (Time.fixedTime - _lastTickTime > dt * 1.5f) SyncFromRigidbody();
            _lastTickTime = Time.fixedTime;

            float airMul = ctx.IsGrounded ? 1f : airControl;
            Vector3 input = ctx.MoveInputWorld;
            bool hasInput = input.sqrMagnitude > 0.0001f;
            bool running = ctx.MoveInputRaw.magnitude > runInputThreshold;

            float targetSpeed = hasInput ? (running ? runSpeed : walkSpeed) : 0f;
            float rampTarget = Mathf.Clamp01(targetSpeed / runSpeed);

            // ---- speed ramp (independent from direction) ----
            float time = ramp > rampTarget ? timeToStop : timeToMaxSpeed;
            float rate = time > 0.0001f ? 1f / time : 1000f;
            ramp = Mathf.MoveTowards(ramp, rampTarget, rate * airMul * dt);

            // ---- direction change (independent from speed) ----
            if (hasInput)
            {
                float desired = Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg;
                if (ramp <= 0.001f)
                {
                    headingDegrees = desired;
                }
                else
                {
                    float turnRate = timeToChangeDirection > 0.0001f ? 180f / timeToChangeDirection : 100000f;
                    headingDegrees = Mathf.MoveTowardsAngle(headingDegrees, desired, turnRate * airMul * dt);
                }
            }

            // ---- apply: XZ only, Y is left to the vertical modules ----
            currentSpeed = Mathf.Max(0f, runSpeed * accelerationCurve.Evaluate(ramp));
            float rad = headingDegrees * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(dir.x * currentSpeed, v.y, dir.z * currentSpeed);
        }

        private void SyncFromRigidbody()
        {
            Vector3 v = Flat(rb.linearVelocity);
            float s = v.magnitude;
            if (s > 0.1f) headingDegrees = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            ramp = RampForSpeed(Mathf.Min(s, runSpeed));
        }

        // Inverse of the curve (assumes it is monotonic): finds the ramp that produces 'speed'.
        private float RampForSpeed(float speed)
        {
            if (speed <= 0f) return 0f;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 12; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (runSpeed * accelerationCurve.Evaluate(mid) < speed) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        protected override void Awake()
        {
            base.Awake();
            Vector3 f = Flat(transform.forward);
            if (f.sqrMagnitude > 0.0001f) headingDegrees = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        protected override void OnDrawGizmosSelected()
        {
            float rad = headingDegrees * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Gizmos.color = Color.yellow;
            Vector3 p = transform.position + Vector3.up * 0.15f;
            Gizmos.DrawRay(p, dir * Mathf.Max(0.3f, currentSpeed * 0.3f));
        }
    }
}