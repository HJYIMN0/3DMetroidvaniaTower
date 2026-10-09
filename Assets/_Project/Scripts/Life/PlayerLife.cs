using UnityEngine;

namespace Game.Life
{
    public class PlayerLife : AbstractLifeController
    {
        protected override void Die()
        {
            base.Die();
            Debug.Log("[PlayerLife] Player died.");
            // TODO: trigger respawn / game over
        }
    }
}