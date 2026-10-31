using UnityEngine;

namespace Jelly
{
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
            EnsureReferences();
            if (physicsBody != null)
            {
                physicsBody.OnGroundImpact += HandleGroundImpact;
                physicsBody.OnWallImpact += HandleWallImpact;
            }
        }

        private void EnsureReferences()
        {
            if (physicsBody == null) physicsBody = GetComponent<JellyPhysicsBody>();
            if (massController == null) massController = GetComponent<JellyMassController>();
            if (morphController == null) morphController = GetComponent<JellyMorphController>();
            if (visualRoot == null)
            {
                Transform child = transform.Find("Visual");
                visualRoot = child != null ? child : transform;
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
            EnsureReferences();
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
}
