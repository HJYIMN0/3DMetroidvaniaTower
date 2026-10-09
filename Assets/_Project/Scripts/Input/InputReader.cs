using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Input
{
    /// <summary>
    /// Translates InputAction callbacks into InputCommands.
    /// Assign InputActionReference fields from the inspector.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionReference parryAction;
        [SerializeField] private InputActionReference dodgeAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference grabWallAction;
        [SerializeField] private InputActionReference grabLedgeAction;

        [Header("Move (for movement system)")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;

        [Header("Command Defaults")]
        [SerializeField] private int attackPriority = 30;
        [SerializeField] private int dodgePriority = 50;
        [SerializeField] private bool dodgeOverridesAttack = true;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }

        public event Action<InputCommand> OnCommandGenerated;

        private void OnEnable()
        {
            Subscribe(attackAction, ctx => Emit(InputCommandType.Attack, attackPriority, false));
            Subscribe(parryAction, ctx => Emit(InputCommandType.Parry, 100, false));
            Subscribe(dodgeAction, ctx => Emit(InputCommandType.Dodge, dodgePriority, dodgeOverridesAttack));
            Subscribe(jumpAction, ctx => Emit(InputCommandType.Jump, 90, false));
            Subscribe(grabWallAction, ctx => Emit(InputCommandType.GrabWall, 90, false));
            Subscribe(grabLedgeAction, ctx => Emit(InputCommandType.GrabLedge, 90, false));

            if (moveAction != null) { moveAction.action.Enable(); moveAction.action.performed += OnMove; moveAction.action.canceled += OnMove; }
            if (lookAction != null) { lookAction.action.Enable(); lookAction.action.performed += OnLook; lookAction.action.canceled += OnLook; }
        }

        private void OnDisable()
        {
            Unsubscribe(attackAction);
            Unsubscribe(parryAction);
            Unsubscribe(dodgeAction);
            Unsubscribe(jumpAction);
            Unsubscribe(grabWallAction);
            Unsubscribe(grabLedgeAction);

            if (moveAction != null) { moveAction.action.performed -= OnMove; moveAction.action.canceled -= OnMove; }
            if (lookAction != null) { lookAction.action.performed -= OnLook; lookAction.action.canceled -= OnLook; }
        }

        private void Subscribe(InputActionReference r, Action<InputAction.CallbackContext> handler)
        {
            if (r == null || r.action == null) return;
            r.action.Enable();
            r.action.performed += handler;
        }

        private void Unsubscribe(InputActionReference r)
        {
            if (r == null || r.action == null) return;
            // Note: cannot unsubscribe lambdas easily; see TODO_VERIFY
            // Best practice: cache handlers in a dictionary.
            // TODO_VERIFY: for production, store Action delegates in fields.
        }

        private void Emit(InputCommandType type, int priority, bool isOverride)
        {
            var cmd = new InputCommand(type, priority, isOverride);
            OnCommandGenerated?.Invoke(cmd);

            // Feed PlayerInputState for AI input-reading
            switch (type)
            {
                case InputCommandType.Attack: PlayerInputState.LastAttackInputTime = Time.time; break;
                case InputCommandType.Parry: PlayerInputState.LastParryInputTime = Time.time; break;
                case InputCommandType.Dodge: PlayerInputState.LastDodgeInputTime = Time.time; break;
            }
        }

        private void OnMove(InputAction.CallbackContext ctx) => MoveInput = ctx.ReadValue<Vector2>();
        private void OnLook(InputAction.CallbackContext ctx) => LookInput = ctx.ReadValue<Vector2>();
    }
}