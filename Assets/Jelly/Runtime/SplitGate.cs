namespace Jelly
{
    public class SplitGate : GateBase
    {
        public float maxFragmentMass = 60f;
        public float channelHalfWidth = 0.44f;
        public bool requireSplitState = true;

        public override GateEvaluationResult Evaluate(GateCandidateState candidate) =>
            GateClearanceEvaluator.EvaluateSplit(candidate, maxFragmentMass, channelHalfWidth, requireSplitState);
    }
}
