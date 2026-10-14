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
        [Tooltip("Il sensore che individua il muro su cui il personaggio sta correndo.")]
        [SerializeField] private WallDetector wall;

        [Header("Entry")]
        [Tooltip("Il tempo minimo che il personaggio deve trascorrere in aria prima di poter iniziare una corsa sul muro (evita agganci immediati appena ci si stacca da terra con un salto).")]
        [SerializeField, Min(0f)] private float minAirTime = 0.05f;
        [Tooltip("La velocità di corsa minima (in metri al secondo) necessaria per agganciarsi al muro e iniziare la corsa.")]
        [SerializeField, Min(0f)] private float minEntrySpeed = 4f;
        [Tooltip("L'angolo massimo (in gradi) tra la direzione in cui premi la levetta e l'orientamento del muro per poter iniziare. Valori bassi richiedono di correre perfettamente paralleli al muro; valori più alti consentono di agganciarsi anche con un'angolazione più diagonale.")]
        [SerializeField, Range(1f, 89f)] private float maxEntryAngle = 60f;

        [Header("Run")]
        [Tooltip("La velocità con cui il personaggio corre lungo il muro.")]
        [SerializeField, Min(0f)] private float wallRunSpeed = 8f;

        [Tooltip("La rapidità con cui il personaggio accelera fino a raggiungere la velocità di corsa sul muro.")]
        [SerializeField, Min(0f)] private float wallRunAcceleration = 30f;
        [Tooltip("Il tempo massimo (in secondi) che il personaggio può trascorrere correndo ininterrottamente sullo stesso muro.")]
        [SerializeField, Min(0.1f)] private float maxDuration = 1.5f;
        [Tooltip("Una piccola spinta verso l'alto applicata all'inizio della corsa per dare una sensazione di slancio ed energia quando si sale sul muro.")]
        [SerializeField] private float entryUpBoost = 2.5f;
        [Tooltip("La forza di gravità ridotta applicata durante la corsa sul muro per rallentare la caduta.")]
        [SerializeField, Min(0f)] private float wallGravity = 8f;
        [Tooltip("La velocità massima di caduta consentita mentre si corre sul muro.")]
        [SerializeField, Min(0f)] private float maxFallSpeed = 6f;
        [Tooltip("La forza con cui il personaggio viene tenuto incollato al muro per impedirgli di scivolare via lateralmente.")]
        [SerializeField, Min(0f)] private float stickSpeed = 1.5f;
        [Tooltip("Se attivo, la corsa sul muro continua solo se il giocatore continua a premere la levetta nella direzione della corsa.")]
        [SerializeField] private bool requireInput = true;
        [Tooltip("Un margine di tolleranza (in secondi) per evitare che il personaggio si stacchi se il sensore perde il contatto con il muro per un breve istante (es. tra una mattonella e l'altra).")]
        [SerializeField, Min(0f)] private float wallLostGrace = 0.12f;

        [Header("Exit")]
        [Tooltip("Il tempo di attesa (in secondi) prima di potersi riagganciare a un muro dopo essere scesi o saltati via dalla corsa.")]
        [SerializeField, Min(0f)] private float reattachCooldown = 0.4f;
        [Tooltip("Se attivo, iniziare a correre sul muro assegna immediatamente un salto in aria extra.")]
        [SerializeField] private bool grantsAirJump = true;
        [Tooltip("Per quanti secondi il gioco ricorda la pressione del tasto di salto se premuto leggermente prima di staccarsi o saltare.")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
        [SerializeField] private WallJumpSettings wallJump = new WallJumpSettings();

        private float _elapsed, _lostTimer, _speed;
        private bool _exitRequested;
        private Vector3 _runDir, _normal;

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

            Vector3 v = rb.linearVelocity;
            _speed = Mathf.Max(0f, Vector3.Dot(Flat(v), _runDir));
            _elapsed = 0f;
            _lostTimer = 0f;
            _exitRequested = false;

            rb.linearVelocity = new Vector3(v.x, Mathf.Max(v.y, entryUpBoost), v.z);
            if (grantsAirJump) ctx.GrantAirJump();
        }

        public override void Tick(MovementContext ctx, float dt)
        {
            _elapsed += dt;

            if (wall.HasWall)
            {
                _lostTimer = 0f;
                _normal = Flat(wall.WallNormal).normalized;
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
                rb.linearVelocity = wallJump.Compute(_normal, _runDir, ctx.MoveInputWorld);
                wallJump.ApplyHeadingLock(ctx, _normal);
                _exitRequested = true;
                ctx.VerticalHandled = true;
                return;
            }

            _speed = Mathf.MoveTowards(_speed, wallRunSpeed, wallRunAcceleration * dt);
            Vector3 horizontal = _runDir * _speed;
            if (wall.HasWall) horizontal -= _normal * stickSpeed;

            float vy = Mathf.Max(rb.linearVelocity.y - wallGravity * dt, -maxFallSpeed);
            rb.linearVelocity = new Vector3(horizontal.x, vy, horizontal.z);
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(p, _runDir * 1.5f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(p, _normal);
        }
    }
}
