using UnityEngine;

namespace Jelly
{
    public class MovingPlatform : MonoBehaviour
    {
        public Vector3 pointA;
        public Vector3 pointB = new Vector3(4f, 0f, 0f);
        public float speed = 2.5f;
        private float _t;

        private void Start()
        {
            if (pointA == Vector3.zero && pointB == new Vector3(4f, 0f, 0f))
            {
                pointA = transform.position;
                pointB = transform.position + new Vector3(4f, 0f, 0f);
            }
        }

        public void StepPlatform(float dt)
        {
            _t += dt * speed;
            float pingPong = Mathf.PingPong(_t, 1f);
            transform.position = Vector3.Lerp(pointA, pointB, pingPong);
        }

        private void FixedUpdate() => StepPlatform(Time.fixedDeltaTime);
    }
}
