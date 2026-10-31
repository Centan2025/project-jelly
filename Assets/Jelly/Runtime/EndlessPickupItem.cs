using UnityEngine;

namespace Jelly
{
    public enum EndlessPickupType
    {
        Grow,
        Shrink,
        Split,
        Merge
    }

    public class EndlessPickupItem : MonoBehaviour
    {
        public EndlessPickupType pickupType = EndlessPickupType.Grow;
        public float amount = 30f;
        public float spinSpeed = 110f;
        private bool _consumed;

        private void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_consumed) return;
            var runner = other.GetComponentInParent<EndlessTrackManager>();
            if (runner == null) runner = FindAnyObjectByType<EndlessTrackManager>();
            if (runner == null) return;

            var jc = other.GetComponentInParent<JellyController>();
            if (jc == null) return;

            _consumed = true;
            runner.ApplyPickup(pickupType, amount);
            gameObject.SetActive(false);
        }
    }
}
