namespace Jelly
{
    public class CircularGate : GateBase
    {
        public float apertureRadius = 0.45f;
        public override GateEvaluationResult Evaluate(GateCandidateState candidate) =>
            GateClearanceEvaluator.EvaluateCircular(candidate, apertureRadius);
    }
}
