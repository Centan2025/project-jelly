using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public class WeightedPressurePlate : MonoBehaviour
    {
        public float requiredMass = 110f;
        public bool stayOpenOncePressed = true;
        public ObstacleStateController targetObstacle;
        public bool IsActivated { get; private set; }
        public float CurrentDetectedMass { get; private set; }

        private readonly HashSet<JellyMassController> _occupants = new HashSet<JellyMassController>();

        public void EvaluateMass(float mass)
        {
            CurrentDetectedMass = mass;
            bool active = mass >= requiredMass - 0.01f;
            if (active || !stayOpenOncePressed)
            {
                IsActivated = active;
                if (targetObstacle != null)
                    targetObstacle.SetOpen(IsActivated);
            }
        }

        private void Recalculate()
        {
            float sum = 0f;
            _occupants.RemoveWhere(x => x == null || !x.gameObject.activeInHierarchy);
            foreach (var occ in _occupants)
                sum += occ.CurrentMass;
            EvaluateMass(sum);
        }

        private void OnTriggerEnter(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) { _occupants.Add(mc); Recalculate(); }
        }

        private void OnTriggerStay(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) { _occupants.Add(mc); Recalculate(); }
        }

        private void OnTriggerExit(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) { _occupants.Remove(mc); Recalculate(); }
        }
    }

    public class DualFragmentSwitchGroup : MonoBehaviour
    {
        public WeightedPressurePlate switchA;
        public WeightedPressurePlate switchB;
        public ObstacleStateController targetObstacle;
        public bool latchWhenBothActive = true;
        public bool BothActivated { get; private set; }

        private void FixedUpdate() => EvaluateState();

        public bool EvaluateState()
        {
            bool a = switchA != null && switchA.IsActivated;
            bool b = switchB != null && switchB.IsActivated;
            if (a && b) BothActivated = true;
            else if (!latchWhenBothActive) BothActivated = false;

            if (targetObstacle != null)
                targetObstacle.SetOpen(BothActivated);
            return BothActivated;
        }
    }

    [RequireComponent(typeof(Rigidbody))]
    public class PushableBlock : MonoBehaviour
    {
        public float minimumPlayerMassToPush = 90f;
        public float blockMass = 2.2f;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = blockMass;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void OnCollisionEnter(Collision collision) => HandleContact(collision);
        private void OnCollisionStay(Collision collision) => HandleContact(collision);

        private void HandleContact(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc == null || _rb == null) return;
            bool canPush = mc.CurrentMass >= minimumPlayerMassToPush - 0.01f;
            _rb.isKinematic = !canPush;
        }
    }

    public class MomentumBreakableBarrier : MonoBehaviour
    {
        public float requiredMomentum = 850f; // gameplayMass * speed
        public ObstacleStateController stateController;
        public bool IsBroken { get; private set; }

        private void Awake()
        {
            if (stateController == null) stateController = GetComponent<ObstacleStateController>();
        }

        public bool EvaluateImpact(float gameplayMass, float impactSpeed)
        {
            float momentum = gameplayMass * impactSpeed;
            if (momentum >= requiredMomentum)
            {
                IsBroken = true;
                if (stateController != null) stateController.SetOpen(true);
                else gameObject.SetActive(false);
            }
            return IsBroken;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc == null) return;
            EvaluateImpact(mc.CurrentMass, collision.relativeVelocity.magnitude);
        }
    }

    public class MassSensitiveBridge : MonoBehaviour
    {
        public float maxSafeMass = 125f;
        public Collider bridgeCollider;
        public bool IsCollapsed { get; private set; }

        private void Awake()
        {
            if (bridgeCollider == null) bridgeCollider = GetComponent<Collider>();
        }

        public bool EvaluateLoad(float mass)
        {
            if (mass > maxSafeMass)
            {
                IsCollapsed = true;
                if (bridgeCollider != null) bridgeCollider.enabled = false;
            }
            return !IsCollapsed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc != null) EvaluateLoad(mc.CurrentMass);
        }
    }

    public class BalancePlatform : MonoBehaviour
    {
        public float maxTiltAngle = 20f;
        public float tiltTorqueSensitivity = 0.08f;
        public float currentTilt { get; private set; }

        public void ApplyTorqueFromOffset(float offsetX, float mass, float dt)
        {
            float target = Mathf.Clamp(-offsetX * (mass / 100f) * maxTiltAngle * tiltTorqueSensitivity, -maxTiltAngle, maxTiltAngle);
            currentTilt = Mathf.MoveTowards(currentTilt, target, 45f * dt);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
        }

        private void OnCollisionStay(Collision collision)
        {
            var mc = collision.collider.GetComponentInParent<JellyMassController>();
            if (mc != null)
            {
                float localX = transform.InverseTransformPoint(mc.transform.position).x;
                ApplyTorqueFromOffset(localX, mc.CurrentMass, Time.fixedDeltaTime);
            }
        }
    }

    public class MovingPlatform : MonoBehaviour
    {
        public Vector3 pointA;
        public Vector3 pointB = new Vector3(4f, 0f, 0f);
        public float speed = 2.5f;
        private float _t;

        private void Start()
        {
            if (pointA == Vector3.zero && pointB == new Vector3(4f, 0f, 0f))
            {
                pointA = transform.position;
                pointB = transform.position + new Vector3(4f, 0f, 0f);
            }
        }

        public void StepPlatform(float dt)
        {
            _t += dt * speed;
            float pingPong = Mathf.PingPong(_t, 1f);
            transform.position = Vector3.Lerp(pointA, pointB, pingPong);
        }

        private void FixedUpdate() => StepPlatform(Time.fixedDeltaTime);
    }

    public class RotatingObstacle : MonoBehaviour
    {
        public Vector3 angularVelocityEuler = new Vector3(0f, 90f, 0f);

        public void StepRotation(float dt)
        {
            transform.Rotate(angularVelocityEuler * dt, Space.Self);
        }

        private void FixedUpdate() => StepRotation(Time.fixedDeltaTime);
    }

    public class TimedGate : GateBase
    {
        public float openDuration = 2.0f;
        public float closedDuration = 1.5f;
        private float _timer;

        public bool IsCurrentlyOpenWindow { get; private set; } = true;

        public void StepTimer(float dt)
        {
            _timer += dt;
            float cycle = Mathf.Max(0.2f, openDuration + closedDuration);
            float phase = _timer % cycle;
            IsCurrentlyOpenWindow = phase <= openDuration;
            if (stateController != null)
                stateController.SetOpen(IsCurrentlyOpenWindow);
            if (barrierCollider != null)
                barrierCollider.enabled = !IsCurrentlyOpenWindow;
        }

        private void FixedUpdate() => StepTimer(Time.fixedDeltaTime);

        public override GateEvaluationResult Evaluate(GateCandidateState candidate)
        {
            return new GateEvaluationResult
            {
                CanPass = IsCurrentlyOpenWindow,
                RequiredLateralCompression = 1f,
                RequiredVerticalCompression = 1f,
                Reason = IsCurrentlyOpenWindow ? "Timed gate open" : "Timed gate closed"
            };
        }
    }

    public class SurfaceFrictionZone : MonoBehaviour
    {
        [Range(0.05f, 3f)] public float frictionMultiplier = 0.2f; // <1 ice, >1 sticky mud
    }

    public class SpringLauncher : MonoBehaviour
    {
        public Vector3 launchVelocity = new Vector3(0f, 7.5f, 11f);

        public bool Launch(JellyPhysicsBody body)
        {
            if (body == null || body.Body == null) return false;
            body.Body.linearVelocity = launchVelocity;
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var pb = other.GetComponentInParent<JellyPhysicsBody>();
            if (pb != null) Launch(pb);
        }
    }
}
