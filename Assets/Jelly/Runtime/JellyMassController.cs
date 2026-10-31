using System;
using UnityEngine;

namespace Jelly
{
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
            physicsBody.InitializeComponents();
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
}
