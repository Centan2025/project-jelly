namespace Jelly
{
    public class NarrowSlotGate : GateBase
    {
        public float slotHalfWidth = 0.34f;
        public float slotHalfHeight = 0.55f;
        public override GateEvaluationResult Evaluate(GateCandidateState candidate) =>
            GateClearanceEvaluator.EvaluateNarrowSlot(candidate, slotHalfWidth, slotHalfHeight);
    }
}
