using UnityEngine;

namespace Game.Movement
{
    public class WallDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask wallMask;
        [SerializeField] private float checkDistance = 0.6f;
        [SerializeField] private float rayHeightOffset = 1.0f;
        [SerializeField] private int rayCount = 3;

        public bool HasWall { get; private set; }
        public Vector3 WallNormal { get; private set; }
        public RaycastHit WallHit { get; private set; }

        public System.Action<RaycastHit> OnWallEnter;
        public System.Action OnWallExit;

        private bool _prevHasWall;

        private void Update()
        {
            HasWall = false;
            Vector3 origin = transform.position + Vector3.up * rayHeightOffset;

            for (int i = 0; i < rayCount; i++)
            {
                float t = rayCount == 1 ? 0.5f : i / (float)(rayCount - 1);
                Vector3 o = origin + Vector3.up * (t - 0.5f) * 1.5f;

                // Check all four horizontal directions
                for (int d = 0; d < 4; d++)
                {
                    Vector3 dir = Quaternion.Euler(0, d * 90f, 0) * transform.forward;
                    if (Physics.Raycast(o, dir, out var hit, checkDistance, wallMask, QueryTriggerInteraction.Ignore))
                    {
                        HasWall = true;
                        WallNormal = hit.normal;
                        WallHit = hit;
                        break;
                    }
                }
                if (HasWall) break;
            }

            if (HasWall && !_prevHasWall) OnWallEnter?.Invoke(WallHit);
            if (!HasWall && _prevHasWall) OnWallExit?.Invoke();
            _prevHasWall = HasWall;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = HasWall ? Color.cyan : Color.gray;
            Vector3 origin = transform.position + Vector3.up * rayHeightOffset;
            for (int d = 0; d < 4; d++)
            {
                Vector3 dir = Quaternion.Euler(0, d * 90f, 0) * transform.forward;
                Gizmos.DrawLine(origin, origin + dir * checkDistance);
            }
        }
    }
}