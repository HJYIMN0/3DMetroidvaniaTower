using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Input
{
    /// <summary>
    /// Per-command queue settings, editable in the Inspector (also at runtime: they are read when the command is emitted).
    /// </summary>
    [Serializable]
    public class InputCommandSettings
    {
        [Tooltip("Priorità del comando quando entra in coda (più alto = più importante).")]
        public int priority = 50;

        [Tooltip("Se true il comando sovrascrive quelli a priorità inferiore già in coda (es. Dodge che cancella un Attack).")]
        public bool overridesLower;

        [Tooltip("Se true questo comando non può mai essere rimosso da un override di un altro comando.")]
        public bool immuneToOverride;

        public InputCommandSettings() { }

        public InputCommandSettings(int priority, bool overridesLower, bool immuneToOverride)
        {
            this.priority = priority;
            this.overridesLower = overridesLower;
            this.immuneToOverride = immuneToOverride;
        }
    }

    /// <summary>
    /// Translates InputAction callbacks into InputCommands. Assign InputActionReference fields from the Inspector.
    /// Also exposes MoveInput, LookInput and JumpHeld for the movement system.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        [Header("Command actions")]
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionReference parryAction;
        [SerializeField] private InputActionReference dodgeAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference grabWallAction;
        [SerializeField] private InputActionReference grabLedgeAction;

        [Header("Move / Look")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;

        [Header("Command settings (priority / override)")]
        [SerializeField] private InputCommandSettings attackSettings = new InputCommandSettings(30, false, false);
        [SerializeField] private InputCommandSettings parrySettings = new InputCommandSettings(100, false, true);
        [SerializeField] private InputCommandSettings dodgeSettings = new InputCommandSettings(50, true, false);
        [SerializeField] private InputCommandSettings jumpSettings = new InputCommandSettings(90, false, true);
        [SerializeField] private InputCommandSettings grabWallSettings = new InputCommandSettings(90, false, true);
        [SerializeField] private InputCommandSettings grabLedgeSettings = new InputCommandSettings(90, false, true);

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }

        /// <summary>True while the jump button is held (used for variable jump height).</summary>
        public bool JumpHeld => jumpAction != null && jumpAction.action != null && jumpAction.action.IsPressed();

        public event Action<InputCommand> OnCommandGenerated;

        private class Subscription
        {
            public InputActionReference reference;
            public Action<InputAction.CallbackContext> handler;
        }

        private readonly List<Subscription> _subscriptions = new List<Subscription>();

        private void OnEnable()
        {
            Subscribe(attackAction, InputCommandType.Attack, attackSettings);
            Subscribe(parryAction, InputCommandType.Parry, parrySettings);
            Subscribe(dodgeAction, InputCommandType.Dodge, dodgeSettings);
            Subscribe(jumpAction, InputCommandType.Jump, jumpSettings);
            Subscribe(grabWallAction, InputCommandType.GrabWall, grabWallSettings);
            Subscribe(grabLedgeAction, InputCommandType.GrabLedge, grabLedgeSettings);

            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
                moveAction.action.performed += OnMove;
                moveAction.action.canceled += OnMove;
            }
            if (lookAction != null && lookAction.action != null)
            {
                lookAction.action.Enable();
                lookAction.action.performed += OnLook;
                lookAction.action.canceled += OnLook;
            }
        }

        private void OnDisable()
        {
            // Handlers are cached, so they really get unsubscribed (the old version leaked lambdas and duplicated commands).
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                Subscription s = _subscriptions[i];
                if (s.reference != null && s.reference.action != null) s.reference.action.performed -= s.handler;
            }
            _subscriptions.Clear();

            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.performed -= OnMove;
                moveAction.action.canceled -= OnMove;
            }
            if (lookAction != null && lookAction.action != null)
            {
                lookAction.action.performed -= OnLook;
                lookAction.action.canceled -= OnLook;
            }

            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
        }

        private void Subscribe(InputActionReference reference, InputCommandType type, InputCommandSettings settings)
        {
            if (reference == null || reference.action == null) return;

            Action<InputAction.CallbackContext> handler = _ => Emit(type, settings);
            reference.action.performed += handler;
            reference.action.Enable();
            _subscriptions.Add(new Subscription { reference = reference, handler = handler });
        }

        private void Emit(InputCommandType type, InputCommandSettings settings)
        {
            var cmd = new InputCommand(type, settings.priority, settings.overridesLower, settings.immuneToOverride);
            OnCommandGenerated?.Invoke(cmd);

            // Feed PlayerInputState for AI input-reading
            switch (type)
            {
                case InputCommandType.Attack: PlayerInputState.LastAttackInputTime = Time.time; break;
                case InputCommandType.Parry: PlayerInputState.LastParryInputTime = Time.time; break;
                case InputCommandType.Dodge: PlayerInputState.LastDodgeInputTime = Time.time; break;
            }
        }

        private void OnMove(InputAction.CallbackContext ctx) { MoveInput = ctx.ReadValue<Vector2>(); }
        private void OnLook(InputAction.CallbackContext ctx) { LookInput = ctx.ReadValue<Vector2>(); }
    }
}