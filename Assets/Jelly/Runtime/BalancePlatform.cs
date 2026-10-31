using UnityEngine;

namespace Jelly
{
    public class BalancePlatform : MonoBehaviour
    {
        public float maxTiltAngle = 20f;
        public float tiltTorqueSensitivity = 0.08f;
        public float currentTilt { get; private set; }

        public void ApplyTorqueFromOffset(float offsetX, float mass, float dt)
        {
            float target = Mathf.Clamp(-offsetX * (mass / 100f) * maxTiltAngle * tiltTorqueSensitivity, -maxTiltAngle, maxTiltAngle);
            currentTilt = Mathf.MoveTowards(currentTilt, target, 45f * dt);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
        }

        private void OnCollisionStay(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc != null)
            {
                float localX = transform.InverseTransformPoint(mc.transform.position).x;
                ApplyTorqueFromOffset(localX, mc.CurrentMass, Time.fixedDeltaTime);
            }
        }
    }
}
