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
}
