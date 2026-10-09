using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Input
{
    /// <summary>
    /// Priority + override queue for player commands.
    /// Rules (fixed by design):
    ///  - Parry is absolute priority (cannot be overridden)
    ///  - Dodge can override Attack
    ///  - Jump / GrabWall / GrabLedge are immune and cannot override anything
    /// </summary>
    public class CommandQueue
    {
        private readonly List<InputCommand> _queue = new();
        private readonly int _maxDepth;

        public event Action<InputCommand> OnEnqueued;
        public event Action<InputCommand> OnDequeued;
        public event Action<InputCommand> OnOverridden;

        public int Count => _queue.Count;
        public int MaxDepth => _maxDepth;

        public CommandQueue(int maxDepth) { _maxDepth = Mathf.Max(1, maxDepth); }

        public int GetPriority(InputCommandType type) => type switch
        {
            InputCommandType.Parry => 100,
            InputCommandType.Jump => 90,
            InputCommandType.GrabWall => 90,
            InputCommandType.GrabLedge => 90,
            InputCommandType.Dodge => 50,
            InputCommandType.Attack => 30,
            _ => 0
        };

        public bool IsImmuneToOverride(InputCommandType type) =>
            type == InputCommandType.Parry ||
            type == InputCommandType.Jump ||
            type == InputCommandType.GrabWall ||
            type == InputCommandType.GrabLedge;

        public bool Enqueue(InputCommand cmd)
        {
            if (cmd == null) return false;

            if (cmd.isOverride)
            {
                // Remove any queued commands with lower priority that are NOT immune
                for (int i = _queue.Count - 1; i >= 0; i--)
                {
                    var existing = _queue[i];
                    if (IsImmuneToOverride(existing.type)) continue;
                    if (existing.priority < cmd.priority)
                    {
                        OnOverridden?.Invoke(existing);
                        _queue.RemoveAt(i);
                    }
                }
            }

            if (_queue.Count >= _maxDepth)
            {
                Debug.Log($"[CommandQueue] Rejected {cmd} — queue full.");
                return false;
            }

            _queue.Add(cmd);
            OnEnqueued?.Invoke(cmd);
            return true;
        }

        public bool TryPeek(out InputCommand cmd)
        {
            if (_queue.Count == 0) { cmd = null; return false; }
            cmd = _queue[0];
            return true;
        }

        public bool TryDequeue(out InputCommand cmd)
        {
            if (_queue.Count == 0) { cmd = null; return false; }
            cmd = _queue[0];
            _queue.RemoveAt(0);
            OnDequeued?.Invoke(cmd);
            return true;
        }

        public void Clear()
        {
            _queue.Clear();
        }

        public void DebugDump()
        {
            Debug.Log($"[CommandQueue] depth={_queue.Count}/{_maxDepth}");
            for (int i = 0; i < _queue.Count; i++) Debug.Log($"  [{i}] {_queue[i]}");
        }
    }
}