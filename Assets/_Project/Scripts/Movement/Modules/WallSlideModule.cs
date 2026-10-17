using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Wall grab + slide. On reaching a wall in the air the player sticks to it for hangDuration (no gravity),
    /// then slides down (speed ramps from minSlideSpeed to maxSlideSpeed). Jump (during hang or slide) = wall jump
    /// away from the wall, steerable with the move input inside a control zone (WallJumpSettings.maxAngleLeft / maxAngleRight).
    /// Wall jumps give the wall contact back after reattachCooldown, so repeated jumps can climb over any wall.
    /// Exclusive.
    /// </summary>
    public class WallSlideModule : MovementModule
    {
        [Header("Refs")]
        [SerializeField] private WallDetector wall;

        [Header("Entry")]
        [Tooltip("true = ti agganci da solo quando raggiungi la parete in aria. false = serve il tasto GrabWall. Il tasto GrabWall funziona sempre.")]
        [SerializeField] private bool autoSlide = true;
        [Tooltip("Con Auto Slide: ti agganci solo se stai premendo verso la parete (evita agganci accidentali passando di lato).")]
        [SerializeField] private bool requireInputTowardWall = true;
        [Tooltip("Quanto deve essere deciso l'input verso la parete (0..1) per agganciarsi.")]
        [SerializeField, Range(0f, 1f)] private float inputTowardWallThreshold = 0.3f;
        [Tooltip("Secondi per cui il tasto GrabWall premuto in anticipo viene ricordato.")]
        [SerializeField, Min(0f)] private float grabBufferTime = 0.2f;
        [Tooltip("Non si aggancia se stai salendo più veloce di così (m/s).")]
        [SerializeField] private float maxUpwardSpeedToStart = 2f;
        [Tooltip("Secondi minimi in aria prima di potersi agganciare.")]
        [SerializeField, Min(0f)] private float minAirTime = 0.05f;

        [Header("Hang (aggancio)")]
        [Tooltip("Secondi in cui il player resta bloccato in posizione appena agganciato (nessuna gravità), prima di iniziare a scivolare. 0 = niente sosta.")]
        [SerializeField, Min(0f)] private float hangDuration = 0.4f;

        [Header("Slide")]
        [Tooltip("Velocità di discesa (m/s) all'inizio dello slide.")]
        [SerializeField, Min(0f)] private float minSlideSpeed = 1.5f;
        [Tooltip("Velocità di discesa massima (m/s).")]
        [SerializeField, Min(0f)] private float maxSlideSpeed = 6f;
        [Tooltip("Accelerazione (m/s²) della discesa.")]
        [SerializeField, Min(0f)] private float slideAcceleration = 4f;
        [Tooltip("Durata massima (s) dello slide, dopo la sosta di aggancio.")]
        [SerializeField, Min(0.1f)] private float maxDuration = 5f;
        [Tooltip("Velocità (m/s) verso la parete per restare a contatto.")]
        [SerializeField, Min(0f)] private float stickSpeed = 1f;
        [Tooltip("Quanto rapidamente (m/s²) la velocità orizzontale converge verso lo stick.")]
        [SerializeField, Min(0f)] private float horizontalDamping = 20f;
        [Tooltip("Secondi di tolleranza se la parete sparisce momentaneamente.")]
        [SerializeField, Min(0f)] private float wallLostGrace = 0.1f;

        [Header("Exit / Wall jump")]
        [Tooltip("Spingere lontano dalla parete fa mollare la presa (durante sosta e slide).")]
        [SerializeField] private bool dropByPushingAway = true;
        [Tooltip("Secondi dopo l'uscita in cui nessun modulo parete può riagganciarsi. Basso (0.1) = si scavalca la parete saltando di continuo; alto = salti più 'staccati'.")]
        [SerializeField, Min(0f)] private float reattachCooldown = 0.2f;
        [Tooltip("Concede un salto in aria all'aggancio.")]
        [SerializeField] private bool grantsAirJump = true;
        [Tooltip("Secondi per cui il salto premuto in anticipo viene ricordato.")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
        [SerializeField] private WallJumpSettings wallJump = new WallJumpSettings { alongSpeed = 0f };

        private float _elapsed, _lostTimer, _speed;
        private bool _exitRequested;
        private Vector3 _normal;
        private GameObject _wallObject;   // the wall we are attached to (remembered when we jump from it)

        public bool IsSliding => IsActive && _elapsed >= hangDuration;
        public bool IsHanging => IsActive && _elapsed < hangDuration;
        public override bool Exclusive => true;
        protected override int DefaultPriority => 70;
        public override string DebugState => IsActive
            ? $"phase={(IsHanging ? "Hang" : "Slide")} t={_elapsed:F2} hangLeft={Mathf.Max(0f, hangDuration - _elapsed):F2} slideSpeed={_speed:F2}"
            : "idle";

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
                if (_exitRequested || ctx.IsGrounded || _elapsed >= hangDuration + maxDuration) return false;
                if (!wall.HasWall && _lostTimer >= wallLostGrace) return false;
                if (dropByPushingAway && Vector3.Dot(ctx.MoveInputWorld, _normal) > 0.6f) return false;
                return true;
            }

            if (!ctx.WallContactAllowed || ctx.IsGrounded || ctx.AirTime < minAirTime) return false;
            if (!wall.HasWall) return false;
            if (ctx.IsBlockedWall(wall.WallObject)) return false;   // same wall as the last wall jump
            if (rb.linearVelocity.y > maxUpwardSpeedToStart) return false;

            bool pressedGrab = ctx.GrabWall.Peek(grabBufferTime);
            bool autoOk = autoSlide
                && (!requireInputTowardWall
                    || Vector3.Dot(ctx.MoveInputWorld, -Flat(wall.WallNormal).normalized) >= inputTowardWallThreshold);
            return pressedGrab || autoOk;
        }

        protected override void OnEnter(MovementContext ctx)
        {
            _normal = Flat(wall.WallNormal).normalized;
            _wallObject = wall.WallObject;
            _speed = minSlideSpeed;
            _elapsed = 0f;
            _lostTimer = 0f;
            _exitRequested = false;
            ctx.GrabWall.Clear();
            ctx.Jump.Clear();   // the press that brought us here must not trigger an instant wall jump
            if (grantsAirJump) ctx.GrantAirJump();
        }

        public override void Tick(MovementContext ctx, float dt)
        {
            _elapsed += dt;

            if (wall.HasWall)
            {
                _lostTimer = 0f;
                _normal = Flat(wall.WallNormal).normalized;
                _wallObject = wall.WallObject;
            }
            else
            {
                _lostTimer += dt;
            }

            // Wall jump: allowed during the hang and during the slide.
            if (ctx.Jump.Consume(jumpBufferTime))
            {
                Vector3 away = wallJump.GetAwayDirection(_normal, transform);
                rb.linearVelocity = wallJump.Compute(away, Vector3.zero, ctx.MoveInputWorld);
                wallJump.ApplyHeadingLock(ctx, away);
                ctx.LastJumpedWall = _wallObject;   // this wall cannot be grabbed or jumped from again until another wall jump or the ground
                _exitRequested = true;
                ctx.VerticalHandled = true;
                return;
            }

            Vector3 target = wall.HasWall ? -_normal * stickSpeed : Vector3.zero;
            Vector3 horizontal = Vector3.MoveTowards(Flat(rb.linearVelocity), target, horizontalDamping * dt);

            if (_elapsed < hangDuration)
            {
                // hang: stuck in place, no gravity
                rb.linearVelocity = new Vector3(horizontal.x, 0f, horizontal.z);
            }
            else
            {
                _speed = Mathf.MoveTowards(_speed, maxSlideSpeed, slideAcceleration * dt);
                rb.linearVelocity = new Vector3(horizontal.x, -_speed, horizontal.z);
            }

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

            Gizmos.color = Color.red;
            Gizmos.DrawRay(p, _normal);

            // wall jump direction (snapped to the player's axes) and optional left / right zone
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(p, wallJump.GetAwayDirection(_normal, transform) * 2f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(p, Quaternion.AngleAxis(-wallJump.maxAngleLeft, Vector3.up) * _normal * 1.5f);
            Gizmos.DrawRay(p, Quaternion.AngleAxis(wallJump.maxAngleRight, Vector3.up) * _normal * 1.5f);
        }
    }
}