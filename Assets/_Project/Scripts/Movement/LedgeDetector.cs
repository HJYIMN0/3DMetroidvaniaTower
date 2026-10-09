using UnityEngine;

namespace Game.Movement
{
    public struct LedgeInfo
    {
        public Vector3 point;
        public Vector3 normal;
        public Vector3 forward;
    }

    public class LedgeDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask ledgeMask;
        [SerializeField] private float headHeight = 1.7f;
        [SerializeField] private float forwardCastDistance = 0.7f;
        [SerializeField] private float downwardCastDistance = 1.2f;
        [SerializeField] private float minLedgeHeight = 0.3f;

        public bool HasLedge { get; private set; }
        public LedgeInfo Ledge { get; private set; }

        private void Update()
        {
            HasLedge = false;
            Vector3 head = transform.position + Vector3.up * headHeight;

            if (Physics.Raycast(head, transform.forward, out var fwd, forwardCastDistance, ledgeMask, QueryTriggerInteraction.Ignore))
            {
                // Hit a wall — check if there's a ledge above by casting down from slightly above
                Vector3 aboveWall = fwd.point + Vector3.up * 0.5f + transform.forward * 0.05f;
                if (Physics.Raycast(aboveWall, Vector3.down, out var down, downwardCastDistance, ledgeMask, QueryTriggerInteraction.Ignore))
                {
                    float ledgeHeight = down.point.y - transform.position.y;
                    if (ledgeHeight >= minLedgeHeight)
                    {
                        HasLedge = true;
                        Ledge = new LedgeInfo
                        {
                            point = down.point,
                            normal = fwd.normal,
                            forward = Vector3.Cross(fwd.normal, Vector3.up).normalized
                        };
                    }
                }
            }
        }
    }
}