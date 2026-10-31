using UnityEngine;

namespace Jelly
{
    [RequireComponent(typeof(Rigidbody))]
    public class PushableBlock : MonoBehaviour
    {
        public float minimumPlayerMassToPush = 90f;
        public float blockMass = 2.2f;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = blockMass;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void OnCollisionEnter(Collision collision) => HandleContact(collision);
        private void OnCollisionStay(Collision collision) => HandleContact(collision);

        private void HandleContact(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc == null || _rb == null) return;
            bool canPush = mc.CurrentMass >= minimumPlayerMassToPush - 0.01f;
            _rb.isKinematic = !canPush;
        }
    }
}
