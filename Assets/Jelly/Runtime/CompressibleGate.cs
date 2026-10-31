using UnityEngine;

namespace Jelly
{
    public class CompressibleGate : GateBase
    {
        public float baseHalfWidth = 0.30f;
        public float maxExpandedHalfWidth = 0.58f;
        public float requiredForceScore = 350f;

        public override GateEvaluationResult Evaluate(GateCandidateState candidate)
        {
            float forceScore = candidate.Mass * Mathf.Max(1f, candidate.ApproachSpeed);
            float t = Mathf.Clamp01(forceScore / Mathf.Max(1f, requiredForceScore));
            float effectiveHalfWidth = Mathf.Lerp(baseHalfWidth, maxExpandedHalfWidth, t);
            return GateClearanceEvaluator.EvaluateNarrowSlot(candidate, effectiveHalfWidth, maxExpandedHalfWidth);
        }
    }
}
