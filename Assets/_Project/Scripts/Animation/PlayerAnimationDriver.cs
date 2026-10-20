using UnityEngine;
using Game.Animation;
using Game.Input;
using Game.Movement;

namespace Game.Animation
{
    /// <summary>
    /// Feeds AnimatorBridge with movement-derived params (Grounded, VerticalVelocity, Jump trigger).
    /// Subscribes to PlayerMovementCoordinator.OnTickCompleted so params are updated in the same tick.
    /// </summary>
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private AnimatorBridge bridge;
        [SerializeField] private PlayerMovementCoordinator coordinator;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private InputReader inputReader;

        private bool _jumpTriggerRequested;

        private void Awake()
        {
            if (bridge == null) bridge = GetComponent<AnimatorBridge>();
            if (coordinator == null) coordinator = GetComponent<PlayerMovementCoordinator>();
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (inputReader == null) inputReader = GetComponent<InputReader>();
        }

        private void OnEnable()
        {
            if (coordinator != null) coordinator.OnTickCompleted += OnTickCompleted;
            if (inputReader != null) inputReader.OnCommandGenerated += OnCommand;
        }

        private void OnDisable()
        {
            if (coordinator != null) coordinator.OnTickCompleted -= OnTickCompleted;
            if (inputReader != null) inputReader.OnCommandGenerated -= OnCommand;
        }

        private void OnCommand(InputCommand cmd)
        {
            if (cmd.type == InputCommandType.Jump)
                _jumpTriggerRequested = true;
        }

        private void OnTickCompleted(MovementContext ctx)
        {
            if (bridge == null || ctx == null) return;

            // 1) continuous params
            bridge.SetGrounded(ctx.IsGrounded);
            if (rb != null) bridge.SetVerticalVelocity(rb.linearVelocity.y);

            // 2) jump trigger: fired only if grounded at this tick (drop stale requests)
            if (_jumpTriggerRequested)
            {
                _jumpTriggerRequested = false;
                if (ctx.IsGrounded) bridge.TriggerJump();
            }
        }
    }
}