namespace Jelly
{
    public class MassThresholdGate : GateBase
    {
        public float minAllowedMass = 0f;
        public float maxAllowedMass = 85f;
        public float apertureRadius = 0.65f;

        public override GateEvaluationResult Evaluate(GateCandidateState candidate)
        {
            var massRes = GateClearanceEvaluator.EvaluateMassThreshold(candidate, minAllowedMass, maxAllowedMass);
            if (!massRes.CanPass) return massRes;
            return GateClearanceEvaluator.EvaluateCircular(candidate, apertureRadius);
        }
    }
}
