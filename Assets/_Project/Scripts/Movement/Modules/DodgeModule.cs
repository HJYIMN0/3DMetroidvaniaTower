using System;
using UnityEngine;
using Game.Core;

namespace Game.Movement
{
    /// <summary>
    /// Dodge (ex Combat.DodgeController). Exclusive, top priority. The curve is a POSITION progress (0..1),
    /// so the total travel is exactly 'distance' whatever the curve looks like. Includes i-frames.
    /// Can be triggered by the buffered Dodge input (coordinator) or by BeginDodge() from a combat manager.
    /// </summary>
    public class DodgeModule : MovementModule
    {
        [Header("Timing")]
        [Tooltip("Durata totale della schivata (s).")]
        [SerializeField, Min(0.05f)] private float duration = 0.5f;
        [Tooltip("Pausa (s) dopo una schivata prima di poterne fare un'altra.")]
        [SerializeField, Min(0f)] private float cooldown = 0.15f;
        [Tooltip("Secondi per cui il tasto Dodge premuto in anticipo viene ricordato.")]
        [SerializeField, Min(0f)] private float inputBufferTime = 0.15f;

        [Header("I-Frames")]
        [Tooltip("Secondi dall'inizio prima che inizi l'invulnerabilità.")]
        [SerializeField, Min(0f)] private float iFrameStartDelay = 0.05f;
        [Tooltip("Durata (s) dell'invulnerabilità.")]
        [SerializeField, Min(0f)] private float iFrameDuration = 0.3f;

        [Header("Motion")]
        [Tooltip("Distanza totale (m) percorsa. Resta esatta qualunque sia la curva.")]
        [SerializeField, Min(0f)] private float distance = 4f;
        [Tooltip("Progresso di posizione nel tempo. X = tempo normalizzato, Y = distanza normalizzata (0→1). " +
                 "La PENDENZA all'inizio è la velocità di partenza: pendenza alta = la schivata parte subito. " +
                 "Una curva EaseInOut parte da velocità 0 e dà la sensazione di ritardo. Menu ⋮ > 'Debug/Reset Curve To Fast Start' per il default.")]
        [SerializeField] private AnimationCurve positionCurve = FastStartCurve();
        [Tooltip("Senza input di movimento: true = schivata all'indietro, false = in avanti (rispetto all'ultima direzione).")]
        [SerializeField] private bool backstepWhenNoInput = true;

        [Header("Vertical")]
        [Tooltip("true = durante la schivata la Y viene azzerata (dash piatto). false = gravità ridotta (vedi sotto).")]
        [SerializeField] private bool cancelVerticalVelocity = false;
        [Tooltip("Gravità (m/s²) applicata durante la schivata se cancelVerticalVelocity è false.")]
        [SerializeField, Min(0f)] private float gravityWhileDodging = 25f;

        [Header("Air")]
        [Tooltip("Permette di schivare in aria.")]
        [SerializeField] private bool canDodgeInAir = true;
        [Tooltip("Quante schivate in aria prima di ritoccare terra.")]
        [SerializeField, Min(0)] private int maxAirDodges = 1;

        private float _elapsed;
        private float _prevProgress;
        private float _cooldownUntil = -999f;
        private Vector3 _dir = Vector3.back;
        private bool _hasPending;
        private float _pendingTime;
        private Vector3 _pendingDir;
        private int _airDodgesUsed;
        private bool _groundedCache;   // last grounded state seen by CanExecute (used by CanStartNow)

        // Starts at full speed (initial slope 2 = 2x the average speed) and decelerates to a stop: the dodge reacts instantly.
        private static AnimationCurve FastStartCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        }

        [ContextMenu("Debug/Reset Curve To Fast Start")]
        private void ResetCurveToFastStart()
        {
            positionCurve = FastStartCurve();
        }

        public override bool Exclusive => true;
        protected override int DefaultPriority => 100;

        public bool IsDodging => IsActive;

        /// <summary>True from BeginDodge() until the dodge starts or the request expires.</summary>
        public bool IsRequested => _hasPending;

