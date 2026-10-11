using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Ledge grab: Snap (short move to the hang pose) -> Hang -> Climb (custom interpolation up, then forward,
    /// no root motion). Exclusive. Reads LedgeDetector; the ledge is cached on entry so the hang stays stable.
    /// </summary>
    public class LedgeGrabModule : MovementModule
    {
        private enum State { Snap, Hang, Climb }

        [Header("Refs")]
        [SerializeField] private LedgeDetector detector;

        [Header("Entry")]
        [Tooltip("true = afferra il bordo da solo mentre sei in aria e non stai salendo. false = serve il tasto GrabLedge.")]
        [SerializeField] private bool autoGrabWhenFalling = true;
        [Tooltip("Secondi per cui il tasto GrabLedge premuto in anticipo viene ricordato.")]
        [SerializeField, Min(0f)] private float grabBufferTime = 0.25f;
        [Tooltip("Non afferra se stai salendo più veloce di così (m/s).")]
        [SerializeField] private float maxUpwardSpeedToGrab = 2f;
        [Tooltip("Secondi dopo essere sceso/arrivato prima di poter riafferrare un bordo.")]
        [SerializeField, Min(0f)] private float regrabCooldown = 0.5f;

        [Header("Hang pose")]
        [Tooltip("Distanza (m) del pivot dalla parete mentre sei appeso. Deve superare il raggio della capsule.")]
        [SerializeField, Min(0f)] private float hangDistanceFromWall = 0.35f;
        [Tooltip("Distanza verticale (m) dal pivot al bordo mentre sei appeso. Con pivot ai piedi ≈ altezza delle mani.")]
        [SerializeField, Min(0f)] private float hangPivotBelowLedge = 1.7f;
        [Tooltip("Secondi per raggiungere la posa appesa.")]
        [SerializeField, Min(0f)] private float snapDuration = 0.08f;
        [Tooltip("Secondi massimi appeso, poi si molla. 0 = infinito.")]
        [SerializeField, Min(0f)] private float maxHangTime = 0f;

        [Header("Climb")]
        [Tooltip("Durata (s) della salita sul bordo.")]
        [SerializeField, Min(0.05f)] private float climbDuration = 0.6f;
        [Tooltip("Easing della salita. X = tempo 0..1, Y = progresso 0..1.")]
        [SerializeField] private AnimationCurve climbCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Quota del percorso (0..1) spesa nella salita verticale; il resto è il passo in avanti.")]
        [SerializeField, Range(0.05f, 0.95f)] private float verticalPortion = 0.65f;
        [Tooltip("Di quanto (m) si avanza sul bordo a fine salita.")]
        [SerializeField, Min(0f)] private float climbForwardDistance = 0.5f;
        [Tooltip("Margine verticale (m) sopra il bordo all'arrivo.")]
        [SerializeField, Min(0f)] private float climbExtraHeight = 0.05f;

        [Header("Hang input")]
        [Tooltip("Il tasto salto fa salire sul bordo.")]
        [SerializeField] private bool climbWithJump = true;
        [Tooltip("Spingere verso la parete fa salire sul bordo.")]
        [SerializeField] private bool climbByPushingTowardWall = true;
        [Tooltip("Spingere lontano dalla parete fa mollare il bordo.")]
        [SerializeField] private bool dropByPushingAway = true;
        [Tooltip("Soglia (0..1) dell'input per salire/mollare.")]
        [SerializeField, Range(0.1f, 1f)] private float pushThreshold = 0.6f;

        private State _state;
        private LedgeInfo _ledge;
        private Vector3 _normal, _startPos, _hangPos, _climbStart, _climbUp, _climbEnd;
        private float _t;
        private float _blockedUntil = -999f;
        private bool _done;

        public override bool Exclusive => true;
        protected override int DefaultPriority => 90;
        public override string DebugState => IsActive ? $"state={_state} t={_t:F2}" : "idle";

        protected override void Reset() { base.Reset(); detector = GetComponent<LedgeDetector>(); }

        protected override void Awake()
        {
            base.Awake();
            if (detector == null) detector = GetComponent<LedgeDetector>();
        }

        public override bool CanExecute(MovementContext ctx)
        {
            if (detector == null) return false;
            if (IsActive) return !_done;

            if (Time.time < _blockedUntil || ctx.IsGrounded) return false;
            if (rb.linearVelocity.y > maxUpwardSpeedToGrab) return false;
            if (!detector.HasLedge) return false;
            return autoGrabWhenFalling || ctx.GrabLedge.Peek(grabBufferTime);
        }

        protected override void OnEnter(MovementContext ctx)
        {
            _ledge = detector.Ledge;
            _normal = _ledge.wallNormal;
            _hangPos = new Vector3(_ledge.wallPoint.x, _ledge.topPoint.y - hangPivotBelowLedge, _ledge.wallPoint.z)
                       + _normal * hangDistanceFromWall;
            _startPos = rb.position;
            _state = State.Snap;
            _t = 0f;
            _done = false;
            ctx.GrabLedge.Clear();
            ctx.Jump.Clear();   // a jump pressed just before grabbing must not trigger an instant climb
            rb.linearVelocity = Vector3.zero;
        }

        public override void Tick(MovementContext ctx, float dt)
        {
            rb.linearVelocity = Vector3.zero;
            ctx.VerticalHandled = true;

            switch (_state)
            {
                case State.Snap:
                    _t += dt;
                    float k = snapDuration > 0.0001f ? Mathf.Clamp01(_t / snapDuration) : 1f;
                    rb.MovePosition(Vector3.Lerp(_startPos, _hangPos, k));
                    if (k >= 1f) { _state = State.Hang; _t = 0f; }
                    break;

                case State.Hang:
                    _t += dt;
                    rb.MovePosition(_hangPos);

                    Vector3 move = ctx.MoveInputWorld;
                    bool climb = (climbWithJump && ctx.Jump.Consume(0.15f))
                                 || (climbByPushingTowardWall && Vector3.Dot(move, -_normal) > pushThreshold);
                    bool drop = (dropByPushingAway && Vector3.Dot(move, _normal) > pushThreshold)
                                || (maxHangTime > 0f && _t >= maxHangTime);

                    if (climb)
                    {
                        _state = State.Climb;
                        _t = 0f;
                        _climbStart = _hangPos;
                        _climbUp = new Vector3(_hangPos.x, _ledge.topPoint.y + climbExtraHeight, _hangPos.z);
                        _climbEnd = new Vector3(_ledge.topPoint.x, _ledge.topPoint.y + climbExtraHeight, _ledge.topPoint.z)
                                    - _normal * climbForwardDistance;
                    }
                    else if (drop)
                    {
                        _done = true;
                    }
                    break;

                case State.Climb:
                    _t += dt;
                    float p = Mathf.Clamp01(_t / climbDuration);
                    float e = Mathf.Clamp01(climbCurve.Evaluate(p));
                    Vector3 pos = e < verticalPortion
                        ? Vector3.Lerp(_climbStart, _climbUp, e / verticalPortion)
                        : Vector3.Lerp(_climbUp, _climbEnd, (e - verticalPortion) / (1f - verticalPortion));
                    rb.MovePosition(pos);
                    if (p >= 1f) _done = true;
                    break;
            }
        }

        protected override void OnExit(MovementContext ctx)
        {
            _blockedUntil = Time.time + regrabCooldown;
            rb.linearVelocity = Vector3.zero;
            ctx.GrabLedge.Clear();
            ctx.Jump.Clear();
        }

        protected override void OnDrawGizmosSelected()
        {
            if (detector == null || !detector.HasLedge) return;
            LedgeInfo l = detector.Ledge;
            Vector3 hang = new Vector3(l.wallPoint.x, l.topPoint.y - hangPivotBelowLedge, l.wallPoint.z) + l.wallNormal * hangDistanceFromWall;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(hang, 0.2f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(l.topPoint - l.wallNormal * climbForwardDistance + Vector3.up * climbExtraHeight, 0.15f);
        }
    }
}