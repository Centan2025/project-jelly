using UnityEngine;

namespace Jelly
{
    public enum EndlessPickupType
    {
        Grow,
        Shrink,
        Split,
        Merge,
        SpeedBoost,
        Shield,
        Magnet,
        StarScore,
        SlowTime
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

        public void SetWorldPosition(Vector3 pos)
        {
            transform.position = pos;
            _basePos = pos;
            _baseCaptured = true;
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
                case EndlessPickupType.SpeedBoost: return "⚡ HIZ +%35";
                case EndlessPickupType.Shield: return "🛡 KALKAN";
                case EndlessPickupType.Magnet: return "🧲 MIKNATIS";
                case EndlessPickupType.StarScore: return "⭐ +500 PUAN";
                case EndlessPickupType.SlowTime: return "⏳ YAVAŞ ZAMAN";
                default: return "";
            }
        }

        public Color GetBadgeColor()
        {
            switch (pickupType)
            {
                case EndlessPickupType.Grow: return new Color(0.18f, 0.96f, 0.48f);
                case EndlessPickupType.Shrink: return new Color(1.00f, 0.30f, 0.38f);
                case EndlessPickupType.Split: return new Color(0.78f, 0.38f, 1.00f);
                case EndlessPickupType.Merge: return new Color(1.00f, 0.82f, 0.18f);
                case EndlessPickupType.SpeedBoost: return new Color(0.20f, 0.94f, 1.00f);
                case EndlessPickupType.Shield: return new Color(0.88f, 0.36f, 0.98f);
                case EndlessPickupType.Magnet: return new Color(0.32f, 0.68f, 1.00f);
                case EndlessPickupType.StarScore: return new Color(1.00f, 0.88f, 0.22f);
                case EndlessPickupType.SlowTime: return new Color(0.96f, 0.68f, 0.28f);
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
