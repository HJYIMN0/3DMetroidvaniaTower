using UnityEngine;
using Game.Input;

namespace Game.Movement
{
    public class _DebugMoveProbe : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private Rigidbody rb;

        [Header("Forced test — spunta per testare la scrittura diretta")]
        [SerializeField] private bool forceVelocity = false;
        [SerializeField] private Vector3 forcedVelocity = new Vector3(0, 0, 5);

        private int _frames;

        private void Awake()
        {
            if (mover == null) mover = GetComponent<PlayerMover>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            _frames++;

            // Test di scrittura forzata: se attivo, riscrive la velocity ogni frame
            if (forceVelocity && rb != null)
                rb.linearVelocity = forcedVelocity;

            if (_frames % 30 != 0) return;

            Vector2 raw = inputReader != null ? inputReader.MoveInput : Vector2.zero;
            Vector3 mw = mover != null ? mover.MoveInputWorld : Vector3.zero;
            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
            bool grounded = mover != null && mover.IsGrounded;
            var moverRb = mover != null ? mover.Rigidbody : null;
            bool same = ReferenceEquals(rb, moverRb);   // <-- niente GetInstanceID

            int rbCount = GetComponents<Rigidbody>().Length;
            int moverCount = GetComponents<PlayerMover>().Length;

            Debug.Log(
                $"[PROBE] input=({raw.x:F2},{raw.y:F2})  " +
                $"MoveWorld=({mw.x:F2},{mw.y:F2},{mw.z:F2})  " +
                $"rb.vel=({vel.x:F2},{vel.y:F2},{vel.z:F2})  " +
                $"grounded={grounded}  " +
                $"| rbCount={rbCount} moverCount={moverCount}  " +
                $"| drag={rb.linearDamping} angDrag={rb.angularDamping} useGravity={rb.useGravity} kinematic={rb.isKinematic} constraints={rb.constraints}"
            );
        }
    }
}