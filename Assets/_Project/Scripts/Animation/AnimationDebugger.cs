using System.Text;
using UnityEngine;
using Game.Animation;
using Game.Movement;

namespace Game.Animation
{
    /// <summary>
    /// Dump completo dello stato animazione + movimento. Premi F9 in Play (o ContextMenu → Dump Now).
    /// Il blocco è pensato per essere copiato dalla Console e incollato altrove.
    /// </summary>
    public class AnimationDebugger : MonoBehaviour
    {
        [Header("References (auto-fill in Awake)")]
        [SerializeField] private Animator animator;
        [SerializeField] private AnimatorBridge bridge;
        [SerializeField] private PlayerMovementCoordinator coordinator;
        [SerializeField] private LocomotionModule locomotion;

        [Header("Trigger")]
        [SerializeField] private KeyCode dumpKey = KeyCode.F9;
        [SerializeField] private bool enableHotkey = true;
        [SerializeField] private bool dumpOnStateChange = false;

        [Header("Layers to inspect")]
        [SerializeField] private int layerToInspect = 0;

        [Header("Known states (for name lookup, from the controller)")]
        [SerializeField]
        private string[] knownStateNames = new[]
        {
            "Locomotion", "Jump_Preparation", "Jump_Ascending", "Jump_Contact", "Jump_Descending"
        };

        private int _lastStateHash;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (bridge == null) bridge = GetComponent<AnimatorBridge>();
            if (coordinator == null) coordinator = GetComponent<PlayerMovementCoordinator>();
            if (locomotion == null) locomotion = GetComponent<LocomotionModule>();
        }

        private void Update()
        {
            if (enableHotkey)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                // Namespace-qualified: 'using Game.Input;' would otherwise bind 'Input' to the namespace.
                if (UnityEngine.Input.GetKeyDown(dumpKey)) DumpNow("KeyPress");
#else
                // New Input System only: no UnityEngine.Input at compile time.
                // Use ContextMenu → Dump Now, or wire a custom input action to DumpNow().
                enableHotkey = false;
#endif
            }

