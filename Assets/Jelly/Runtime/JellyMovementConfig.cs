using UnityEngine;

namespace Jelly
{
    [CreateAssetMenu(menuName = "Jelly/Movement Config", fileName = "JellyMovementConfig")]
    public class JellyMovementConfig : ScriptableObject
    {
        public float acceleration = 28f;
        public float airControlMultiplier = 0.35f;
        public float maxSpeed = 14f;
        public float minMoveThreshold = 0.05f;
        public bool clampMaxSpeed = true;
        public float groundDrag = 1.4f;
        public float airDrag = 0.2f;
        public float angularDrag = 0.8f;
        public float brakeDrag = 10f;
        public float brakeForce = 35f;
        public float rollTorqueMultiplier = 12f;
        public float uphillAssist = 1.15f;
        public float downhillLimit = 18f;
        public float wallBounceDamping = 0.65f;
        public float fallAbyssY = -12f;
        public float groundCheckExtraDistance = 0.15f;
        public float maxSlopeAngle = 55f;
        public float touchSensitivity = 0.012f;
        public float inputSmoothing = 14f;
    }
}
