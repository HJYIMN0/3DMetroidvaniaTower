using UnityEngine;

namespace Game.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract class AbstractUnitMover : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] protected Rigidbody rb;

        [Header("Locomotion")]
        [SerializeField] protected float walkSpeed = 3f;
        [SerializeField] protected float runSpeed = 6f;
        [SerializeField] protected float rotationSpeed = 720f;
        [SerializeField] protected float acceleration = 40f;

        [Header("Jump / Gravity")]
        [SerializeField] protected float jumpForce = 8f;
        [SerializeField] protected float gravityScale = 3f;
        [SerializeField] protected float groundCheckDistance = 0.2f;
        [SerializeField] protected LayerMask groundMask;

        [Header("Debug")]
        [SerializeField] protected bool drawGizmos = true;

        protected Vector3 _moveInputWorld;
        protected bool _isGrounded;
        protected float _verticalVelocity;

        public bool IsGrounded => _isGrounded;
        public Rigidbody Rigidbody => rb;

        protected virtual void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.useGravity = false; // we apply custom gravity
        }

        protected virtual void FixedUpdate()
        {
            GroundCheck();
            ApplyCustomGravity();
            ApplyMovement(Time.fixedDeltaTime);
        }

        protected virtual void GroundCheck()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            _isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.1f, groundMask, QueryTriggerInteraction.Ignore);
        }

        protected virtual void ApplyCustomGravity()
        {
            _verticalVelocity += Physics.gravity.y * gravityScale * Time.fixedDeltaTime;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, _verticalVelocity, rb.linearVelocity.z);
        }

        protected abstract void ApplyMovement(float dt);

        protected virtual void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + Vector3.down * (groundCheckDistance + 0.1f));
        }
    }
}