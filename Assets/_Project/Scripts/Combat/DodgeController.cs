using System;
using UnityEngine;
using Game.Movement;

namespace Game.Combat
{
    /// <summary>
    /// TEMPORARY ADAPTER. The dodge logic now lives in Game.Movement.DodgeModule; this component only forwards
    /// the old DodgeController API to it, so the combat code (AbstractCombatManager, PlayerLife, ...) keeps
    /// compiling and working without being edited. It keeps the same file and class name, so existing
    /// scene references stay valid. Delete it once every user of DodgeController points to DodgeModule.
    /// </summary>
    [RequireComponent(typeof(DodgeModule))]
    public class DodgeController : MonoBehaviour
    {
        private DodgeModule _module;

        private DodgeModule Module
        {
            get
            {
                if (_module == null) _module = GetComponent<DodgeModule>();
                return _module;
            }
        }

        private void Awake()
        {
            if (Module == null)
                Debug.LogError("[DodgeController] DodgeModule mancante sul Player: aggiungilo, la schivata non funzionerà.");
        }

        public bool IsDodging => Module != null && Module.IsDodging;
        public bool IsInvulnerable => Module != null && Module.IsInvulnerable;
        public bool CancelVerticalVelocity => Module != null && Module.CancelVerticalVelocity;
        public Vector3 DodgeDirection => Module != null ? Module.DodgeDirection : Vector3.zero;
        public float DodgeDistance => Module != null ? Module.DodgeDistance : 0f;

        public event Action OnDodgeStart
        {
            add { if (Module != null) Module.OnDodgeStart += value; }
            remove { if (Module != null) Module.OnDodgeStart -= value; }
        }

        public event Action OnDodgeEnd
        {
            add { if (Module != null) Module.OnDodgeEnd += value; }
            remove { if (Module != null) Module.OnDodgeEnd -= value; }
        }

        /// <summary>Same signature as before. Zero direction = DodgeModule picks the move input, else a backstep.</summary>
        public void BeginDodge(Vector3 direction, Life.AbstractLifeController life)
        {
            if (Module != null) Module.BeginDodge(direction);
        }
    }
}