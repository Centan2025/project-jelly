namespace Jelly
{
    public class RectangularGate : GateBase
    {
        public float halfWidth = 0.55f;
        public float halfHeight = 0.55f;
        public override GateEvaluationResult Evaluate(GateCandidateState candidate) =>
            GateClearanceEvaluator.EvaluateRectangular(candidate, halfWidth, halfHeight);
    }
}
