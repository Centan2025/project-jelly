using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jelly
{
    public class JellyMergeController : MonoBehaviour
    {
        public JellySplitController splitController;
        public JellyFragmentRegistry registry;
        public JellyFragmentPool pool;
        public float mergeDistancePadding = 0.35f;
        public float maxRelativeSpeed = 9f;
        public float minTimeSinceSplit = 1.2f;
        public bool autoMergeOnProximity = false;

        public event Action<JellyFragment, float> OnMerged;

        private void Awake() => EnsureComponents();

        private void EnsureComponents()
        {
            if (splitController == null) splitController = GetComponent<JellySplitController>();
            if (registry == null) registry = GetComponent<JellyFragmentRegistry>() ?? (splitController != null ? splitController.registry : null);
            if (pool == null) pool = GetComponent<JellyFragmentPool>() ?? (splitController != null ? splitController.pool : null);
        }

        private void Update()
        {
            EnsureComponents();
            if (splitController != null) splitController.EnsureSetup();
            if (registry == null || registry.ActiveCount < 2) return;

            var kb = Keyboard.current;
            bool keyMerge = kb != null && (kb.eKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame);
            bool adapterMerge = splitController != null
                                && splitController.primaryController != null
                                && splitController.primaryController.inputAdapter != null
                                && splitController.primaryController.inputAdapter.Current.MergePressed;

            if (keyMerge || adapterMerge)
            {
                TryMergeIfEligible(forceMergeByDistance: true);
            }
        }

        private void FixedUpdate()
        {
            EnsureComponents();
            if (splitController == null || registry == null || registry.ActiveCount < 2) return;
            bool manualReq = splitController.primaryController != null
                             && splitController.primaryController.inputAdapter != null
                             && splitController.primaryController.inputAdapter.Current.MergePressed;

            if (manualReq || autoMergeOnProximity)
                TryMergeIfEligible(manualReq);
        }

        public bool CanMerge(JellyFragment a, JellyFragment b, bool forceMergeByDistance = false)
        {
            if (a == null || b == null || a == b || !a.IsActiveFragment || !b.IsActiveFragment) return false;

            if (forceMergeByDistance)
                return true;

            if (splitController != null && Time.time - splitController.LastSplitTime < minTimeSinceSplit) return false;

            float rA = a.MassController != null ? a.MassController.CurrentRadius : 0.5f;
            float rB = b.MassController != null ? b.MassController.CurrentRadius : 0.5f;
            float dist = Vector3.Distance(a.transform.position, b.transform.position);
            if (dist > rA + rB + mergeDistancePadding) return false;

            if (a.PhysicsBody?.Body != null && b.PhysicsBody?.Body != null)
            {
                float relSpeed = (a.PhysicsBody.Body.linearVelocity - b.PhysicsBody.Body.linearVelocity).magnitude;
                if (relSpeed > maxRelativeSpeed) return false;
            }
            return true;
        }

        public bool TryMergeIfEligible(bool forceMergeByDistance = false)
        {
            EnsureComponents();
            if (splitController != null) splitController.EnsureSetup();
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

            if (primary.Controller.inputAdapter != null)
                primary.Controller.inputAdapter.ClearSimulatedInput();

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
