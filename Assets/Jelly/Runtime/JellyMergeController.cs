using System;
using UnityEngine;

namespace Jelly
{
    public class JellyMergeController : MonoBehaviour
    {
        public JellySplitController splitController;
        public JellyFragmentRegistry registry;
        public JellyFragmentPool pool;
        public float mergeDistancePadding = 0.35f;
        public float maxRelativeSpeed = 9f;
        public float minTimeSinceSplit = 0.55f;
        public bool autoMergeOnProximity = true;

        public event Action<JellyFragment, float> OnMerged;

        private void Awake()
        {
            if (splitController == null) splitController = GetComponent<JellySplitController>();
            if (registry == null) registry = GetComponent<JellyFragmentRegistry>();
            if (pool == null) pool = GetComponent<JellyFragmentPool>();
        }

        private void FixedUpdate()
        {
            if (splitController == null || registry == null || registry.ActiveCount < 2) return;
            bool manualReq = splitController.primaryController != null
                             && splitController.primaryController.inputAdapter != null
                             && splitController.primaryController.inputAdapter.Current.MergePressed;

            if (manualReq || autoMergeOnProximity)
                TryMergeIfEligible(manualReq);
        }

        public bool CanMerge(JellyFragment a, JellyFragment b, bool ignoreVelocity = false)
        {
            if (a == null || b == null || a == b || !a.IsActiveFragment || !b.IsActiveFragment) return false;
            if (splitController != null && Time.time - splitController.LastSplitTime < minTimeSinceSplit) return false;

            float rA = a.MassController != null ? a.MassController.CurrentRadius : 0.5f;
            float rB = b.MassController != null ? b.MassController.CurrentRadius : 0.5f;
            float dist = Vector3.Distance(a.transform.position, b.transform.position);
            if (dist > rA + rB + mergeDistancePadding) return false;

            if (!ignoreVelocity && a.PhysicsBody?.Body != null && b.PhysicsBody?.Body != null)
            {
                float relSpeed = (a.PhysicsBody.Body.linearVelocity - b.PhysicsBody.Body.linearVelocity).magnitude;
                if (relSpeed > maxRelativeSpeed) return false;
            }
            return true;
        }

        public bool TryMergeIfEligible(bool forceMergeByDistance = false)
        {
            if (registry == null || registry.ActiveCount < 2) return false;
            var a = registry.ActiveFragments[0];
            var b = registry.ActiveFragments[1];
            if (!CanMerge(a, b, forceMergeByDistance)) return false;
            return ExecuteMerge(a, b);
        }

        public bool ExecuteMerge(JellyFragment primary, JellyFragment secondary)
        {
            if (primary == null || secondary == null || primary == secondary) return false;
            float mA = primary.MassController.CurrentMass;
            float mB = secondary.MassController.CurrentMass;
            float mergedMass = JellyMassConservation.ComputeMergedMass(mA, mB);

            Vector3 vA = primary.PhysicsBody.Body != null ? primary.PhysicsBody.Body.linearVelocity : Vector3.zero;
            Vector3 vB = secondary.PhysicsBody.Body != null ? secondary.PhysicsBody.Body.linearVelocity : Vector3.zero;
            Vector3 mergedVel = (vA * mA + vB * mB) / Mathf.Max(1f, mergedMass);
            Vector3 mid = (primary.transform.position * mA + secondary.transform.position * mB) / Mathf.Max(1f, mergedMass);

            registry.Unregister(secondary);
            if (pool != null) pool.Release(secondary);
            else secondary.gameObject.SetActive(false);

            float newRadius = JellyMassModel.ComputeRadius(mergedMass, primary.MassController.Model.BaseMass, primary.MassController.Model.BaseRadius);
            Vector3 safePos = JellySplitController.FindSafePosition(mid, newRadius);

            primary.Controller.ResetState(safePos, Quaternion.identity, mergedMass);
            if (primary.PhysicsBody.Body != null)
                primary.PhysicsBody.Body.linearVelocity = mergedVel;

            if (splitController != null)
                splitController.LastSplitTime = Time.time;

            OnMerged?.Invoke(primary, mergedMass);
            return true;
        }
    }
}
