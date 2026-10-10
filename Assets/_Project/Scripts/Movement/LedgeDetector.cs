using UnityEngine;

namespace Game.Movement
{
    /// <summary>Data describing a grabbable ledge.</summary>
    public struct LedgeInfo
    {
        /// <summary>Point on the top surface of the ledge, just behind the edge.</summary>
        public Vector3 topPoint;
        /// <summary>Point on the wall face hit at head height.</summary>
        public Vector3 wallPoint;
        /// <summary>Horizontal normal of the wall (points away from the wall).</summary>
        public Vector3 wallNormal;
    }

    /// <summary>
    /// Pure raycast ledge detector: casts forward at head height, then down from above the hit point.
    /// The facing direction is passed in by the coordinator, so it does not depend on the rotation system.
    /// </summary>
    public class LedgeDetector : MonoBehaviour
    {
        [Header("Masks")]
        [Tooltip("Layer dei bordi afferrabili (es. Ledge).")]
        [SerializeField] private LayerMask ledgeMask;
        [Tooltip("Layer che bloccano lo spazio sopra il bordo (soffitti, muri, bordi). 0 = salta il controllo.")]
        [SerializeField] private LayerMask obstructionMask;

        [Header("Probe")]
        [Tooltip("Altezza (m) dal pivot da cui parte il raggio in avanti.")]
        [SerializeField, Min(0f)] private float headHeight = 1.7f;
        [Tooltip("Lunghezza (m) del raggio in avanti.")]
        [SerializeField, Min(0.05f)] private float forwardCastDistance = 0.7f;
        [Tooltip("Quanto sopra il punto colpito parte il raggio verso il basso (m).")]
        [SerializeField, Min(0f)] private float probeHeightAbove = 0.5f;
        [Tooltip("Quanto dentro la parete (m) si sposta il raggio verso il basso.")]
        [SerializeField, Min(0f)] private float probeInset = 0.05f;
        [Tooltip("Lunghezza (m) del raggio verso il basso.")]
        [SerializeField, Min(0.05f)] private float downCastDistance = 1.2f;
        [Tooltip("Altezza minima (m) del bordo rispetto al pivot.")]
        [SerializeField] private float minLedgeHeight = 0.3f;
        [Tooltip("Scarta pareti con |normal.y| sopra questo valore.")]
        [SerializeField, Range(0f, 1f)] private float maxWallNormalY = 0.2f;
        [Tooltip("Scarta superfici superiori con normal.y sotto questo valore (troppo inclinate).")]
        [SerializeField, Range(0f, 1f)] private float minTopNormalY = 0.7f;

        [Header("Clearance")]
        [Tooltip("Altezza (m) dello spazio libero richiesto sopra il bordo.")]
        [SerializeField, Min(0.1f)] private float clearanceHeight = 1.8f;
        [Tooltip("Raggio (m) dello spazio libero richiesto.")]
        [SerializeField, Min(0.05f)] private float clearanceRadius = 0.3f;
        [Tooltip("Quanto dentro il bordo (m) si controlla lo spazio libero.")]
        [SerializeField, Min(0f)] private float clearanceForwardOffset = 0.5f;

        public bool HasLedge { get; private set; }
        public LedgeInfo Ledge { get; private set; }

        private Vector3 _lastFacing = Vector3.forward;

        /// <summary>Samples ledges in front of the player. Call once per FixedUpdate.</summary>
        public void Refresh(Vector3 facing)
        {
            HasLedge = false;

            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) return;
            facing.Normalize();
            _lastFacing = facing;

            Vector3 head = transform.position + Vector3.up * headHeight;
            if (!Physics.Raycast(head, facing, out RaycastHit fwd, forwardCastDistance, ledgeMask, QueryTriggerInteraction.Ignore)) return;
            if (Mathf.Abs(fwd.normal.y) > maxWallNormalY) return;

            Vector3 probe = fwd.point + Vector3.up * probeHeightAbove + facing * probeInset;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, downCastDistance, ledgeMask, QueryTriggerInteraction.Ignore)) return;
            if (top.normal.y < minTopNormalY) return;
            if (top.point.y - transform.position.y < minLedgeHeight) return;

            Vector3 wallNormal = fwd.normal;
            wallNormal.y = 0f;
            wallNormal.Normalize();

            if (obstructionMask.value != 0)
            {
                Vector3 basePos = top.point - wallNormal * clearanceForwardOffset;
                Vector3 p0 = basePos + Vector3.up * (clearanceRadius + 0.05f);
                Vector3 p1 = basePos + Vector3.up * Mathf.Max(clearanceRadius + 0.05f, clearanceHeight - clearanceRadius);
                if (Physics.CheckCapsule(p0, p1, clearanceRadius, obstructionMask, QueryTriggerInteraction.Ignore)) return;
            }

            HasLedge = true;
            Ledge = new LedgeInfo { topPoint = top.point, wallPoint = fwd.point, wallNormal = wallNormal };
        }

        [ContextMenu("Debug/Print State")]
        private void PrintState()
        {
            Debug.Log($"[LedgeDetector] hasLedge={HasLedge} top={Ledge.topPoint} normal={Ledge.wallNormal}");
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 head = transform.position + Vector3.up * headHeight;
            Gizmos.color = HasLedge ? Color.green : Color.gray;
            Gizmos.DrawLine(head, head + _lastFacing * forwardCastDistance);
            if (HasLedge)
            {
                Gizmos.DrawWireSphere(Ledge.topPoint, 0.08f);
                Gizmos.DrawLine(Ledge.wallPoint, Ledge.topPoint);
            }
        }
    }
}