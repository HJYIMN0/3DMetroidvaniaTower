using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Pure raycast wall detector. Casts horizontal rays in world-aligned directions (independent from the
    /// player's facing) at several heights and keeps the nearest near-vertical hit.
    /// The coordinator calls Refresh() once per physics tick.
    /// </summary>
    public class WallDetector : MonoBehaviour
    {
        [Header("Masks")]
        [Tooltip("Layer considerati parete (es. Wall).")]
        [SerializeField] private LayerMask wallMask;

        [Header("Rays")]
        [Tooltip("Lunghezza dei raggi (m) dal centro del player. Deve superare il raggio della capsule.")]
        [SerializeField, Min(0.05f)] private float checkDistance = 0.6f;
        [Tooltip("Altezza del raggio più basso, dal pivot (m).")]
        [SerializeField] private float rayHeightMin = 0.4f;
        [Tooltip("Altezza del raggio più alto, dal pivot (m).")]
        [SerializeField] private float rayHeightMax = 1.4f;
        [Tooltip("Quanti livelli di altezza controllare.")]
        [SerializeField, Range(1, 5)] private int heightRayCount = 3;
        [Tooltip("Quante direzioni orizzontali controllare (8 = ogni 45°).")]
        [SerializeField, Range(4, 16)] private int directionCount = 8;
        [Tooltip("Scarta superfici con |normal.y| sopra questo valore (pavimenti, rampe).")]
        [SerializeField, Range(0f, 1f)] private float maxWallNormalY = 0.2f;

        public bool HasWall { get; private set; }
        /// <summary>Normal of the nearest wall (points away from the wall).</summary>
        public Vector3 WallNormal { get; private set; } = Vector3.forward;
        public Vector3 WallPoint { get; private set; }
        public float WallDistance { get; private set; }
        public RaycastHit WallHit { get; private set; }

        /// <summary>GameObject of the nearest wall (null if there is none). Used to remember the last wall jumped from.</summary>
        public GameObject WallObject => HasWall && WallHit.collider != null ? WallHit.collider.gameObject : null;

        public System.Action<RaycastHit> OnWallEnter;
        public System.Action OnWallExit;

        private bool _prevHasWall;

        /// <summary>Samples walls around the player. Call once per FixedUpdate.</summary>
        public void Refresh()
        {
            bool found = false;
            float best = float.MaxValue;
            RaycastHit bestHit = default;

            Vector3 pos = transform.position;
            float step = 360f / directionCount;

            for (int h = 0; h < heightRayCount; h++)
            {
                float t = heightRayCount == 1 ? 0.5f : h / (float)(heightRayCount - 1);
                Vector3 origin = pos + Vector3.up * Mathf.Lerp(rayHeightMin, rayHeightMax, t);

                for (int d = 0; d < directionCount; d++)
                {
                    Vector3 dir = Quaternion.Euler(0f, d * step, 0f) * Vector3.forward;
                    if (!Physics.Raycast(origin, dir, out RaycastHit hit, checkDistance, wallMask, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(hit.normal.y) > maxWallNormalY) continue;
                    if (hit.distance < best)
                    {
                        best = hit.distance;
                        bestHit = hit;
                        found = true;
                    }
                }
            }

            HasWall = found;
            if (found)
            {
                WallHit = bestHit;
                WallNormal = bestHit.normal;
                WallPoint = bestHit.point;
                WallDistance = best;
            }

            if (HasWall && !_prevHasWall) OnWallEnter?.Invoke(WallHit);
            if (!HasWall && _prevHasWall) OnWallExit?.Invoke();
            _prevHasWall = HasWall;
        }

        [ContextMenu("Debug/Print State")]
        private void PrintState()
        {
            Debug.Log($"[WallDetector] hasWall={HasWall} normal={WallNormal} distance={WallDistance:F2}");
        }

        private void OnDrawGizmosSelected()
        {
            float step = 360f / Mathf.Max(1, directionCount);
            for (int h = 0; h < heightRayCount; h++)
            {
                float t = heightRayCount == 1 ? 0.5f : h / (float)(heightRayCount - 1);
                Vector3 origin = transform.position + Vector3.up * Mathf.Lerp(rayHeightMin, rayHeightMax, t);
                Gizmos.color = HasWall ? Color.cyan : Color.gray;
                for (int d = 0; d < directionCount; d++)
                {
                    Vector3 dir = Quaternion.Euler(0f, d * step, 0f) * Vector3.forward;
                    Gizmos.DrawLine(origin, origin + dir * checkDistance);
                }
            }

            if (HasWall)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(WallPoint, WallNormal * 0.7f);
            }
        }
    }
}