using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Base class of every movement module.
    /// Exclusive modules (Dodge, LedgeGrab, WallRun, WallSlide): the first one whose CanExecute() is true
    /// (highest Priority first) owns the whole tick. Passive modules (Locomotion, Jump) run every tick
    /// when no exclusive module claimed it. Disable the component to switch a module off.
    /// </summary>
    public abstract class MovementModule : MonoBehaviour
    {
        [Header("Module")]
        [Tooltip("Maggiore = valutato prima. Si può cambiare anche a runtime.")]
        [SerializeField] private int priority;

        protected Rigidbody rb;

        public int Priority => priority;

        /// <summary>Exclusive modules take over the tick; passive ones run when nobody does.</summary>
        public abstract bool Exclusive { get; }

        /// <summary>True between Enter() and Exit() (only meaningful for exclusive modules).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Extra text shown by the debug HUD and by Debug/Print State.</summary>
        public virtual string DebugState => string.Empty;

        protected abstract int DefaultPriority { get; }

        protected virtual void Reset() { priority = DefaultPriority; }
        protected virtual void Awake() { rb = GetComponent<Rigidbody>(); }

        /// <summary>Exclusive: entry condition when inactive, sustain condition when active. Passive: normally true.</summary>
        public abstract bool CanExecute(MovementContext ctx);

        /// <summary>Applies the module to the Rigidbody for this physics tick.</summary>
        public abstract void Tick(MovementContext ctx, float dt);

        public void Enter(MovementContext ctx) { IsActive = true; OnEnter(ctx); }
        public void Exit(MovementContext ctx) { IsActive = false; OnExit(ctx); }

        protected virtual void OnEnter(MovementContext ctx) { }
        protected virtual void OnExit(MovementContext ctx) { }

        protected static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        [ContextMenu("Debug/Print State")]
        public void PrintState()
        {
            Debug.Log($"[{GetType().Name}] enabled={enabled} priority={priority} exclusive={Exclusive} active={IsActive} {DebugState}");
        }

        protected virtual void OnDrawGizmosSelected() { }
    }
}