using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public class WeightedPressurePlate : MonoBehaviour
    {
        public float requiredMass = 110f;
        public bool stayOpenOncePressed = true;
        public ObstacleStateController targetObstacle;
        public bool IsActivated { get; private set; }
        public float CurrentDetectedMass { get; private set; }

        private readonly HashSet<JellyMassController> _occupants = new HashSet<JellyMassController>();

        public void EvaluateMass(float mass)
        {
            CurrentDetectedMass = mass;
            bool active = mass >= requiredMass - 0.01f;
            if (active || !stayOpenOncePressed)
            {
                IsActivated = active;
                if (targetObstacle != null)
                    targetObstacle.SetOpen(IsActivated);
            }
        }

        private void Recalculate()
        {
            float sum = 0f;
            _occupants.RemoveWhere(x => x == null || !x.gameObject.activeInHierarchy);
            foreach (var occ in _occupants)
                sum += occ.CurrentMass;
            EvaluateMass(sum);
        }

        private void OnTriggerEnter(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) { _occupants.Add(mc); Recalculate(); }
        }

        private void OnTriggerStay(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) { _occupants.Add(mc); Recalculate(); }
        }

        private void OnTriggerExit(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) { _occupants.Remove(mc); Recalculate(); }
        }
    }
}
