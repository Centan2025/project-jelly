using UnityEngine;

namespace Jelly
{
    public class JellyCameraController : MonoBehaviour
    {
        public Transform primaryTarget;
        public JellyFragmentRegistry fragmentRegistry;
        public JellyMassController massController;

        public Vector3 baseOffset = new Vector3(0f, 5.2f, -6.8f);
        public float lookAheadFactor = 0.25f;
        public float maxLookAhead = 2.4f;
        public float positionSmoothTime = 0.14f;
        public float rotationLerpSpeed = 10f;
        public float fragmentSpreadZoomFactor = 0.55f;
        public float massZoomFactor = 0.012f;
        public LayerMask obstructionMask = ~0;

        private Vector3 _velocityRef;
        private Vector3 _smoothedLookTarget;

        public void SnapImmediate()
        {
            Vector3 focus = ComputeFocusPoint();
            _smoothedLookTarget = focus;
            transform.position = ComputeDesiredCameraPosition(focus);
            transform.LookAt(focus + Vector3.up * 0.4f);
        }

        public Vector3 ComputeFocusPoint()
        {
            if (fragmentRegistry != null && fragmentRegistry.ActiveCount > 0)
                return fragmentRegistry.GetCentroid();
            if (primaryTarget != null)
                return primaryTarget.position;
            return Vector3.zero;
        }

        public Vector3 ComputeDesiredCameraPosition(Vector3 focus)
        {
            float spread = fragmentRegistry != null ? fragmentRegistry.GetMaxSpreadDistance() : 0f;
            float massDelta = massController != null ? (massController.CurrentMass - 100f) : 0f;
            float zoomScale = 1f + Mathf.Max(0f, spread * fragmentSpreadZoomFactor) + Mathf.Clamp(massDelta * massZoomFactor, -0.2f, 0.45f);

            Vector3 lookAhead = Vector3.zero;
            if (primaryTarget != null)
            {
                var rb = primaryTarget.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    lookAhead = Vector3.ClampMagnitude(flatVel * lookAheadFactor, maxLookAhead);
                }
            }

            Vector3 desired = focus + lookAhead + baseOffset * zoomScale;

            Vector3 rayOrigin = focus + Vector3.up * 0.5f;
            Vector3 dir = desired - rayOrigin;
            float dist = dir.magnitude;
            if (dist > 0.2f && Physics.SphereCast(rayOrigin, 0.22f, dir.normalized, out RaycastHit hit, dist, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.GetComponentInParent<JellyController>() == null && !hit.collider.isTrigger)
                {
                    desired = rayOrigin + dir.normalized * Mathf.Max(1.5f, hit.distance - 0.25f);
                }
            }
            return desired;
        }

        public void StepCamera(float dt)
        {
            if (dt <= 0f) return;
            Vector3 focus = ComputeFocusPoint();
            Vector3 desiredPos = ComputeDesiredCameraPosition(focus);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocityRef, positionSmoothTime, Mathf.Infinity, dt);
            _smoothedLookTarget = Vector3.Lerp(_smoothedLookTarget, focus + Vector3.up * 0.4f, Mathf.Clamp01(dt * rotationLerpSpeed));
            Vector3 lookDir = _smoothedLookTarget - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized, Vector3.up), Mathf.Clamp01(dt * rotationLerpSpeed));
        }

        private void LateUpdate() => StepCamera(Time.deltaTime);
    }
}