        /// <summary>
        /// True if a dodge requested right now would be accepted at the next physics tick
        /// (not dodging, cooldown over, grounded or air dodges left). Used by the combat buffer to wait instead of losing the press.
        /// </summary>
        public bool CanStartNow =>
            !IsActive
            && Time.time >= _cooldownUntil
            && (_groundedCache || (canDodgeInAir && _airDodgesUsed < maxAirDodges));
        public bool IsInvulnerable { get; private set; }
        public bool CancelVerticalVelocity => cancelVerticalVelocity;
        public Vector3 DodgeDirection => _dir;
        public float DodgeDistance => distance;

        public event Action OnDodgeStart;
        public event Action OnDodgeEnd;

        public override string DebugState => $"t={_elapsed:F2}/{duration:F2} invulnerable={IsInvulnerable} dir={_dir}";

        /// <summary>
        /// Requests a dodge. Direction zero = use the current move input, or a backstep if there is none.
        /// The request expires after inputBufferTime if the dodge cannot start (cooldown, air rules).
        /// </summary>
        public void BeginDodge(Vector3 direction)
        {
            _hasPending = true;
            _pendingTime = Time.time;
            _pendingDir = direction;
        }

        public override bool CanExecute(MovementContext ctx)
        {
            if (IsActive) return _elapsed < duration;

            if (_hasPending && Time.time - _pendingTime > inputBufferTime) _hasPending = false;   // stale request

            _groundedCache = ctx.IsGrounded;
            if (ctx.IsGrounded) _airDodgesUsed = 0;   // refilled here because CanExecute runs every tick

            if (Time.time < _cooldownUntil) return false;
            if (!_hasPending && !ctx.Dodge.Peek(inputBufferTime)) return false;
            if (!ctx.IsGrounded && (!canDodgeInAir || _airDodgesUsed >= maxAirDodges)) return false;
            return true;
        }

        protected override void OnEnter(MovementContext ctx)
        {
            Vector3 dir = _hasPending ? _pendingDir : Vector3.zero;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { dir = ctx.MoveInputWorld; dir.y = 0f; }   // no explicit direction: use the move input
            if (dir.sqrMagnitude < 0.0001f) dir = backstepWhenNoInput ? -ctx.Facing : ctx.Facing;
            _dir = dir.normalized;

            _hasPending = false;
            ctx.Dodge.Clear();
            if (!ctx.IsGrounded) _airDodgesUsed++;

            _elapsed = 0f;
            _prevProgress = 0f;
            IsInvulnerable = false;
            PlayerInputState.IsDodging = true;
            OnDodgeStart?.Invoke();
        }

        public override void Tick(MovementContext ctx, float dt)
        {
            _elapsed += dt;
            float progress = Mathf.Clamp01(_elapsed / duration);

            float step = positionCurve.Evaluate(progress) - positionCurve.Evaluate(_prevProgress);
            _prevProgress = progress;
            Vector3 horizontal = _dir * (step * distance / dt);

            float vy = rb.linearVelocity.y;
            if (cancelVerticalVelocity) vy = 0f;
            else if (ctx.IsGrounded && vy <= 0f) vy = 0f;
            else vy -= gravityWhileDodging * dt;

            rb.linearVelocity = new Vector3(horizontal.x, vy, horizontal.z);

            IsInvulnerable = _elapsed >= iFrameStartDelay && _elapsed <= iFrameStartDelay + iFrameDuration;
            ctx.VerticalHandled = true;
        }

        protected override void OnExit(MovementContext ctx)
        {
            IsInvulnerable = false;
            _cooldownUntil = Time.time + cooldown;
            PlayerInputState.IsDodging = false;
            OnDodgeEnd?.Invoke();
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                IsInvulnerable = false;
                PlayerInputState.IsDodging = false;
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            Vector3 p = transform.position + Vector3.up * 0.1f;
            Vector3 d = Application.isPlaying && IsActive ? _dir : -transform.forward;
            Gizmos.color = IsInvulnerable ? Color.cyan : Color.magenta;
            Gizmos.DrawLine(p, p + d * distance);
            Gizmos.DrawWireSphere(p + d * distance, 0.15f);
        }
    }
}