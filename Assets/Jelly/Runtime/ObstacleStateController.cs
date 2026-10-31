using UnityEngine;

namespace Jelly
{
    public class ObstacleStateController : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public Collider blockingCollider;
        public Transform visualDoor;
        public Vector3 openLocalOffset = new Vector3(0f, 2.5f, 0f);
        public float transitionSpeed = 8f;

        private Vector3 _closedLocalPos;
        private bool _initialized;

        private void Awake() => EnsureInit();

        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            if (blockingCollider == null) blockingCollider = GetComponent<Collider>();
            if (visualDoor != null) _closedLocalPos = visualDoor.localPosition;
        }

        public void SetOpen(bool open)
        {
            EnsureInit();
            IsOpen = open;
            if (blockingCollider != null)
                blockingCollider.enabled = !open;
        }

        private void Update()
        {
            if (visualDoor == null) return;
            EnsureInit();
            Vector3 target = IsOpen ? _closedLocalPos + openLocalOffset : _closedLocalPos;
            visualDoor.localPosition = Vector3.MoveTowards(visualDoor.localPosition, target, Time.deltaTime * transitionSpeed);
        }
    }
}
