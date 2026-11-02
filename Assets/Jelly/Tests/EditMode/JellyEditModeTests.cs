using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Jelly.Tests.EditMode
{
    public class JellyEditModeTests
    {
        [Test]
        public void MassFormula_CubeRootRadiusScaling_IsAccurate()
        {
            float baseMass = 100f;
            float baseRadius = 0.5f;

            float rBase = JellyMassModel.ComputeRadius(100f, baseMass, baseRadius);
            Assert.AreEqual(0.5f, rBase, 0.0001f);

            // 8x mass -> 2x radius
            float r8x = JellyMassModel.ComputeRadius(800f, baseMass, baseRadius);
            Assert.AreEqual(1.0f, r8x, 0.0001f);

            // 50 mass -> 0.5 * (0.5)^(1/3) = ~0.39685
            float rHalf = JellyMassModel.ComputeRadius(50f, baseMass, baseRadius);
            Assert.AreEqual(0.5f * Mathf.Pow(0.5f, 1f / 3f), rHalf, 0.0001f);
        }

        [Test]
        public void MassModel_MinMaxBounds_AreClampedCorrectly()
        {
            var model = new JellyMassModel(100f, 20f, 200f, 0.5f);
            Assert.AreEqual(100f, model.CurrentMass, 0.001f);

            model.AddMass(500f);
            Assert.AreEqual(200f, model.CurrentMass, 0.001f);

            model.AddMass(-500f);
            Assert.AreEqual(20f, model.CurrentMass, 0.001f);
        }

        [Test]
        public void MassConservation_SplitAndMerge_ConservesTotalMass()
        {
            bool ok50 = JellyMassConservation.TryComputeSplitMasses(100f, 0.5f, 20f, out float a1, out float b1);
            Assert.IsTrue(ok50);
            Assert.AreEqual(50f, a1, 0.001f);
            Assert.AreEqual(50f, b1, 0.001f);
            Assert.AreEqual(100f, JellyMassConservation.ComputeMergedMass(a1, b1), 0.001f);

            bool ok7030 = JellyMassConservation.TryComputeSplitMasses(100f, 0.7f, 20f, out float a2, out float b2);
            Assert.IsTrue(ok7030);
            Assert.AreEqual(70f, a2, 0.001f);
            Assert.AreEqual(30f, b2, 0.001f);
            Assert.AreEqual(100f, JellyMassConservation.ComputeMergedMass(a2, b2), 0.001f);

            // Reject split below minFragmentMass
            bool badSplit = JellyMassConservation.TryComputeSplitMasses(30f, 0.5f, 20f, out _, out _);
            Assert.IsFalse(badSplit);

            // Fragment loss reduces remaining mass without phantom restoration
            float remaining = JellyMassConservation.ComputeRemainingMassAfterLoss(100f, 50f);
            Assert.AreEqual(50f, remaining, 0.001f);
        }

        [Test]
        public void GateClearanceEvaluator_ThresholdsAndBoundaryConditions_AreCorrect()
        {
            var largeState = new GateCandidateState
            {
                Mass = 100f,
                Radius = 0.50f,
                MaxLateralCompressionRatio = 0.62f,
                MaxVerticalCompressionRatio = 0.65f,
                IsFragment = false,
                ActiveFragmentCount = 1
            };

            var smallState = new GateCandidateState
            {
                Mass = 50f,
                Radius = 0.397f,
                MaxLateralCompressionRatio = 0.62f,
                MaxVerticalCompressionRatio = 0.65f,
                IsFragment = true,
                ActiveFragmentCount = 2
            };

            // Circular gate (aperture 0.43): blocks 0.50, allows 0.397
            Assert.IsFalse(GateClearanceEvaluator.EvaluateCircular(largeState, 0.43f).CanPass);
            Assert.IsTrue(GateClearanceEvaluator.EvaluateCircular(smallState, 0.43f).CanPass);

            // Narrow slot gate: 0.35 half-width requires 0.35/0.50 = 0.70 compression (>= 0.62 limit -> pass)
            Assert.IsTrue(GateClearanceEvaluator.EvaluateNarrowSlot(largeState, 0.35f, 0.55f).CanPass);
            // Too narrow slot (0.20 half-width requires 0.40 < 0.62 -> fail)
            Assert.IsFalse(GateClearanceEvaluator.EvaluateNarrowSlot(largeState, 0.20f, 0.55f).CanPass);

            // Split gate: blocks unsplit 100 mass, allows split 50 mass fragment
            Assert.IsFalse(GateClearanceEvaluator.EvaluateSplit(largeState, 60f, 0.44f, true).CanPass);
            Assert.IsTrue(GateClearanceEvaluator.EvaluateSplit(smallState, 60f, 0.44f, true).CanPass);
        }

        [Test]
        public void Phase2Core1_SmallCenterHole_SuccessFailureBoundaryAndSplitScenarios()
        {
            // Success: Small jelly (mass 60 <= 75) aligned at center
            var pass = Phase2CoreObstacleEvaluator.EvaluateSmallCenterHole(60f, 0f, false);
            Assert.IsTrue(pass.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.CleanPass, pass.Outcome);
            Assert.IsFalse(pass.ShatteredCrystal);

            // Boundary mass: exact 75.0 passes, 75.5 fails
            var boundaryPass = Phase2CoreObstacleEvaluator.EvaluateSmallCenterHole(75.0f, 0f, false);
            Assert.IsTrue(boundaryPass.CanPass);
            var boundaryFail = Phase2CoreObstacleEvaluator.EvaluateSmallCenterHole(75.5f, 0f, false);
            Assert.IsFalse(boundaryFail.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.SolidWallBounce, boundaryFail.Outcome);
            Assert.IsFalse(boundaryFail.ShatteredCrystal);

            // Failure: Misaligned small jelly hits solid wall
            var misaligned = Phase2CoreObstacleEvaluator.EvaluateSmallCenterHole(60f, 1.85f, false);
            Assert.IsFalse(misaligned.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.SolidWallBounce, misaligned.Outcome);

            // Split jelly: centerX = +1.32 -> left fragment at 0.0 (in hole, fragment mass = 120*0.5 = 60 <= 75), right fragment hits wall -> PartialSplitSacrifice
            var splitSacrifice = Phase2CoreObstacleEvaluator.EvaluateSmallCenterHole(120f, 1.32f, true);
            Assert.IsTrue(splitSacrifice.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.PartialSplitSacrifice, splitSacrifice.Outcome);
            Assert.IsTrue(splitSacrifice.SacrificedOneSplitPiece);
            Assert.AreEqual(0f, splitSacrifice.SurvivingLateralX, 0.01f);
        }

        [Test]
        public void Phase2Core2_VerticalCrusherGate_SuccessFailureBoundaryAndSplitScenarios()
        {
            // Success: Small jelly (mass 70 <= 75) passes under low crusher (0.65m)
            var smallPass = Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(70f, 0f, false, 0.65f);
            Assert.IsTrue(smallPass.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.CleanPass, smallPass.Outcome);
            Assert.IsFalse(smallPass.ShatteredCrystal);

            // Boundary mass at low crusher (0.65m): 75.0 passes, 76.0 fails when crusher is low
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(75.0f, 0f, false, 0.65f).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(76.0f, 0f, false, 0.65f).CanPass);

            // Timing success: Normal jelly (mass 100) passes when crusher is raised (1.60m), fails when low (0.68m)
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(100f, 0f, false, 1.60f).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(100f, 0f, false, 0.68f).CanPass);

            // Split jelly: Total mass 120 (unsplit fails at 0.68m), when split each fragment is 60 (<= 75) -> passes under low crusher!
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(120f, 0f, false, 0.68f).CanPass);
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(120f, 0f, true, 0.68f).CanPass);
        }

        [Test]
        public void Phase2Core3_HeavySmashWall_SuccessFailureBoundaryAndSplitScenarios()
        {
            // Success: Giant jelly (mass 130 >= 115) aligned with crystal panel shatters crystal
            var smashPass = Phase2CoreObstacleEvaluator.EvaluateHeavySmashWall(130f, 0f, false);
            Assert.IsTrue(smashPass.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.CrystalShatterPass, smashPass.Outcome);
            Assert.IsTrue(smashPass.ShatteredCrystal);

            // Boundary mass: 115.0 shatters crystal, 114.5 bounces off without shattering
            var boundPass = Phase2CoreObstacleEvaluator.EvaluateHeavySmashWall(115.0f, 0f, false);
            Assert.IsTrue(boundPass.CanPass);
            Assert.IsTrue(boundPass.ShatteredCrystal);

            var boundFail = Phase2CoreObstacleEvaluator.EvaluateHeavySmashWall(114.5f, 0f, false);
            Assert.IsFalse(boundFail.CanPass);
            Assert.IsFalse(boundFail.ShatteredCrystal);
            Assert.AreEqual(Phase2ObstacleOutcome.SolidWallBounce, boundFail.Outcome);

            // Split jelly: Total mass 180 -> split fragments have 90 mass each (< 115) -> CANNOT shatter crystal!
            var splitInsufficient = Phase2CoreObstacleEvaluator.EvaluateHeavySmashWall(180f, 0f, true);
            Assert.IsFalse(splitInsufficient.CanPass);
            Assert.IsFalse(splitInsufficient.ShatteredCrystal);

            // Split jelly with massive total mass 240 -> each fragment has 120 (>= 115) -> CAN shatter crystal!
            var splitMassive = Phase2CoreObstacleEvaluator.EvaluateHeavySmashWall(240f, 0f, true);
            Assert.IsTrue(splitMassive.CanPass);
            Assert.IsTrue(splitMassive.ShatteredCrystal);
        }

        [Test]
        public void Phase2Core4_TwinSplitTunnels_SuccessFailureBoundaryAndSplitScenarios()
        {
            // Success: Split jelly (total mass 120 -> 60 each) centered at 0 -> left=-1.32, right=+1.32 -> both in tunnels!
            var twinPass = Phase2CoreObstacleEvaluator.EvaluateTwinSplitTunnels(120f, 0f, true);
            Assert.IsTrue(twinPass.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.CleanPass, twinPass.Outcome);
            Assert.IsFalse(twinPass.ShatteredCrystal);

            // Failure: Unsplit jelly centered at 0 hits the solid center divider
            var centerHit = Phase2CoreObstacleEvaluator.EvaluateTwinSplitTunnels(90f, 0f, false);
            Assert.IsFalse(centerHit.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.SolidWallBounce, centerHit.Outcome);

            // Boundary mass in single side tunnel: unsplit at X=-1.45 with mass 95.0 passes, 96.0 fails
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateTwinSplitTunnels(95.0f, -1.45f, false).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateTwinSplitTunnels(96.0f, -1.45f, false).CanPass);

            // Split partial sacrifice: shifted to X=+1.32 -> left fragment at 0.0 (hits center pillar), right fragment at +2.64 (hits outer wall) -> bounce;
            // shifted to X=+0.50 -> left fragment at -0.82 (inside left tunnel), right fragment at +1.82 (inside right tunnel);
            // shifted to X=+1.15 -> left fragment at -0.17 (hits center pillar!), right fragment at +2.47 (hits outer post) -> bounce;
            // shifted to X=-0.95 -> left fragment at -2.27 (inside left tunnel), right fragment at +0.37 (hits center pillar!) -> PartialSplitSacrifice!
            var partial = Phase2CoreObstacleEvaluator.EvaluateTwinSplitTunnels(120f, -0.95f, true);
            Assert.IsTrue(partial.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.PartialSplitSacrifice, partial.Outcome);
            Assert.IsTrue(partial.SacrificedOneSplitPiece);
        }

        [Test]
        public void Phase2Core5_MultiHoleDecisionWall_AllThreeVariantsAndCustomConfig()
        {
            // Variant 1: SizeChoice (Left Small <=75 at X=-1.55, Right Normal 76..114 at X=+1.55)
            var sizeCfg = MultiHoleWallConfig.CreateSizeChoicePreset();
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(sizeCfg, 70f, -1.55f, false).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(sizeCfg, 95f, -1.55f, false).CanPass); // Too big for left small hole
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(sizeCfg, 95f, +1.55f, false).CanPass);  // Fits right normal hole
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(sizeCfg, 120f, +1.55f, false).CanPass); // Too big for normal hole (115+)

            // Variant 2: SplitChoice (Center Single <=70 at X=0, Twin Split at X=+-1.32 requiring split state)
            var splitCfg = MultiHoleWallConfig.CreateSplitChoicePreset();
            // Single small jelly (mass 68 <= 70) passes center hole
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(splitCfg, 68f, 0f, false).CanPass);
            // Single large jelly (mass 100) blocked at center AND blocked at side twin hole when unsplit
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(splitCfg, 100f, 0f, false).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(splitCfg, 70f, -1.32f, false).CanPass);
            // Split jelly (total mass 120 -> 60 each <= 85) centered at X=0 passes both left (-1.32) and right (+1.32) holes!
            var splitTwinRes = Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(splitCfg, 120f, 0f, true);
            Assert.IsTrue(splitTwinRes.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.CleanPass, splitTwinRes.Outcome);

            // Variant 3: RiskRewardChoice (Left Safe <=114 at X=-1.45, Right Bonus <=68 at X=+1.65)
            var riskCfg = MultiHoleWallConfig.CreateRiskRewardPreset();
            var safePass = Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(riskCfg, 100f, -1.45f, false);
            Assert.IsTrue(safePass.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.CleanPass, safePass.Outcome);
            Assert.AreEqual(30, safePass.ScoreAwarded);

            var bonusPass = Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(riskCfg, 65f, +1.65f, false);
            Assert.IsTrue(bonusPass.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.BonusRiskPass, bonusPass.Outcome);
            Assert.AreEqual(90, bonusPass.ScoreAwarded);
            Assert.AreEqual(2, bonusPass.ComboIncrement);

            // Boundary on Risk hole: 68.0 passes, 69.0 fails
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(riskCfg, 68.0f, +1.65f, false).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(riskCfg, 69.0f, +1.65f, false).CanPass);
        }

        [Test]
        public void Phase3MobileHUD_CapabilityIconsAndContextualButton_WorkCorrectly()
        {
            // 1) Mass 70 (<= 75): Small Gate icon lit, contextual button = "÷2  BÖLÜN", combo hidden when 0
            var vm70 = Phase3MobileHUDSystem.BuildViewModel(70f, false, 145.8f, 0, 0f, false);
            Assert.IsTrue(vm70.IsSmallGateIconLit);
            Assert.IsFalse(vm70.IsNormalSplitIconLit);
            Assert.IsFalse(vm70.IsCrystalSmashIconLit);
            Assert.AreEqual(145, vm70.DistanceMeters);
            Assert.AreEqual(ContextualActionKind.Split, vm70.ActionKind);
            Assert.IsTrue(vm70.ActionButtonInteractable);
            Assert.IsFalse(vm70.ShowComboBadge);
            Assert.IsFalse(vm70.ShowTransientBanner);
            Assert.IsFalse(vm70.ShowPowerupPill);

            // 2) Mass 120 (>= 115): Crystal Smash icon lit
            var vm120 = Phase3MobileHUDSystem.BuildViewModel(120f, false, 210f, 4, 1.5f, true);
            Assert.IsFalse(vm120.IsSmallGateIconLit);
            Assert.IsFalse(vm120.IsNormalSplitIconLit);
            Assert.IsTrue(vm120.IsCrystalSmashIconLit);
            Assert.IsTrue(vm120.ShowComboBadge);
            Assert.AreEqual(3, vm120.LiveComboMultiplier);
            Assert.IsTrue(vm120.ShowTransientBanner);
            Assert.IsTrue(vm120.ShowPowerupPill);

            // 3) Split state (total mass 120 -> 60 per piece): contextual button switches to "><  BİRLEŞ" & effective piece lights SmallGate icon
            var vmSplit = Phase3MobileHUDSystem.BuildViewModel(120f, true, 220f, 1, 0f, false);
            Assert.AreEqual(60f, vmSplit.EffectivePieceMass, 0.01f);
            Assert.IsTrue(vmSplit.IsSmallGateIconLit);
            Assert.AreEqual(ContextualActionKind.Merge, vmSplit.ActionKind);

            // 4) Single contextual button toggle behavior
            Assert.IsTrue(Phase3MobileHUDSystem.TryExecuteContextualAction(false, 90f, out bool afterSplit));
            Assert.IsTrue(afterSplit);
            Assert.IsTrue(Phase3MobileHUDSystem.TryExecuteContextualAction(true, 90f, out bool afterMerge));
            Assert.IsFalse(afterMerge);
            Assert.IsFalse(Phase3MobileHUDSystem.TryExecuteContextualAction(false, 30f, out _));
        }

        [Test]
        public void Phase4And6_NewObjectFamilies_Scenarios_PassAndFail()
        {
            // 1) Esnek Geçit (FlexibleSqueezeGate): <= 88 mass passes at center
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateFlexibleSqueezeGate(88f, 0f, false).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateFlexibleSqueezeGate(92f, 0f, false).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateFlexibleSqueezeGate(70f, 1.5f, false).CanPass);

            // 2) Kütle Terazisi (MassScaleGate): >= 105 total mass opens scale door
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateMassScaleGate(105f, 0f, false).CanPass);
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateMassScaleGate(110f, 0f, true).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateMassScaleGate(100f, 0f, false).CanPass);

            // 3) Hareketli Delik Duvarı (SlidingHoleWall): must match moving hole X and mass <= 95
            Assert.IsTrue(Phase2CoreObstacleEvaluator.EvaluateSlidingHoleWall(85f, 1.10f, false, 1.15f).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateSlidingHoleWall(85f, -1.20f, false, 1.15f).CanPass);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateSlidingHoleWall(110f, 1.15f, false, 1.15f).CanPass);

            // 4) Parçalanma Kapısı (MicroShatterGate - Faz 6): >= 50 mass in grid passes with bonus combo
            var shatterRes = Phase2CoreObstacleEvaluator.EvaluateMicroShatterGate(80f, 0f, false);
            Assert.IsTrue(shatterRes.CanPass);
            Assert.AreEqual(Phase2ObstacleOutcome.BonusRiskPass, shatterRes.Outcome);
            Assert.IsFalse(Phase2CoreObstacleEvaluator.EvaluateMicroShatterGate(40f, 0f, false).CanPass);
        }

        [Test]
        public void LevelValidator_ValidatesAll50LevelsAndRejectsInvalidData()
        {
            var catalog = JellyLevelCatalogFactory.CreateFullCatalog50();
            Assert.AreEqual(50, catalog.Count);

            var report = LevelValidator.ValidateCatalog(catalog);
            Assert.IsTrue(report.IsValid, string.Join("; ", report.Errors));

            // Duplicate ID detection
            var dupCatalog = new List<JellyLevelDefinition> { catalog[0], catalog[0] };
            var dupReport = LevelValidator.ValidateCatalog(dupCatalog);
            Assert.IsFalse(dupReport.IsValid);
        }

        [Test]
        public void CheckpointManager_StateTransitions_UpdateRespawnState()
        {
            var go = new GameObject("CPTest");
            var cp = go.AddComponent<CheckpointManager>();
            cp.InitializeStart(new Vector3(0f, 0.6f, 0f), 100f);
            Assert.AreEqual("START", cp.ActiveCheckpointId);

            cp.ActivateCheckpoint("CP_MID", new Vector3(0f, 0.6f, 12f), 120f);
            Assert.AreEqual("CP_MID", cp.ActiveCheckpointId);
            Assert.AreEqual(new Vector3(0f, 0.6f, 12f), cp.RespawnPosition);
            Assert.AreEqual(120f, cp.RespawnMass, 0.001f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SaveGameService_PersistsAndValidatesProgression()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "jelly_test_save.json");
            if (File.Exists(tempPath)) File.Delete(tempPath);

            var saveSvc = new SaveGameService(tempPath);
            var progSvc = new LevelProgressionService(saveSvc);

            var level01 = JellyLevelCatalogFactory.BuildLevel01_FirstRoll();
            level01.nextLevelId = "Level_02";

            var res = new LevelCompletionResult
            {
                Completed = true,
                ElapsedSeconds = 12.5f,
                FinalMass = 100f,
                StarsEarned = 3
            };
            progSvc.RecordCompletion(level01, res);

            var reloaded = saveSvc.LoadOrCreate();
            Assert.IsTrue(SaveGameService.ValidateSaveData(reloaded));
            Assert.AreEqual("Level_02", reloaded.lastPlayedLevelId);

            var prog2 = new LevelProgressionService(saveSvc);
            Assert.IsTrue(prog2.GetOrCreateRecord("Level_01").completed);
            Assert.AreEqual(3, prog2.GetOrCreateRecord("Level_01").bestStars);
            Assert.IsTrue(prog2.GetOrCreateRecord("Level_02").unlocked);

            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
