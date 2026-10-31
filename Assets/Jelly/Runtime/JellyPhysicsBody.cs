using System;
using UnityEngine;

namespace Jelly
{
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
            if (Body != null)
            {
                Body.interpolation = RigidbodyInterpolation.Interpolate;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (Body.mass <= 0f) Body.mass = 1f;
            }
        }

        public void SyncMassAndRadius(float gameplayMass, float baseMass, float radius)
        {
            InitializeComponents();
            float normalizedMass = Mathf.Max(0.1f, gameplayMass / Mathf.Max(1f, baseMass));
            if (Body != null) Body.mass = normalizedMass;
            if (Sphere != null) Sphere.radius = Mathf.Max(0.1f, radius);
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
}
