using UnityEngine;
using Game.Animation;

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

        [Header("Animation")]
        [Tooltip("AnimatorBridge sullo stesso GameObject. Riceve Speed normalizzato (0..1) per il BlendTree.")]
        [SerializeField] private AnimatorBridge animatorBridge;

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
            if (Time.fixedTime - _lastTickTime > dt * 1.5f) SyncFromRigidbody();
            _lastTickTime = Time.fixedTime;

            if (ctx.OnSteepSurface)
            {
                SyncFromRigidbody();
                FeedAnimator();
                ApplyRotation();
                return;
            }

            if (ctx.IsGrounded) ctx.ClearHeadingLock();
            if (ctx.IsHeadingLocked(rb.linearVelocity.y))
            {
                TickLocked(ctx, dt);
                FeedAnimator();
                ApplyRotation();
                return;
            }

            float airMul = ctx.IsGrounded ? 1f : airControl;
            Vector3 input = ctx.MoveInputWorld;
            bool hasInput = input.sqrMagnitude > 0.0001f;
            bool running = ctx.MoveInputRaw.magnitude > runInputThreshold;

            float targetSpeed = hasInput ? (running ? runSpeed : walkSpeed) : 0f;
            float rampTarget = Mathf.Clamp01(targetSpeed / runSpeed);

            float time = ramp > rampTarget ? timeToStop : timeToMaxSpeed;
            float rate = time > 0.0001f ? 1f / time : 1000f;
            ramp = Mathf.MoveTowards(ramp, rampTarget, rate * airMul * dt);

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

            currentSpeed = Mathf.Max(0f, runSpeed * accelerationCurve.Evaluate(ramp));
            float rad = headingDegrees * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(dir.x * currentSpeed, v.y, dir.z * currentSpeed);

            FeedAnimator();
            ApplyRotation();
        }

        private void FeedAnimator()
        {
            if (animatorBridge == null) return;
            animatorBridge.SetLocomotionSpeed(accelerationCurve.Evaluate(ramp));
        }

        /// <summary>Applica l'heading alla Transform, così il modello visivo segue la direzione di movimento.</summary>
        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, headingDegrees, 0f);
        }

        private void TickLocked(MovementContext ctx, float dt)
        {
            Vector3 v = rb.linearVelocity;
            Vector3 h = Flat(v);
            float speed = h.magnitude;

            if (speed > 0.05f)
            {
                float heading = Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg;

                Vector3 input = ctx.MoveInputWorld;
                if (input.sqrMagnitude > 0.0001f)
                {
                    float desired = ClampToLock(ctx, Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg);
                    float turnRate = timeToChangeDirection > 0.0001f ? 180f / timeToChangeDirection : 100000f;
                    heading = Mathf.MoveTowardsAngle(heading, desired, turnRate * airControl * dt);
                }

                heading = ClampToLock(ctx, heading);
                headingDegrees = heading;

                float rad = heading * Mathf.Deg2Rad;
                rb.linearVelocity = new Vector3(Mathf.Sin(rad) * speed, v.y, Mathf.Cos(rad) * speed);
            }

            ramp = RampForSpeed(Mathf.Min(speed, runSpeed));
            currentSpeed = speed;
        }

        private static float ClampToLock(MovementContext ctx, float headingDeg)
        {
            float offset = Mathf.DeltaAngle(ctx.HeadingLockCenterDeg, headingDeg);
            offset = Mathf.Clamp(offset, -ctx.HeadingLockLeftDeg, ctx.HeadingLockRightDeg);
            return ctx.HeadingLockCenterDeg + offset;
        }

        private void SyncFromRigidbody()
        {
            Vector3 v = Flat(rb.linearVelocity);
            float s = v.magnitude;
            if (s > 0.1f) headingDegrees = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            ramp = RampForSpeed(Mathf.Min(s, runSpeed));
        }

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
            if (animatorBridge == null) animatorBridge = GetComponent<AnimatorBridge>();
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