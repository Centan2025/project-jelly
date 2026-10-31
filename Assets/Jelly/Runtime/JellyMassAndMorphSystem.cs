using System;
using UnityEngine;

namespace Jelly
{
    [CreateAssetMenu(menuName = "Jelly/Mass Config", fileName = "JellyMassConfig")]
    public class JellyMassConfig : ScriptableObject
    {
        public float baseMass = 100f;
        public float minMass = 20f;
        public float maxMass = 200f;
        public float baseRadius = 0.5f;
        public float pickupDelta = 20f;
        public float drainDelta = 20f;
        public float interpolationSpeed = 10f;
        public float maxLateralCompressionRatio = 0.62f;
        public float maxVerticalCompressionRatio = 0.65f;
        public float deformationSpring = 18f;
        public float deformationDamping = 6f;
    }

    [Serializable]
    public class JellyMassModel
    {
        public float BaseMass { get; private set; }
        public float MinMass { get; private set; }
        public float MaxMass { get; private set; }
        public float BaseRadius { get; private set; }
        public float CurrentMass { get; private set; }

        public JellyMassModel(float baseMass = 100f, float minMass = 20f, float maxMass = 200f, float baseRadius = 0.5f)
        {
            BaseMass = Mathf.Max(1f, baseMass);
            MinMass = Mathf.Max(1f, minMass);
            MaxMass = Mathf.Max(MinMass, maxMass);
            BaseRadius = Mathf.Max(0.05f, baseRadius);
            CurrentMass = Mathf.Clamp(BaseMass, MinMass, MaxMass);
        }

        public static float ComputeRadius(float mass, float baseMass, float baseRadius)
        {
            float safeMass = Mathf.Max(0.001f, mass);
            float safeBase = Mathf.Max(0.001f, baseMass);
            return baseRadius * Mathf.Pow(safeMass / safeBase, 1f / 3f);
        }

        public static float ComputeVolume(float radius)
        {
            return (4f / 3f) * Mathf.PI * radius * radius * radius;
        }

        public float TargetRadius => ComputeRadius(CurrentMass, BaseMass, BaseRadius);
        public float TargetVolume => ComputeVolume(TargetRadius);

        public float SetMass(float newMass)
        {
            CurrentMass = Mathf.Clamp(newMass, MinMass, MaxMass);
            return CurrentMass;
        }

        public float AddMass(float delta) => SetMass(CurrentMass + delta);
    }

    public class JellySizeInterpolator
    {
        public float CurrentRadius { get; private set; }
        public float TargetRadius { get; private set; }

        public JellySizeInterpolator(float initialRadius)
        {
            CurrentRadius = initialRadius;
            TargetRadius = initialRadius;
        }

        public void SetImmediate(float radius)
        {
            CurrentRadius = radius;
            TargetRadius = radius;
        }

        public void SetTarget(float targetRadius) => TargetRadius = Mathf.Max(0.05f, targetRadius);

        public float Step(float dt, float speed)
        {
            CurrentRadius = Mathf.MoveTowards(CurrentRadius, TargetRadius, Mathf.Max(0.1f, speed) * dt);
            return CurrentRadius;
        }
    }

    public class JellyMorphController : MonoBehaviour
    {
        [Range(0.4f, 1f)] public float lateralCompression = 1f;
        [Range(0.4f, 1f)] public float verticalCompression = 1f;
        public float preSplitStretch;

        public Vector3 GetEffectiveHalfExtents(float radius)
        {
            float rx = radius * lateralCompression;
            float ry = radius * verticalCompression;
            float rz = radius / Mathf.Max(0.35f, Mathf.Sqrt(lateralCompression * verticalCompression));
            return new Vector3(rx, ry, rz);
        }

        public void SetSqueeze(float lateralRatio, float verticalRatio, JellyMassConfig config)
        {
            float minLat = config != null ? config.maxLateralCompressionRatio : 0.62f;
            float minVert = config != null ? config.maxVerticalCompressionRatio : 0.65f;
            lateralCompression = Mathf.Clamp(lateralRatio, minLat, 1f);
            verticalCompression = Mathf.Clamp(verticalRatio, minVert, 1f);
        }

        public void Relax(float dt, float speed = 6f)
        {
            lateralCompression = Mathf.MoveTowards(lateralCompression, 1f, dt * speed);
            verticalCompression = Mathf.MoveTowards(verticalCompression, 1f, dt * speed);
            preSplitStretch = Mathf.MoveTowards(preSplitStretch, 0f, dt * speed);
        }
    }

