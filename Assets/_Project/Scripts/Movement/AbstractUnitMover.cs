using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Minimal base for movers. Owns the Rigidbody setup (the part that was painful to debug)
    /// and exposes the public API other systems depend on: IsGrounded, Rigidbody, MoveInputWorld.
    /// No gameplay logic lives here.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class AbstractUnitMover : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Rigidbody da controllare. Se vuoto viene preso con GetComponent.")]
        [SerializeField] protected Rigidbody rb;

        [Header("Rigidbody setup")]
        [Tooltip("Blocca la rotazione Y del Rigidbody. Metti false quando arriva la classe di rotazione, se ruota via Rigidbody.")]
        [SerializeField] private bool freezeYaw = true;

        public Rigidbody Rigidbody => rb;

        /// <summary>True when standing on valid ground.</summary>
        public abstract bool IsGrounded { get; }

        /// <summary>World-space move direction this tick (XZ). Zero when an exclusive module owns the movement.</summary>
        public abstract Vector3 MoveInputWorld { get; }

        protected virtual void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();

            rb.useGravity = false;      // gravity is applied by our own code
            rb.sleepThreshold = 0f;     // never sleep: velocity writes on a sleeping body are ignored
            rb.linearDamping = 0f;      // damping would silently fight our velocity writes

            RigidbodyConstraints c = rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            c = freezeYaw ? (c | RigidbodyConstraints.FreezeRotationY) : (c & ~RigidbodyConstraints.FreezeRotationY);
            rb.constraints = c;

            rb.WakeUp();
        }
    }
}