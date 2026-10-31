using UnityEngine;

namespace Jelly
{
    public enum EndlessPickupType
    {
        Grow,
        Shrink,
        Split,
        Merge,
        SpeedBoost
    }

    public class EndlessPickupItem : MonoBehaviour
    {
        public EndlessPickupType pickupType = EndlessPickupType.Grow;
        public float amount = 30f;
        public float spinSpeed = 125f;
        public float trackZ;
        private bool _consumed;
        private Vector3 _basePos;
        private bool _baseCaptured;
        private float _phaseOffset;

        public bool IsConsumed => _consumed;

        private void Start()
        {
            _basePos = transform.position;
            _baseCaptured = true;
            _phaseOffset = transform.position.z * 0.35f + transform.position.x;
        }

        private void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            if (_baseCaptured && pickupType != EndlessPickupType.SpeedBoost)
            {
                float bob = Mathf.Sin(Time.time * 3.8f + _phaseOffset) * 0.14f;
                transform.position = _basePos + Vector3.up * bob;
            }
        }

        public string GetShortBadgeText()
        {
            switch (pickupType)
            {
                case EndlessPickupType.Grow: return $"+{amount:F0} BÜYÜ";
                case EndlessPickupType.Shrink: return $"-{amount:F0} KÜÇÜL";
                case EndlessPickupType.Split: return "÷2 BÖLÜN";
                case EndlessPickupType.Merge: return ">< BİRLEŞ";
                case EndlessPickupType.SpeedBoost: return "⚡ HIZLAN";
                default: return "";
            }
        }

        public Color GetBadgeColor()
        {
            switch (pickupType)
            {
                case EndlessPickupType.Grow: return new Color(0.18f, 0.92f, 0.45f);
                case EndlessPickupType.Shrink: return new Color(0.98f, 0.28f, 0.38f);
                case EndlessPickupType.Split: return new Color(0.75f, 0.35f, 1.00f);
                case EndlessPickupType.Merge: return new Color(1.00f, 0.82f, 0.18f);
                case EndlessPickupType.SpeedBoost: return new Color(0.20f, 0.88f, 1.00f);
                default: return Color.white;
            }
        }

        public bool TryCollect(EndlessTrackManager runner)
        {
            if (_consumed || runner == null) return false;
            _consumed = true;
            runner.ApplyPickup(pickupType, amount, transform.position);
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
