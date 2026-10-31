using System;
using UnityEngine;

namespace Jelly
{
    public struct GateCandidateState
    {
        public float Mass;
        public float Radius;
        public float MaxLateralCompressionRatio;
        public float MaxVerticalCompressionRatio;
        public float ApproachSpeed;
        public bool IsFragment;
        public int ActiveFragmentCount;
    }

    public struct GateEvaluationResult
    {
        public bool CanPass;
        public float RequiredLateralCompression;
        public float RequiredVerticalCompression;
        public string Reason;
    }

    public static class GateClearanceEvaluator
    {
        public static GateEvaluationResult EvaluateCircular(GateCandidateState candidate, float holeRadius, float tolerance = 0.015f)
        {
            bool fits = candidate.Radius <= holeRadius + tolerance;
            return new GateEvaluationResult
            {
                CanPass = fits,
                RequiredLateralCompression = 1f,
                RequiredVerticalCompression = 1f,
                Reason = fits ? "Fits circular aperture" : $"Radius {candidate.Radius:F2} exceeds hole {holeRadius:F2}"
            };
        }

        public static GateEvaluationResult EvaluateRectangular(GateCandidateState candidate, float halfWidth, float halfHeight, float tolerance = 0.015f)
        {
            bool fits = candidate.Radius <= halfWidth + tolerance && candidate.Radius <= halfHeight + tolerance;
            return new GateEvaluationResult
            {
                CanPass = fits,
                RequiredLateralCompression = 1f,
                RequiredVerticalCompression = 1f,
                Reason = fits ? "Fits rectangular gate" : "Exceeds rectangular dimensions"
            };
        }

        public static GateEvaluationResult EvaluateNarrowSlot(GateCandidateState candidate, float slotHalfWidth, float slotHalfHeight, float tolerance = 0.015f)
        {
            float reqLat = Mathf.Clamp((slotHalfWidth + tolerance) / Mathf.Max(0.01f, candidate.Radius), 0.1f, 1f);
            float reqVert = Mathf.Clamp((slotHalfHeight + tolerance) / Mathf.Max(0.01f, candidate.Radius), 0.1f, 1f);
            float minLat = candidate.MaxLateralCompressionRatio > 0f ? candidate.MaxLateralCompressionRatio : 0.62f;
            float minVert = candidate.MaxVerticalCompressionRatio > 0f ? candidate.MaxVerticalCompressionRatio : 0.65f;

            bool fits = reqLat >= minLat && reqVert >= minVert;
            return new GateEvaluationResult
            {
                CanPass = fits,
                RequiredLateralCompression = reqLat,
                RequiredVerticalCompression = reqVert,
                Reason = fits ? "Can squeeze through slot" : $"Required compression ({reqLat:F2}, {reqVert:F2}) exceeds limits ({minLat:F2}, {minVert:F2})"
            };
        }

        public static GateEvaluationResult EvaluateMassThreshold(GateCandidateState candidate, float minMass, float maxMass)
        {
            bool fits = candidate.Mass >= minMass - 0.01f && candidate.Mass <= maxMass + 0.01f;
            return new GateEvaluationResult
            {
                CanPass = fits,
                RequiredLateralCompression = 1f,
                RequiredVerticalCompression = 1f,
                Reason = fits ? "Mass within threshold" : $"Mass {candidate.Mass:F1} outside [{minMass:F1}, {maxMass:F1}]"
            };
        }

        public static GateEvaluationResult EvaluateSplit(GateCandidateState candidate, float maxAllowedSingleMass, float channelHalfWidth, bool requireSplitState = true)
        {
            bool splitOk = !requireSplitState || candidate.IsFragment || candidate.ActiveFragmentCount >= 2;
            bool massOk = candidate.Mass <= maxAllowedSingleMass + 0.01f;
            bool geomOk = candidate.Radius <= channelHalfWidth + 0.02f;
            bool canPass = splitOk && massOk && geomOk;
            return new GateEvaluationResult
            {
                CanPass = canPass,
                RequiredLateralCompression = 1f,
                RequiredVerticalCompression = 1f,
                Reason = canPass ? "Fragment fits split gate" : "Must split into smaller fragments to pass"
            };
        }
    }

    public abstract class GateBase : MonoBehaviour
    {
        public Collider barrierCollider;
        public ObstacleStateController stateController;
        public event Action<GateBase, bool> OnGateEvaluated;

        protected virtual void Awake()
        {
            if (barrierCollider == null) barrierCollider = GetComponent<Collider>();
            if (stateController == null) stateController = GetComponent<ObstacleStateController>();
        }

        public abstract GateEvaluationResult Evaluate(GateCandidateState candidate);

        public GateCandidateState BuildCandidateState(JellyMassController massController)
        {
            float mass = massController != null ? massController.CurrentMass : 100f;
            float radius = massController != null ? massController.CurrentRadius : 0.5f;
            float maxLat = massController != null && massController.config != null ? massController.config.maxLateralCompressionRatio : 0.62f;
            float maxVert = massController != null && massController.config != null ? massController.config.maxVerticalCompressionRatio : 0.65f;
            var rb = massController != null ? massController.GetComponent<Rigidbody>() : null;
            var frag = massController != null ? massController.GetComponent<JellyFragment>() : null;
            return new GateCandidateState
            {
                Mass = mass,
                Radius = radius,
                MaxLateralCompressionRatio = maxLat,
                MaxVerticalCompressionRatio = maxVert,
                ApproachSpeed = rb != null ? rb.linearVelocity.magnitude : 0f,
                IsFragment = frag != null && frag.IsActiveFragment,
                ActiveFragmentCount = JellyFragmentRegistry.Instance != null ? JellyFragmentRegistry.Instance.ActiveCount : 1
            };
        }

        public bool TryProcessCandidate(JellyMassController massController)
        {
            if (massController == null) return false;
            GateCandidateState state = BuildCandidateState(massController);
            GateEvaluationResult res = Evaluate(state);

            var col = massController.GetComponent<Collider>();
            if (barrierCollider == null) barrierCollider = GetComponent<Collider>();
            if (barrierCollider != null && col != null)
                Physics.IgnoreCollision(col, barrierCollider, res.CanPass);

            if (res.CanPass && (res.RequiredLateralCompression < 0.999f || res.RequiredVerticalCompression < 0.999f))
            {
                var morph = massController.GetComponent<JellyMorphController>();
                if (morph != null)
                {
                    morph.SetSqueeze(res.RequiredLateralCompression, res.RequiredVerticalCompression, massController.config);
                    massController.StepMassInterpolation(0.02f);
                }
            }

            if (stateController == null) stateController = GetComponent<ObstacleStateController>();
            if (stateController != null)
                stateController.SetOpen(res.CanPass);

            OnGateEvaluated?.Invoke(this, res.CanPass);
            return res.CanPass;
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc != null) TryProcessCandidate(mc);
        }

        protected virtual void OnCollisionStay(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc != null) TryProcessCandidate(mc);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) TryProcessCandidate(mc);
        }

        protected virtual void OnTriggerStay(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) TryProcessCandidate(mc);
        }
    }
}
