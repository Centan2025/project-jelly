using UnityEngine;

namespace Jelly
{
    [RequireComponent(typeof(Collider))]
    public class JellyMassDrain : MonoBehaviour
    {
        public float massLoss = 20f;
        public bool continuousDrain;
        public float drainPerSecond = 25f;
        public bool oneShot = true;
        public bool Triggered { get; private set; }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public bool TryApplyInstant(JellyMassController massController)
        {
            if (massController == null || (oneShot && Triggered)) return false;
            massController.ChangeMass(-Mathf.Abs(massLoss));
            if (oneShot) Triggered = true;
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (continuousDrain) return;
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) TryApplyInstant(mc);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!continuousDrain) return;
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) mc.ChangeMass(-Mathf.Abs(drainPerSecond) * Time.fixedDeltaTime);
        }
    }
}
