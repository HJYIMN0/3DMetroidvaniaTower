using System.Collections.Generic;
using UnityEngine;
using Game.Input;
using Game.Life;
using Game.Movement;

namespace Game.Combat
{
    /// <summary>What the combat manager does with a queued command this frame.</summary>
    public enum CommandDecision
    {
        /// <summary>Remove it from the queue and process it now.</summary>
        Execute = 0,
        /// <summary>Keep it in the queue (buffer): the character cannot act yet. It waits until it can run, is overridden, or expires.</summary>
        Wait = 1,
        /// <summary>Remove it without processing (it can never be valid).</summary>
        Discard = 2
    }

    /// <summary>
    /// Base class for combat managers. Player and Enemy can derive from it.
    /// The queue is a real input buffer: every frame the queued commands are examined by priority (highest first,
    /// then oldest first) and EvaluateCommand() decides for each one: Execute, Wait or Discard.
    /// </summary>
    public abstract class AbstractCombatManager : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] protected AbstractLifeController life;
        [SerializeField] protected ComboDefinition defaultCombo;
        [SerializeField] protected WeaponHitboxRaycaster weapon;
        [SerializeField] protected ParryController parry;
        [Tooltip("Modulo di schivata del movimento. Se vuoto viene preso con GetComponent (i nemici non ce l'hanno).")]
        [SerializeField] protected DodgeModule dodge;
        [SerializeField] protected Animation.AnimatorBridge animatorBridge;

        [Header("Queue (input buffer)")]
        [Tooltip("Max Depth = quanti comandi possono aspettare. Full Policy = cosa fare se è piena. Command Lifetime = per quanto un comando aspetta prima di scadere. Modificabili in Play.")]
        [SerializeField] protected CommandQueueSettings queueSettings = new CommandQueueSettings { maxDepth = 2 };
        [Tooltip("Logga in Console ogni comando che entra, esce, viene sovrascritto o scade.")]
        [SerializeField] protected bool logQueue = false;

        protected ComboController comboController;
        protected CommandQueue _queue;
        public CommandQueue Queue => _queue;

        public ComboDefinition DefaultCombo => defaultCombo;

        private readonly List<InputCommand> _scratch = new List<InputCommand>(8);

        /// <summary>
        /// If true, at most one command is executed per frame (the others stay buffered and are re-evaluated next frame).
        /// Default false: every executable command runs this frame (old behaviour). The player turns it on.
        /// </summary>
        protected virtual bool OneActionPerFrame => false;

        protected virtual void Awake()
        {
            if (life == null) life = GetComponent<AbstractLifeController>();
            if (parry == null) parry = GetComponent<ParryController>();
            if (dodge == null) dodge = GetComponent<DodgeModule>();
            _queue = new CommandQueue(queueSettings);
            comboController = new ComboController();   // <-- init runtime

            _queue.OnEnqueued += c => { if (logQueue) Debug.Log($"[{GetType().Name}] queue + {c}"); };
            _queue.OnDequeued += c => { if (logQueue) Debug.Log($"[{GetType().Name}] queue - {c}"); };
            _queue.OnOverridden += c => { if (logQueue) Debug.Log($"[{GetType().Name}] queue overridden {c}"); };
            _queue.OnDropped += c => { if (logQueue) Debug.Log($"[{GetType().Name}] queue dropped (full/expired) {c}"); };
        }

        /// <summary>Called by InputReader or AI to submit a command.</summary>
        public virtual void SubmitCommand(InputCommand cmd)
        {
            _queue.Enqueue(cmd);
        }

        /// <summary>Runs a command that EvaluateCommand() approved. The command is already out of the queue.</summary>
        protected abstract void ProcessCommand(InputCommand cmd);

        /// <summary>
        /// Decides what to do with a queued command this frame. Called every frame for every queued command
        /// (highest priority first). Default: always Execute (no buffering, like before).
        /// </summary>
        protected virtual CommandDecision EvaluateCommand(InputCommand cmd)
        {
            return CommandDecision.Execute;
        }

        protected virtual void Update()
        {
            // Settings are re-applied every frame so Inspector edits in Play Mode take effect.
            _queue.ApplySettings(queueSettings);
            _queue.RemoveExpired();
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            if (_queue.Count == 0) return;

            _queue.CopySorted(_scratch);   // snapshot: the queue can change while we process
            for (int i = 0; i < _scratch.Count; i++)
            {
                InputCommand cmd = _scratch[i];
                switch (EvaluateCommand(cmd))
                {
                    case CommandDecision.Execute:
                        _queue.Remove(cmd);
                        ProcessCommand(cmd);
                        if (OneActionPerFrame) return;
                        break;

                    case CommandDecision.Discard:
                        _queue.Remove(cmd);
                        break;

                        // Wait: stays in the queue. A blocked command does NOT block the ones behind it.
                }
            }
        }

        [ContextMenu("Debug/Dump Queue")]
        private void DumpQueue()
        {
            if (_queue != null) _queue.DebugDump();
        }
    }
}