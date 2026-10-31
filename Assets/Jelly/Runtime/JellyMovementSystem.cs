using System;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Serializable]
    public struct JellyInputFrame
    {
        public Vector2 Move;
        public bool Brake;
        public bool SplitPressed;
        public bool MergePressed;
    }

    public class JellyInputAdapter : MonoBehaviour
    {
        public enum ControlMode { DragAnalog, SmoothedTarget, VirtualJoystick }
        public ControlMode mode = ControlMode.DragAnalog;
        public JellyMovementConfig config;
        public bool overrideWithSimulatedInput;
        public JellyInputFrame simulatedInput;

        private Vector2 _touchStart;
        private bool _dragging;
        private float _holdTimer;
        private Vector2 _smoothedMove;
        private bool _uiSplitQueued;
        private bool _uiMergeQueued;
        private bool _uiBrakeHeld;

        public JellyInputFrame Current { get; private set; }

        public void TriggerSplitFromUI() => _uiSplitQueued = true;
        public void TriggerMergeFromUI() => _uiMergeQueued = true;
        public void SetBrakeFromUI(bool held) => _uiBrakeHeld = held;

        public void SetSimulatedInput(Vector2 move, bool brake = false, bool split = false, bool merge = false)
        {
            overrideWithSimulatedInput = true;
            simulatedInput = new JellyInputFrame
            {
                Move = Vector2.ClampMagnitude(move, 1f),
                Brake = brake,
                SplitPressed = split,
                MergePressed = merge
            };
            Current = simulatedInput;
        }

        public void ClearSimulatedInput()
        {
            overrideWithSimulatedInput = false;
            simulatedInput = default;
            Current = default;
        }

        public void SampleInput(float dt)
        {
            if (overrideWithSimulatedInput)
            {
                Current = simulatedInput;
                simulatedInput.SplitPressed = false;
                simulatedInput.MergePressed = false;
                return;
            }

            Vector2 rawMove = Vector2.zero;
            bool brake = _uiBrakeHeld;
            bool split = _uiSplitQueued;
            bool merge = _uiMergeQueued;
            _uiSplitQueued = false;
            _uiMergeQueued = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawMove.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawMove.x += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawMove.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawMove.y -= 1f;
                if (kb.LeftShiftKeyPressed() || kb.ctrlKey.isPressed) brake = true;
                if (kb.spaceKey.wasPressedThisFrame) split = true;
                if (kb.eKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame) merge = true;
            }

            float sens = config != null ? config.touchSensitivity : 0.012f;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                Vector2 pos = ts.primaryTouch.position.ReadValue();
                if (!_dragging) { _dragging = true; _touchStart = pos; _holdTimer = 0f; }
                Vector2 delta = (pos - _touchStart) * sens;
                rawMove += Vector2.ClampMagnitude(delta, 1f);
                if (delta.magnitude < 0.08f) _holdTimer += dt; else _holdTimer = 0f;
                if (_holdTimer > 0.35f) brake = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                if (!_dragging) { _dragging = true; _touchStart = pos; _holdTimer = 0f; }
                Vector2 delta = (pos - _touchStart) * sens;
                rawMove += Vector2.ClampMagnitude(delta, 1f);
                if (delta.magnitude < 0.08f) _holdTimer += dt; else _holdTimer = 0f;
                if (_holdTimer > 0.35f) brake = true;
            }
            else
            {
                _dragging = false;
                _holdTimer = 0f;
            }

            rawMove = Vector2.ClampMagnitude(rawMove, 1f);
            float smooth = config != null ? config.inputSmoothing : 14f;
            _smoothedMove = mode == ControlMode.SmoothedTarget
                ? Vector2.Lerp(_smoothedMove, rawMove, Mathf.Clamp01(dt * smooth))
                : rawMove;

            Current = new JellyInputFrame
            {
                Move = _smoothedMove,
                Brake = brake,
                SplitPressed = split,
                MergePressed = merge
            };
        }

        private void Update() => SampleInput(Time.deltaTime);
    }

    internal static class KeyboardExt
    {
        public static bool LeftShiftKeyPressed(this Keyboard kb) => kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
    }

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

    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class JellyPhysicsBody : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public SphereCollider Sphere { get; private set; }
        public Vector3 LastWallCollisionNormal { get; private set; }
        public float LastImpactSpeed { get; private set; }
        public float LastGroundImpactSpeed { get; private set; }
        public event Action<Collision> OnWallImpact;
        public event Action<Collision> OnGroundImpact;

        private Vector3 _preVelocity;

        private void Awake() => InitializeComponents();

        public void InitializeComponents()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (Sphere == null) Sphere = GetComponent<SphereCollider>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (Body.mass <= 0f) Body.mass = 1f;
        }

        public void SyncMassAndRadius(float gameplayMass, float baseMass, float radius)
        {
            InitializeComponents();
            float normalizedMass = Mathf.Max(0.1f, gameplayMass / Mathf.Max(1f, baseMass));
            Body.mass = normalizedMass;
            Sphere.radius = Mathf.Max(0.1f, radius);
        }

        private void FixedUpdate()
        {
            if (Body != null) _preVelocity = Body.linearVelocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;
            ContactPoint cp = collision.GetContact(0);
            float relSpeed = collision.relativeVelocity.magnitude;
            if (cp.normal.y > 0.55f)
            {
                LastGroundImpactSpeed = relSpeed;
                OnGroundImpact?.Invoke(collision);
            }
            else if (Mathf.Abs(cp.normal.y) <= 0.55f)
            {
                LastWallCollisionNormal = cp.normal;
                LastImpactSpeed = relSpeed;
                if (Body != null && _preVelocity.sqrMagnitude > 0.25f)
                {
                    Vector3 reflected = Vector3.Reflect(_preVelocity, cp.normal) * 0.35f;
                    Body.linearVelocity = new Vector3(reflected.x, Body.linearVelocity.y, reflected.z);
                }
                OnWallImpact?.Invoke(collision);
            }
        }
    }

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
            if (physicsBody == null || physicsBody.Body == null) return;
            if (config == null) config = ScriptableObject.CreateInstance<JellyMovementConfig>();

            physicsBody.InitializeComponents();
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

    [RequireComponent(typeof(JellyPhysicsBody), typeof(JellyGroundDetector), typeof(JellyMotor))]
    public class JellyController : MonoBehaviour
    {
        public JellyMovementConfig movementConfig;
        public JellyInputAdapter inputAdapter;
        public JellyMotor motor;
        public JellyPhysicsBody physicsBody;
        public JellyGroundDetector groundDetector;
        public JellyMassController massController;
        public JellyMorphController morphController;
        public JellyDeformationController deformationController;
        public event Action<JellyController> OnFellIntoAbyss;

        private bool _abyssTriggered;

        private void Awake()
        {
            if (physicsBody == null) physicsBody = GetComponent<JellyPhysicsBody>();
            if (groundDetector == null) groundDetector = GetComponent<JellyGroundDetector>();
            if (motor == null) motor = GetComponent<JellyMotor>();
            if (inputAdapter == null) inputAdapter = GetComponent<JellyInputAdapter>();
            if (massController == null) massController = GetComponent<JellyMassController>();
            if (morphController == null) morphController = GetComponent<JellyMorphController>();
            if (deformationController == null) deformationController = GetComponent<JellyDeformationController>();

            if (movementConfig == null) movementConfig = ScriptableObject.CreateInstance<JellyMovementConfig>();
            motor.config = movementConfig;
            if (inputAdapter != null) inputAdapter.config = movementConfig;
        }

        private void FixedUpdate()
        {
            JellyInputFrame frame = inputAdapter != null ? inputAdapter.Current : default;
            float radius = massController != null ? massController.CurrentRadius : (physicsBody.Sphere != null ? physicsBody.Sphere.radius : 0.5f);
            motor.StepPhysics(frame, radius);

            if (groundDetector.IsInAbyss && !_abyssTriggered)
            {
                _abyssTriggered = true;
                OnFellIntoAbyss?.Invoke(this);
            }
            else if (!groundDetector.IsInAbyss)
            {
                _abyssTriggered = false;
            }
        }

        public void ResetState(Vector3 position, Quaternion rotation, float mass)
        {
            _abyssTriggered = false;
            transform.SetPositionAndRotation(position, rotation);
            if (physicsBody != null && physicsBody.Body != null)
            {
                physicsBody.Body.position = position;
                physicsBody.Body.rotation = rotation;
                physicsBody.Body.linearVelocity = Vector3.zero;
                physicsBody.Body.angularVelocity = Vector3.zero;
            }
            if (massController != null)
                massController.SetMassImmediate(mass);
        }
    }
}
