using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    [Flags]
    public enum LevelMechanicsFlags
    {
        None = 0,
        Roll = 1 << 0,
        Grow = 1 << 1,
        Shrink = 1 << 2,
        Split = 1 << 3,
        Merge = 1 << 4,
        Squeeze = 1 << 5,
        Momentum = 1 << 6,
        Timed = 1 << 7,
        MultiPath = 1 << 8,
        All = ~0
    }

    public enum LevelObjectType
    {
        Platform,
        Ramp,
        Wall,
        MassPickup,
        MassDrain,
        CircularGate,
        NarrowSlotGate,
        MassThresholdGate,
        SplitGate,
        TimedGate,
        PressurePlate,
        DualSwitchGroup,
        PushableBlock,
        MomentumBarrier,
        MassSensitiveBridge,
        BalancePlatform,
        MovingPlatform,
        RotatingObstacle,
        SpringLauncher,
        FrictionZone
    }

    [Serializable]
    public struct LevelObjectPlacement
    {
        public string id;
        public LevelObjectType type;
        public Vector3 position;
        public Vector3 eulerRotation;
        public Vector3 scale;
        public float paramA;
        public float paramB;
        public string linkedTargetId;
    }

    [Serializable]
    public struct GateConfigEntry
    {
        public string gateId;
        public LevelObjectType gateType;
        public Vector3 position;
        public float apertureOrHalfWidth;
        public float minMass;
        public float maxMass;
        public bool requireSplit;
    }

    [Serializable]
    public struct CheckpointDefinition
    {
        public string checkpointId;
        public Vector3 position;
        public float radius;
    }

    [Serializable]
    public struct StarRatingRules
    {
        public float threeStarMaxTime;
        public float twoStarMaxTime;
        public float threeStarMinMass;
        public float twoStarMinMass;
    }

    [CreateAssetMenu(menuName = "Jelly/Level Definition", fileName = "Level_01")]
    public class JellyLevelDefinition : ScriptableObject
    {
        public string levelId = "Level_01";
        public string displayName = "First Roll";
        public int difficulty = 1;
        public float startingMass = 100f;
        public Vector3 startPosition = new Vector3(0f, 0.6f, 0f);
        public Vector3 startEulerRotation = Vector3.zero;
        public LevelMechanicsFlags allowedMechanics = LevelMechanicsFlags.All;
        public List<LevelObjectPlacement> objects = new List<LevelObjectPlacement>();
        public List<GateConfigEntry> gates = new List<GateConfigEntry>();
        public List<CheckpointDefinition> checkpoints = new List<CheckpointDefinition>();
        public Vector3 goalPosition = new Vector3(0f, 0.5f, 24f);
        public Vector3 goalScale = new Vector3(3f, 1.5f, 3f);
        public float requiredMinMassAtGoal = 20f;
        public bool requireSingleMergedJellyAtGoal = false;
        public float targetTimeSeconds = 25f;
        public float massPreservationTarget = 80f;
        public StarRatingRules ratingRules = new StarRatingRules
        {
            threeStarMaxTime = 20f,
            twoStarMaxTime = 35f,
            threeStarMinMass = 80f,
            twoStarMinMass = 50f
        };
        public string nextLevelId = "Level_02";
        public JellyLevelDefinition nextLevelReference;
    }
}
