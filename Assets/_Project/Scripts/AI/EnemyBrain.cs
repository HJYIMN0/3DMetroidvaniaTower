using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Life;
using Game.Animation;

namespace Game.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBrain : MonoBehaviour
    {
        [SerializeField] private EnemyData data;
        [SerializeField] private Transform target;

        public EnemyData Data => data;
        public Transform Target => target;
        public NavMeshAgent Agent { get; private set; }
        public AnimatorBridge AnimatorBridge { get; private set; }
        public ParryController Parry { get; private set; }
        public EnemyLife Life { get; private set; }

        // States
        public EnemyIdleState IdleState { get; private set; }
        public EnemyChaseState ChaseState { get; private set; }
        public EnemyCombatState CombatState { get; private set; }
        public EnemyAttackState AttackState { get; private set; }
        public EnemyParryState ParryState { get; private set; }
        public EnemyStunnedState StunnedState { get; private set; }

        private EnemyState _currentState;
        private float _lastAttackTime = -999f;
        private float _lastParryTime = -999f;

        // NEW: token state for CombatDirector
        private bool _hasAttackToken;

        public bool IsAlive => Life == null || Life.IsAlive;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            AnimatorBridge = GetComponent<AnimatorBridge>();
            Parry = GetComponent<ParryController>();
            Life = GetComponent<EnemyLife>();

            IdleState = new EnemyIdleState();
            ChaseState = new EnemyChaseState();
            CombatState = new EnemyCombatState();
            AttackState = new EnemyAttackState();
            ParryState = new EnemyParryState();
            StunnedState = new EnemyStunnedState();
        }

        private void Start()
        {
            if (Life != null)
            {
                Life.OnPoseBroken += () => ChangeState(StunnedState);
                Life.onStunEnd.AddListener(() => ChangeState(CombatState));
            }
            ChangeState(IdleState);
        }

        private void Update()
        {
            _currentState?.Tick(this, Time.deltaTime);
        }

        public void ChangeState(EnemyState next)
        {
            _currentState?.Exit(this);
            _currentState = next;
            _currentState?.Enter(this);
        }

        // --- Combat gating ---

        public bool CanAttack()
        {
            if (Time.time - _lastAttackTime < data.combatProfile.attackCooldown) return false;
            if (CombatDirector.Instance != null && CombatDirector.Instance.ParryCooldownRemaining > 0f)
                return false; // rispetta la finestra di parry del player
            return true;
        }

        public bool CanUseParry() => Time.time - _lastParryTime >= data.combatProfile.parryCooldown;

        public void RegisterAttackUsed() => _lastAttackTime = Time.time;
        public void RegisterParryUsed() => _lastParryTime = Time.time;

        // --- CombatDirector integration ---

        /// <summary>Requests an attack token. Returns true if granted immediately.</summary>
        public bool TryAttack()
        {
            if (CombatDirector.Instance == null) return true; // fallback single-enemy scene
            return CombatDirector.Instance.TryRequestAttack(this);
        }

        /// <summary>Releases the attack token back to the director.</summary>
        public void ReleaseAttackToken()
        {
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.ReleaseAttack(this);
        }

        /// <summary>Explicit token setter used by states.</summary>
        public void SetAttackToken(bool hasToken) => _hasAttackToken = hasToken;

        public bool HasAttackPermission() => _hasAttackToken;
    }
}