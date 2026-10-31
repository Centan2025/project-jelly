using UnityEngine;

namespace Jelly
{
    public class DualFragmentSwitchGroup : MonoBehaviour
    {
        public WeightedPressurePlate switchA;
        public WeightedPressurePlate switchB;
        public ObstacleStateController targetObstacle;
        public bool latchWhenBothActive = true;
        public bool BothActivated { get; private set; }

        private void FixedUpdate() => EvaluateState();

        public bool EvaluateState()
        {
            bool a = switchA != null && switchA.IsActivated;
            bool b = switchB != null && switchB.IsActivated;
            if (a && b) BothActivated = true;
            else if (!latchWhenBothActive) BothActivated = false;

            if (targetObstacle != null)
                targetObstacle.SetOpen(BothActivated);
            return BothActivated;
        }
    }
}
