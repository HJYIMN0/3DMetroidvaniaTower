using System;
using UnityEngine;
using UnityEngine.Events;
using Game.Core;

namespace Game.Life
{
    /// <summary>
    /// Base class for anything that has HP and Posture (pose).
    /// Pose accumulates; when >= maxPose, unit enters Stun and is exposed to critical damage.
    /// </summary>
    public abstract class AbstractLifeController : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] protected int maxHP = 100;
        [SerializeField] protected int currentHP = 100;

        [Header("Posture / Pose")]
        [SerializeField] protected int maxPose = 100;
        [SerializeField] protected int currentPose = 0;
        [SerializeField] protected float poseDecayPerSecond = 10f;
        [SerializeField] protected float poseDecayDelay = 1.5f;   // seconds after last pose gain before decay

        [Header("Stun")]
        [SerializeField] protected float stunDuration = 2f;
        [SerializeField] protected int criticalDamage = 50;

        [Header("Events (UnityEvent - serializable)")]
        public UnityEvent<int, int> onHPChanged;         // (current, max)
        public UnityEvent<int, int> onPoseChanged;       // (current, max)
        public UnityEvent onDeath;
        public UnityEvent onStunStart;
        public UnityEvent onStunEnd;
        public UnityEvent<DamageInfo> onDamageTakenUnity;

        // Runtime C# events (high frequency)
        public event Action<DamageInfo> OnDamageTaken;
        public event Action OnPoseBroken;

        protected float _lastPoseGainTime = -999f;
        protected float _stunTimer;

        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public int CurrentPose => currentPose;
        public int MaxPose => maxPose;
        public int CriticalDamage => criticalDamage;
        public float StunDuration => stunDuration;

        public bool IsAlive => currentHP > 0;
        public bool IsInvulnerable { get; set; }
        public bool IsExposedToCritical { get; protected set; }
        public bool IsStunned { get; protected set; }
        public Transform Transform => transform;

        protected virtual void Awake()
        {
            currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 0, maxHP);
        }

        protected virtual void Update()
        {
            // Pose decay
            if (!IsStunned && currentPose > 0 && Time.time - _lastPoseGainTime >= poseDecayDelay)
            {
                int decay = Mathf.CeilToInt(poseDecayPerSecond * Time.deltaTime);
                if (decay > 0) AddPose(-decay);
            }

            // Stun timer
            if (IsStunned)
            {
                _stunTimer -= Time.deltaTime;
                if (_stunTimer <= 0f) ExitStun();
            }
        }

        public virtual void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            if (IsInvulnerable && !info.ignoreInvulnerability) return;

            int finalDamage = info.isCritical ? criticalDamage : info.amount;

            currentHP = Mathf.Max(0, currentHP - finalDamage);
            onHPChanged?.Invoke(currentHP, maxHP);
            onDamageTakenUnity?.Invoke(info);
            OnDamageTaken?.Invoke(info);

            if (info.poseDamage != 0) AddPose(info.poseDamage);

            if (currentHP <= 0) Die();
        }

        public virtual void AddPose(int amount)
        {
            if (!IsAlive) return;
            currentPose = Mathf.Clamp(currentPose + amount, 0, maxPose);
            if (amount > 0) _lastPoseGainTime = Time.time;
            onPoseChanged?.Invoke(currentPose, maxPose);

            if (currentPose >= maxPose && !IsStunned) EnterStun();
        }

        public virtual void Heal(int amount)
        {
            currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
            onHPChanged?.Invoke(currentHP, maxHP);
        }

        protected virtual void EnterStun()
        {
            IsStunned = true;
            IsExposedToCritical = true;
            _stunTimer = stunDuration;
            onStunStart?.Invoke();
            OnPoseBroken?.Invoke();
        }

        protected virtual void ExitStun()
        {
            IsStunned = false;
            IsExposedToCritical = false;
            currentPose = 0;
            onPoseChanged?.Invoke(currentPose, maxPose);
            onStunEnd?.Invoke();
        }

        protected virtual void Die()
        {
            IsAlive.GetType(); // no-op to satisfy struct if needed
            onDeath?.Invoke();
        }
    }
}