    public class JellyDeformationController : MonoBehaviour
    {
        public Transform visualRoot;
        public JellyPhysicsBody physicsBody;
        public JellyMassController massController;
        public JellyMorphController morphController;

        private Vector3 _scaleOffset = Vector3.zero;
        private Vector3 _scaleVelocity = Vector3.zero;
        private Vector3 _prevVelocity;

        public Vector3 CurrentVisualScale { get; private set; } = Vector3.one;

        private void Awake()
        {
            if (physicsBody == null) physicsBody = GetComponent<JellyPhysicsBody>();
            if (massController == null) massController = GetComponent<JellyMassController>();
            if (morphController == null) morphController = GetComponent<JellyMorphController>();
            if (visualRoot == null)
            {
                Transform child = transform.Find("Visual");
                visualRoot = child != null ? child : transform;
            }
            if (physicsBody != null)
            {
                physicsBody.OnGroundImpact += HandleGroundImpact;
                physicsBody.OnWallImpact += HandleWallImpact;
            }
        }

        private void OnDestroy()
        {
            if (physicsBody != null)
            {
                physicsBody.OnGroundImpact -= HandleGroundImpact;
                physicsBody.OnWallImpact -= HandleWallImpact;
            }
        }

        private void HandleGroundImpact(Collision col) => ApplyImpulseDeformation(new Vector3(0.18f, -0.28f, 0.18f));
        private void HandleWallImpact(Collision col) => ApplyImpulseDeformation(new Vector3(-0.20f, 0.12f, -0.15f));

        public void ApplyImpulseDeformation(Vector3 impulse)
        {
            _scaleVelocity += impulse * 8f;
        }

        public void StepVisualDeformation(float dt)
        {
            if (dt <= 0f) return;
            float radius = massController != null ? massController.CurrentRadius : 0.5f;
            float diameter = radius * 2f;

            Vector3 vel = physicsBody != null && physicsBody.Body != null ? physicsBody.Body.linearVelocity : Vector3.zero;
            Vector3 accel = (vel - _prevVelocity) / Mathf.Max(0.001f, dt);
            _prevVelocity = vel;

            float speed = new Vector2(vel.x, vel.z).magnitude;
            float forwardDot = Vector3.Dot(accel, vel.sqrMagnitude > 0.01f ? vel.normalized : Vector3.forward);

            float stretchZ = Mathf.Clamp(speed * 0.02f + forwardDot * 0.006f, -0.22f, 0.28f);
            float squashY = -stretchZ * 0.45f;
            float lat = morphController != null ? morphController.lateralCompression : 1f;
            float vert = morphController != null ? morphController.verticalCompression : 1f;
            float splitStretch = morphController != null ? morphController.preSplitStretch : 0f;

            Vector3 targetOffset = new Vector3(
                (lat - 1f) + splitStretch * 0.25f - stretchZ * 0.35f,
                (vert - 1f) + squashY - splitStretch * 0.15f,
                stretchZ
            );

            float spring = massController != null && massController.config != null ? massController.config.deformationSpring : 18f;
            float damping = massController != null && massController.config != null ? massController.config.deformationDamping : 6f;

            _scaleVelocity += (targetOffset - _scaleOffset) * (spring * dt);
            _scaleVelocity *= Mathf.Clamp01(1f - damping * dt);
            _scaleOffset += _scaleVelocity * dt;

            Vector3 s = new Vector3(
                Mathf.Clamp(1f + _scaleOffset.x, 0.45f, 1.65f),
                Mathf.Clamp(1f + _scaleOffset.y, 0.45f, 1.65f),
                Mathf.Clamp(1f + _scaleOffset.z, 0.45f, 1.65f)
            );
            float volCorrection = Mathf.Pow(Mathf.Max(0.2f, s.x * s.y * s.z), -1f / 3f);
            CurrentVisualScale = s * (volCorrection * diameter);

            if (visualRoot != null && visualRoot != transform)
                visualRoot.localScale = CurrentVisualScale;
        }

        private void LateUpdate() => StepVisualDeformation(Time.deltaTime);
    }

    [RequireComponent(typeof(JellyPhysicsBody))]
    public class JellyMassController : MonoBehaviour
    {
        public JellyMassConfig config;
        public JellyPhysicsBody physicsBody;
        public JellyMorphController morphController;
        public JellyDeformationController deformationController;

        public JellyMassModel Model { get; private set; }
        public JellySizeInterpolator Interpolator { get; private set; }

        public float CurrentMass => Model != null ? Model.CurrentMass : (config != null ? config.baseMass : 100f);
        public float CurrentRadius => Interpolator != null ? Interpolator.CurrentRadius : (config != null ? config.baseRadius : 0.5f);
        public float TargetRadius => Model != null ? Model.TargetRadius : CurrentRadius;

