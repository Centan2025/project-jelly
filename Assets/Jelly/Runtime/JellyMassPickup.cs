using UnityEngine;

namespace Jelly
{
    [RequireComponent(typeof(Collider))]
    public class JellyMassPickup : MonoBehaviour
    {
        public float massGain = 20f;
        public bool consumeOnTrigger = true;
        public bool Consumed { get; private set; }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public bool TryApply(JellyMassController massController)
        {
            if (Consumed || massController == null) return false;
            float before = massController.CurrentMass;
            float after = massController.ChangeMass(Mathf.Abs(massGain));
            if (after <= before && Mathf.Approximately(before, massController.Model.MaxMass))
                return false;
            if (consumeOnTrigger)
            {
                Consumed = true;
                gameObject.SetActive(false);
            }
            return true;
        }

        public void ResetPickup()
        {
            Consumed = false;
            gameObject.SetActive(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) TryApply(mc);
        }
    }
}
