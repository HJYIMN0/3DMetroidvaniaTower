using System;
using UnityEngine;
using Game.Input;
using Game.Life;

namespace Game.Combat
{
    /// <summary>
    /// Base class for combat managers. Player and Enemy can derive from it.
    /// </summary>
    public abstract class AbstractCombatManager : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] protected AbstractLifeController life;
        [SerializeField] protected ComboDefinition defaultCombo;
        [SerializeField] protected WeaponHitboxRaycaster weapon;
        [SerializeField] protected ParryController parry;
        [SerializeField] protected DodgeController dodge;
        [SerializeField] protected Animation.AnimatorBridge animatorBridge;

        [Header("Queue")]
        [SerializeField] protected int maxQueueDepth = 2;


        protected ComboController comboController;
        protected CommandQueue _queue;
        public CommandQueue Queue => _queue;

        public ComboDefinition DefaultCombo => defaultCombo;

        protected virtual void Awake()
        {
            if (life == null) life = GetComponent<AbstractLifeController>();
            if (parry == null) parry = GetComponent<ParryController>();
            if (dodge == null) dodge = GetComponent<DodgeController>();
            _queue = new CommandQueue(maxQueueDepth);
            comboController = new ComboController();   // <-- init runtime
        }

        /// <summary>Called by InputReader or AI to submit a command.</summary>
        public virtual void SubmitCommand(InputCommand cmd)
        {
            _queue.Enqueue(cmd);
        }

        protected abstract void ProcessCommand(InputCommand cmd);

        protected virtual void Update()
        {
            while (_queue.TryDequeue(out var cmd))
            {
                ProcessCommand(cmd);
            }
        }
    }
}