        public event Action<float, float> OnMassChanged;

        private void Awake() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (config == null) config = ScriptableObject.CreateInstance<JellyMassConfig>();
            if (physicsBody == null) physicsBody = GetComponent<JellyPhysicsBody>();
            if (morphController == null) morphController = GetComponent<JellyMorphController>();
            if (deformationController == null) deformationController = GetComponent<JellyDeformationController>();

            if (Model == null)
                Model = new JellyMassModel(config.baseMass, config.minMass, config.maxMass, config.baseRadius);
            if (Interpolator == null)
                Interpolator = new JellySizeInterpolator(Model.TargetRadius);

            SyncPhysicsSafe(Interpolator.CurrentRadius);
        }

        public void SetMassImmediate(float newMass)
        {
            EnsureInitialized();
            float prev = Model.CurrentMass;
            Model.SetMass(newMass);
            Interpolator.SetImmediate(Model.TargetRadius);
            SyncPhysicsSafe(Interpolator.CurrentRadius);
            if (!Mathf.Approximately(prev, Model.CurrentMass))
                OnMassChanged?.Invoke(prev, Model.CurrentMass);
        }

        public float ChangeMass(float delta)
        {
            EnsureInitialized();
            float prev = Model.CurrentMass;
            float updated = Model.AddMass(delta);
            Interpolator.SetTarget(Model.TargetRadius);
            if (deformationController != null)
                deformationController.ApplyImpulseDeformation(delta >= 0f ? new Vector3(0.15f, 0.15f, 0.15f) : new Vector3(-0.12f, -0.12f, -0.12f));
            if (!Mathf.Approximately(prev, updated))
                OnMassChanged?.Invoke(prev, updated);
            return updated;
        }

        public void StepMassInterpolation(float dt)
        {
            EnsureInitialized();
            float prevRadius = Interpolator.CurrentRadius;
            float nextRadius = Interpolator.Step(dt, config.interpolationSpeed);
            if (!Mathf.Approximately(prevRadius, nextRadius))
                SyncPhysicsSafe(nextRadius);
            if (morphController != null)
                morphController.Relax(dt);
        }

        private void SyncPhysicsSafe(float newRadius)
        {
            if (physicsBody == null) return;
            float oldRadius = physicsBody.Sphere != null ? physicsBody.Sphere.radius : newRadius;
            float deltaR = newRadius - oldRadius;
            if (deltaR > 0.0001f && physicsBody.Body != null)
            {
                Vector3 lifted = physicsBody.Body.position + Vector3.up * deltaR;
                physicsBody.Body.position = lifted;
                transform.position = lifted;
            }
            float lateral = morphController != null ? morphController.lateralCompression : 1f;
            physicsBody.SyncMassAndRadius(Model.CurrentMass, config.baseMass, newRadius * lateral);
        }

        private void FixedUpdate() => StepMassInterpolation(Time.fixedDeltaTime);
    }

    [RequireComponent(typeof(Collider))]
    public class JellyMassPickup : MonoBehaviour
    {
        public float massGain = 20f;
        public bool consumeOnTrigger = true;
        public bool Consumed { get; private set; }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public bool TryApply(JellyMassController massController)
        {
            if (Consumed || massController == null) return false;
            float before = massController.CurrentMass;
            float after = massController.ChangeMass(Mathf.Abs(massGain));
            if (after <= before && Mathf.Approximately(before, massController.Model.MaxMass))
                return false;
            if (consumeOnTrigger)
            {
                Consumed = true;
                gameObject.SetActive(false);
            }
            return true;
        }

        public void ResetPickup()
        {
            Consumed = false;
            gameObject.SetActive(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) TryApply(mc);
        }
    }

    [RequireComponent(typeof(Collider))]
    public class JellyMassDrain : MonoBehaviour
    {
        public float massLoss = 20f;
        public bool continuousDrain;
        public float drainPerSecond = 25f;
        public bool oneShot = true;
        public bool Triggered { get; private set; }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public bool TryApplyInstant(JellyMassController massController)
        {
            if (massController == null || (oneShot && Triggered)) return false;
            massController.ChangeMass(-Mathf.Abs(massLoss));
            if (oneShot) Triggered = true;
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (continuousDrain) return;
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) TryApplyInstant(mc);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!continuousDrain) return;
            var mc = other.GetComponentInParent<JellyMassController>();
            if (mc != null) mc.ChangeMass(-Mathf.Abs(drainPerSecond) * Time.fixedDeltaTime);
        }
    }
}
