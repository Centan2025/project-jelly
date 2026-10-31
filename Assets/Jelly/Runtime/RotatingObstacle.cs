using UnityEngine;

namespace Jelly
{
    public class RotatingObstacle : MonoBehaviour
    {
        public Vector3 angularVelocityEuler = new Vector3(0f, 90f, 0f);

        public void StepRotation(float dt)
        {
            transform.Rotate(angularVelocityEuler * dt, Space.Self);
        }

        private void FixedUpdate() => StepRotation(Time.fixedDeltaTime);
    }
}
