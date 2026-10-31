using UnityEngine;

namespace Jelly
{
    public class JellyGroundDetector : MonoBehaviour
    {
        public LayerMask groundMask = ~0;
        public bool IsGrounded { get; private set; }
        public bool IsOnSlope { get; private set; }
        public bool IsInAbyss { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float SlopeAngle { get; private set; }
        public float SurfaceFrictionMultiplier { get; set; } = 1f;

        public void Evaluate(Vector3 position, float radius, JellyMovementConfig config)
        {
            float extra = config != null ? config.groundCheckExtraDistance : 0.15f;
            float abyssY = config != null ? config.fallAbyssY : -12f;
            IsInAbyss = position.y < abyssY;

            Vector3 origin = position + Vector3.up * (radius * 0.25f);
            float castDist = radius * 0.45f + extra;
            if (Physics.SphereCast(origin, radius * 0.8f, Vector3.down, out RaycastHit hit, castDist, groundMask, QueryTriggerInteraction.Ignore))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                SlopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                float maxSlope = config != null ? config.maxSlopeAngle : 55f;
                IsOnSlope = SlopeAngle > 1f && SlopeAngle <= maxSlope;
                var surface = hit.collider != null ? hit.collider.GetComponent<SurfaceFrictionZone>() : null;
                SurfaceFrictionMultiplier = surface != null ? surface.frictionMultiplier : 1f;
            }
            else
            {
                IsGrounded = false;
                IsOnSlope = false;
                GroundNormal = Vector3.up;
                SlopeAngle = 0f;
                SurfaceFrictionMultiplier = 1f;
            }
        }
    }
}
