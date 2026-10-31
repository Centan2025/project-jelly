using UnityEngine;

namespace Jelly
{
    public static class JellyMassConservation
    {
        public static bool TryComputeSplitMasses(float totalMass, float ratioA, float minFragmentMass, out float massA, out float massB)
        {
            float clampedRatio = Mathf.Clamp(ratioA, 0.1f, 0.9f);
            massA = totalMass * clampedRatio;
            massB = totalMass - massA;
            if (massA < minFragmentMass - 0.001f || massB < minFragmentMass - 0.001f)
            {
                massA = totalMass;
                massB = 0f;
                return false;
            }
            return true;
        }

        public static float ComputeMergedMass(float massA, float massB)
        {
            return Mathf.Max(0f, massA) + Mathf.Max(0f, massB);
        }

        public static float ComputeRemainingMassAfterLoss(float currentTotalMass, float lostFragmentMass)
        {
            return Mathf.Max(0f, currentTotalMass - Mathf.Max(0f, lostFragmentMass));
        }
    }
}
