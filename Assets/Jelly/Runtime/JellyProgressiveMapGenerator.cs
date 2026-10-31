using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public struct TrackChunkSpec
    {
        public TrackSegmentModuleType ModuleType;
        public bool HasGateObstacle;
        public EndlessObstacleType GateType;
        public SurfaceHazardKind HazardKind;
        public LaserGateKind LaserKind;
        public int ColorIndex;
    }

    public class JellyLevelBlueprint
    {
        public int LevelNumber;
        public string LevelTitle;
        public int BiomeIndex;
        public float SpeedMultiplier;
        public float CurveIntensity;
        public float TotalTrackLength;
        public readonly List<TrackChunkSpec> Chunks = new List<TrackChunkSpec>();
    }

    /// <summary>
    /// Core Progressive Map & Level Architecture ("Harita Geliştirme Sistemi").
    /// Generates structured, progressively harder multi-mechanic 3D courses for Level 1..999+.
    /// </summary>
    public static class JellyProgressiveMapGenerator
    {
        public static JellyLevelBlueprint GenerateLevel(int levelNumber)
        {
            levelNumber = Mathf.Max(1, levelNumber);
            var bp = new JellyLevelBlueprint
            {
                LevelNumber = levelNumber,
                BiomeIndex = (levelNumber - 1) % 3,
                SpeedMultiplier = Mathf.Min(1.55f, 1.0f + (levelNumber - 1) * 0.045f),
                CurveIntensity = Mathf.Min(1.30f, 0.72f + (levelNumber - 1) * 0.05f)
            };

            bp.LevelTitle = GetLevelThemeTitle(levelNumber);

            // Opening safe runway chunk (0m .. 12m)
            bp.Chunks.Add(new TrackChunkSpec { ModuleType = TrackSegmentModuleType.StandardRoad });

            // Number of challenge stages grows with level (from 7 chunks in Level 1 up to 16+ chunks in higher levels)
            int stageCount = Mathf.Clamp(6 + levelNumber, 7, 16);
            var pool = BuildAllowedModulePool(levelNumber);

            for (int i = 0; i < stageCount; i++)
            {
                TrackChunkSpec spec = CreateStageSpec(levelNumber, i, pool);
                bp.Chunks.Add(spec);

                // Add a recovery/transition road chunk after intense aerial/bridge modules so pacing feels great
                if (spec.ModuleType == TrackSegmentModuleType.JumpRampOverAbyss ||
                    spec.ModuleType == TrackSegmentModuleType.DoubleDeckerOverpass ||
                    spec.ModuleType == TrackSegmentModuleType.SplitTwinBridge)
                {
                    bp.Chunks.Add(new TrackChunkSpec
                    {
                        ModuleType = TrackSegmentModuleType.StandardRoad,
                        HasGateObstacle = (levelNumber >= 3 && i % 2 == 1),
                        GateType = PickGateForLevel(levelNumber, i + 3)
                    });
                }
            }

            // Finale Multiplier Runway (2 chunks at the end: x2 -> x3 -> x5 -> x10 + Finish Arch)
            bp.Chunks.Add(new TrackChunkSpec { ModuleType = TrackSegmentModuleType.LevelFinaleMultiplier });
            bp.Chunks.Add(new TrackChunkSpec { ModuleType = TrackSegmentModuleType.LevelFinaleMultiplier });

            bp.TotalTrackLength = bp.Chunks.Count * 12f;
            return bp;
        }

        private static string GetLevelThemeTitle(int lvl)
        {
            switch (lvl)
            {
                case 1: return "SEVİYE 1: İLK MORFOLOJİ & RAMPA";
                case 2: return "SEVİYE 2: ÇATALLANAN İKİZ KÖPRÜLER";
                case 3: return "SEVİYE 3: LAZER KESİCİLER & KAYAR KAPI";
                case 4: return "SEVİYE 4: İKİ KATLI YOL & CAM KÖPRÜ";
                case 5: return "SEVİYE 5: KONVEYÖR BANT & SARKAÇLAR";
                case 6: return "SEVİYE 6: RENK BARİYERLERİ & MAKAS KAPI";
                default: return $"SEVİYE {lvl}: USTA PARKUR (Zorluk x{1f + (lvl - 1) * 0.08f:F1})";
            }
        }

        private static List<TrackSegmentModuleType> BuildAllowedModulePool(int lvl)
        {
            var list = new List<TrackSegmentModuleType>
            {
                TrackSegmentModuleType.StandardRoad,
                TrackSegmentModuleType.JumpRampOverAbyss,
                TrackSegmentModuleType.SplitTwinBridge
            };

            if (lvl >= 2)
            {
                list.Add(TrackSegmentModuleType.LaserSlicerGate);
            }
            if (lvl >= 3)
            {
                list.Add(TrackSegmentModuleType.DoubleDeckerOverpass);
                list.Add(TrackSegmentModuleType.SurfaceHazardZone);
            }
            if (lvl >= 4)
            {
                list.Add(TrackSegmentModuleType.ColorMatchSection);
            }
            return list;
        }

        private static TrackChunkSpec CreateStageSpec(int lvl, int stageIdx, List<TrackSegmentModuleType> pool)
        {
            // Guarantee signature showcase modules appear early so every level immediately feels rich and varied!
            TrackSegmentModuleType mod;
            if (stageIdx == 1)
                mod = TrackSegmentModuleType.JumpRampOverAbyss;
            else if (stageIdx == 2)
                mod = TrackSegmentModuleType.SplitTwinBridge;
            else if (stageIdx == 3 && lvl >= 1)
                mod = TrackSegmentModuleType.DoubleDeckerOverpass;
            else if (stageIdx == 4)
                mod = TrackSegmentModuleType.LaserSlicerGate;
            else if (stageIdx == 5)
                mod = TrackSegmentModuleType.SurfaceHazardZone;
            else if (stageIdx == 6)
                mod = TrackSegmentModuleType.ColorMatchSection;
            else
                mod = pool[(lvl * 3 + stageIdx * 5) % pool.Count];

            var spec = new TrackChunkSpec
            {
                ModuleType = mod,
                HasGateObstacle = (mod == TrackSegmentModuleType.StandardRoad || mod == TrackSegmentModuleType.SurfaceHazardZone),
                GateType = PickGateForLevel(lvl, stageIdx),
                HazardKind = (SurfaceHazardKind)((lvl + stageIdx) % 4),
                LaserKind = (stageIdx % 2 == 0) ? LaserGateKind.BlueVerticalAutoSplitter : LaserGateKind.RedHorizontalMassTrimmer,
                ColorIndex = (lvl + stageIdx) % 3
            };

            return spec;
        }

        private static EndlessObstacleType PickGateForLevel(int lvl, int seed)
        {
            int idx = (lvl * 2 + seed) % 10;
            switch (idx)
            {
                case 0: return EndlessObstacleType.SmallCenterHole;
                case 1: return EndlessObstacleType.SlidingDoorWall;
                case 2: return EndlessObstacleType.TwinSplitTunnels;
                case 3: return EndlessObstacleType.HeavySmashWall;
                case 4: return EndlessObstacleType.PincerShutters;
                case 5: return EndlessObstacleType.VerticalCrusherGate;
                case 6: return EndlessObstacleType.SwingingPendulum;
                case 7: return EndlessObstacleType.MovingTwinTunnels;
                case 8: return EndlessObstacleType.LeftOpeningWall;
                default: return EndlessObstacleType.RightOpeningWall;
            }
        }
    }
}
