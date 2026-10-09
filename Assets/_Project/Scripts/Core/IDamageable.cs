namespace Game.Core
{
    public interface IDamageable
    {
        void ApplyDamage(DamageInfo info);
        bool IsAlive { get; }
        bool IsInvulnerable { get; set; }
        bool IsExposedToCritical { get; }
        UnityEngine.Transform Transform { get; }
    }
}