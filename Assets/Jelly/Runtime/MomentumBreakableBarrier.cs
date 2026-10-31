using UnityEngine;

namespace Jelly
{
    public class MomentumBreakableBarrier : MonoBehaviour
    {
        public float requiredMomentum = 850f;
        public ObstacleStateController stateController;
        public bool IsBroken { get; private set; }

        private void Awake()
        {
            if (stateController == null) stateController = GetComponent<ObstacleStateController>();
        }

        public bool EvaluateImpact(float gameplayMass, float impactSpeed)
        {
            float momentum = gameplayMass * impactSpeed;
            if (momentum >= requiredMomentum)
            {
                IsBroken = true;
                if (stateController != null) stateController.SetOpen(true);
                else gameObject.SetActive(false);
            }
            return IsBroken;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc == null) return;
            EvaluateImpact(mc.CurrentMass, collision.relativeVelocity.magnitude);
        }
    }
}
