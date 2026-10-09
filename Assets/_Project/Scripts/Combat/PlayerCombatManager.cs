using UnityEngine;
using Game.Input;

namespace Game.Combat
{
    public class PlayerCombatManager : AbstractCombatManager
    {
        [SerializeField] private InputReader inputReader;

        private void OnEnable()
        {
            if (inputReader != null) inputReader.OnCommandGenerated += SubmitCommand;
            if (parry != null) parry.OnParrySuccess += OnParry;
            if (weapon != null) weapon.OnHit += OnWeaponHit;
        }
        private void OnDisable()
        {
            if (inputReader != null) inputReader.OnCommandGenerated -= SubmitCommand;
            if (parry != null) parry.OnParrySuccess -= OnParry;
            if (weapon != null) weapon.OnHit -= OnWeaponHit;
        }

        protected override void ProcessCommand(InputCommand cmd)
        {
            switch (cmd.type)
            {
                case InputCommandType.Attack:
                    if (TryStartOrAdvanceCombo()) break;
                    break;
                case InputCommandType.Parry:
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
                    BeginDodgeFromInput();
                    break;
                case InputCommandType.Jump:
                case InputCommandType.GrabWall:
                case InputCommandType.GrabLedge:
                    // handled by PlayerMover
                    break;
            }
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

        private void BeginDodgeFromInput()
        {
            Vector3 dir = -transform.forward;
            dodge?.BeginDodge(dir, life);
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