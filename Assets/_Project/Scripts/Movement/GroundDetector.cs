using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Ground check using a SphereCast from the bottom of the CapsuleCollider (independent from the pivot position),
    /// plus steep-surface detection: surfaces steeper than Max Slope Angle are not ground, cannot be walked on and
    /// cannot be climbed (the coordinator removes the horizontal velocity that pushes into them).
    /// Assumes an upright capsule (axis Y). The coordinator calls Refresh() once per physics tick.
    /// </summary>
    public class GroundDetector : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Capsule del player. Se vuoto viene preso con GetComponent.")]
        [SerializeField] private CapsuleCollider capsule;

        [Header("Detection")]
        [Tooltip("Layer considerati terreno (escludi il layer del Player).")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Distanza (m) sotto i piedi entro cui si è 'a terra'. Più alta = più tollerante (scale, discese), ma salto/coyote più permissivi.")]
        [SerializeField, Min(0f)] private float checkDistance = 0.12f;
        [Tooltip("Margine (m) sopra i piedi da cui parte lo spherecast, per non partire dentro al terreno.")]
        [SerializeField, Min(0.001f)] private float skin = 0.05f;
        [Tooltip("Quanto si restringe il raggio della sfera rispetto alla capsule, per non toccare le pareti laterali.")]
        [SerializeField, Min(0f)] private float radiusShrink = 0.03f;

        [Header("Slope limit")]
        [Tooltip("Pendenza massima (gradi) percorribile. Oltre questo angolo la superficie NON è terreno: non puoi saltare, non puoi camminarci sopra " +
                 "(scivoli) e non puoi salirci (vedi Block Steep Surfaces).")]
        [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 55f;

        [Header("Steep surfaces")]
        [Tooltip("true = la velocità orizzontale verso una superficie più ripida di Max Slope Angle viene azzerata (puoi solo muoverti lungo di essa o allontanartene), " +
                 "e sopra di essa non hai controllo orizzontale. false = comportamento della fisica (con attrito zero la capsule sale sulle pendenze).")]
        [SerializeField] private bool blockSteepSurfaces = true;
        [Tooltip("Di quanto (m) si alza il fondo della capsule usata per il controllo in avanti, per non toccare il pavimento " +
                 "(un pavimento e una rampa dello stesso collider verrebbero altrimenti ignorati).")]
        [SerializeField, Min(0f)] private float steepCastLift = 0.1f;
        [Tooltip("Distanza extra (m) oltre al movimento del tick entro cui si rileva una superficie ripida.")]
        [SerializeField, Min(0f)] private float steepCastSkin = 0.05f;
        [Tooltip("Quanto si restringe il raggio della capsule di controllo (m). Serve a rilevare la superficie anche quando la capsule la sta già toccando.")]
        [SerializeField, Min(0f)] private float steepCastRadiusShrink = 0.02f;

        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public Vector3 GroundPoint { get; private set; }
        /// <summary>Distance between the feet and the ground (valid only while grounded).</summary>
        public float GroundDistance { get; private set; }

        /// <summary>True if the surface right under the feet is steeper than MaxSlopeAngle (so it is NOT ground).</summary>
        public bool IsOnSteepSurface { get; private set; }
        public Vector3 SteepNormal { get; private set; } = Vector3.up;

        public float MaxSlopeAngle => maxSlopeAngle;
        public bool BlocksSteepSurfaces => blockSteepSurfaces;

        private void Reset() { capsule = GetComponent<CapsuleCollider>(); }
        private void Awake() { if (capsule == null) capsule = GetComponent<CapsuleCollider>(); }

        /// <summary>Samples the ground. Call once per FixedUpdate.</summary>
        public void Refresh()
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
            GroundDistance = 0f;
            IsOnSteepSurface = false;
            SteepNormal = Vector3.up;

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
                else
                {
                    IsOnSteepSurface = true;
                    SteepNormal = hit.normal;
                }
            }
        }

        /// <summary>
        /// Looks ahead along a horizontal direction for a surface steeper than MaxSlopeAngle.
        /// 'distance' is the horizontal distance the player will cover this tick (speed * dt).
        /// </summary>
        public bool TryGetSteepSurface(Vector3 horizontalDir, float distance, out Vector3 normal)
        {
            normal = Vector3.up;
            if (!blockSteepSurfaces || capsule == null) return false;
            if (!GetSteepCastGeometry(out Vector3 p1, out Vector3 p2, out float radius)) return false;

            if (!Physics.CapsuleCast(p1, p2, radius, horizontalDir, out RaycastHit hit, distance + steepCastSkin, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            if (Vector3.Angle(hit.normal, Vector3.up) <= maxSlopeAngle) return false;   // walkable: let the player go

            normal = hit.normal;
            return true;
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

        // Capsule used for the forward look-ahead: same body, raised by steepCastLift and slightly thinner.
        private bool GetSteepCastGeometry(out Vector3 p1, out Vector3 p2, out float radius)
        {
            p1 = p2 = transform.position;
            radius = 0.2f;
            if (capsule == null) return false;

            Vector3 scale = capsule.transform.lossyScale;
            float radial = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            radius = Mathf.Max(0.01f, capsule.radius * radial - steepCastRadiusShrink);

            Vector3 centerWorld = capsule.transform.TransformPoint(capsule.center);
            float halfHeight = capsule.height * 0.5f * Mathf.Abs(scale.y);
            float bottomY = centerWorld.y - halfHeight + steepCastLift + radius;
            float topY = Mathf.Max(bottomY, centerWorld.y + halfHeight - radius);

            p1 = new Vector3(centerWorld.x, bottomY, centerWorld.z);
            p2 = new Vector3(centerWorld.x, topY, centerWorld.z);
            return true;
        }

        [ContextMenu("Debug/Print State")]
        private void PrintState()
        {
            Debug.Log($"[GroundDetector] grounded={IsGrounded} distance={GroundDistance:F3} normal={GroundNormal} slope={Vector3.Angle(GroundNormal, Vector3.up):F1} " +
                      $"| onSteep={IsOnSteepSurface} steepSlope={Vector3.Angle(SteepNormal, Vector3.up):F1} maxSlope={maxSlopeAngle:F0}");
        }

        private void OnDrawGizmosSelected()
        {
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
            if (GetCastGeometry(out Vector3 origin, out float radius))
            {
                Vector3 end = origin + Vector3.down * (skin + checkDistance);
                Gizmos.color = IsGrounded ? Color.green : (IsOnSteepSurface ? new Color(1f, 0.5f, 0f) : Color.red);
                Gizmos.DrawWireSphere(origin, radius);
                Gizmos.DrawWireSphere(end, radius);
                Gizmos.DrawLine(origin, end);
            }

            if (blockSteepSurfaces && GetSteepCastGeometry(out Vector3 p1, out Vector3 p2, out float r))
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);   // forward look-ahead capsule
                Gizmos.DrawWireSphere(p1, r);
                Gizmos.DrawWireSphere(p2, r);
            }
        }
    }
}