using System;
using UnityEngine;

namespace Jelly
{
    public class JellySplitController : MonoBehaviour
    {
        public JellyController primaryController;
        public JellyFragmentRegistry registry;
        public JellyFragmentPool pool;
        public JellyFragmentInputRouter router;
        public float minFragmentMass = 20f;
        public float defaultSplitRatio = 0.5f;
        public float splitImpulse = 3.2f;
        public float splitCooldown = 0.45f;
        public int maxFragments = 2;

        public float LastSplitTime { get; set; } = -10f;
        public bool IsSplit => registry != null && registry.ActiveCount > 1;
        public event Action<JellyFragment, JellyFragment> OnSplitPerformed;

        private void Awake() => EnsureSetup();

        public void EnsureSetup()
        {
            if (primaryController == null) primaryController = GetComponent<JellyController>();
            if (registry == null)
            {
                registry = FindAnyObjectByType<JellyFragmentRegistry>();
                if (registry == null) registry = gameObject.AddComponent<JellyFragmentRegistry>();
            }
            if (pool == null)
            {
                pool = FindAnyObjectByType<JellyFragmentPool>();
                if (pool == null) pool = gameObject.AddComponent<JellyFragmentPool>();
            }
            if (router == null)
            {
                router = FindAnyObjectByType<JellyFragmentInputRouter>();
                if (router == null) router = gameObject.AddComponent<JellyFragmentInputRouter>();
            }

            if (primaryController != null)
            {
                var primaryFrag = primaryController.GetComponent<JellyFragment>();
                if (primaryFrag == null) primaryFrag = primaryController.gameObject.AddComponent<JellyFragment>();
                primaryFrag.FragmentId = 0;
                registry.Register(primaryFrag);
                router.primaryInput = primaryController.inputAdapter;
                router.registry = registry;
            }
        }

        private void Update()
        {
            if (primaryController != null && primaryController.inputAdapter != null && primaryController.inputAdapter.Current.SplitPressed)
                TrySplit(defaultSplitRatio);
        }

        public bool CanSplit(float ratio = 0.5f)
        {
            EnsureSetup();
            if (Time.time - LastSplitTime < splitCooldown) return false;
            if (registry.ActiveCount >= maxFragments) return false;
            if (primaryController == null || primaryController.massController == null) return false;
            return JellyMassConservation.TryComputeSplitMasses(
                primaryController.massController.CurrentMass,
                ratio,
                minFragmentMass,
                out _,
                out _);
        }

        public bool TrySplit(float ratio = 0.5f)
        {
            EnsureSetup();
            if (!CanSplit(ratio)) return false;

            float totalMass = primaryController.massController.CurrentMass;
            if (!JellyMassConservation.TryComputeSplitMasses(totalMass, ratio, minFragmentMass, out float massA, out float massB))
                return false;

            LastSplitTime = Time.time;
            var primaryFrag = primaryController.GetComponent<JellyFragment>();
            var secondFrag = pool.GetOrCreate(primaryController);
            secondFrag.FragmentId = 1;

            var extraSplit = secondFrag.GetComponent<JellySplitController>();
            if (extraSplit != null) { if (Application.isPlaying) Destroy(extraSplit); else DestroyImmediate(extraSplit); }
            var extraMerge = secondFrag.GetComponent<JellyMergeController>();
            if (extraMerge != null) { if (Application.isPlaying) Destroy(extraMerge); else DestroyImmediate(extraMerge); }

            primaryController.massController.SetMassImmediate(massA);
            secondFrag.MassController.SetMassImmediate(massB);

            float rA = primaryController.massController.CurrentRadius;
            float rB = secondFrag.MassController.CurrentRadius;
            Vector3 center = primaryController.transform.position;
            Vector3 right = Vector3.right;
            float sep = (rA + rB) * 0.65f + 0.08f;

            Vector3 posA = FindSafePosition(center - right * sep, rA);
            Vector3 posB = FindSafePosition(center + right * sep, rB);

            Vector3 baseVel = primaryController.physicsBody.Body != null ? primaryController.physicsBody.Body.linearVelocity : Vector3.zero;
            primaryController.ResetState(posA, Quaternion.identity, massA);
            secondFrag.Controller.ResetState(posB, Quaternion.identity, massB);

            if (primaryController.physicsBody.Body != null)
                primaryController.physicsBody.Body.linearVelocity = baseVel - right * splitImpulse;
            if (secondFrag.PhysicsBody.Body != null)
                secondFrag.PhysicsBody.Body.linearVelocity = baseVel + right * splitImpulse;

            secondFrag.Controller.OnFellIntoAbyss -= HandleFragmentAbyss;
            secondFrag.Controller.OnFellIntoAbyss += HandleFragmentAbyss;

            registry.Register(primaryFrag);
            registry.Register(secondFrag);
            router.RouteInput();

            OnSplitPerformed?.Invoke(primaryFrag, secondFrag);
            return true;
        }

        private void HandleFragmentAbyss(JellyController fallen)
        {
            var frag = fallen != null ? fallen.GetComponent<JellyFragment>() : null;
            if (frag != null && frag != primaryController.GetComponent<JellyFragment>())
            {
                registry.ReportFragmentLost(frag);
                pool.Release(frag);
            }
        }

        public static Vector3 FindSafePosition(Vector3 desired, float radius)
        {
            Vector3 candidate = desired;
            candidate.y = Mathf.Max(candidate.y, radius + 0.05f);
            Collider[] hits = Physics.OverlapSphere(candidate, radius * 0.9f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null || h.GetComponentInParent<JellyController>() != null) continue;
                if (h.bounds.size.y > 0.3f && h.bounds.center.y > candidate.y - radius * 0.5f)
                {
                    Vector3 pushDir = candidate - h.ClosestPoint(candidate);
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.0001f) pushDir = Vector3.back;
                    candidate += pushDir.normalized * (radius * 1.15f);
                }
            }
            return candidate;
        }
    }
}
