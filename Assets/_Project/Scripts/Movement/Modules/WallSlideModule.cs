using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Wall slide (ex WallSlideController): slide down a wall, speed ramps from minSlideSpeed to maxSlideSpeed.
    /// Started with the GrabWall button (or automatically if enabled). Ends on landing, wall lost, timeout,
    /// pushing away from the wall; Jump performs a wall jump. Exclusive.
    /// </summary>
    public class WallSlideModule : MovementModule
    {
        [Header("Refs")]
        [SerializeField] private WallDetector wall;

        [Header("Entry")]
        [Tooltip("true = si aggancia da solo cadendo contro una parete. false = serve il tasto GrabWall.")]
        [SerializeField] private bool autoSlide = false;
        [Tooltip("Secondi per cui il tasto GrabWall premuto in anticipo viene ricordato.")]
        [SerializeField, Min(0f)] private float grabBufferTime = 0.2f;
        [Tooltip("Non si aggancia se stai salendo più veloce di così (m/s).")]
        [SerializeField] private float maxUpwardSpeedToStart = 2f;
        [Tooltip("Secondi minimi in aria prima di potersi agganciare.")]
        [SerializeField, Min(0f)] private float minAirTime = 0.05f;

        [Header("Slide")]
        [Tooltip("Velocità di discesa (m/s) all'inizio.")]
        [SerializeField, Min(0f)] private float minSlideSpeed = 1.5f;
        [Tooltip("Velocità di discesa massima (m/s).")]
        [SerializeField, Min(0f)] private float maxSlideSpeed = 6f;
        [Tooltip("Accelerazione (m/s²) della discesa.")]
        [SerializeField, Min(0f)] private float slideAcceleration = 4f;
        [Tooltip("Durata massima (s) dello slide.")]
        [SerializeField, Min(0.1f)] private float maxDuration = 5f;
        [Tooltip("Velocità (m/s) verso la parete per restare a contatto.")]
        [SerializeField, Min(0f)] private float stickSpeed = 1f;
        [Tooltip("Quanto rapidamente (m/s²) la velocità orizzontale converge verso lo stick.")]
        [SerializeField, Min(0f)] private float horizontalDamping = 20f;
        [Tooltip("Secondi di tolleranza se la parete sparisce momentaneamente.")]
        [SerializeField, Min(0f)] private float wallLostGrace = 0.1f;

        [Header("Exit")]
        [Tooltip("Spingere lontano dalla parete fa mollare lo slide.")]
        [SerializeField] private bool dropByPushingAway = true;
        [Tooltip("Secondi dopo l'uscita in cui nessun modulo parete può riagganciarsi.")]
        [SerializeField, Min(0f)] private float reattachCooldown = 0.3f;
        [Tooltip("Concede un salto in aria all'aggancio.")]
        [SerializeField] private bool grantsAirJump = true;
        [Tooltip("Secondi per cui il salto premuto in anticipo viene ricordato.")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
        [SerializeField] private WallJumpSettings wallJump = new WallJumpSettings { upSpeed = 8f, awaySpeed = 5f, alongSpeed = 0f };

        private float _elapsed, _lostTimer, _speed;
        private bool _exitRequested;
        private Vector3 _normal;

        public bool IsSliding => IsActive;
        public override bool Exclusive => true;
        protected override int DefaultPriority => 70;
        public override string DebugState => IsActive ? $"t={_elapsed:F2}/{maxDuration:F2} slideSpeed={_speed:F2}" : "idle";

        protected override void Reset() { base.Reset(); wall = GetComponent<WallDetector>(); }

        protected override void Awake()
        {
            base.Awake();
            if (wall == null) wall = GetComponent<WallDetector>();
        }

        public override bool CanExecute(MovementContext ctx)
        {
            if (wall == null) return false;

            if (IsActive)
            {
                if (_exitRequested || ctx.IsGrounded || _elapsed >= maxDuration) return false;
                if (!wall.HasWall && _lostTimer >= wallLostGrace) return false;
                if (dropByPushingAway && Vector3.Dot(ctx.MoveInputWorld, _normal) > 0.6f) return false;
                return true;
            }

            if (!ctx.WallContactAllowed || ctx.IsGrounded || ctx.AirTime < minAirTime) return false;
            if (!wall.HasWall) return false;
            if (rb.linearVelocity.y > maxUpwardSpeedToStart) return false;
            return autoSlide || ctx.GrabWall.Peek(grabBufferTime);
        }

        protected override void OnEnter(MovementContext ctx)
        {
            _normal = Flat(wall.WallNormal).normalized;
            _speed = minSlideSpeed;
            _elapsed = 0f;
            _lostTimer = 0f;
            _exitRequested = false;
            ctx.GrabWall.Clear();
            if (grantsAirJump) ctx.GrantAirJump();
        }

        public override void Tick(MovementContext ctx, float dt)
        {
            _elapsed += dt;

            if (wall.HasWall)
            {
                _lostTimer = 0f;
                _normal = Flat(wall.WallNormal).normalized;
            }
            else
            {
                _lostTimer += dt;
            }

            if (ctx.Jump.Consume(jumpBufferTime))
            {
                rb.linearVelocity = wallJump.Compute(_normal, Vector3.zero);
                _exitRequested = true;
                ctx.VerticalHandled = true;
                return;
            }

            _speed = Mathf.MoveTowards(_speed, maxSlideSpeed, slideAcceleration * dt);

            Vector3 target = wall.HasWall ? -_normal * stickSpeed : Vector3.zero;
            Vector3 horizontal = Vector3.MoveTowards(Flat(rb.linearVelocity), target, horizontalDamping * dt);

            rb.linearVelocity = new Vector3(horizontal.x, -_speed, horizontal.z);
            ctx.VerticalHandled = true;
        }

        protected override void OnExit(MovementContext ctx)
        {
            ctx.BlockWallContact(reattachCooldown);
        }

        protected override void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !IsActive) return;
            Vector3 p = transform.position + Vector3.up;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(p, Vector3.down * 1.5f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(p, _normal);
        }
    }
}