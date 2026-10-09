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
        public int priority;            // higher = more important, cannot be overridden by lower
        public bool isOverride;         // if true, clears queue of lower-priority commands

        public InputCommand() { }

        public InputCommand(InputCommandType type, int priority, bool isOverride)
        {
            this.type = type;
            this.priority = priority;
            this.isOverride = isOverride;
            this.timestamp = UnityEngine.Time.time;
        }

        public override string ToString() => $"{type}(p{priority},ovr={isOverride},t={timestamp:F3})";
    }
}