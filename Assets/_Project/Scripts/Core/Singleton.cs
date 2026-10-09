using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Manual generic singleton for MonoBehaviours. Call base.Awake() in overrides.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        [SerializeField] protected bool persistAcrossScenes = false;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Singleton] Duplicate {typeof(T).Name}, destroying {name}.");
                Destroy(gameObject);
                return;
            }
            Instance = (T)(object)this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}