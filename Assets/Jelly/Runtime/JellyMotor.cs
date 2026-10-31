using UnityEngine;

namespace Jelly
{
    [RequireComponent(typeof(JellyPhysicsBody), typeof(JellyGroundDetector))]
    public class JellyMotor : MonoBehaviour
    {
        public JellyMovementConfig config;
        public JellyPhysicsBody physicsBody;
        public JellyGroundDetector groundDetector;

        private void Awake()
        {
            if (physicsBody == null) physicsBody = GetComponent<JellyPhysicsBody>();
            if (groundDetector == null) groundDetector = GetComponent<JellyGroundDetector>();
        }

        public void StepPhysics(JellyInputFrame input, float currentRadius)
        {
            if (physicsBody == null) physicsBody = GetComponent<JellyPhysicsBody>();
            if (groundDetector == null) groundDetector = GetComponent<JellyGroundDetector>();
            if (physicsBody == null) return;
            physicsBody.InitializeComponents();
            if (physicsBody.Body == null) return;
            if (config == null) config = ScriptableObject.CreateInstance<JellyMovementConfig>();

            groundDetector.Evaluate(transform.position, currentRadius, config);

            Rigidbody rb = physicsBody.Body;
            bool grounded = groundDetector.IsGrounded;
            float frictionMul = groundDetector.SurfaceFrictionMultiplier;

            rb.linearDamping = grounded
                ? (input.Brake ? config.brakeDrag : config.groundDrag * frictionMul)
                : config.airDrag;
            rb.angularDamping = config.angularDrag;

            Vector3 desiredDir = new Vector3(input.Move.x, 0f, input.Move.y);
            if (desiredDir.magnitude < config.minMoveThreshold)
                desiredDir = Vector3.zero;
            else
                desiredDir = Vector3.ClampMagnitude(desiredDir, 1f);

            if (grounded && groundDetector.IsOnSlope && desiredDir.sqrMagnitude > 0.001f)
                desiredDir = Vector3.ProjectOnPlane(desiredDir, groundDetector.GroundNormal).normalized * desiredDir.magnitude;

            float controlMul = grounded ? Mathf.Lerp(0.4f, 1.25f, Mathf.Clamp01(frictionMul)) : config.airControlMultiplier;
            float slopeFactor = 1f;
            if (grounded && groundDetector.IsOnSlope && desiredDir.sqrMagnitude > 0.001f)
            {
                float verticalDot = Vector3.Dot(desiredDir.normalized, Vector3.up);
                if (verticalDot > 0.05f) slopeFactor = config.uphillAssist;
            }

            if (!input.Brake && desiredDir.sqrMagnitude > 0.0001f)
            {
                Vector3 force = desiredDir * (config.acceleration * controlMul * slopeFactor * rb.mass);
                rb.AddForce(force, ForceMode.Force);

                Vector3 torqueAxis = Vector3.Cross(Vector3.up, desiredDir);
                rb.AddTorque(torqueAxis * (config.rollTorqueMultiplier * rb.mass), ForceMode.Force);
            }
            else if (input.Brake && grounded)
            {
                Vector3 horizVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                rb.AddForce(-horizVel * (config.brakeForce * rb.mass), ForceMode.Force);
                rb.angularVelocity *= 0.7f;
            }

            if (config.clampMaxSpeed)
            {
                Vector3 horiz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                float maxSpd = grounded && groundDetector.IsOnSlope && rb.linearVelocity.y < -0.1f
                    ? config.downhillLimit
                    : config.maxSpeed;
                if (horiz.magnitude > maxSpd)
                {
                    Vector3 clamped = horiz.normalized * maxSpd;
                    rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
                }
            }
        }
    }
}
