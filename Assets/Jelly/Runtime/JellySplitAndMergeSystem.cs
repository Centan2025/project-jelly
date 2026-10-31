using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public static class JellyMassConservation
    {
        public static bool TryComputeSplitMasses(float totalMass, float ratioA, float minFragmentMass, out float massA, out float massB)
        {
            float clampedRatio = Mathf.Clamp(ratioA, 0.1f, 0.9f);
            massA = totalMass * clampedRatio;
            massB = totalMass - massA;
            if (massA < minFragmentMass - 0.001f || massB < minFragmentMass - 0.001f)
            {
                massA = totalMass;
                massB = 0f;
                return false;
            }
            return true;
        }

        public static float ComputeMergedMass(float massA, float massB)
        {
            return Mathf.Max(0f, massA) + Mathf.Max(0f, massB);
        }

        public static float ComputeRemainingMassAfterLoss(float currentTotalMass, float lostFragmentMass)
        {
            return Mathf.Max(0f, currentTotalMass - Mathf.Max(0f, lostFragmentMass));
        }
    }

    public class JellyFragment : MonoBehaviour
    {
        public int FragmentId { get; set; }
        public bool IsActiveFragment { get; set; }
        public JellyController Controller { get; private set; }
        public JellyMassController MassController { get; private set; }
        public JellyPhysicsBody PhysicsBody { get; private set; }

        private void Awake() => EnsureComponents();

        public void EnsureComponents()
        {
            if (Controller == null) Controller = GetComponent<JellyController>();
            if (MassController == null) MassController = GetComponent<JellyMassController>();
            if (PhysicsBody == null) PhysicsBody = GetComponent<JellyPhysicsBody>();
        }
    }

    public class JellyFragmentRegistry : MonoBehaviour
    {
        public static JellyFragmentRegistry Instance { get; private set; }
        private readonly List<JellyFragment> _activeFragments = new List<JellyFragment>();

        public IReadOnlyList<JellyFragment> ActiveFragments => _activeFragments;
        public int ActiveCount => _activeFragments.Count;
        public float LostMassTotal { get; private set; }
        public event Action<JellyFragment, float> OnFragmentLost;

        private void Awake()
        {
            Instance = this;
        }

        public void Register(JellyFragment fragment)
        {
            if (fragment == null) return;
            fragment.EnsureComponents();
            fragment.IsActiveFragment = true;
            if (!_activeFragments.Contains(fragment))
                _activeFragments.Add(fragment);
        }

        public void Unregister(JellyFragment fragment)
        {
            if (fragment == null) return;
            fragment.IsActiveFragment = false;
            _activeFragments.Remove(fragment);
        }

        public void ReportFragmentLost(JellyFragment fragment)
        {
            if (fragment == null || !_activeFragments.Contains(fragment)) return;
            float lostMass = fragment.MassController != null ? fragment.MassController.CurrentMass : 0f;
            LostMassTotal += lostMass;
            Unregister(fragment);
            OnFragmentLost?.Invoke(fragment, lostMass);
        }

        public float GetTotalActiveMass()
        {
            float sum = 0f;
            for (int i = 0; i < _activeFragments.Count; i++)
            {
                var f = _activeFragments[i];
                if (f != null && f.MassController != null)
                    sum += f.MassController.CurrentMass;
            }
            return sum;
        }

        public Vector3 GetCentroid()
        {
            if (_activeFragments.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int valid = 0;
            for (int i = 0; i < _activeFragments.Count; i++)
            {
                if (_activeFragments[i] != null)
                {
                    sum += _activeFragments[i].transform.position;
                    valid++;
                }
            }
            return valid > 0 ? sum / valid : Vector3.zero;
        }

        public float GetMaxSpreadDistance()
        {
            if (_activeFragments.Count < 2) return 0f;
            Vector3 c = GetCentroid();
            float maxD = 0f;
            for (int i = 0; i < _activeFragments.Count; i++)
            {
                if (_activeFragments[i] != null)
                    maxD = Mathf.Max(maxD, Vector3.Distance(c, _activeFragments[i].transform.position));
            }
            return maxD * 2f;
        }

        public void ResetRegistry()
        {
            _activeFragments.Clear();
            LostMassTotal = 0f;
        }
    }

    public class JellyFragmentPool : MonoBehaviour
    {
        private readonly Queue<JellyFragment> _pool = new Queue<JellyFragment>();
        public int PrewarmCount = 2;

        public JellyFragment GetOrCreate(JellyController template)
        {
            JellyFragment frag = null;
            while (_pool.Count > 0 && frag == null)
                frag = _pool.Dequeue();

            if (frag == null)
            {
                GameObject clone = Instantiate(template.gameObject, template.transform.parent);
                clone.name = template.gameObject.name + "_Fragment";
                frag = clone.GetComponent<JellyFragment>();
                if (frag == null) frag = clone.AddComponent<JellyFragment>();
            }

            frag.gameObject.SetActive(true);
            frag.EnsureComponents();
            return frag;
        }

        public void Release(JellyFragment fragment)
        {
            if (fragment == null) return;
            fragment.IsActiveFragment = false;
            fragment.gameObject.SetActive(false);
            if (!_pool.Contains(fragment))
                _pool.Enqueue(fragment);
        }
    }

    public class JellyFragmentInputRouter : MonoBehaviour
    {
        public JellyInputAdapter primaryInput;
        public JellyFragmentRegistry registry;
        public int selectedFragmentIndex = -1; // -1 = control all simultaneously

        private void FixedUpdate() => RouteInput();

        public void RouteInput()
        {
            if (primaryInput == null || registry == null) return;
            JellyInputFrame frame = primaryInput.Current;
            var list = registry.ActiveFragments;
            for (int i = 0; i < list.Count; i++)
            {
                var frag = list[i];
                if (frag == null || frag.Controller == null) continue;
                var adapter = frag.Controller.inputAdapter;
                if (adapter == null || adapter == primaryInput) continue;

                bool active = selectedFragmentIndex < 0 || selectedFragmentIndex == i;
                if (active)
                    adapter.SetSimulatedInput(frame.Move, frame.Brake, false, false);
                else
                    adapter.SetSimulatedInput(Vector2.zero, true, false, false);
            }
        }
    }

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

            // Remove duplicate split/merge managers on cloned fragment
            var extraSplit = secondFrag.GetComponent<JellySplitController>();
            if (extraSplit != null) Destroy(extraSplit);
            var extraMerge = secondFrag.GetComponent<JellyMergeController>();
            if (extraMerge != null) Destroy(extraMerge);

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
