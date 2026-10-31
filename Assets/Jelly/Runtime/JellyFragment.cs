using UnityEngine;

namespace Jelly
{
    public class JellyFragment : MonoBehaviour
    {
        public int FragmentId { get; set; }
        public bool IsActiveFragment { get; set; }
        public JellyController Controller { get; private set; }
        public JellyMassController MassController { get; private set; }
        public JellyPhysicsBody PhysicsBody { get; private set; }

        private void Awake() => EnsureComponents();

        public void EnsureComponents()
        {
            if (Controller == null) Controller = GetComponent<JellyController>();
            if (MassController == null) MassController = GetComponent<JellyMassController>();
            if (PhysicsBody == null) PhysicsBody = GetComponent<JellyPhysicsBody>();
        }
    }
}
