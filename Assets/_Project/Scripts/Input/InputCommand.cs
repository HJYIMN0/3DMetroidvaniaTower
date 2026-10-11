using System;

namespace Game.Input
{
    /// <summary>
    /// Serialized command emitted by InputReader, consumed by CombatManager / Mover.
    /// </summary>
    [Serializable]
    public class InputCommand
    {
        public InputCommandType type;
        public float timestamp;         // Time.time at generation
        public int priority;            // higher = more important
        public bool isOverride;         // if true, removes queued commands with lower priority (unless they are immune)
        public bool immuneToOverride;   // if true, other commands can never remove this one from the queue

        public InputCommand() { }

        /// <summary>Legacy constructor: immunity follows the old fixed rules (Parry, Jump, GrabWall, GrabLedge).</summary>
        public InputCommand(InputCommandType type, int priority, bool isOverride)
            : this(type, priority, isOverride, DefaultImmunity(type)) { }

        public InputCommand(InputCommandType type, int priority, bool isOverride, bool immuneToOverride)
        {
            this.type = type;
            this.priority = priority;
            this.isOverride = isOverride;
            this.immuneToOverride = immuneToOverride;
            this.timestamp = UnityEngine.Time.time;
        }

        private static bool DefaultImmunity(InputCommandType type)
        {
            return type == InputCommandType.Parry
                || type == InputCommandType.Jump
                || type == InputCommandType.GrabWall
                || type == InputCommandType.GrabLedge;
        }

        public override string ToString() =>
            $"{type}(p{priority},ovr={isOverride},imm={immuneToOverride},t={timestamp:F3})";
    }
}