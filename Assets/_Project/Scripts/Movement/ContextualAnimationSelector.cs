using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Selects which jump animation to play based on what's in front of the player.
    /// </summary>
    public class ContextualAnimationSelector : MonoBehaviour
    {
        [SerializeField] private LayerMask wallMask;
        [SerializeField] private LayerMask ledgeMask;
        [SerializeField] private float probeDistance = 2.0f;

        [Header("State Names")]
        [SerializeField] private string jumpAirState = "Jump_Air";
        [SerializeField] private string jumpToWallState = "Jump_ToWall";
        [SerializeField] private string jumpToLedgeState = "Jump_ToLedge";

        [SerializeField] private Animation.AnimatorBridge animatorBridge;

        public string ResolveJumpState()
        {
            Vector3 origin = transform.position + Vector3.up * 1.0f;
            if (Physics.Raycast(origin, transform.forward, out var wallHit, probeDistance, wallMask))
            {
                if (Physics.Raycast(wallHit.point + Vector3.up * 0.5f + transform.forward * 0.05f,
                                    Vector3.down, out _, 1.5f, ledgeMask))
                {
                    return jumpToLedgeState;
                }
                return jumpToWallState;
            }
            return jumpAirState;
        }
    }
}