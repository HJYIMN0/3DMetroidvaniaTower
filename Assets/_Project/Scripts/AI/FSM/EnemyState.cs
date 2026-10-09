namespace Game.AI
{
    public abstract class EnemyState
    {
        public virtual void Enter(EnemyBrain b) { }
        public virtual void Tick(EnemyBrain b, float dt) { }
        public virtual void Exit(EnemyBrain b) { }
    }
}