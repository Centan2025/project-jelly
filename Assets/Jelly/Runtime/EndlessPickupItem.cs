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

        public bool IsConsumed => _consumed;

        private void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        public bool TryCollect(EndlessTrackManager runner)
        {
            if (_consumed || runner == null) return false;
            _consumed = true;
            runner.ApplyPickup(pickupType, amount);
            gameObject.SetActive(false);
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_consumed || other == null) return;
            bool isPlayerPiece = other.GetComponentInParent<JellyController>() != null
                                 || other.name.Contains("Jelly");
            if (!isPlayerPiece) return;

            var runner = FindAnyObjectByType<EndlessTrackManager>();
            TryCollect(runner);
        }
    }
}
