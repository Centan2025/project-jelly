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
        public int MultiHoleVariant; // -1 = auto, 0 = SizeChoice, 1 = SplitChoice, 2 = RiskReward
        public bool HasBonusPickup;
        public EndlessPickupType BonusPickup;
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
    /// Short, sequential showcase & test level curriculum (Levels 1..15) so every 3D obstacle,
    /// pickup, and track mechanic can be seen and tested one by one in ~8–12 second stages.
    /// </summary>
    public static class JellyProgressiveMapGenerator
    {
        public const int TotalShowcaseLevels = 15;

        public static JellyLevelBlueprint GenerateLevel(int levelNumber)
        {
            levelNumber = Mathf.Max(1, levelNumber);
            int showcaseIdx = ((levelNumber - 1) % TotalShowcaseLevels) + 1;

            var bp = new JellyLevelBlueprint
            {
                LevelNumber = levelNumber,
                BiomeIndex = (levelNumber - 1) % 5,
                SpeedMultiplier = 1.0f,
                // Graceful waterpark S-curve matching Alternative 1
                CurveIntensity = 0.68f,
                LevelTitle = GetLevelThemeTitle(showcaseIdx, levelNumber)
            };

            // Clean 12m Warm-Up Runway at start so the camera has an unobstructed sightline
            bp.Chunks.Add(EmptyRunwayChunk());

            BuildSequentialTestStages(bp, showcaseIdx);

            // Clean 12m Breathing Runway before Finale Multiplier walls
            bp.Chunks.Add(EmptyRunwayChunk());

            // Finale Multiplier chunk at the end (~60m total)
            bp.Chunks.Add(new TrackChunkSpec { ModuleType = TrackSegmentModuleType.LevelFinaleMultiplier });

            bp.TotalTrackLength = bp.Chunks.Count * 12f;
            return bp;
        }

        public static string GetLevelThemeTitle(int showcaseIdx, int rawLevelNumber)
        {
            switch (showcaseIdx)
            {
                case 1: return $"LVL {rawLevelNumber}: 1. DAR GEÇİT & KÜÇÜLME (-)";
                case 2: return $"LVL {rawLevelNumber}: 2. PİSTON KAPI & YAVAŞ ZAMAN (⏳)";
                case 3: return $"LVL {rawLevelNumber}: 3. KIRILABİLİR CAM DUVAR & BÜYÜME (+)";
                case 4: return $"LVL {rawLevelNumber}: 4. İKİZ TÜNEL & BÖLÜNME (÷2) / BİRLEŞME (><)";
                case 5: return $"LVL {rawLevelNumber}: 5A. ÇOKLU DELİK — BOYUT SEÇİMİ";
                case 6: return $"LVL {rawLevelNumber}: 5B. ÇOKLU DELİK — BÖLÜNME SEÇİMİ";
                case 7: return $"LVL {rawLevelNumber}: 5C. ÇOKLU DELİK — RİSK & ÖDÜL (⭐)";
                case 8: return $"LVL {rawLevelNumber}: 6. HAREKETLİ İKİZ TÜNEL & HIZ BANDI (⚡)";
                case 9: return $"LVL {rawLevelNumber}: 7. SARKAÇ (PENDULUM) & KALKAN (🛡)";
                case 10: return $"LVL {rawLevelNumber}: 8. DÖNEN ENGEL (SPINNER) & MIKNATIS (🧲)";
                case 11: return $"LVL {rawLevelNumber}: 9. ESNEK GEÇİT & KÜTLE TERAZİSİ";
                case 12: return $"LVL {rawLevelNumber}: 10. HAREKETLİ DELİK & PARÇALANMA KAPISI";
                case 13: return $"LVL {rawLevelNumber}: 11. KÖPRÜ RAMPA & İKİZ KÖPRÜ";
                case 14: return $"LVL {rawLevelNumber}: 12. MIKNATIS ALANI, YAPIŞKAN ZEMİN & ZIPLAMA PEDİ";
                default: return $"LVL {rawLevelNumber}: 13. LAZER KESİCİ & RENK EŞLEŞTİRME";
            }
        }

        private static void BuildSequentialTestStages(JellyLevelBlueprint bp, int showcaseIdx)
        {
            switch (showcaseIdx)
            {
                case 1: // 1. Dar Geçit (Gate_Narrow) + Shrink (-)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.SmallCenterHole));
                    bp.Chunks.Add(EmptyRunwayChunk());
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.SmallCenterHole));
                    break;

                case 2: // 2. Piston Kapı (Gate_Piston) + SlowTime (⏳)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.VerticalCrusherGate, true, EndlessPickupType.SlowTime));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.VerticalCrusherGate, true, EndlessPickupType.SlowTime));
                    break;

                case 3: // 3. Kırılabilir Cam Duvar (Obstacle_GlassWall) + Grow (+)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.HeavySmashWall));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.HeavySmashWall));
                    break;

                case 4: // 4. İkiz Tünel (Tunnel_Twin) + Split (÷2) & Merge (><)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.TwinSplitTunnels));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.TwinSplitTunnels));
                    break;

                case 5: // 5A. Çoklu Delik Duvarı — Boyut Seçimi (SizeChoice)
                    bp.Chunks.Add(MultiHoleChunk(0));
                    bp.Chunks.Add(MultiHoleChunk(0));
                    break;

                case 6: // 5B. Çoklu Delik Duvarı — Bölünme Seçimi (SplitChoice)
                    bp.Chunks.Add(MultiHoleChunk(1));
                    bp.Chunks.Add(MultiHoleChunk(1));
                    break;

                case 7: // 5C. Çoklu Delik Duvarı — Risk–Ödül Seçimi (RiskReward) + Star (⭐)
                    bp.Chunks.Add(MultiHoleChunk(2, true, EndlessPickupType.StarScore));
                    bp.Chunks.Add(MultiHoleChunk(2, true, EndlessPickupType.StarScore));
                    break;

                case 8: // 6. Hareketli İkiz Tünel (Tunnel_Moving) + SpeedBoost (⚡)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.MovingTwinTunnels, true, EndlessPickupType.SpeedBoost));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.MovingTwinTunnels, true, EndlessPickupType.SpeedBoost));
                    break;

                case 9: // 7. Sarkaç (Obstacle_Pendulum) + Shield (🛡)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.SwingingPendulum, true, EndlessPickupType.Shield));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.SwingingPendulum, true, EndlessPickupType.Shield));
                    break;

                case 10: // 8. Dönen Engel (Obstacle_Spinner) + Magnet (🧲) & Star (⭐)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.SpinnerBar, true, EndlessPickupType.Magnet));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.SpinnerBar, true, EndlessPickupType.StarScore));
                    break;

                case 11: // 9. Faz 4: Esnek Geçit (Gate_FlexibleSqueeze) + Kütle Terazisi (Gate_MassScale)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.FlexibleSqueezeGate));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.MassScaleGate));
                    break;

                case 12: // 10. Faz 4/6: Hareketli Delik Duvarı (Gate_SlidingHoleWall) + Parçalanma Kapısı (Gate_MicroShatter)
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.SlidingHoleWall));
                    bp.Chunks.Add(GateChunk(EndlessObstacleType.MicroShatterGate));
                    break;

                case 13: // 11. Köprü Rampa (JumpRampOverAbyss) + İkiz Köprü (SplitTwinBridge)
                    bp.Chunks.Add(new TrackChunkSpec { ModuleType = TrackSegmentModuleType.JumpRampOverAbyss });
                    bp.Chunks.Add(new TrackChunkSpec { ModuleType = TrackSegmentModuleType.SplitTwinBridge });
                    break;

                case 14: // 12. Yüzey Tuzakları: Mıknatıs Alanı, Yapışkan Zemin, Zıplama Pedi
                    bp.Chunks.Add(HazardChunk(SurfaceHazardKind.MagneticField));
                    bp.Chunks.Add(HazardChunk(SurfaceHazardKind.StickyFloor));
                    bp.Chunks.Add(HazardChunk(SurfaceHazardKind.JumpPad));
                    break;

                default: // 13. Lazer Kesici + Renk Eşleştirme Kapısı
                    bp.Chunks.Add(new TrackChunkSpec
                    {
                        ModuleType = TrackSegmentModuleType.LaserSlicerGate,
                        LaserKind = LaserGateKind.BlueVerticalAutoSplitter
                    });
                    bp.Chunks.Add(new TrackChunkSpec
                    {
                        ModuleType = TrackSegmentModuleType.ColorMatchSection,
                        ColorIndex = 0
                    });
                    break;
            }
        }

        private static TrackChunkSpec EmptyRunwayChunk()
        {
            return new TrackChunkSpec
            {
                ModuleType = TrackSegmentModuleType.StandardRoad,
                HasGateObstacle = false,
                MultiHoleVariant = -1
            };
        }

        private static TrackChunkSpec GateChunk(
            EndlessObstacleType gate,
            bool hasBonus = false,
            EndlessPickupType bonus = EndlessPickupType.StarScore)
        {
            return new TrackChunkSpec
            {
                ModuleType = TrackSegmentModuleType.StandardRoad,
                HasGateObstacle = true,
                GateType = gate,
                MultiHoleVariant = -1,
                HasBonusPickup = hasBonus,
                BonusPickup = bonus
            };
        }

        private static TrackChunkSpec MultiHoleChunk(
            int variantIndex,
            bool hasBonus = false,
            EndlessPickupType bonus = EndlessPickupType.StarScore)
        {
            return new TrackChunkSpec
            {
                ModuleType = TrackSegmentModuleType.StandardRoad,
                HasGateObstacle = true,
                GateType = EndlessObstacleType.MultiHoleDecisionWall,
                MultiHoleVariant = variantIndex,
                HasBonusPickup = hasBonus,
                BonusPickup = bonus
            };
        }

        private static TrackChunkSpec HazardChunk(SurfaceHazardKind hazard)
        {
            return new TrackChunkSpec
            {
                ModuleType = TrackSegmentModuleType.SurfaceHazardZone,
                HazardKind = hazard,
                HasGateObstacle = false,
                MultiHoleVariant = -1
            };
        }
    }
}
