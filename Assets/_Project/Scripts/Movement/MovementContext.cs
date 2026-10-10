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