using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public static class JellyLevelCatalogFactory
    {
        public static List<JellyLevelDefinition> CreateVerticalSlice10()
        {
            var list = new List<JellyLevelDefinition>(10)
            {
                BuildLevel01_FirstRoll(),
                BuildLevel02_Grow(),
                BuildLevel03_Shrink(),
                BuildLevel04_ChooseYourSize(),
                BuildLevel05_Split(),
                BuildLevel06_Reunite(),
                BuildLevel07_HeavyImpact(),
                BuildLevel08_Squeeze(),
                BuildLevel09_MassSurvival(),
                BuildLevel10_TheJellyTrial()
            };

            for (int i = 0; i < list.Count - 1; i++)
            {
                list[i].nextLevelId = list[i + 1].levelId;
                list[i].nextLevelReference = list[i + 1];
            }
            list[list.Count - 1].nextLevelId = "Level_11";
            return list;
        }

        public static List<JellyLevelDefinition> CreateFullCatalog50()
        {
            var list = CreateVerticalSlice10();
            for (int n = 11; n <= 50; n++)
            {
                list.Add(BuildExpansionLevel(n));
            }
            for (int i = 0; i < list.Count - 1; i++)
            {
                list[i].nextLevelId = list[i + 1].levelId;
                list[i].nextLevelReference = list[i + 1];
            }
            list[list.Count - 1].nextLevelId = "";
            list[list.Count - 1].nextLevelReference = null;
            return list;
        }

        private static JellyLevelDefinition NewDef(string id, string name, int diff, float startMass, float goalZ, float minGoalMass)
        {
            var d = ScriptableObject.CreateInstance<JellyLevelDefinition>();
            d.name = id;
            d.levelId = id;
            d.displayName = name;
            d.difficulty = diff;
            d.startingMass = startMass;
            d.startPosition = new Vector3(0f, 0.65f, 0f);
            d.goalPosition = new Vector3(0f, 0.5f, goalZ);
            d.goalScale = new Vector3(4.5f, 1.5f, 3f);
            d.requiredMinMassAtGoal = minGoalMass;
            d.targetTimeSeconds = 18f + diff * 3f;
            d.massPreservationTarget = Mathf.Max(minGoalMass, startMass * 0.8f);
            d.ratingRules = new StarRatingRules
            {
                threeStarMaxTime = d.targetTimeSeconds * 0.8f,
                twoStarMaxTime = d.targetTimeSeconds * 1.4f,
                threeStarMinMass = d.massPreservationTarget,
                twoStarMinMass = minGoalMass
            };
            d.checkpoints.Add(new CheckpointDefinition
            {
                checkpointId = $"{id}_CP1",
                position = new Vector3(0f, 0.6f, goalZ * 0.5f),
                radius = 1.5f
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel01_FirstRoll()
        {
            var d = NewDef("Level_01", "First Roll", 1, 100f, 22f, 60f);
            d.allowedMechanics = LevelMechanicsFlags.Roll;
            d.objects.Add(new LevelObjectPlacement
            {
                id = "GentleRamp",
                type = LevelObjectType.Ramp,
                position = new Vector3(0f, 0.1f, 8f),
                eulerRotation = new Vector3(-6f, 0f, 0f),
                scale = new Vector3(5f, 0.3f, 5f)
            });
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L01_WideGate",
                gateType = LevelObjectType.CircularGate,
                position = new Vector3(0f, 1f, 15f),
                apertureOrHalfWidth = 0.72f,
                minMass = 20f,
                maxMass = 200f
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel02_Grow()
        {
            var d = NewDef("Level_02", "Grow", 1, 100f, 24f, 120f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Grow;
            d.objects.Add(new LevelObjectPlacement { id = "GrowPickup1", type = LevelObjectType.MassPickup, position = new Vector3(0f, 0.6f, 6f), paramA = 20f });
            d.objects.Add(new LevelObjectPlacement { id = "GrowPickup2", type = LevelObjectType.MassPickup, position = new Vector3(0f, 0.6f, 10f), paramA = 20f });
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L02_HeavyDoor",
                gateType = LevelObjectType.MassThresholdGate,
                position = new Vector3(0f, 1f, 17f),
                apertureOrHalfWidth = 0.85f,
                minMass = 120f,
                maxMass = 220f
            });
            d.objects.Add(new LevelObjectPlacement
            {
                id = "HeavySwitch",
                type = LevelObjectType.PressurePlate,
                position = new Vector3(0f, 0.1f, 14f),
                paramA = 120f,
                linkedTargetId = "L02_HeavyDoor"
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel03_Shrink()
        {
            var d = NewDef("Level_03", "Shrink", 2, 100f, 24f, 40f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Shrink;
            d.objects.Add(new LevelObjectPlacement { id = "DrainPad1", type = LevelObjectType.MassDrain, position = new Vector3(0f, 0.25f, 7f), scale = new Vector3(3f, 0.4f, 2f), paramA = 25f });
            d.objects.Add(new LevelObjectPlacement { id = "DrainPad2", type = LevelObjectType.MassDrain, position = new Vector3(0f, 0.25f, 11f), scale = new Vector3(3f, 0.4f, 2f), paramA = 25f });
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L03_SmallHole",
                gateType = LevelObjectType.CircularGate,
                position = new Vector3(0f, 1f, 16f),
                apertureOrHalfWidth = 0.43f, // radius at 50 mass is ~0.397 (<0.43), while at 100 mass is 0.50 (>0.43)
                minMass = 20f,
                maxMass = 70f
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel04_ChooseYourSize()
        {
            var d = NewDef("Level_04", "Choose Your Size", 2, 100f, 26f, 40f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Grow | LevelMechanicsFlags.Shrink | LevelMechanicsFlags.MultiPath;
            // Left path: grow + momentum barrier
            d.objects.Add(new LevelObjectPlacement { id = "LeftPickup1", type = LevelObjectType.MassPickup, position = new Vector3(-1.8f, 0.6f, 7f), paramA = 25f });
            d.objects.Add(new LevelObjectPlacement { id = "LeftPickup2", type = LevelObjectType.MassPickup, position = new Vector3(-1.8f, 0.6f, 10f), paramA = 25f });
            // Right path: shrink + small gate
            d.objects.Add(new LevelObjectPlacement { id = "RightDrain1", type = LevelObjectType.MassDrain, position = new Vector3(1.8f, 0.25f, 8f), scale = new Vector3(2f, 0.4f, 2f), paramA = 40f });
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L04_MultiGate",
                gateType = LevelObjectType.CircularGate,
                position = new Vector3(0f, 1f, 17f),
                apertureOrHalfWidth = 0.78f,
                minMass = 20f,
                maxMass = 200f
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel05_Split()
        {
            var d = NewDef("Level_05", "Split", 3, 100f, 26f, 60f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Split;
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L05_SplitGate",
                gateType = LevelObjectType.SplitGate,
                position = new Vector3(0f, 1f, 14f),
                apertureOrHalfWidth = 0.44f,
                minMass = 20f,
                maxMass = 60f,
                requireSplit = true
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel06_Reunite()
        {
            var d = NewDef("Level_06", "Reunite", 3, 100f, 28f, 90f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Split | LevelMechanicsFlags.Merge;
            d.requireSingleMergedJellyAtGoal = true;
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L06_SplitChannel",
                gateType = LevelObjectType.SplitGate,
                position = new Vector3(0f, 1f, 12f),
                apertureOrHalfWidth = 0.44f,
                minMass = 20f,
                maxMass = 60f,
                requireSplit = true
            });
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L06_ReunitedMassGate",
                gateType = LevelObjectType.MassThresholdGate,
                position = new Vector3(0f, 1f, 21f),
                apertureOrHalfWidth = 0.65f,
                minMass = 90f,
                maxMass = 200f
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel07_HeavyImpact()
        {
            var d = NewDef("Level_07", "Heavy Impact", 3, 100f, 28f, 110f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Grow | LevelMechanicsFlags.Momentum;
            d.objects.Add(new LevelObjectPlacement { id = "L07_Pickup1", type = LevelObjectType.MassPickup, position = new Vector3(0f, 0.6f, 5f), paramA = 20f });
            d.objects.Add(new LevelObjectPlacement { id = "L07_Pickup2", type = LevelObjectType.MassPickup, position = new Vector3(0f, 0.6f, 8f), paramA = 20f });
            d.objects.Add(new LevelObjectPlacement
            {
                id = "L07_SpeedBoost",
                type = LevelObjectType.SpringLauncher,
                position = new Vector3(0f, 0.3f, 12f),
                scale = new Vector3(3f, 0.4f, 2f),
                paramA = 1.5f,
                paramB = 12f
            });
            d.objects.Add(new LevelObjectPlacement
            {
                id = "L07_BreakableWall",
                type = LevelObjectType.MomentumBarrier,
                position = new Vector3(0f, 1f, 18f),
                scale = new Vector3(5.5f, 2f, 0.6f),
                paramA = 900f
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel08_Squeeze()
        {
            var d = NewDef("Level_08", "Squeeze", 4, 100f, 26f, 80f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Squeeze;
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L08_NarrowSlot",
                gateType = LevelObjectType.NarrowSlotGate,
                position = new Vector3(0f, 1f, 14f),
                apertureOrHalfWidth = 0.35f, // 0.35 / 0.50 = 0.70 >= 0.62 min compression ratio
                minMass = 20f,
                maxMass = 140f
            });
            return d;
        }

        public static JellyLevelDefinition BuildLevel09_MassSurvival()
        {
            var d = NewDef("Level_09", "Mass Survival", 4, 120f, 30f, 80f);
            d.allowedMechanics = LevelMechanicsFlags.Roll | LevelMechanicsFlags.Grow | LevelMechanicsFlags.Shrink;
            d.objects.Add(new LevelObjectPlacement { id = "L09_Pickup1", type = LevelObjectType.MassPickup, position = new Vector3(-1.2f, 0.6f, 6f), paramA = 20f });
            d.objects.Add(new LevelObjectPlacement { id = "L09_Hazard1", type = LevelObjectType.MassDrain, position = new Vector3(0f, 0.25f, 11f), scale = new Vector3(2.2f, 0.4f, 2f), paramA = 20f });
            d.objects.Add(new LevelObjectPlacement { id = "L09_Pickup2", type = LevelObjectType.MassPickup, position = new Vector3(1.2f, 0.6f, 16f), paramA = 20f });
            d.objects.Add(new LevelObjectPlacement { id = "L09_Hazard2", type = LevelObjectType.MassDrain, position = new Vector3(0f, 0.25f, 21f), scale = new Vector3(2.2f, 0.4f, 2f), paramA = 20f });
            return d;
        }

        public static JellyLevelDefinition BuildLevel10_TheJellyTrial()
        {
            var d = NewDef("Level_10", "The Jelly Trial", 5, 100f, 36f, 95f);
            d.allowedMechanics = LevelMechanicsFlags.All;
            d.requireSingleMergedJellyAtGoal = true;

            d.gates.Add(new GateConfigEntry
            {
                gateId = "L10_NarrowEntry",
                gateType = LevelObjectType.NarrowSlotGate,
                position = new Vector3(0f, 1f, 8f),
                apertureOrHalfWidth = 0.35f,
                minMass = 20f,
                maxMass = 140f
            });
            d.gates.Add(new GateConfigEntry
            {
                gateId = "L10_SplitSection",
                gateType = LevelObjectType.SplitGate,
                position = new Vector3(0f, 1f, 16f),
                apertureOrHalfWidth = 0.44f,
                minMass = 20f,
                maxMass = 60f,
                requireSplit = true
            });
            d.objects.Add(new LevelObjectPlacement { id = "L10_Pickup", type = LevelObjectType.MassPickup, position = new Vector3(0f, 0.6f, 23f), paramA = 20f });
            d.objects.Add(new LevelObjectPlacement
            {
                id = "L10_Booster",
                type = LevelObjectType.SpringLauncher,
                position = new Vector3(0f, 0.3f, 26f),
                scale = new Vector3(3.5f, 0.4f, 2f),
                paramA = 1.2f,
                paramB = 11f
            });
            d.objects.Add(new LevelObjectPlacement
            {
                id = "L10_FinalBarrier",
                type = LevelObjectType.MomentumBarrier,
                position = new Vector3(0f, 1f, 30f),
                scale = new Vector3(5.5f, 2f, 0.6f),
                paramA = 750f
            });
            return d;
        }

        public static JellyLevelDefinition BuildExpansionLevel(int number)
        {
            string id = $"Level_{number:D2}";
            int tier = (number - 1) / 10; // 1: 11-20, 2: 21-30, 3: 31-40, 4: 41-50
            string tierName = tier switch
            {
                1 => "Combined Mechanics",
                2 => "Multi-Path & Split",
                3 => "Momentum & Timing",
                _ => "Master Trial"
            };
            float startMass = 80f + (number % 4) * 20f;
            float goalZ = 26f + (number % 5) * 3f;
            float minGoalMass = Mathf.Clamp(startMass - 20f, 40f, 140f);

            var d = NewDef(id, $"{tierName} #{number - 10}", Mathf.Clamp(tier + 1, 2, 5), startMass, goalZ, minGoalMass);
            d.allowedMechanics = LevelMechanicsFlags.All;
            d.requireSingleMergedJellyAtGoal = (number % 3 == 0);

            // Vary puzzle sequence deterministically by level number
            d.objects.Add(new LevelObjectPlacement
            {
                id = $"{id}_PickA",
                type = LevelObjectType.MassPickup,
                position = new Vector3((number % 2 == 0) ? -1.2f : 1.2f, 0.6f, 6f),
                paramA = 20f
            });

            if (tier == 1)
            {
                d.gates.Add(new GateConfigEntry
                {
                    gateId = $"{id}_Slot",
                    gateType = LevelObjectType.NarrowSlotGate,
                    position = new Vector3(0f, 1f, 12f),
                    apertureOrHalfWidth = 0.36f,
                    minMass = 20f,
                    maxMass = 160f
                });
                d.gates.Add(new GateConfigEntry
                {
                    gateId = $"{id}_MassGate",
                    gateType = LevelObjectType.MassThresholdGate,
                    position = new Vector3(0f, 1f, 19f),
                    apertureOrHalfWidth = 0.75f,
                    minMass = minGoalMass,
                    maxMass = 220f
                });
            }
            else if (tier == 2)
            {
                d.gates.Add(new GateConfigEntry
                {
                    gateId = $"{id}_Split",
                    gateType = LevelObjectType.SplitGate,
                    position = new Vector3(0f, 1f, 13f),
                    apertureOrHalfWidth = 0.44f,
                    minMass = 20f,
                    maxMass = 70f,
                    requireSplit = true
                });
                d.objects.Add(new LevelObjectPlacement
                {
                    id = $"{id}_Bridge",
                    type = LevelObjectType.MassSensitiveBridge,
                    position = new Vector3(0f, 0.1f, 19f),
                    scale = new Vector3(4f, 0.3f, 4f),
                    paramA = 150f
                });
            }
            else if (tier == 3)
            {
                d.gates.Add(new GateConfigEntry
                {
                    gateId = $"{id}_Timed",
                    gateType = LevelObjectType.TimedGate,
                    position = new Vector3(0f, 1f, 12f),
                    apertureOrHalfWidth = 0.65f,
                    minMass = 1.8f,
                    maxMass = 2.2f
                });
                d.objects.Add(new LevelObjectPlacement
                {
                    id = $"{id}_Launcher",
                    type = LevelObjectType.SpringLauncher,
                    position = new Vector3(0f, 0.3f, 16f),
                    scale = new Vector3(3.5f, 0.4f, 2f),
                    paramA = 1.2f,
                    paramB = 11.5f
                });
                d.objects.Add(new LevelObjectPlacement
                {
                    id = $"{id}_BreakWall",
                    type = LevelObjectType.MomentumBarrier,
                    position = new Vector3(0f, 1f, 21f),
                    scale = new Vector3(5.5f, 2f, 0.6f),
                    paramA = 700f
                });
            }
            else
            {
                d.gates.Add(new GateConfigEntry
                {
                    gateId = $"{id}_Narrow",
                    gateType = LevelObjectType.NarrowSlotGate,
                    position = new Vector3(0f, 1f, 10f),
                    apertureOrHalfWidth = 0.36f,
                    minMass = 20f,
                    maxMass = 160f
                });
                d.gates.Add(new GateConfigEntry
                {
                    gateId = $"{id}_Split",
                    gateType = LevelObjectType.SplitGate,
                    position = new Vector3(0f, 1f, 16f),
                    apertureOrHalfWidth = 0.44f,
                    minMass = 20f,
                    maxMass = 70f,
                    requireSplit = true
                });
                d.objects.Add(new LevelObjectPlacement
                {
                    id = $"{id}_IceZone",
                    type = LevelObjectType.FrictionZone,
                    position = new Vector3(0f, 0.05f, 21f),
                    scale = new Vector3(5f, 0.2f, 4f),
                    paramA = 0.35f
                });
            }

            return d;
        }
    }
}
