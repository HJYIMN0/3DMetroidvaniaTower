using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Timestamp-based input buffer. Press() records the time; Peek/Consume check it against a buffer window.
    /// </summary>
    public struct BufferedInput
    {
        public float time;

        public void Press() { time = Time.time; }
        public void Clear() { time = float.NegativeInfinity; }
        public bool Peek(float buffer) { return Time.time - time <= buffer; }

        public bool Consume(float buffer)
        {
            if (!Peek(buffer)) return false;
            Clear();
            return true;
        }
    }

    /// <summary>
    /// Plain data object created once by PlayerMovementCoordinator and passed to every module each physics tick.
    /// It is the only thing modules share, so they never need to know about each other.
    /// </summary>
    public class MovementContext
    {
        // ---- Written by the coordinator every tick ----
        public float DeltaTime;
        public Vector2 MoveInputRaw;
        /// <summary>Camera-relative move direction on XZ (length 0..1).</summary>
        public Vector3 MoveInputWorld;
        /// <summary>Last non-zero move direction (XZ, normalized). Used by detectors.</summary>
        public Vector3 Facing = Vector3.forward;
        public bool IsGrounded;
        /// <summary>Seconds since the player left the ground (0 while grounded).</summary>
        public float AirTime;
        public bool JumpHeld;

        // ---- Buffered button presses ----
        public BufferedInput Jump;
        public BufferedInput Dodge;
        public BufferedInput GrabWall;
        public BufferedInput GrabLedge;

        // ---- Shared state written by modules ----
        /// <summary>Wall modules cannot (re)attach before this time.</summary>
        public float WallBlockedUntil = float.NegativeInfinity;
        /// <summary>Air jumps granted by other modules (e.g. wall contact); consumed by JumpModule.</summary>
        public int PendingAirJumpGrants;
        /// <summary>Set by a module that handled vertical velocity this tick. If false, the coordinator applies fallback gravity.</summary>
        public bool VerticalHandled;

        // ---- Steep surfaces (written by the coordinator, read by LocomotionModule) ----
        /// <summary>True while standing on a surface steeper than GroundDetector.MaxSlopeAngle: no horizontal control, physics slides the player.</summary>
        public bool OnSteepSurface;
        public Vector3 SteepNormal = Vector3.up;
        /// <summary>True if this tick the coordinator removed horizontal velocity pushing into a steep surface.</summary>
        public bool SteepBlocked;

        // ---- Heading lock (set by wall jumps, read by LocomotionModule) ----
        public float HeadingLockUntil = float.NegativeInfinity;
        public float HeadingLockCenterDeg;
        public float HeadingLockLeftDeg;
        public float HeadingLockRightDeg;
        public bool HeadingLockUntilApex;
        private bool _headingLockActive;

        public void LockHeading(float seconds, float centerDeg, float leftDeg, float rightDeg, bool untilApex)
        {
            HeadingLockUntil = Time.time + seconds;
            HeadingLockCenterDeg = centerDeg;
            HeadingLockLeftDeg = leftDeg;
            HeadingLockRightDeg = rightDeg;
            HeadingLockUntilApex = untilApex;
            _headingLockActive = true;
        }

        /// <summary>
        /// True while the air heading must stay inside the lock zone (center +/- left/right degrees):
        /// for the lock time, and (if enabled) as long as the player is still rising.
        /// </summary>
        public bool IsHeadingLocked(float verticalVelocity)
        {
            if (!_headingLockActive) return false;
            if (Time.time < HeadingLockUntil) return true;
            if (HeadingLockUntilApex && verticalVelocity > 0f) return true;
            _headingLockActive = false;
            return false;
        }

        /// <summary>Same as IsHeadingLocked but without side effects (for debug tools).</summary>
        public bool PeekHeadingLocked(float verticalVelocity)
        {
            if (!_headingLockActive) return false;
            return Time.time < HeadingLockUntil || (HeadingLockUntilApex && verticalVelocity > 0f);
        }

        public void ClearHeadingLock()
        {
            _headingLockActive = false;
            HeadingLockUntil = float.NegativeInfinity;
        }

        // ---- Last wall jumped from ----
        /// <summary>
        /// The last wall the player ran on or wall-jumped from. WallRunModule and WallSlideModule refuse to attach to it again.
        /// Set to null when the player touches ground or attaches to a DIFFERENT wall.
        /// </summary>
        public GameObject LastWall;

        public bool IsBlockedWall(GameObject wallObject)
        {
            return wallObject != null && LastWall != null && wallObject == LastWall;
        }

        public MovementContext()
        {
            Jump.Clear();
            Dodge.Clear();
            GrabWall.Clear();
            GrabLedge.Clear();
        }

        public bool WallContactAllowed => Time.time >= WallBlockedUntil;
        public void BlockWallContact(float seconds) { WallBlockedUntil = Time.time + seconds; }
        public void GrantAirJump() { PendingAirJumpGrants++; }
    }
}