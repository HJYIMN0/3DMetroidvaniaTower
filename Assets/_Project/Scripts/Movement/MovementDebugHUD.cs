using System.Text;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// On-screen debug HUD (IMGUI, no Canvas needed): active module, priorities, speed, vertical velocity,
    /// grounded state, and the measured result of the last jump (peak height, air time, distance).
    /// Toggle with the 'show' checkbox.
    /// </summary>
    public class MovementDebugHUD : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Coordinator da osservare. Se vuoto viene preso dal GameObject.")]
        [SerializeField] private PlayerMovementCoordinator coordinator;

        [Header("Display")]
        [Tooltip("Mostra/nasconde l'HUD.")]
        [SerializeField] private bool show = true;
        [Tooltip("Posizione in pixel dall'angolo in alto a sinistra.")]
        [SerializeField] private Vector2 screenPosition = new Vector2(10f, 10f);
        [SerializeField, Range(10, 32)] private int fontSize = 14;

        [Header("Cursor")]
        [Tooltip("Blocca il cursore all'avvio per guardarti intorno col mouse. Esc lo sblocca nell'Editor.")]
        [SerializeField] private bool lockCursor = true;

        private JumpModule _jump;
        private GUIStyle _style;
        private readonly StringBuilder _sb = new StringBuilder(512);

        private bool _wasGrounded = true;
        private bool _inAir;
        private Vector3 _takeoff;
        private float _peak, _airTimer;
        private float _lastPeak, _lastAirTime, _lastDistance;

        private void Reset() { coordinator = GetComponent<PlayerMovementCoordinator>(); }

        private void Awake()
        {
            if (coordinator == null) coordinator = GetComponent<PlayerMovementCoordinator>();
            _jump = GetComponent<JumpModule>();
        }

        private void Start()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void FixedUpdate()
        {
            if (coordinator == null) return;

            bool grounded = coordinator.IsGrounded;
            Vector3 pos = coordinator.Rigidbody.position;

            if (_wasGrounded && !grounded)
            {
                _inAir = true;
                _takeoff = pos;
                _peak = 0f;
                _airTimer = 0f;
            }

            if (_inAir)
            {
                _airTimer += Time.fixedDeltaTime;
                _peak = Mathf.Max(_peak, pos.y - _takeoff.y);

                if (grounded)
                {
                    _lastPeak = _peak;
                    _lastAirTime = _airTimer;
                    Vector3 d = pos - _takeoff;
                    d.y = 0f;
                    _lastDistance = d.magnitude;
                    _inAir = false;
                }
            }

            _wasGrounded = grounded;
        }

        private void OnGUI()
        {
            if (!show || coordinator == null) return;

            if (_style == null) _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = fontSize;
            _style.normal.textColor = Color.white;

            Vector3 v = coordinator.Rigidbody.linearVelocity;
            float speedXZ = new Vector2(v.x, v.z).magnitude;
            MovementModule active = coordinator.ActiveModule;

            _sb.Clear();
            if (active != null)
            {
                _sb.Append("Active: ").Append(active.GetType().Name).Append(" (prio ").Append(active.Priority).Append(")\n");
                if (!string.IsNullOrEmpty(active.DebugState)) _sb.Append("  ").Append(active.DebugState).Append('\n');
            }
            else
            {
                _sb.Append("Active: none (Locomotion + Jump)\n");
            }

            _sb.Append("Grounded: ").Append(coordinator.IsGrounded)
               .Append("   AirTime: ").Append(coordinator.Context.AirTime.ToString("F2")).Append(" s\n");
            _sb.Append("Speed XZ: ").Append(speedXZ.ToString("F2")).Append(" m/s   Vy: ").Append(v.y.ToString("F2")).Append(" m/s\n");
            _sb.Append("Input: ").Append(coordinator.Context.MoveInputRaw.ToString("F2"))
               .Append("   JumpHeld: ").Append(coordinator.Context.JumpHeld).Append('\n');
            _sb.Append("Last jump: peak ").Append(_lastPeak.ToString("F2")).Append(" m | air ")
               .Append(_lastAirTime.ToString("F2")).Append(" s | dist ").Append(_lastDistance.ToString("F2")).Append(" m\n");

            if (_jump != null)
            {
                _sb.Append("Coyote: left ").Append(_jump.CoyoteRemaining.ToString("F3")).Append(" s | last coyote jump delay ")
                   .Append(_jump.LastCoyoteDelay >= 0f ? _jump.LastCoyoteDelay.ToString("F3") + " s" : "-").Append('\n');
            }

            GameObject lastWall = coordinator.Context.LastWall;
            _sb.Append("Last wall jumped: ").Append(lastWall != null ? lastWall.name : "-").Append(" (blocked until ground / another wall jump)\n");

            _sb.Append("Modules:\n");
            MovementModule[] modules = coordinator.Modules;
            for (int i = 0; i < modules.Length; i++)
            {
                MovementModule m = modules[i];
                if (m == null) continue;
                _sb.Append("  [").Append(m.enabled ? "x" : " ").Append("] ")
                   .Append(m.Priority).Append("  ").Append(m.GetType().Name)
                   .Append(m.Exclusive ? "" : "  (passive)").Append('\n');
            }

            string text = _sb.ToString();
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            float height = lines * (fontSize + 6f) + 10f;

            Rect rect = new Rect(screenPosition.x, screenPosition.y, 520f, height);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), text, _style);
        }
    }
}