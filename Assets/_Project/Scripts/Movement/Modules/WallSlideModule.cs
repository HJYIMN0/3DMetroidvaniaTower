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
        [Tooltip("Il sensore che legge a quale muro si sta appoggiando il personaggio.")]
        [SerializeField] private WallDetector wall;

        [Header("Entry")]
        [Tooltip("Se attivo, il personaggio si aggancia da solo quando tocca un muro in aria. Se spento, serve premere il tasto di aggancio (che comunque resta sempre attivo se premuto).")]
        [SerializeField] private bool autoSlide = true;
        [Tooltip("Se l'aggancio automatico è attivo, richiede che il giocatore stia muovendo la levetta verso il muro per evitare che si attacchi per sbaglio mentre ci cammina di fianco.")]
        [SerializeField] private bool requireInputTowardWall = true;
        [Tooltip("Quanta spinta con la levetta verso il muro è necessaria (da 0 a 1) per far scattare l'aggancio automatico.")]
        [SerializeField, Range(0f, 1f)] private float inputTowardWallThreshold = 0.3f;
        [Tooltip("Per quanti secondi il gioco si ricorda se hai premuto il tasto di aggancio leggermente in anticipo.")]
        [SerializeField, Min(0f)] private float grabBufferTime = 0.2f;
        [Tooltip("Impedisce di agganciarsi al muro se il personaggio sta saltando verso l'alto troppo velocemente.")]
        [SerializeField] private float maxUpwardSpeedToStart = 2f;
        [Tooltip("Tempo minimo che il personaggio deve passare in aria prima di poter effettuare l'aggancio a un muro.")]
        [SerializeField, Min(0f)] private float minAirTime = 0.05f;

        [Header("Hang (aggancio)")]
        [Tooltip("Per quanti secondi il personaggio resta immobile e sospeso sul muro (senza cadere) appena si aggancia, prima di iniziare a scivolare. Metti 0 per iniziare a scivolare subito.")]
        [SerializeField, Min(0f)] private float hangDuration = 0.4f;

        [Header("Slide")]
        [Tooltip("La velocità iniziale con cui il personaggio comincia a scivolare giù lungo il muro.")]
        [SerializeField, Min(0f)] private float minSlideSpeed = 1.5f;
        [Tooltip("La velocità massima che la scivolata può raggiungere lasciandosi cadere lungo il muro.")]
        [SerializeField, Min(0f)] private float maxSlideSpeed = 6f;
        [Tooltip("Quanto velocemente il personaggio accelera mentre scivola giù (passando dalla velocità minima alla massima).")]
        [SerializeField, Min(0f)] private float slideAcceleration = 4f;
        [Tooltip("Il tempo massimo in secondi che il personaggio può passare a scivolare sullo stesso muro prima di staccarsi automaticamente.")]
        [SerializeField, Min(0.1f)] private float maxDuration = 5f;
        [Tooltip("La forza con cui il personaggio viene tenuto incollato al muro per non staccarsi per inerzia.")]
        [SerializeField, Min(0f)] private float stickSpeed = 1f;
        [Tooltip("Quanto dolcemente o bruscamente il personaggio si attacca alla superficie del muro.")]
        [SerializeField, Min(0f)] private float horizontalDamping = 20f;
        [Tooltip("Un piccolo margine di tempo per evitare che il personaggio si stacchi se il muro perde il contatto per un istante.")]
        [SerializeField, Min(0f)] private float wallLostGrace = 0.1f;

        [Header("Exit / Wall jump")]
        [Tooltip("Se attivo, tirare la levetta nella direzione opposta al muro fa staccare e mollare la presa istantaneamente.")]
        [SerializeField] private bool dropByPushingAway = true;
        [Tooltip("Il tempo di attesa prima di potersi riagganciare allo stesso muro dopo essersi staccati. Valori bassi (es. 0.1) permettono di scalare rapidamente la parete saltando in continuazione; valori alti obbligano a salti più distanziati e cadenzati.")]
        [SerializeField, Min(0f)] private float reattachCooldown = 0.2f;
        [Tooltip("Se attivo, agganciarsi a un muro conferisce immediatamente un salto extra utilizzabile in aria.")]
        [SerializeField] private bool grantsAirJump = true;
        [Tooltip("Per quanti secondi il gioco ricorda la pressione del tasto di salto se premuto leggermente prima di toccare il muro.")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
        [SerializeField] private WallJumpSettings wallJump = new WallJumpSettings { alongSpeed = 0f };

        private float _elapsed, _lostTimer, _speed;
        private bool _exitRequested;
        private Vector3 _normal;

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
            }
            else
            {
                _lostTimer += dt;
            }

            // Wall jump: allowed during the hang and during the slide.
            if (ctx.Jump.Consume(jumpBufferTime))
            {
                rb.linearVelocity = wallJump.Compute(_normal, Vector3.zero, ctx.MoveInputWorld);
                wallJump.ApplyHeadingLock(ctx, _normal);
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

            // wall jump control zone (left / right limits)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(p, Quaternion.AngleAxis(-wallJump.maxAngleLeft, Vector3.up) * _normal * 1.5f);
            Gizmos.DrawRay(p, Quaternion.AngleAxis(wallJump.maxAngleRight, Vector3.up) * _normal * 1.5f);
        }
    }
}