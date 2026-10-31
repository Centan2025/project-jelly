using System;
using UnityEngine;

namespace Jelly
{
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

        private void Awake() => EnsureComponents();

        public void EnsureComponents()
        {
            if (physicsBody == null) physicsBody = GetComponent<JellyPhysicsBody>();
            if (groundDetector == null) groundDetector = GetComponent<JellyGroundDetector>();
            if (motor == null) motor = GetComponent<JellyMotor>();
            if (inputAdapter == null) inputAdapter = GetComponent<JellyInputAdapter>();
            if (massController == null) massController = GetComponent<JellyMassController>();
            if (morphController == null) morphController = GetComponent<JellyMorphController>();
            if (deformationController == null) deformationController = GetComponent<JellyDeformationController>();

            if (movementConfig == null) movementConfig = ScriptableObject.CreateInstance<JellyMovementConfig>();
            if (motor != null) motor.config = movementConfig;
            if (inputAdapter != null) inputAdapter.config = movementConfig;
        }

        private void FixedUpdate()
        {
            EnsureComponents();
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
            EnsureComponents();
            _abyssTriggered = false;
            transform.SetPositionAndRotation(position, rotation);
            if (physicsBody != null)
            {
                physicsBody.InitializeComponents();
                if (physicsBody.Body != null)
                {
                    physicsBody.Body.position = position;
                    physicsBody.Body.rotation = rotation;
                    physicsBody.Body.linearVelocity = Vector3.zero;
                    physicsBody.Body.angularVelocity = Vector3.zero;
                }
            }
            if (massController != null)
                massController.SetMassImmediate(mass);
            if (deformationController != null)
                deformationController.StepVisualDeformation(0.02f);
        }
    }
}
