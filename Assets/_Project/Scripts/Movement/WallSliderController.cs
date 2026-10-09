using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Wall slide: starts from minSlideSpeed, accelerates to maxSlideSpeed.
    /// </summary>
    public class WallSlideController : MonoBehaviour
    {
        [SerializeField] private float minSlideSpeed = 1.5f;
        [SerializeField] private float maxSlideSpeed = 6f;
        [SerializeField] private float slideAcceleration = 4f;
        [SerializeField] private float slideDurationMax = 5f;

        public bool IsSliding { get; private set; }
        private float _currentSpeed;
        private float _slideTimer;

        public void BeginSlide()
        {
            IsSliding = true;
            _currentSpeed = minSlideSpeed;
            _slideTimer = 0f;
        }

        public void StopSlide()
        {
            IsSliding = false;
        }

        public void TickSlide(Rigidbody rb, float dt)
        {
            if (!IsSliding) return;
            _slideTimer += dt;
            if (_slideTimer >= slideDurationMax) { StopSlide(); return; }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSlideSpeed, slideAcceleration * dt);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.3f, -_currentSpeed, rb.linearVelocity.z * 0.3f);
        }
    }
}