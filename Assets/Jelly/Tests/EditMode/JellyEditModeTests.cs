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
