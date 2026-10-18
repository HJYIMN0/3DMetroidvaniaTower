using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Wall run: while airborne and moving fast enough roughly along a wall, run along it with low gravity.
    /// Ends on timeout, wall lost, landing or input released; Jump performs a wall jump. Exclusive.
    /// </summary>
    public class WallRunModule : MovementModule
    {
        [Header("Refs")]
        [SerializeField] private WallDetector wall;

        [Header("Entry")]
        [Tooltip("Secondi minimi in aria prima di poter agganciare la parete (evita di agganciarsi subito dopo il salto da terra).")]
        [SerializeField, Min(0f)] private float minAirTime = 0.05f;
        [Tooltip("Velocità orizzontale minima (m/s) per iniziare il wall run.")]
        [SerializeField, Min(0f)] private float minEntrySpeed = 4f;
        [Tooltip("Angolo massimo (gradi) tra la direzione dell'input e la parete per iniziare. 0 = parallelo, 90 = dritto contro la parete.")]
        [SerializeField, Range(1f, 89f)] private float maxEntryAngle = 60f;

        [Header("Run")]
        [Tooltip("Velocità (m/s) di corsa sulla parete.")]
        [SerializeField, Min(0f)] private float wallRunSpeed = 8f;
        [Tooltip("Quanto rapidamente (m/s²) si raggiunge wallRunSpeed.")]
        [SerializeField, Min(0f)] private float wallRunAcceleration = 30f;
        [Tooltip("Durata massima (s) del wall run.")]
        [SerializeField, Min(0.1f)] private float maxDuration = 1.5f;
        [Tooltip("Velocità verticale massima VERSO L'ALTO (m/s) durante il wall run, anche all'aggancio. " +
                 "Se agganci mentre stai salendo, la salita viene tagliata a questo valore: impedisce di risalire il muro correndo. 0 = mai verso l'alto.")]
        [SerializeField, Min(0f)] private float maxUpwardSpeed = 0.5f;
        [Tooltip("Gravità (m/s²) durante il wall run.")]
        [SerializeField, Min(0f)] private float wallGravity = 8f;
        [Tooltip("Velocità massima di caduta (m/s) durante il wall run.")]
        [SerializeField, Min(0f)] private float maxFallSpeed = 6f;
        [Tooltip("Velocità (m/s) verso la parete per restare a contatto.")]
        [SerializeField, Min(0f)] private float stickSpeed = 1.5f;
        [Tooltip("Se true il wall run continua solo finché premi nella direzione di corsa.")]
        [SerializeField] private bool requireInput = true;
        [Tooltip("Secondi di tolleranza se la parete sparisce momentaneamente (es. giunti tra muri).")]
        [SerializeField, Min(0f)] private float wallLostGrace = 0.12f;

        [Header("Exit")]
        [Tooltip("Secondi dopo l'uscita in cui nessun modulo parete può riagganciarsi.")]
        [SerializeField, Min(0f)] private float reattachCooldown = 0.4f;
        [Tooltip("Concede un salto in aria all'aggancio.")]
        [SerializeField] private bool grantsAirJump = true;
        [Tooltip("Secondi per cui il salto premuto in anticipo viene ricordato.")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
        [SerializeField] private WallJumpSettings wallJump = new WallJumpSettings();

        private float _elapsed, _lostTimer, _speed;
        private bool _exitRequested;
        private Vector3 _runDir, _normal;
        private GameObject _wallObject;   // the wall we are running on (remembered when we jump from it)

        public override bool Exclusive => true;
        protected override int DefaultPriority => 80;
        public override string DebugState => IsActive ? $"t={_elapsed:F2}/{maxDuration:F2} speed={_speed:F2} runDir={_runDir}" : "idle";

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
                if (requireInput && Vector3.Dot(ctx.MoveInputWorld, _runDir) < 0.1f) return false;
                return true;
            }

            if (!ctx.WallContactAllowed || ctx.IsGrounded || ctx.AirTime < minAirTime) return false;
            if (ctx.IsBlockedWall(wall.WallObject)) return false;   // same wall as the last wall jump
            if (Flat(rb.linearVelocity).magnitude < minEntrySpeed) return false;
            return TryGetRunDirection(ctx, out _);
        }

        // Wall tangent aligned with where the player is going, if the angle is acceptable.
        private bool TryGetRunDirection(MovementContext ctx, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (!wall.HasWall) return false;

            Vector3 n = Flat(wall.WallNormal).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, n);

            Vector3 reference = ctx.MoveInputWorld.sqrMagnitude > 0.01f ? ctx.MoveInputWorld : Flat(rb.linearVelocity);
            if (reference.sqrMagnitude < 0.0001f) return false;

            if (Vector3.Dot(tangent, reference) < 0f) tangent = -tangent;
            if (Vector3.Angle(tangent, reference) > maxEntryAngle) return false;

            dir = tangent;
            return true;
        }

        protected override void OnEnter(MovementContext ctx)
        {
            TryGetRunDirection(ctx, out _runDir);
            _normal = Flat(wall.WallNormal).normalized;
            _wallObject = wall.WallObject;

            Vector3 v = rb.linearVelocity;
            _speed = Mathf.Max(0f, Vector3.Dot(Flat(v), _runDir));
            _elapsed = 0f;
            _lostTimer = 0f;
            _exitRequested = false;

            // Cap the upward speed: keeping a jump's rising speed (e.g. 8 m/s) with the low wall-run gravity made the player climb the wall.
            rb.linearVelocity = new Vector3(v.x, Mathf.Min(v.y, maxUpwardSpeed), v.z);

            // Attaching to a different wall frees the one we ran on before (CanExecute already refused the same wall).
            ctx.LastWall = null;
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
                Vector3 t = Vector3.Cross(Vector3.up, _normal);
                if (Vector3.Dot(t, _runDir) < 0f) t = -t;
                _runDir = t;
            }
            else
            {
                _lostTimer += dt;
            }

            if (ctx.Jump.Consume(jumpBufferTime))
            {
                Vector3 away = wallJump.GetAwayDirection(_normal, transform);
                rb.linearVelocity = wallJump.Compute(away, _runDir, ctx.MoveInputWorld);
                wallJump.ApplyHeadingLock(ctx, away);
                ctx.LastWall = _wallObject;   // this wall cannot be grabbed, run on or jumped from again until another wall jump or the ground
                _exitRequested = true;
                ctx.VerticalHandled = true;
                return;
            }

            _speed = Mathf.MoveTowards(_speed, wallRunSpeed, wallRunAcceleration * dt);
            Vector3 horizontal = _runDir * _speed;
            if (wall.HasWall) horizontal -= _normal * stickSpeed;

            float vy = Mathf.Clamp(rb.linearVelocity.y - wallGravity * dt, -maxFallSpeed, maxUpwardSpeed);
            rb.linearVelocity = new Vector3(horizontal.x, vy, horizontal.z);
            ctx.VerticalHandled = true;
        }

        protected override void OnExit(MovementContext ctx)
        {
            ctx.BlockWallContact(reattachCooldown);
            // Remember the wall: no new wall run on it until we touch ground or attach to another wall (even if the cooldown expires while still touching it).
            if (_wallObject != null) ctx.LastWall = _wallObject;
        }

        protected override void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !IsActive) return;
            Vector3 p = transform.position + Vector3.up;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(p, _runDir * 1.5f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(p, _normal);
        }
    }
}