using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Input
{
    /// <summary>What the queue does when it is full and a new command arrives.</summary>
    public enum QueueFullPolicy
    {
        /// <summary>The new command is dropped (old behaviour).</summary>
        RejectNewest = 0,
        /// <summary>The oldest non-immune command is dropped to make room: fresh input wins.</summary>
        DropOldest = 1
    }

    /// <summary>
    /// Serializable queue tuning. Put a field of this type on the owner of the queue (the combat manager)
    /// so it shows in the Inspector, then call CommandQueue.ApplySettings(...) (also each frame, to tweak at runtime).
    /// </summary>
    [Serializable]
    public class CommandQueueSettings
    {
        [Tooltip("Numero massimo di comandi in coda (il 'pool'). Minimo 1.")]
        [Min(1)] public int maxDepth = 3;

        [Tooltip("Cosa fare quando la coda è piena: scartare il nuovo comando o il più vecchio (non immune).")]
        public QueueFullPolicy fullPolicy = QueueFullPolicy.RejectNewest;

        [Tooltip("Dopo quanti secondi un comando ancora in coda viene scartato. 0 = non scade mai. " +
                 "Ha effetto solo se CanProcessCommand trattiene i comandi: di default la coda si svuota a ogni frame.")]
        [Min(0f)] public float commandLifetime = 0.3f;
    }

    /// <summary>
    /// Priority + override queue for player commands. Rules now come from the commands themselves
    /// (set per command in InputReader's Inspector settings):
    ///  - isOverride: on enqueue, removes queued commands with LOWER priority
    ///  - immuneToOverride: a command with this flag is never removed by an override
    ///  - the queue has a max depth and a policy for when it is full (both changeable at runtime)
    /// </summary>
    public class CommandQueue
    {
        private readonly List<InputCommand> _queue = new();
        private int _maxDepth = 1;

        public event Action<InputCommand> OnEnqueued;
        public event Action<InputCommand> OnDequeued;
        /// <summary>Removed by a higher-priority override command.</summary>
        public event Action<InputCommand> OnOverridden;
        /// <summary>Removed to make room because the queue was full (DropOldest policy).</summary>
        public event Action<InputCommand> OnDropped;

        public int Count => _queue.Count;

        public int MaxDepth
        {
            get => _maxDepth;
            set => _maxDepth = Mathf.Max(1, value);
        }

        public QueueFullPolicy FullPolicy { get; set; } = QueueFullPolicy.RejectNewest;

        /// <summary>Seconds after which a queued command is discarded by RemoveExpired(). 0 = never.</summary>
        public float CommandLifetime { get; set; }

        public CommandQueue(int maxDepth) { MaxDepth = maxDepth; }

        public CommandQueue(int maxDepth, QueueFullPolicy fullPolicy)
        {
            MaxDepth = maxDepth;
            FullPolicy = fullPolicy;
        }

        public CommandQueue(CommandQueueSettings settings) { ApplySettings(settings); }

        /// <summary>Copies max depth and full policy from the Inspector settings. Safe to call every frame.</summary>
        public void ApplySettings(CommandQueueSettings settings)
        {
            if (settings == null) return;
            MaxDepth = settings.maxDepth;
            FullPolicy = settings.fullPolicy;
            CommandLifetime = settings.commandLifetime;
        }

        // ---- Legacy helpers (kept so existing callers keep compiling). Enqueue no longer uses them. ----

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
                for (int i = _queue.Count - 1; i >= 0; i--)
                {
                    var existing = _queue[i];
                    if (existing.immuneToOverride) continue;
                    if (existing.priority < cmd.priority)
                    {
                        OnOverridden?.Invoke(existing);
                        _queue.RemoveAt(i);
                    }
                }
            }

            while (_queue.Count >= _maxDepth)
            {
                if (FullPolicy == QueueFullPolicy.DropOldest && TryDropOldestNonImmune()) continue;

                Debug.Log($"[CommandQueue] Rejected {cmd} - queue full ({_queue.Count}/{_maxDepth}).");
                return false;
            }

            _queue.Add(cmd);
            OnEnqueued?.Invoke(cmd);
            return true;
        }

        private bool TryDropOldestNonImmune()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].immuneToOverride) continue;
                var dropped = _queue[i];
                _queue.RemoveAt(i);
                OnDropped?.Invoke(dropped);
                return true;
            }
            return false;
        }

        /// <summary>Discards commands older than CommandLifetime. Returns how many were removed.</summary>
        public int RemoveExpired()
        {
            if (CommandLifetime <= 0f) return 0;

            int removed = 0;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (Time.time - _queue[i].timestamp <= CommandLifetime) continue;
                var expired = _queue[i];
                _queue.RemoveAt(i);
                OnDropped?.Invoke(expired);
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// Fills 'dst' with the queued commands ordered by priority (highest first); equal priority keeps arrival order
        /// (oldest first). Does not modify the queue and does not allocate if 'dst' is reused.
        /// </summary>
        public void CopySorted(List<InputCommand> dst)
        {
            dst.Clear();
            for (int i = 0; i < _queue.Count; i++)
            {
                InputCommand c = _queue[i];
                int j = dst.Count;
                while (j > 0 && dst[j - 1].priority < c.priority) j--;
                dst.Insert(j, c);
            }
        }

        /// <summary>Removes a specific command (e.g. after it was executed or discarded).</summary>
        public bool Remove(InputCommand cmd)
        {
            int i = _queue.IndexOf(cmd);
            if (i < 0) return false;
            _queue.RemoveAt(i);
            OnDequeued?.Invoke(cmd);
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
            Debug.Log($"[CommandQueue] depth={_queue.Count}/{_maxDepth} policy={FullPolicy}");
            for (int i = 0; i < _queue.Count; i++) Debug.Log($"  [{i}] {_queue[i]}");
        }
    }
}