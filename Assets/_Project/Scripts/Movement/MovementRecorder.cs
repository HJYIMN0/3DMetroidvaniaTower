using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Movement
{
    /// <summary>
    /// Debug recorder for the player movement. Keeps the last few seconds of physics ticks in a ring buffer and writes
    /// ONE text file (config + summary + events + one row per tick) when it detects an anomaly, when you press the
    /// "mark bug" action, or when you stop Play Mode. Send that file and the movement bug can be analysed offline.
    /// For every tick it stores both what the modules WROTE (velocity) and what PHYSICS gave back at the start of the
    /// next tick, plus the collision contacts, so depenetration / collision kicks show up as a difference (dV).
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // runs before the coordinator: it sees the velocity AFTER the physics step
    public class MovementRecorder : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Coordinator da osservare. Se vuoto viene preso dal GameObject.")]
        [SerializeField] private PlayerMovementCoordinator coordinator;

        [Header("Recording")]
        [Tooltip("Attiva/disattiva la registrazione.")]
        [SerializeField] private bool recording = true;
        [Tooltip("Quanti secondi di storia tenere in memoria (e scrivere nel file). La dimensione si fissa all'avvio.")]
        [SerializeField, Min(1f)] private float bufferSeconds = 8f;
        [Tooltip("Secondi da registrare DOPO un evento prima di scrivere il file.")]
        [SerializeField, Min(0f)] private float secondsAfterTrigger = 1f;

        [Header("Auto detection")]
        [Tooltip("Scrive il file da solo quando rileva un'anomalia.")]
        [SerializeField] private bool autoDump = true;
        [Tooltip("Anomalia KickUp: la fisica ha aumentato la velocità verticale di più di questo (m/s) rispetto a quella scritta dai moduli.")]
        [SerializeField, Min(0f)] private float kickUpThreshold = 1f;
        [Tooltip("Anomalia ExtraRise: in un tick il player è salito di più di quanto previsto dalla velocità scritta (m).")]
        [SerializeField, Min(0f)] private float extraRiseThreshold = 0.03f;
        [Tooltip("Anomalia WallRunRise: velocità verticale verso l'alto (m/s) durante il wall run oltre questo valore.")]
        [SerializeField, Min(0f)] private float wallRunRiseThreshold = 0.5f;
        [Tooltip("Secondi minimi tra due file automatici.")]
        [SerializeField, Min(0f)] private float autoDumpCooldown = 4f;
        [Tooltip("Massimo di file automatici per sessione di Play.")]
        [SerializeField, Min(0)] private int maxAutoDumps = 8;

        [Header("Manual")]
        [Tooltip("Azione (es. F9) da premere appena vedi il bug: segna il momento e scrive il file. Opzionale.")]
        [SerializeField] private InputActionReference markBugAction;
        [Tooltip("Scrive un file anche quando fermi il Play Mode (così basta riprodurre il bug e fermare).")]
        [SerializeField] private bool dumpOnExit = true;
        [Tooltip("Scrive il percorso del file in Console e lo copia negli appunti.")]
        [SerializeField] private bool printToConsole = true;

        [Flags]
        private enum Flag : ushort
        {
            None = 0,
            KickUp = 1,
            ExtraRise = 2,
            WallRunRise = 4,
            GroundFlicker = 8,
            Mark = 16,
            ModuleChange = 32
        }

        private struct Contact
        {
            public Collider other;
            public Vector3 normal;
            public float impulse;
        }

        private struct Sample
        {
            public float time;
            public int tick;
            public Vector3 pos;          // rb.position when the tick started
            public Vector3 writtenVel;   // velocity written by the modules this tick
            public Vector3 physVel;      // velocity at the start of the NEXT tick (after physics)
            public Vector3 posAfter;     // rb.position at the start of the NEXT tick
            public string module;
            public bool grounded;
            public float airTime;
            public float groundDist;
            public Vector2 input;
            public bool jumpHeld;
            public bool onSteep;
            public bool steepBlocked;
            public bool hasWall;
            public Vector3 wallNormal;
            public string wallName;
            public string lastWallName;
            public bool headingLocked;
            public float coyote;
            public int contactCount;
            public Contact c0, c1, c2;
            public Flag flags;
        }

        private sealed class NameCache
        {
            private GameObject _obj;
            private string _name = "-";
            public string Get(GameObject go)
            {
                if (go == null) return "-";
                if (go != _obj) { _obj = go; _name = go.name; }
                return _name;
            }
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private Rigidbody _rb;
        private GroundDetector _ground;
        private WallDetector _wall;
        private JumpModule _jump;

        private Sample[] _ring;
        private int _head = -1;
        private int _count;
        private int _tick;
        private bool _hasOpen;
        private int _openIndex;

        // contacts collected between two ticks (physics callbacks run after FixedUpdate)
        private readonly Contact[] _contacts = new Contact[3];
        private int _contactCount;

        private readonly NameCache _wallNames = new NameCache();
        private readonly NameCache _lastWallNames = new NameCache();
        private readonly List<string> _events = new List<string>(256);

        private float _dumpAt = -1f;
        private string _dumpReason = "";
        private float _lastAutoDump = -999f;
        private int _autoDumps;

        private bool _prevGrounded = true;
        private float _lastGroundChange = -999f;
        private string _lastWallSeenInRun;
        private string _lastWallRunWall;
        private bool _groundedSinceWallRun = true;

        private int _kickCount, _riseCount, _runRiseCount, _restartCount, _flickerCount, _markCount;

        private void Reset() { coordinator = GetComponent<PlayerMovementCoordinator>(); }

        private void Awake()
        {
            if (coordinator == null) coordinator = GetComponent<PlayerMovementCoordinator>();
            _ground = GetComponent<GroundDetector>();
            _wall = GetComponent<WallDetector>();
            _jump = GetComponent<JumpModule>();

            int capacity = Mathf.CeilToInt(bufferSeconds / Mathf.Max(0.001f, Time.fixedDeltaTime)) + 16;
            _ring = new Sample[capacity];
        }

        private void OnEnable()
        {
            if (coordinator == null) { Debug.LogWarning("[MovementRecorder] Coordinator mancante."); return; }
            _rb = coordinator.Rigidbody;
            coordinator.OnTickCompleted += OnTick;
            coordinator.OnModuleChanged += OnModuleChanged;

            if (markBugAction != null && markBugAction.action != null)
            {
                markBugAction.action.Enable();
                markBugAction.action.performed += OnMarkPerformed;
            }
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.OnTickCompleted -= OnTick;
                coordinator.OnModuleChanged -= OnModuleChanged;
            }
            if (markBugAction != null && markBugAction.action != null)
                markBugAction.action.performed -= OnMarkPerformed;

            if (_ring != null && _count > 0)
            {
                if (_dumpAt >= 0f) WriteDump(_dumpReason + "+exit");
                else if (dumpOnExit) WriteDump("exit");
            }
        }

        private void OnMarkPerformed(InputAction.CallbackContext ctx) { MarkBug(); }

        [ContextMenu("Debug/Mark Bug")]
        public void MarkBug()
        {
            _markCount++;
            if (_hasOpen) _ring[_openIndex].flags |= Flag.Mark;
            AddEvent("MARK (bug segnalato dall'utente)");
            ScheduleDump("mark");
        }

        [ContextMenu("Debug/Save Log Now")]
        public void SaveLogNow()
        {
            WriteDump("manual");
        }

        // ------------------------------------------------------------------ physics callbacks
        private void OnCollisionEnter(Collision c) { CollectContacts(c); }
        private void OnCollisionStay(Collision c) { CollectContacts(c); }

        private void CollectContacts(Collision c)
        {
            if (!recording) return;
            float impulse = c.impulse.magnitude;
            for (int i = 0; i < c.contactCount && _contactCount < _contacts.Length; i++)
            {
                ContactPoint cp = c.GetContact(i);
                _contacts[_contactCount++] = new Contact { other = c.collider, normal = cp.normal, impulse = impulse };
            }
        }

        // ------------------------------------------------------------------ per tick
        // Runs BEFORE the coordinator (execution order -1000): closes the previous sample with the post-physics state.
        private void FixedUpdate()
        {
            if (!recording || _ring == null) return;

            if (_hasOpen)
            {
                CloseSample(ref _ring[_openIndex], Time.fixedDeltaTime);
                _hasOpen = false;
            }
            _contactCount = 0;

            if (_dumpAt >= 0f && Time.time >= _dumpAt)
            {
                string reason = _dumpReason;
                _dumpAt = -1f;
                _dumpReason = "";
                WriteDump(reason);
            }
        }

        // Called by the coordinator at the end of the tick: opens a new sample with the state the modules produced.
        private void OnTick(MovementContext ctx)
        {
            if (!recording || _ring == null || _rb == null) return;

            _head = (_head + 1) % _ring.Length;
            if (_count < _ring.Length) _count++;
            _openIndex = _head;
            _hasOpen = true;

            ref Sample s = ref _ring[_head];
            s = default;
            s.time = Time.time;
            s.tick = _tick++;
            s.pos = _rb.position;
            s.writtenVel = _rb.linearVelocity;

            MovementModule active = coordinator.ActiveModule;
            s.module = active != null ? active.GetType().Name : "-";
            s.grounded = ctx.IsGrounded;
            s.airTime = ctx.AirTime;
            s.groundDist = _ground != null && ctx.IsGrounded ? _ground.GroundDistance : -1f;
            s.input = ctx.MoveInputRaw;
            s.jumpHeld = ctx.JumpHeld;
            s.onSteep = ctx.OnSteepSurface;
            s.steepBlocked = ctx.SteepBlocked;
            s.hasWall = _wall != null && _wall.HasWall;
            s.wallNormal = s.hasWall ? _wall.WallNormal : Vector3.zero;
            s.wallName = s.hasWall ? _wallNames.Get(_wall.WallObject) : "-";
            s.lastWallName = _lastWallNames.Get(ctx.LastWall);
            s.headingLocked = ctx.PeekHeadingLocked(_rb.linearVelocity.y);
            s.coyote = _jump != null ? _jump.CoyoteRemaining : 0f;

            if (ctx.IsGrounded) _groundedSinceWallRun = true;
            if (s.module == "WallRunModule" && s.hasWall) _lastWallSeenInRun = s.wallName;

            if (ctx.IsGrounded != _prevGrounded)
            {
                if (Time.time - _lastGroundChange < 0.1f)
                {
                    s.flags |= Flag.GroundFlicker;
                    _flickerCount++;
                }
                _lastGroundChange = Time.time;
                _prevGrounded = ctx.IsGrounded;
            }
        }

        private void CloseSample(ref Sample s, float dt)
        {
            s.physVel = _rb.linearVelocity;
            s.posAfter = _rb.position;
            s.contactCount = _contactCount;
            if (_contactCount > 0) s.c0 = _contacts[0];
            if (_contactCount > 1) s.c1 = _contacts[1];
            if (_contactCount > 2) s.c2 = _contacts[2];

            if (s.tick < 5) return;   // ignore the first ticks while the body settles

            bool ledgeMoving = s.module == "LedgeGrabModule";   // it moves the body with MovePosition
            Vector3 dv = s.physVel - s.writtenVel;
            float extraRise = (s.posAfter.y - s.pos.y) - s.writtenVel.y * dt;

            Flag trigger = Flag.None;
            if (!ledgeMoving && dv.y > kickUpThreshold) { s.flags |= Flag.KickUp; _kickCount++; trigger |= Flag.KickUp; }
            if (!ledgeMoving && extraRise > extraRiseThreshold) { s.flags |= Flag.ExtraRise; _riseCount++; trigger |= Flag.ExtraRise; }
            if (s.module == "WallRunModule" && s.writtenVel.y > wallRunRiseThreshold) { s.flags |= Flag.WallRunRise; _runRiseCount++; trigger |= Flag.WallRunRise; }

            if (trigger != Flag.None)
            {
                AddEvent("ANOMALY " + trigger + " tick=" + s.tick + " module=" + s.module + " dV=" + V(dv) + " extraRise=" + F(extraRise));
                RequestAutoDump(trigger.ToString());
            }
        }

        // ------------------------------------------------------------------ events
        private void OnModuleChanged(MovementModule prev, MovementModule next)
        {
            if (!recording || _rb == null) return;

            MovementContext ctx = coordinator.Context;
            string pn = prev != null ? prev.GetType().Name : "-";
            string nn = next != null ? next.GetType().Name : "-";
            string wallName = _wall != null ? _wallNames.Get(_wall.WallObject) : "-";

            AddEvent(pn + " -> " + nn + " | wall=" + wallName + " last=" + _lastWallNames.Get(ctx.LastWall) +
                     " gnd=" + ctx.IsGrounded + " air=" + F(ctx.AirTime) + " v=" + V(_rb.linearVelocity) +
                     (next != null && !string.IsNullOrEmpty(next.DebugState) ? " | " + next.DebugState : ""));

            if (_hasOpen) _ring[_openIndex].flags |= Flag.ModuleChange;

            if (prev is WallRunModule)
            {
                _lastWallRunWall = _lastWallSeenInRun;
                _groundedSinceWallRun = false;
            }

            bool nextIsWallModule = next is WallRunModule || next is WallSlideModule;
            if (nextIsWallModule && _lastWallRunWall != null && wallName != _lastWallRunWall)
                _lastWallRunWall = null;   // attached to a different wall: the old one is free again

            if (next is WallRunModule && _lastWallRunWall != null && !_groundedSinceWallRun && wallName == _lastWallRunWall)
            {
                _restartCount++;
                AddEvent("ANOMALY SameWallRestart: wall run restarted on '" + wallName + "' without touching ground or another wall");
                RequestAutoDump("SameWallRestart");
            }
        }

        private void AddEvent(string text)
        {
            if (_events.Count >= 300) _events.RemoveRange(0, 100);
            _events.Add(F(Time.time) + "s  " + text);
        }

        private void RequestAutoDump(string reason)
        {
            if (!autoDump) return;
            if (_autoDumps >= maxAutoDumps) return;
            if (Time.time - _lastAutoDump < autoDumpCooldown) return;
            _lastAutoDump = Time.time;
            _autoDumps++;
            ScheduleDump("auto_" + reason);
        }

        private void ScheduleDump(string reason)
        {
            if (_dumpAt >= 0f) { _dumpReason += "+" + reason; return; }
            _dumpAt = Time.time + secondsAfterTrigger;
            _dumpReason = reason;
        }

        // ------------------------------------------------------------------ output
        private static string F(float v) { return v.ToString("F2", Inv); }
        private static string F3(float v) { return v.ToString("F3", Inv); }
        private static string V(Vector3 v) { return F(v.x) + "," + F(v.y) + "," + F(v.z); }

        private string BuildPath(string reason)
        {
            string dir = Application.isEditor
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "MovementLogs")
                : Path.Combine(Application.persistentDataPath, "MovementLogs");
            Directory.CreateDirectory(dir);

            StringBuilder clean = new StringBuilder();
            foreach (char ch in reason) clean.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '+' ? ch : '_');
            string name = "movement_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv) + "_" + clean + ".txt";
            return Path.Combine(dir, name);
        }

        private void WriteDump(string reason)
        {
            if (_ring == null || _count == 0) return;
            try
            {
                string path = BuildPath(reason);
                StringBuilder sb = new StringBuilder(96 * 1024);

                sb.AppendLine("MOVEMENT LOG");
                sb.AppendLine("reason=" + reason + "  time=" + F(Time.time) + "s  scene=" + SceneManager.GetActiveScene().name +
                              "  unity=" + Application.unityVersion);
                sb.AppendLine("fixedDt=" + F3(Time.fixedDeltaTime) + "  timeScale=" + F(Time.timeScale) +
                              "  targetFps=" + Application.targetFrameRate + "  vsync=" + QualitySettings.vSyncCount);
                sb.AppendLine();

                AppendSummary(sb);
                AppendEvents(sb);
                AppendConfig(sb);
                AppendSamples(sb);

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                if (printToConsole)
                {
                    Debug.Log("[MovementRecorder] Log salvato (" + reason + "): " + path + "  (percorso copiato negli appunti)");
                    GUIUtility.systemCopyBuffer = path;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[MovementRecorder] Errore nel salvataggio: " + e.Message);
            }
        }

        private void AppendSummary(StringBuilder sb)
        {
            sb.AppendLine("== SUMMARY ==");
            sb.AppendLine("anomalies: KickUp=" + _kickCount + " ExtraRise=" + _riseCount + " WallRunRise=" + _runRiseCount +
                          " SameWallRestart=" + _restartCount + " | groundFlicker=" + _flickerCount + " | userMarks=" + _markCount);
            sb.AppendLine("KickUp      = physics ADDED upward velocity vs what modules wrote (collision/depenetration kick)");
            sb.AppendLine("ExtraRise   = the body rose more than the written velocity explains (depenetration pushing up)");
            sb.AppendLine("WallRunRise = vertical velocity upward during WallRunModule above the threshold");
            sb.AppendLine("SameWallRestart = wall run restarted on the same wall without touching ground / another wall");
            sb.AppendLine("GroundFlicker = grounded toggled twice within 0.1 s");
            sb.AppendLine();
        }

        private void AppendEvents(StringBuilder sb)
        {
            sb.AppendLine("== EVENTS (module transitions, anomalies, marks) ==");
            for (int i = 0; i < _events.Count; i++) sb.AppendLine(_events[i]);
            sb.AppendLine();
        }

        private void AppendConfig(StringBuilder sb)
        {
            sb.AppendLine("== CONFIG ==");
            if (_rb != null)
            {
                sb.AppendLine("Rigidbody: mass=" + F(_rb.mass) + " useGravity=" + _rb.useGravity + " kinematic=" + _rb.isKinematic +
                              " interpolation=" + _rb.interpolation + " collision=" + _rb.collisionDetectionMode +
                              " constraints=" + _rb.constraints + " linearDamping=" + F(_rb.linearDamping) +
                              " maxDepenVel=" + F(_rb.maxDepenetrationVelocity));
            }

            CapsuleCollider cap = GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                var mat = cap.sharedMaterial;
                string matInfo = mat != null
                    ? mat.name + " dyn=" + F(mat.dynamicFriction) + " sta=" + F(mat.staticFriction) + " fricCombine=" + mat.frictionCombine
                    : "none";
                sb.AppendLine("Capsule: center=" + V(cap.center) + " radius=" + F(cap.radius) + " height=" + F(cap.height) +
                              " layer=" + LayerMask.LayerToName(gameObject.layer) + " material=" + matInfo);
            }

            sb.AppendLine("Physics: contactOffset=" + F3(Physics.defaultContactOffset) + " defaultMaxDepenVel=" + F(Physics.defaultMaxDepenetrationVelocity) +
                          " solverIters=" + Physics.defaultSolverIterations + " gravity=" + V(Physics.gravity));

            AppendJson(sb, coordinator);
            AppendJson(sb, _ground);
            AppendJson(sb, _wall);
            AppendJson(sb, GetComponent<LedgeDetector>());
            if (coordinator != null)
            {
                MovementModule[] modules = coordinator.Modules;
                for (int i = 0; i < modules.Length; i++) AppendJson(sb, modules[i]);
            }
            sb.AppendLine();
        }

        private static void AppendJson(StringBuilder sb, Component c)
        {
            if (c == null) return;
            try { sb.AppendLine(c.GetType().Name + (c is Behaviour b && !b.enabled ? " [DISABLED]" : "") + ": " + JsonUtility.ToJson(c)); }
            catch (Exception) { sb.AppendLine(c.GetType().Name + ": (not serializable)"); }
        }

        private void AppendSamples(StringBuilder sb)
        {
            sb.AppendLine("== SAMPLES (one row per physics tick, oldest first) ==");
            sb.AppendLine("columns: t ; module ; gnd(1=grounded) ; air(s) ; gDist ; input ; vW=velocity WRITTEN by modules ; vP=velocity after PHYSICS ; " +
                          "dV=vP-vW ; pos ; dYextra=rise not explained by vW ; wall(name,normal) ; lastWall ; lock=heading locked ; jh=jump held ; steep=standing on a surface steeper than Max Slope Angle ; blk=steep block removed horizontal velocity ; coyote ; contacts(name:normal:impulse) ; flags");

            int start = (_head - _count + 1 + _ring.Length) % _ring.Length;
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < _count; i++)
            {
                ref Sample s = ref _ring[(start + i) % _ring.Length];
                bool closed = s.contactCount >= 0 && (s.physVel != Vector3.zero || s.posAfter != Vector3.zero);
                Vector3 dv = closed ? s.physVel - s.writtenVel : Vector3.zero;
                float dyExtra = closed ? (s.posAfter.y - s.pos.y) - s.writtenVel.y * dt : 0f;

                sb.Append(F3(s.time)).Append(';').Append(s.module).Append(';').Append(s.grounded ? '1' : '0').Append(';')
                  .Append(F(s.airTime)).Append(';').Append(s.groundDist < 0f ? "-" : F(s.groundDist)).Append(';')
                  .Append(F(s.input.x)).Append(',').Append(F(s.input.y)).Append(';')
                  .Append(V(s.writtenVel)).Append(';').Append(closed ? V(s.physVel) : "-").Append(';').Append(closed ? V(dv) : "-").Append(';')
                  .Append(V(s.pos)).Append(';').Append(closed ? F3(dyExtra) : "-").Append(';')
                  .Append(s.hasWall ? s.wallName + "," + V(s.wallNormal) : "-").Append(';').Append(s.lastWallName).Append(';')
                  .Append(s.headingLocked ? '1' : '0').Append(';').Append(s.jumpHeld ? '1' : '0').Append(';').Append(s.onSteep ? '1' : '0').Append(';').Append(s.steepBlocked ? '1' : '0').Append(';')
                  .Append(F(s.coyote)).Append(';');

                AppendContact(sb, s.contactCount > 0, s.c0);
                AppendContact(sb, s.contactCount > 1, s.c1);
                AppendContact(sb, s.contactCount > 2, s.c2);
                if (s.contactCount == 0) sb.Append('-');

                sb.Append(';').Append(s.flags == Flag.None ? "-" : s.flags.ToString().Replace(", ", "+")).AppendLine();
            }
        }

        private static void AppendContact(StringBuilder sb, bool present, Contact c)
        {
            if (!present) return;
            sb.Append(c.other != null ? c.other.name : "?").Append(':').Append(V(c.normal)).Append(':').Append(F(c.impulse)).Append('|');
        }
    }
}