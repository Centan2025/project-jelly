using UnityEngine;

namespace Jelly
{
    public class SpringLauncher : MonoBehaviour
    {
        public Vector3 launchVelocity = new Vector3(0f, 7.5f, 11f);

        public bool Launch(JellyPhysicsBody body)
        {
            if (body == null) return false;
            body.InitializeComponents();
            if (body.Body == null) return false;
            body.Body.linearVelocity = launchVelocity;
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var pb = other.GetComponentInParent<JellyPhysicsBody>();
            if (pb != null) Launch(pb);
        }
    }
}
