using UnityEngine;

namespace Game.Core
{
    public struct DamageInfo
    {
        public int amount;
        public int poseDamage;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public GameObject source;
        public DamageType type;
        public bool isCritical;
        public bool ignoreInvulnerability;

        public static DamageInfo Basic(int amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)
        {
            return new DamageInfo
            {
                amount = amount,
                poseDamage = 0,
                source = source,
                hitPoint = hitPoint,
                hitNormal = hitNormal,
                type = DamageType.Physical,
                isCritical = false,
                ignoreInvulnerability = false
            };
        }
    }
}