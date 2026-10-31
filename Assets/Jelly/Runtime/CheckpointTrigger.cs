using UnityEngine;

namespace Jelly
{
    public class CheckpointTrigger : MonoBehaviour
    {
        public string checkpointId;
        public CheckpointManager manager;
        public bool Activated { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            var jc = other.GetComponentInParent<JellyController>();
            if (jc != null && manager != null)
            {
                Activated = true;
                float m = jc.massController != null ? jc.massController.CurrentMass : 100f;
                manager.ActivateCheckpoint(checkpointId, transform.position + Vector3.up * 0.6f, m);
            }
        }
    }
}
