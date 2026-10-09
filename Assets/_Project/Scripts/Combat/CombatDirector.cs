using System.Collections.Generic;
using UnityEngine;
using Game.AI;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// Centralized coordinator for multi-enemy combat and player parry recovery.
    /// SINGLETON: place on a single empty GameObject "CombatDirector" in each combat scene.
    ///
    /// Responsibilities:
    ///  - Enforce a global parry cooldown for the player (prevents parry spam).
    ///  - Limit simultaneous enemy attackers.
    ///  - Queue enemy attack requests in FIFO and stagger them with an exposed delay,
    ///    so the player can chain parries in a readable rhythm.
    /// </summary>
    public class CombatDirector : Singleton<CombatDirector>
    {
        [Header("Player Parry Recovery")]
        [Tooltip("Minimum time between two successive parry inputs. Prevents spam.")]
        [SerializeField] private float parryCooldown = 1.2f;

        [Header("Enemy Attack Coordination")]
        [Tooltip("How many enemies may attack simultaneously. 1 = classic Sekiro-like.")]
        [SerializeField] private int maxSimultaneousAttackers = 1;

        [Tooltip("Minimum gap between the end of one enemy attack and the start of the next.")]
        [SerializeField] private float attackStaggerTime = 0.4f;

        [Tooltip("Safety timeout: an attack token auto-releases after this many seconds.")]
        [SerializeField] private float attackSlotTimeout = 3f;

        [Tooltip("If true, enemy attack requests are queued FIFO. If false, first-come-first-served with no queue.")]
        [SerializeField] private bool useFifoQueue = true;

        // --- Parry runtime ---
        private float _lastParryTime = -999f;

        // --- Attack slots ---
        private class AttackSlot { public EnemyBrain brain; public float startTime; }
        private readonly List<AttackSlot> _activeSlots = new();
        private readonly List<EnemyBrain> _queue = new();
        private float _lastAttackEndTime = -999f;

        // --- Public API ---

        public float ParryCooldown => parryCooldown;
        public bool CanParry => Time.time - _lastParryTime >= parryCooldown;
        public float ParryCooldownRemaining => Mathf.Max(0f, parryCooldown - (Time.time - _lastParryTime));

        public int ActiveAttackers => _activeSlots.Count;
        public int QueuedAttackers => _queue.Count;

        // --- Parry ---

        /// <summary>Returns true if the parry is accepted (cooldown elapsed).</summary>
        public bool RequestParry()
        {
            if (!CanParry) return false;
            _lastParryTime = Time.time;
            return true;
        }

        // --- Enemy attacks ---

        /// <summary>
        /// Enemy brain asks for permission to attack.
        /// Returns true if granted a token immediately; false if it should wait.
        /// </summary>
        public bool TryRequestAttack(EnemyBrain brain)
        {
            if (brain == null || !brain.IsAlive) return false;

            if (HasToken(brain)) return true;

            int qIdx = _queue.IndexOf(brain);

            // If queued, only the head can take a slot (FIFO fairness).
            if (useFifoQueue && qIdx >= 0 && qIdx != 0) return false;

            bool slotAvailable = _activeSlots.Count < maxSimultaneousAttackers;
            bool staggerOk = Time.time - _lastAttackEndTime >= attackStaggerTime;

            if (slotAvailable && staggerOk)
            {
                if (qIdx >= 0) _queue.RemoveAt(qIdx);
                GrantToken(brain);
                return true;
            }

            if (qIdx < 0) _queue.Add(brain);
            return false;
        }

        /// <summary>Called by an enemy when its attack animation ends.</summary>
        public void ReleaseAttack(EnemyBrain brain)
        {
            int idx = _activeSlots.FindIndex(s => s.brain == brain);
            if (idx >= 0)
            {
                _activeSlots.RemoveAt(idx);
                _lastAttackEndTime = Time.time;
            }
        }

        public void ClearAll()
        {
            _activeSlots.Clear();
            _queue.Clear();
        }

        // --- Internals ---

        private bool HasToken(EnemyBrain brain) => _activeSlots.Exists(s => s.brain == brain);

        private void GrantToken(EnemyBrain brain)
        {
            _activeSlots.Add(new AttackSlot { brain = brain, startTime = Time.time });
        }

        private void Update()
        {
            // Safety timeout + dead cleanup
            for (int i = _activeSlots.Count - 1; i >= 0; i--)
            {
                var s = _activeSlots[i];
                if (s.brain == null || !s.brain.IsAlive || Time.time - s.startTime > attackSlotTimeout)
                {
                    _activeSlots.RemoveAt(i);
                    _lastAttackEndTime = Time.time;
                }
            }
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i] == null || !_queue[i].IsAlive) _queue.RemoveAt(i);
            }
        }

        // --- Debug ---
        [ContextMenu("Debug/Dump State")]
        private void DebugDump()
        {
            Debug.Log($"[CombatDirector] ParryCD={ParryCooldownRemaining:F2}s Active={_activeSlots.Count} Queued={_queue.Count}");
            foreach (var s in _activeSlots) Debug.Log($"  ACTIVE: {s.brain?.name} ({Time.time - s.startTime:F2}s)");
            for (int i = 0; i < _queue.Count; i++) Debug.Log($"  QUEUE[{i}]: {_queue[i]?.name}");
        }
    }
}