            if (dumpOnStateChange && animator != null)
            {
                var st = animator.GetCurrentAnimatorStateInfo(layerToInspect);
                if (st.shortNameHash != _lastStateHash)
                {
                    _lastStateHash = st.shortNameHash;
                    DumpNow("StateChanged");
                }
            }
        }

        [ContextMenu("Dump Now")]
        public void DumpNow() => DumpNow("ContextMenu");

        public void DumpNow(string reason)
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("========== ANIMATION DEBUG DUMP ==========");
            sb.AppendLine($"Reason: {reason}    Time: {Time.time:F3}s    Frame: {Time.frameCount}");
            sb.AppendLine($"GameObject: {name}    Scene: {gameObject.scene.name}");
            sb.AppendLine();

            DumpAnimatorBasics(sb);
            DumpLayers(sb);
            DumpParameters(sb);
            DumpTransitionAnalysis(sb);
            DumpExternalState(sb);
            DumpDiagnostics(sb);

            sb.AppendLine("========== END ANIMATION DEBUG DUMP ==========");
            Debug.Log(sb.ToString(), this);
        }

        // ------------------------------------------------------------------

        private void DumpAnimatorBasics(StringBuilder sb)
        {
            sb.AppendLine("--- ANIMATOR ---");
            if (animator == null) { sb.AppendLine("  Animator: NULL"); sb.AppendLine(); return; }
            sb.AppendLine($"  Controller:       {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<null>")}");
            sb.AppendLine($"  Enabled:          {animator.enabled}   isActiveAndEnabled={animator.isActiveAndEnabled}");
            sb.AppendLine($"  Speed:            {animator.speed:F2}   {(Mathf.Approximately(animator.speed, 0f) ? "  <-- FROZEN!" : "")}");
            sb.AppendLine($"  UpdateMode:       {animator.updateMode}");
            sb.AppendLine($"  CullingMode:      {animator.cullingMode}");
            sb.AppendLine($"  ApplyRootMotion:  {animator.applyRootMotion}");
            sb.AppendLine();
        }

        private void DumpLayers(StringBuilder sb)
        {
            sb.AppendLine("--- LAYERS ---");
            if (animator == null || animator.layerCount == 0) { sb.AppendLine("  (no layers)"); sb.AppendLine(); return; }

            int layer = Mathf.Clamp(layerToInspect, 0, animator.layerCount - 1);
            string layerName = animator.GetLayerName(layer);
            float weight = animator.GetLayerWeight(layer);
            var st = animator.GetCurrentAnimatorStateInfo(layer);
            string stateName = IdentifyState(st);

            sb.AppendLine($"  [{layer}] {layerName}    weight={weight:F2}");
            sb.AppendLine($"      State:           {stateName}   (hash={st.shortNameHash})");
            sb.AppendLine($"      NormalizedTime:  {st.normalizedTime:F3}    Length: {st.length:F3}s    Looping: {st.loop}");
            sb.AppendLine($"      Tag hash:        {st.tagHash}");

            var clips = animator.GetCurrentAnimatorClipInfo(layer);
            if (clips != null && clips.Length > 0)
            {
                sb.Append("      Clips:           ");
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i].clip != null) sb.Append($"{clips[i].clip.name}(w={clips[i].weight:F2}) ");
                sb.AppendLine();
            }

            if (animator.IsInTransition(layer))
            {
                var nxt = animator.GetNextAnimatorStateInfo(layer);
                sb.AppendLine($"      IN TRANSITION -> {IdentifyState(nxt)}   (hash={nxt.shortNameHash})   nt={nxt.normalizedTime:F3}");
            }
            sb.AppendLine();
        }

        private string IdentifyState(AnimatorStateInfo st)
        {
            if (knownStateNames != null)
                for (int i = 0; i < knownStateNames.Length; i++)
                    if (!string.IsNullOrEmpty(knownStateNames[i]) && st.IsName(knownStateNames[i]))
                        return knownStateNames[i];
            return $"<unknown hash={st.shortNameHash}>";
        }

        private void DumpParameters(StringBuilder sb)
        {
            sb.AppendLine("--- PARAMETERS (Animator Controller) ---");
            if (animator == null) { sb.AppendLine("  (no animator)"); sb.AppendLine(); return; }

            var ps = animator.parameters;
            if (ps == null || ps.Length == 0) { sb.AppendLine("  (none)"); sb.AppendLine(); return; }

            foreach (var p in ps)
            {
                string live;
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float: live = animator.GetFloat(p.name).ToString("F3"); break;
                    case AnimatorControllerParameterType.Int: live = animator.GetInteger(p.name).ToString(); break;
                    case AnimatorControllerParameterType.Bool:
                    case AnimatorControllerParameterType.Trigger: live = animator.GetBool(p.name).ToString(); break;
                    default: live = "?"; break;
                }
                sb.AppendLine($"  {p.name,-20} {p.type,-10} = {live}");
            }
            sb.AppendLine();
        }

        // Known transitions from PlayerAnimationController (update if you add states).
        private void DumpTransitionAnalysis(StringBuilder sb)
        {
            sb.AppendLine("--- TRANSITION ANALYSIS ---");
            if (animator == null) { sb.AppendLine("  (no animator)"); sb.AppendLine(); return; }

            var st = animator.GetCurrentAnimatorStateInfo(layerToInspect);
            string current = IdentifyState(st);
            sb.AppendLine($"  Current state: {current}");

            switch (current)
            {
                case "Locomotion":
                    CheckJumpToJumpPrep(sb);
                    break;
                case "Jump_Ascending":
                    CheckAscToDesc(sb);
                    break;
                case "Jump_Descending":
                    CheckDescToContact(sb);
                    break;
                case "Jump_Preparation":
                    sb.AppendLine("    → Jump_Ascending: on exit time 1.0 (no condition) — automatic");
                    break;
                case "Jump_Contact":
                    sb.AppendLine("    → Locomotion:     on exit time 0.7 (no condition) — automatic");
                    break;
                default:
                    sb.AppendLine("    (no known transition rules for this state)");
                    break;
            }
            sb.AppendLine();
        }

        private void CheckJumpToJumpPrep(StringBuilder sb)
        {
            bool jump = SafeGetBool("Jump");
            bool grounded = SafeGetBool("Grounded");
            bool ok = jump && grounded;
            sb.AppendLine("    → Jump_Preparation:  requires Jump(trigger)=true AND Grounded(bool)=true");
            sb.AppendLine($"        Jump = {jump}    Grounded = {grounded}");
            sb.AppendLine($"        Result: {(ok ? "WOULD TRANSITION" : "BLOCKED")}");
        }

        private void CheckAscToDesc(StringBuilder sb)
        {
            float vv = SafeGetFloat("VerticalVelocity");
            bool ok = vv < 0f;
            sb.AppendLine("    → Jump_Descending:   requires VerticalVelocity < 0");
            sb.AppendLine($"        VerticalVelocity = {vv:F3}");
            sb.AppendLine($"        Result: {(ok ? "WOULD TRANSITION" : "BLOCKED")}");
        }

        private void CheckDescToContact(StringBuilder sb)
        {
            bool grounded = SafeGetBool("Grounded");
            sb.AppendLine("    → Jump_Contact:      requires Grounded == true");
            sb.AppendLine($"        Grounded = {grounded}");
            sb.AppendLine($"        Result: {(grounded ? "WOULD TRANSITION" : "BLOCKED")}");
        }

        private void DumpExternalState(StringBuilder sb)
        {
            sb.AppendLine("--- EXTERNAL STATE (movement) ---");

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                sb.AppendLine($"  Rigidbody velocity:     {rb.linearVelocity:F3}");
                sb.AppendLine($"  Rigidbody isKinematic:  {rb.isKinematic}");
                sb.AppendLine($"  Rigidbody constraints:  {rb.constraints}");
            }
            else sb.AppendLine("  Rigidbody: (none)");

            Vector3 euler = transform.rotation.eulerAngles;
            sb.AppendLine($"  Transform rotation Y:   {euler.y:F1}°");
            sb.AppendLine($"  Transform forward:      {transform.forward:F3}");

            if (coordinator != null)
            {
                var ctx = coordinator.Context;
                if (ctx != null)
                {
                    sb.AppendLine($"  Context.IsGrounded:     {ctx.IsGrounded}");
                    sb.AppendLine($"  Context.MoveInputRaw:   {ctx.MoveInputRaw}");
                    sb.AppendLine($"  Context.MoveInputWorld: {ctx.MoveInputWorld:F3}");
                    sb.AppendLine($"  Context.AirTime:        {ctx.AirTime:F2}s");
                }
                string active = coordinator.ActiveModule != null ? coordinator.ActiveModule.GetType().Name : "none";
                sb.AppendLine($"  Active module:          {active}");
            }
            else sb.AppendLine("  PlayerMovementCoordinator: (none)");

            if (locomotion != null)
            {
                sb.AppendLine($"  Locomotion.CurrentSpeed:   {locomotion.CurrentSpeed:F2}");
                sb.AppendLine($"  Locomotion.HeadingDegrees: {locomotion.HeadingDegrees:F1}°");
            }
            else sb.AppendLine("  LocomotionModule: (none)");

            sb.AppendLine();
        }

        private void DumpDiagnostics(StringBuilder sb)
        {
            sb.AppendLine("--- DIAGNOSTICS ---");
            if (animator == null) { sb.AppendLine("  (no animator)"); return; }

            float w0 = animator.GetLayerWeight(0);
            sb.AppendLine(w0 < 0.99f
                ? $"  ✗ Layer 0 weight = {w0:F2} (should be 1.00)"
                : $"  ✓ Layer 0 weight = {w0:F2}");

            if (coordinator != null && coordinator.Context != null)
            {
                bool animG = SafeGetBool("Grounded");
                bool detG = coordinator.Context.IsGrounded;
                sb.AppendLine(animG != detG
                    ? $"  ✗ Grounded MISMATCH: animator={animG} vs detector={detG} — Animator NOT fed!"
                    : $"  ✓ Grounded in sync ({animG})");
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                float animVV = SafeGetFloat("VerticalVelocity");
                float rbVV = rb.linearVelocity.y;
                sb.AppendLine(Mathf.Abs(animVV - rbVV) > 0.05f
                    ? $"  ✗ VerticalVelocity MISMATCH: animator={animVV:F3} vs rb.vy={rbVV:F3} — Animator NOT fed!"
                    : $"  ✓ VerticalVelocity in sync ({animVV:F3})");
            }

            if (locomotion != null && locomotion.CurrentSpeed > 0.1f)
            {
                float heading = locomotion.HeadingDegrees;
                float rotY = transform.rotation.eulerAngles.y;
                float diff = Mathf.Abs(Mathf.DeltaAngle(rotY, heading));
                sb.AppendLine(diff > 5f
                    ? $"  ✗ Rotation MISMATCH: transform.Y={rotY:F1}° vs heading={heading:F1}° — rotation NOT applied!"
                    : $"  ✓ Rotation in sync with heading (Δ={diff:F1}°)");
            }

            if (Mathf.Approximately(animator.speed, 0f))
                sb.AppendLine("  ✗ Animator.speed = 0 — animazione congelata");
        }

        // ------------------------------------------------------------------

        private float SafeGetFloat(string name)
        {
            try { return animator != null ? animator.GetFloat(name) : 0f; } catch { return 0f; }
        }
        private bool SafeGetBool(string name)
        {
            try { return animator != null && animator.GetBool(name); } catch { return false; }
        }
    }
}