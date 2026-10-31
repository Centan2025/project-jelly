using System;
using UnityEngine;

namespace Jelly
{
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
}
