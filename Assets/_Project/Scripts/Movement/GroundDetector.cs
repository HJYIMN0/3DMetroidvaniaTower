using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Ground check using a SphereCast from the bottom of the CapsuleCollider.
    /// Independent from where the pivot is. Assumes an upright capsule (axis Y).
    /// The coordinator calls Refresh() once per physics tick.
    /// </summary>
    public class GroundDetector : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Capsule del player. Se vuoto viene preso con GetComponent.")]
        [SerializeField] private CapsuleCollider capsule;

        [Header("Detection")]
        [Tooltip("Layer considerati terreno. Escludi il layer del Player.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Distanza (m) sotto i piedi entro cui si è 'a terra'. Più alta = più tollerante (scale, discese), ma salto/coyote più permissivi.")]
        [SerializeField, Min(0f)] private float checkDistance = 0.12f;
        [Tooltip("Margine (m) sopra i piedi da cui parte lo spherecast, per non partire dentro al terreno.")]
        [SerializeField, Min(0.001f)] private float skin = 0.05f;
        [Tooltip("Quanto si restringe il raggio della sfera rispetto alla capsule, per non toccare le pareti laterali.")]
        [SerializeField, Min(0f)] private float radiusShrink = 0.03f;
        [Tooltip("Pendenza massima (gradi) ancora considerata terreno.")]
        [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 55f;

        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public Vector3 GroundPoint { get; private set; }
        /// <summary>Distance between the feet and the ground (valid only while grounded).</summary>
        public float GroundDistance { get; private set; }

        private void Reset() { capsule = GetComponent<CapsuleCollider>(); }
        private void Awake() { if (capsule == null) capsule = GetComponent<CapsuleCollider>(); }

        /// <summary>Samples the ground. Call once per FixedUpdate.</summary>
        public void Refresh()
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
            GroundDistance = 0f;

            if (!GetCastGeometry(out Vector3 origin, out float radius)) return;

            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, skin + checkDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Angle(hit.normal, Vector3.up) <= maxSlopeAngle)
                {
                    IsGrounded = true;
                    GroundNormal = hit.normal;
                    GroundPoint = hit.point;
                    GroundDistance = Mathf.Max(0f, hit.distance - skin);
                }
            }
        }

        private bool GetCastGeometry(out Vector3 origin, out float radius)
        {
            origin = transform.position;
            radius = 0.2f;
            if (capsule == null) return false;

            Vector3 scale = capsule.transform.lossyScale;
            float radial = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            radius = Mathf.Max(0.01f, capsule.radius * radial - radiusShrink);

            Vector3 centerWorld = capsule.transform.TransformPoint(capsule.center);
            float feetY = centerWorld.y - capsule.height * 0.5f * Mathf.Abs(scale.y);
            origin = new Vector3(centerWorld.x, feetY + skin + radius, centerWorld.z);
            return true;
        }

        [ContextMenu("Debug/Print State")]
        private void PrintState()
        {
            Debug.Log($"[GroundDetector] grounded={IsGrounded} distance={GroundDistance:F3} normal={GroundNormal} slope={Vector3.Angle(GroundNormal, Vector3.up):F1}");
        }

        private void OnDrawGizmosSelected()
        {
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
            if (!GetCastGeometry(out Vector3 origin, out float radius)) return;
            Vector3 end = origin + Vector3.down * (skin + checkDistance);
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.DrawWireSphere(end, radius);
            Gizmos.DrawLine(origin, end);
        }
    }
}