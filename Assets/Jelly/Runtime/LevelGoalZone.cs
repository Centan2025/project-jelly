using UnityEngine;

namespace Jelly
{
    public class LevelGoalZone : MonoBehaviour
    {
        public LevelManager levelManager;

        private void OnTriggerEnter(Collider other)
        {
            var jc = other.GetComponentInParent<JellyController>();
            if (jc != null && levelManager != null)
                levelManager.NotifyGoalZoneEntered();
        }

        private void OnTriggerStay(Collider other)
        {
            var jc = other.GetComponentInParent<JellyController>();
            if (jc != null && levelManager != null && !levelManager.IsLevelCompleted)
                levelManager.NotifyGoalZoneEntered();
        }
    }
}
