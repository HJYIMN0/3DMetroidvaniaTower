using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Attach to Animator states. Emits phase events (Prep/Active/Recovery) based on
    /// the AnimationPhaseProfile referenced by the AnimatorBridge on the same GameObject.
    /// </summary>
    public class AnimStateEvents : StateMachineBehaviour
    {
        [SerializeField] private string stateNameOverride = "";

        public event System.Action OnPrepStart;
        public event System.Action OnActiveStart;
        public event System.Action OnRecoveryStart;

        private AnimatorBridge _bridge;
        private AnimationPhaseProfile.StatePhases _phases;
        private string _resolvedName;
        private bool _prepFired, _activeFired, _recoveryFired;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _bridge = animator.GetComponent<AnimatorBridge>();
            if (_bridge == null || _bridge.PhaseProfile == null) return;

            _resolvedName = string.IsNullOrEmpty(stateNameOverride) ? animator.GetCurrentAnimatorClipInfo(layerIndex)[0].clip.name : stateNameOverride;
            _phases = _bridge.PhaseProfile.Get(_resolvedName);

            _prepFired = _activeFired = _recoveryFired = false;
            OnPrepStart?.Invoke();
            _prepFired = true;
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_phases == null) return;
            float nt = stateInfo.normalizedTime % 1f;

            if (!_activeFired && nt >= _phases.prepEnd)
            {
                _activeFired = true;
                OnActiveStart?.Invoke();
                // Notify combat manager
                var pcm = animator.GetComponent<Combat.PlayerCombatManager>();
                if (pcm != null) pcm.OnAttackActiveStart();
            }

            if (!_recoveryFired && nt >= _phases.activeEnd)
            {
                _recoveryFired = true;
                OnRecoveryStart?.Invoke();
                var pcm = animator.GetComponent<Combat.PlayerCombatManager>();
                if (pcm != null) pcm.OnAttackActiveEnd();
            }
        }
    }
}