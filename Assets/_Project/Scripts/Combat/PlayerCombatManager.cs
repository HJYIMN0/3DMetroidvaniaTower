using UnityEngine;
using Game.Input;

namespace Game.Combat
{
    /// <summary>
    /// Player combat. The command queue works as a real input buffer: a command that cannot run yet
    /// (character busy) waits in the queue until it can, is overridden by a higher-priority command, or expires.
    /// What can start while something else is happening is decided in EvaluateCommand() and tuned from the Inspector.
    /// </summary>
    public class PlayerCombatManager : AbstractCombatManager
    {
        [SerializeField] private InputReader inputReader;

        [Header("Action rules (what may start while something else is going on)")]
        [Tooltip("true = una schivata può partire durante un attacco e lo interrompe. false = aspetta (buffer) la fine dell'attacco.")]
        [SerializeField] private bool dodgeInterruptsAttack = true;
        [Tooltip("true = un parry può partire durante un attacco e lo interrompe. false = aspetta la fine dell'attacco.")]
        [SerializeField] private bool parryInterruptsAttack = true;
        [Tooltip("true = si può attaccare mentre si schiva. false = l'attacco aspetta la fine della schivata.")]
        [SerializeField] private bool attackDuringDodge = false;
        [Tooltip("true = si può parare mentre si schiva. false = il parry aspetta la fine della schivata.")]
        [SerializeField] private bool parryDuringDodge = false;

        protected override bool OneActionPerFrame => true;

        private bool IsDodgeBusy => dodge != null && (dodge.IsDodging || dodge.IsRequested);
        private bool IsAttackInProgress => comboController != null && comboController.IsInCombo;

        private void OnEnable()
        {
            // Jump / GrabWall / GrabLedge never enter the combat queue (see OnInputCommand).
            if (inputReader != null) inputReader.OnCommandGenerated += OnInputCommand;
            if (parry != null) parry.OnParrySuccess += OnParry;
            if (weapon != null) weapon.OnHit += OnWeaponHit;
            if (dodge != null) dodge.OnDodgeStart += OnDodgeStarted;
        }

        private void OnDisable()
        {
            if (inputReader != null) inputReader.OnCommandGenerated -= OnInputCommand;
            if (parry != null) parry.OnParrySuccess -= OnParry;
            if (weapon != null) weapon.OnHit -= OnWeaponHit;
            if (dodge != null) dodge.OnDodgeStart -= OnDodgeStarted;
        }

        // Jump / GrabWall / GrabLedge are consumed directly by PlayerMovementCoordinator (it has its own input buffers).
        // Keeping them out of this queue stops them from occupying slots and making attacks/dodges "queue full".
        private void OnInputCommand(InputCommand cmd)
        {
            if (cmd == null) return;
            switch (cmd.type)
            {
                case InputCommandType.Jump:
                case InputCommandType.GrabWall:
                case InputCommandType.GrabLedge:
                    return;
            }
            SubmitCommand(cmd);
        }

        /// <summary>
        /// The buffer logic. Called every frame for each queued command, highest priority first.
        /// Wait = the character cannot do it yet, keep it in the queue. Execute = do it now.
        /// </summary>
        protected override CommandDecision EvaluateCommand(InputCommand cmd)
        {
            switch (cmd.type)
            {
                case InputCommandType.Attack:
                    if (IsDodgeBusy && !attackDuringDodge) return CommandDecision.Wait;
                    if (!IsAttackInProgress) return CommandDecision.Execute;   // starts the combo
                    // Combo already running: the next hit is accepted only inside the combo window.
                    // Before the window opens the press is buffered and fires the moment it opens.
                    float normTime = animatorBridge != null ? animatorBridge.GetNormalizedTime(1) : -1f;
                    return comboController.IsInWindow(normTime) ? CommandDecision.Execute : CommandDecision.Wait;

                case InputCommandType.Parry:
                    if (IsDodgeBusy && !parryDuringDodge) return CommandDecision.Wait;
                    if (IsAttackInProgress && !parryInterruptsAttack) return CommandDecision.Wait;
                    // Parry on cooldown: wait for the cooldown instead of losing the press.
                    if (CombatDirector.Instance != null && CombatDirector.Instance.ParryCooldownRemaining > 0f) return CommandDecision.Wait;
                    return CommandDecision.Execute;

                case InputCommandType.Dodge:
                    if (IsDodgeBusy) return CommandDecision.Wait;
                    if (IsAttackInProgress && !dodgeInterruptsAttack) return CommandDecision.Wait;
                    // The movement side can refuse (cooldown, no air dodges left): wait until it is ready.
                    if (dodge != null && !dodge.CanStartNow) return CommandDecision.Wait;
                    return CommandDecision.Execute;

                default:
                    return CommandDecision.Discard;
            }
        }

        protected override void ProcessCommand(InputCommand cmd)
        {
            switch (cmd.type)
            {
                case InputCommandType.Attack:
                    TryStartOrAdvanceCombo();
                    break;

                case InputCommandType.Parry:
                    if (IsAttackInProgress) CancelAttack();
                    if (CombatDirector.Instance == null || CombatDirector.Instance.RequestParry())
                    {
                        parry?.BeginParry();
                        animatorBridge?.PlayState("Parry", 1);
                    }
                    else
                    {
                        Debug.Log($"[PlayerCombatManager] Parry on cooldown ({CombatDirector.Instance.ParryCooldownRemaining:F2}s).");
                    }
                    break;

                case InputCommandType.Dodge:
                    if (IsAttackInProgress) CancelAttack();
                    // Zero direction: DodgeModule uses the current move input, or a backstep if there is none.
                    dodge?.BeginDodge(Vector3.zero);
                    break;
            }
        }

        // Interrupts the current attack so a dodge/parry can start.
        // TODO_VERIFY: ComboController's API was not seen; re-creating it resets its state (same as base Awake does).
        // Replace with a proper ComboController reset method when we look at that class.
        private void CancelAttack()
        {
            weapon?.Deactivate();
            comboController = new ComboController();
        }

        private bool TryStartOrAdvanceCombo()
        {
            if (defaultCombo == null) return false;

            if (!comboController.IsInCombo)
            {
                var step = comboController.StartCombo(defaultCombo);
                PlayComboStep(step);
                return true;
            }

            float normTime = animatorBridge != null ? animatorBridge.GetNormalizedTime(1) : -1f;
            if (!comboController.IsInWindow(normTime)) return false;

            var next = comboController.TryAdvance(InputCommandType.Attack);
            if (next != null) { PlayComboStep(next); return true; }
            return false;
        }

        private void PlayComboStep(ComboStep step)
        {
            if (step == null) return;
            animatorBridge?.PlayState(step.animatorStateName, 1);
        }

        // The dodge animation plays when the dodge really starts (DodgeModule can refuse a request).
        private void OnDodgeStarted()
        {
            animatorBridge?.PlayState("Dodge", 2);
        }

        private void OnParry(GameObject attacker)
        {
            Debug.Log($"[PlayerCombatManager] Parried {attacker.name}");
        }

        private void OnWeaponHit(RaycastHit hit, Core.IDamageable dmg)
        {
            Debug.Log($"[PlayerCombatManager] Hit {dmg.Transform.name}");
        }

        /// <summary>Called by AnimStatePhaseBehaviour when the "active" phase starts.</summary>
        public void OnAttackActiveStart()
        {
            float mult = comboController.CurrentStep != null ? comboController.CurrentStep.damageMultiplier : 1f;
            weapon?.Activate(mult);
        }

        public void OnAttackActiveEnd()
        {
            weapon?.Deactivate();
        }
    }
}