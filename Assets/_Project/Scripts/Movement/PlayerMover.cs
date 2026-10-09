using UnityEngine;
using Game.Input;

namespace Game.Movement
{
    [RequireComponent(typeof(WallDetector), typeof(LedgeDetector), typeof(WallSlideController))]
    public class PlayerMover : AbstractUnitMover
    {
        [Header("Input")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform cameraTransform;

        [Header("Dodge")]
        [SerializeField] private Game.Combat.DodgeController dodge;

        [Header("Jump Feel")]
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.15f;
        [SerializeField] private bool canDoubleJumpOnlyAfterWallGrab = true;

        [Header("Runtime State (read-only)")]
        [SerializeField] private bool _canDoubleJump;

        private float _lastGroundedTime = -999f;
        private float _lastJumpInputTime = -999f;

        private WallDetector _wall;
        private LedgeDetector _ledge;
        private WallSlideController _slide;

        protected override void Awake()
        {
            base.Awake();
            _wall = GetComponent<WallDetector>();
            _ledge = GetComponent<LedgeDetector>();
            _slide = GetComponent<WallSlideController>();
            if (dodge == null) dodge = GetComponent<Game.Combat.DodgeController>();
        }

        private void OnEnable()
        {
            if (inputReader != null) inputReader.OnCommandGenerated += OnCommand;
        }
        private void OnDisable()
        {
            if (inputReader != null) inputReader.OnCommandGenerated -= OnCommand;
        }

        private void OnCommand(InputCommand cmd)
        {
            switch (cmd.type)
            {
                case InputCommandType.Jump: _lastJumpInputTime = Time.time; break;
                case InputCommandType.GrabWall: TryGrabWall(); break;
                case InputCommandType.GrabLedge: TryGrabLedge(); break;
            }
        }

        protected override void GroundCheck()
        {
            base.GroundCheck();
            if (_isGrounded)
            {
                _lastGroundedTime = Time.time;
                if (!canDoubleJumpOnlyAfterWallGrab) _canDoubleJump = true;
                else _canDoubleJump = false;
            }
        }

        protected override void ApplyMovement(float dt)
        {
            // Dodge has absolute priority over locomotion
            if (dodge != null && dodge.IsDodging)
            {
                Vector3 v = dodge.GetCurrentVelocity();
                float y = dodge.CancelVerticalVelocity ? 0f : rb.linearVelocity.y;
                rb.linearVelocity = new Vector3(v.x, y, v.z);
                return;
            }

            // Wall slide overrides horizontal control while active
            if (_slide.IsSliding)
            {
                _slide.TickSlide(rb, dt);
                return;
            }

            Vector2 raw = inputReader != null ? inputReader.MoveInput : Vector2.zero;
            Vector3 camF = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camR = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camF.y = 0f; camR.y = 0f;
            camF.Normalize(); camR.Normalize();

            _moveInputWorld = (camF * raw.y + camR * raw.x);
            if (_moveInputWorld.sqrMagnitude > 1f) _moveInputWorld.Normalize();

            bool running = raw.magnitude > 0.75f;
            float targetSpeed = running ? runSpeed : walkSpeed;

            Vector3 targetVel = _moveInputWorld * targetSpeed;
            Vector3 currentHoriz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 newHoriz = Vector3.MoveTowards(currentHoriz, targetVel, acceleration * dt);
            rb.linearVelocity = new Vector3(newHoriz.x, rb.linearVelocity.y, newHoriz.z);

            // Rotation
            if (_moveInputWorld.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_moveInputWorld);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * dt));
            }

            // Jump handling (buffered + coyote)
            bool coyoteOk = Time.time - _lastGroundedTime <= coyoteTime;
            bool bufferOk = Time.time - _lastJumpInputTime <= jumpBufferTime;

            if (bufferOk && coyoteOk)
            {
                _verticalVelocity = jumpForce;
                _lastJumpInputTime = -999f;
                _lastGroundedTime = -999f;
            }
            else if (bufferOk && _canDoubleJump)
            {
                _verticalVelocity = jumpForce;
                _canDoubleJump = false;
                _lastJumpInputTime = -999f;
            }
        }

        private void TryGrabWall()
        {
            if (_wall.HasWall)
            {
                _canDoubleJump = true;   // wall grab enables double jump
                _slide.BeginSlide();
            }
        }

        private void TryGrabLedge()
        {
            if (_ledge.HasLedge)
            {
                // TODO: snap to ledge, play hang animation, disable gravity
                Debug.Log("[PlayerMover] Ledge grab detected.");
            }
        }
    }
}