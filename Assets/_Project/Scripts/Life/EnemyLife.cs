using UnityEngine;

namespace Game.Life
{
    public class EnemyLife : AbstractLifeController
    {
        public System.Action OnEnemyDied;

        protected override void Die()
        {
            base.Die();
            Debug.Log($"[EnemyLife] {name} died.");
            OnEnemyDied?.Invoke();
        }
    }
}