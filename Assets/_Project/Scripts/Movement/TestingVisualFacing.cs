using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// TEST HELPER ONLY. Rotates a visual child toward the horizontal velocity so you can see turns and
    /// direction changes in the test scene. It never rotates the Rigidbody. Remove it when the real rotation class arrives.
    /// </summary>
    public class TestVisualFacing : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Rigidbody del player. Se vuoto viene preso con GetComponent.")]
        [SerializeField] private Rigidbody body;
        [Tooltip("Figlio visivo da ruotare (es. la capsule 'Model'). NON il GameObject col Rigidbody.")]
        [SerializeField] private Transform visual;

        [Header("Tuning")]
        [Tooltip("Velocità di rotazione del visual (gradi/s).")]
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [Tooltip("Sotto questa velocità orizzontale (m/s) il visual non ruota.")]
        [SerializeField, Min(0f)] private float minSpeed = 0.5f;

        private void Reset() { body = GetComponent<Rigidbody>(); }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
        }

        private void LateUpdate()
        {
            if (body == null || visual == null) return;

            Vector3 v = body.linearVelocity;
            v.y = 0f;
            if (v.magnitude < minSpeed) return;

            Quaternion target = Quaternion.LookRotation(v.normalized, Vector3.up);
            visual.rotation = Quaternion.RotateTowards(visual.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}