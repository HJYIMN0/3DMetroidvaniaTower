using System;
using UnityEngine;
using Game.Input;

namespace Game.Movement
{
    /// <summary>
    /// Thin orchestrator (ex PlayerMover). Every FixedUpdate it: refreshes detectors, builds the shared
    /// MovementContext from input, lets the highest-priority exclusive module that CanExecute() own the tick,
    /// otherwise runs the passive modules (Locomotion, Jump). Contains no movement math itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovementCoordinator : AbstractUnitMover
    {
        [Header("Input")]
        [Tooltip("Sorgente di MoveInput e comandi. Se vuoto viene preso dal GameObject.")]
        [SerializeField] private InputReader inputReader;
        [Tooltip("Camera per il movimento camera-relative (la Main Camera gestita da Cinemachine). Se vuoto usa Camera.main.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Se true, il comando Dodge dell'InputReader viene bufferizzato qui. Mettilo a false quando sarà il combat manager a chiamare il dodge.")]
        [SerializeField] private bool recordDodgeCommands = true;

        [Header("Detectors")]
        [SerializeField] private GroundDetector ground;
        [SerializeField] private WallDetector wall;
        [SerializeField] private LedgeDetector ledge;

        [Header("Fallback gravity")]
        [Tooltip("Gravità (m/s²) usata solo se nessun modulo verticale (JumpModule) gestisce la Y.")]
        [SerializeField, Min(0f)] private float fallbackGravity = 30f;
        [Tooltip("Velocità massima di caduta (m/s) del fallback.")]
        [SerializeField, Min(0f)] private float fallbackMaxFallSpeed = 25f;

        [Header("Debug")]
        [Tooltip("Logga in Console ogni cambio di modulo esclusivo.")]
        [SerializeField] private bool logTransitions = true;

        private readonly MovementContext _ctx = new MovementContext();
        private MovementModule[] _modules = Array.Empty<MovementModule>();
        private MovementModule _active;
        private Vector3 _publicMoveInput;

        public MovementContext Context => _ctx;
        public MovementModule[] Modules => _modules;
        /// <summary>The exclusive module owning the current tick, or null.</summary>
        public MovementModule ActiveModule => _active;

        public override bool IsGrounded => _ctx.IsGrounded;
        public override Vector3 MoveInputWorld => _publicMoveInput;

        public bool IsDodging => _active is DodgeModule;
        public bool IsWallSliding => _active is WallSlideModule;
        public bool IsWallRunning => _active is WallRunModule;
        public bool IsLedgeGrabbing => _active is LedgeGrabModule;

        protected override void Awake()
        {
            base.Awake();
            if (inputReader == null) inputReader = GetComponent<InputReader>();
            if (ground == null) ground = GetComponent<GroundDetector>();
            if (wall == null) wall = GetComponent<WallDetector>();
            if (ledge == null) ledge = GetComponent<LedgeDetector>();

            if (ground == null) Debug.LogWarning("[PlayerMovementCoordinator] GroundDetector mancante: IsGrounded sarà sempre false.");
            if (inputReader == null) Debug.LogWarning("[PlayerMovementCoordinator] InputReader mancante: nessun input.");

            Vector3 f = transform.forward; f.y = 0f;
            _ctx.Facing = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;

            RefreshModules();
        }

        private void OnEnable()
        {
            if (inputReader != null) inputReader.OnCommandGenerated += OnCommand;
        }

        private void OnDisable()
        {
            if (inputReader != null) inputReader.OnCommandGenerated -= OnCommand;
        }

        [ContextMenu("Debug/Refresh Modules")]
        public void RefreshModules()
        {
            _modules = GetComponents<MovementModule>();
        }

        private void OnCommand(InputCommand cmd)
        {
            switch (cmd.type)
            {
                case InputCommandType.Jump: _ctx.Jump.Press(); break;
                case InputCommandType.Dodge: if (recordDodgeCommands) _ctx.Dodge.Press(); break;
                case InputCommandType.GrabWall: _ctx.GrabWall.Press(); break;
                case InputCommandType.GrabLedge: _ctx.GrabLedge.Press(); break;
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (rb.IsSleeping()) rb.WakeUp();

            BuildInput();

            // 1) detectors, in a deterministic order, before any module reads them
            if (ground != null) ground.Refresh();
            _ctx.IsGrounded = ground != null && ground.IsGrounded;
            _ctx.AirTime = _ctx.IsGrounded ? 0f : _ctx.AirTime + dt;
            if (_ctx.IsGrounded) _ctx.LastJumpedWall = null;   // touching ground (GroundDetector mask) frees every wall again
            if (wall != null) wall.Refresh();
            if (ledge != null) ledge.Refresh(_ctx.Facing);

            // 2) modules
            _ctx.DeltaTime = dt;
            _ctx.VerticalHandled = false;
            SortModules();
            SelectAndTick(dt);

            // 3) safety net: someone must own Y
            if (!_ctx.VerticalHandled) ApplyFallbackGravity(dt);

            _publicMoveInput = _active != null ? Vector3.zero : _ctx.MoveInputWorld;
        }

        private void BuildInput()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            Vector2 raw = inputReader != null ? inputReader.MoveInput : Vector2.zero;

            Vector3 camF = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camR = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camF.y = 0f; camR.y = 0f;
            camF.Normalize(); camR.Normalize();

            Vector3 world = camF * raw.y + camR * raw.x;
            if (world.sqrMagnitude > 1f) world.Normalize();

            _ctx.MoveInputRaw = raw;
            _ctx.MoveInputWorld = world;
            if (world.sqrMagnitude > 0.01f) _ctx.Facing = world.normalized;
            _ctx.JumpHeld = inputReader != null && inputReader.JumpHeld;
        }

        private void SortModules()
        {
            // insertion sort, descending priority; no allocations (priorities can change at runtime)
            for (int i = 1; i < _modules.Length; i++)
            {
                MovementModule key = _modules[i];
                if (key == null) continue;
                int j = i - 1;
                while (j >= 0 && _modules[j] != null && _modules[j].Priority < key.Priority)
                {
                    _modules[j + 1] = _modules[j];
                    j--;
                }
                _modules[j + 1] = key;
            }
        }

        private void SelectAndTick(float dt)
        {
            MovementModule chosen = null;
            for (int i = 0; i < _modules.Length; i++)
            {
                MovementModule m = _modules[i];
                if (m == null || !m.isActiveAndEnabled || !m.Exclusive) continue;
                if (m.CanExecute(_ctx)) { chosen = m; break; }
            }

            if (chosen != _active) Transition(chosen);

            if (_active != null)
            {
                _active.Tick(_ctx, dt);
                _ctx.VerticalHandled = true;   // an exclusive module always owns Y
                return;
            }

            for (int i = 0; i < _modules.Length; i++)
            {
                MovementModule m = _modules[i];
                if (m == null || !m.isActiveAndEnabled || m.Exclusive) continue;
                if (m.CanExecute(_ctx)) m.Tick(_ctx, dt);
            }
        }

        private void Transition(MovementModule next)
        {
            MovementModule prev = _active;
            if (prev != null) prev.Exit(_ctx);
            _active = next;
            if (_active != null) _active.Enter(_ctx);

            if (logTransitions)
            {
                string from = prev != null ? prev.GetType().Name : "none";
                string to = _active != null ? _active.GetType().Name : "none";
                Debug.Log($"[PlayerMovementCoordinator] {from} -> {to}");
            }
        }

        private void ApplyFallbackGravity(float dt)
        {
            Vector3 v = rb.linearVelocity;
            if (_ctx.IsGrounded && v.y <= 0f) v.y = 0f;
            else v.y = Mathf.Max(v.y - fallbackGravity * dt, -fallbackMaxFallSpeed);
            rb.linearVelocity = v;
        }

        [ContextMenu("Debug/Print State")]
        public void PrintState()
        {
            string active = _active != null ? _active.GetType().Name : "none";
            Debug.Log($"[PlayerMovementCoordinator] active={active} grounded={_ctx.IsGrounded} airTime={_ctx.AirTime:F2} " +
                      $"vel={(rb != null ? rb.linearVelocity.ToString("F2") : "n/a")} input={_ctx.MoveInputRaw}");
            for (int i = 0; i < _modules.Length; i++)
            {
                if (_modules[i] != null) _modules[i].PrintState();
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 p = transform.position + Vector3.up * 0.1f;
            Gizmos.color = _ctx.IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(p, 0.12f);
            Gizmos.color = Color.white;
            Gizmos.DrawRay(p, _ctx.Facing * 0.8f);
            if (rb != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(p, rb.linearVelocity * 0.2f);
            }
        }
    }
}