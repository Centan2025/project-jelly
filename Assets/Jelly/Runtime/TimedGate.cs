using UnityEngine;

namespace Jelly
{
    public class TimedGate : GateBase
    {
        public float openDuration = 2.0f;
        public float closedDuration = 1.5f;
        private float _timer;

        public bool IsCurrentlyOpenWindow { get; private set; } = true;

        public void StepTimer(float dt)
        {
            _timer += dt;
            float cycle = Mathf.Max(0.2f, openDuration + closedDuration);
            float phase = _timer % cycle;
            IsCurrentlyOpenWindow = phase <= openDuration;
            if (stateController != null)
                stateController.SetOpen(IsCurrentlyOpenWindow);
            if (barrierCollider != null)
                barrierCollider.enabled = !IsCurrentlyOpenWindow;
        }

        private void FixedUpdate() => StepTimer(Time.fixedDeltaTime);

        public override GateEvaluationResult Evaluate(GateCandidateState candidate)
        {
            return new GateEvaluationResult
            {
                CanPass = IsCurrentlyOpenWindow,
                RequiredLateralCompression = 1f,
                RequiredVerticalCompression = 1f,
                Reason = IsCurrentlyOpenWindow ? "Timed gate open" : "Timed gate closed"
            };
        }
    }
}
