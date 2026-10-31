using UnityEngine;

namespace Jelly
{
    public class MassSensitiveBridge : MonoBehaviour
    {
        public float maxSafeMass = 125f;
        public Collider bridgeCollider;
        public bool IsCollapsed { get; private set; }

        private void Awake()
        {
            if (bridgeCollider == null) bridgeCollider = GetComponent<Collider>();
        }

        public bool EvaluateLoad(float mass)
        {
            if (mass > maxSafeMass)
            {
                IsCollapsed = true;
                if (bridgeCollider != null) bridgeCollider.enabled = false;
            }
            return !IsCollapsed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc != null) EvaluateLoad(mc.CurrentMass);
        }
    }
}
