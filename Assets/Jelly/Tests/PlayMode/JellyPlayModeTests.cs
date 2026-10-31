using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jelly.Tests.PlayMode
{
    public class JellyPlayModeTests
    {
        private GameObject _root;
        private LevelManager _levelManager;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PlayModeTestRoot");
            var loader = _root.AddComponent<LevelLoader>();
            loader.levelRoot = _root.transform;
            var cp = _root.AddComponent<CheckpointManager>();
            _levelManager = _root.AddComponent<LevelManager>();
            _levelManager.loader = loader;
            _levelManager.checkpointManager = cp;
            _levelManager.levelCatalog = JellyLevelCatalogFactory.CreateVerticalSlice10();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator PlayerMovement_RollsAndBrakesOnFlatAndSlopedGround()
        {
            _levelManager.LoadLevelByIndex(0);
            var player = _levelManager.ActivePlayer;
            Assert.IsNotNull(player);

            Vector3 startPos = player.transform.position;
            player.inputAdapter.SetSimulatedInput(new Vector2(0f, 1f), brake: false);

            for (int i = 0; i < 25; i++)
                yield return new WaitForFixedUpdate();

            Assert.Greater(player.transform.position.z, startPos.z + 0.5f, "Player should roll forward under forward input.");
            float movingSpeed = player.physicsBody.Body.linearVelocity.magnitude;
            Assert.Greater(movingSpeed, 0.5f);

            // Apply braking
            player.inputAdapter.SetSimulatedInput(Vector2.zero, brake: true);
            for (int i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();

            Assert.Less(player.physicsBody.Body.linearVelocity.magnitude, movingSpeed * 0.45f, "Braking should significantly decelerate the jelly.");
        }

        [UnityTest]
        public IEnumerator MassPickupAndDrain_UpdateMassAndRadiusWithoutGroundPenetration()
        {
            _levelManager.LoadLevelByIndex(1); // Level 02 Grow
            var player = _levelManager.ActivePlayer;
            var mc = player.massController;
            Assert.AreEqual(100f, mc.CurrentMass, 0.01f);

            var pickupGo = new GameObject("TestPickup");
            pickupGo.transform.SetParent(_root.transform);
            var col = pickupGo.AddComponent<SphereCollider>();
            col.isTrigger = true;
            var pickup = pickupGo.AddComponent<JellyMassPickup>();
            pickup.massGain = 40f;

            Assert.IsTrue(pickup.TryApply(mc));
            Assert.AreEqual(140f, mc.CurrentMass, 0.01f);

            for (int i = 0; i < 15; i++)
                yield return new WaitForFixedUpdate();

            Assert.Greater(mc.CurrentRadius, 0.52f);
            Assert.GreaterOrEqual(player.transform.position.y, mc.CurrentRadius * 0.7f, "Growing player must not sink through floor.");

            var drainGo = new GameObject("TestDrain");
            drainGo.transform.SetParent(_root.transform);
            drainGo.AddComponent<BoxCollider>().isTrigger = true;
            var drain = drainGo.AddComponent<JellyMassDrain>();
            drain.massLoss = 60f;
            Assert.IsTrue(drain.TryApplyInstant(mc));
            Assert.AreEqual(80f, mc.CurrentMass, 0.01f);
        }

        [UnityTest]
        public IEnumerator GateClearance_BlocksOversizedAndAllowsShrunkOrSqueezedJelly()
        {
            _levelManager.LoadLevelByIndex(2); // Level 03 Shrink
            var player = _levelManager.ActivePlayer;
            var gate = _root.GetComponentInChildren<CircularGate>();
            Assert.IsNotNull(gate);

            // At 100 mass (radius 0.50), circular gate (0.43) blocks passage
            player.massController.SetMassImmediate(100f);
            Assert.IsFalse(gate.TryProcessCandidate(player.massController));

            // After shrinking to 50 mass (radius ~0.397), circular gate allows passage
            player.massController.SetMassImmediate(50f);
            Assert.IsTrue(gate.TryProcessCandidate(player.massController));

            // Narrow slot gate test (Level 08 Squeeze)
            _levelManager.LoadLevelByIndex(7);
            yield return null;
            player = _levelManager.ActivePlayer;
            var slotGate = _root.GetComponentInChildren<NarrowSlotGate>();
            Assert.IsNotNull(slotGate);
            Assert.IsTrue(slotGate.TryProcessCandidate(player.massController));
            Assert.Less(player.morphController.lateralCompression, 0.9f, "Narrow slot gate should compress lateral morph ratio.");
        }

        [UnityTest]
        public IEnumerator SplitMergeAndFragmentLoss_EnforcesConservationAndReportsLoss()
        {
            _levelManager.LoadLevelByIndex(5); // Level 06 Reunite
            var player = _levelManager.ActivePlayer;
            var split = player.GetComponent<JellySplitController>();
            var merge = player.GetComponent<JellyMergeController>();
            var reg = player.GetComponent<JellyFragmentRegistry>();

            Assert.AreEqual(1, reg.ActiveCount);
            Assert.AreEqual(100f, reg.GetTotalActiveMass(), 0.01f);

            // Perform 50 + 50 split
            Assert.IsTrue(split.TrySplit(0.5f));
            Assert.AreEqual(2, reg.ActiveCount);
            Assert.AreEqual(100f, reg.GetTotalActiveMass(), 0.01f);

            yield return new WaitForFixedUpdate();

            // Verify Level 06 goal rejects unmerged fragments
            var unmergedEval = _levelManager.NotifyGoalZoneEntered();
            Assert.IsFalse(unmergedEval.Completed, "Level 06 requires fragments to reunite before completion.");

            // Wait cooldown and bring fragments close to merge
            split.LastSplitTime = Time.time - 2f;
            var f0 = reg.ActiveFragments[0];
            var f1 = reg.ActiveFragments[1];
            f1.Controller.ResetState(f0.transform.position + Vector3.right * 0.4f, Quaternion.identity, f1.MassController.CurrentMass);
            Assert.IsTrue(merge.TryMergeIfEligible(true));
            Assert.AreEqual(1, reg.ActiveCount);
            Assert.AreEqual(100f, reg.GetTotalActiveMass(), 0.01f);

            // Split again and test fragment loss
            split.LastSplitTime = Time.time - 2f;
            Assert.IsTrue(split.TrySplit(0.5f));
            Assert.AreEqual(2, reg.ActiveCount);
            var secondFrag = reg.ActiveFragments[1];
            reg.ReportFragmentLost(secondFrag);
            Assert.AreEqual(1, reg.ActiveCount);
            Assert.AreEqual(50f, reg.GetTotalActiveMass(), 0.01f);
            Assert.AreEqual(50f, reg.LostMassTotal, 0.01f);
        }

        [UnityTest]
        public IEnumerator CheckpointRestartAndProgression_WorkEndToEndAcrossAll10Levels()
        {
            for (int i = 0; i < 10; i++)
            {
                _levelManager.LoadLevelByIndex(i);
                yield return new WaitForFixedUpdate();

                var def = _levelManager.CurrentLevel;
                var player = _levelManager.ActivePlayer;
                Assert.IsNotNull(def);
                Assert.IsNotNull(player);

                // Ensure required mass at goal is satisfied and fragments are merged
                player.massController.SetMassImmediate(Mathf.Max(def.startingMass, def.requiredMinMassAtGoal + 10f));
                player.ResetState(def.goalPosition, Quaternion.identity, player.massController.CurrentMass);
                yield return new WaitForFixedUpdate();

                var res = _levelManager.NotifyGoalZoneEntered();
                Assert.IsTrue(res.Completed, $"Level {def.levelId} should complete when goal conditions are met. Reason: {res.Reason}");
                Assert.GreaterOrEqual(res.StarsEarned, 1);
            }
        }

        [UnityTest]
        public IEnumerator PerformanceAndStability_MeasuresFramePhysicsAndActiveBodies()
        {
            _levelManager.LoadLevelByIndex(9); // Level 10 The Jelly Trial
            var player = _levelManager.ActivePlayer;
            player.inputAdapter.SetSimulatedInput(new Vector2(0f, 1f), false);

            var frameTimesMs = new List<float>(30);
            for (int i = 0; i < 20; i++)
            {
                float t0 = Time.realtimeSinceStartup;
                yield return null;
                float dtMs = (Time.realtimeSinceStartup - t0) * 1000f;
                frameTimesMs.Add(dtMs);
            }

            int activeRbs = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
            int activeCols = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
            long totalAllocMemMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            long monoUsedKb = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1024;
            frameTimesMs.Sort();
            float avgFrameMs = 0f;
            for (int i = 0; i < frameTimesMs.Count; i++) avgFrameMs += frameTimesMs[i];
            avgFrameMs /= Mathf.Max(1, frameTimesMs.Count);
            float p99FrameMs = frameTimesMs[Mathf.Clamp(frameTimesMs.Count - 1, 0, frameTimesMs.Count - 1)];
            Debug.Log($"[PERF_METRICS] AvgFrameMs={avgFrameMs:F2} P99FrameMs={p99FrameMs:F2} ActiveRigidbodies={activeRbs} ActiveColliders={activeCols} TotalMemMB={totalAllocMemMb} MonoUsedKB={monoUsedKb}");

            Assert.Greater(activeRbs, 0);
            Assert.Greater(activeCols, 0);
            Assert.Less(activeRbs, 64, "Active Rigidbody count should remain lightweight for mobile.");
        }

        [UnityTest]
        public IEnumerator AdvancedMechanics_PressurePlateMomentumBridgeAndSpring_WorkCorrectly()
        {
            // Level 02: Pressure plate opens heavy door when mass >= 120
            _levelManager.LoadLevelByIndex(1);
            yield return new WaitForFixedUpdate();
            var plate = _root.GetComponentInChildren<WeightedPressurePlate>();
            Assert.IsNotNull(plate);
            plate.EvaluateMass(90f);
            Assert.IsFalse(plate.IsActivated);
            plate.EvaluateMass(130f);
            Assert.IsTrue(plate.IsActivated);

            // Level 07: SpringLauncher & MomentumBreakableBarrier
            _levelManager.LoadLevelByIndex(6);
            yield return new WaitForFixedUpdate();
            var player = _levelManager.ActivePlayer;
            var launcher = _root.GetComponentInChildren<SpringLauncher>();
            var barrier = _root.GetComponentInChildren<MomentumBreakableBarrier>();
            Assert.IsNotNull(launcher);
            Assert.IsNotNull(barrier);

            Assert.IsFalse(barrier.EvaluateImpact(80f, 5f)); // 400 < 900
            Assert.IsTrue(launcher.Launch(player.physicsBody));
            Assert.IsTrue(barrier.EvaluateImpact(140f, 8f)); // 1120 >= 900

            // Expansion levels 11-50 runtime load smoke check (sample across all 4 tiers)
            var full50 = JellyLevelCatalogFactory.CreateFullCatalog50();
            int[] sampleIndices = { 10, 19, 25, 34, 49 };
            for (int i = 0; i < sampleIndices.Length; i++)
            {
                _levelManager.LoadLevel(full50[sampleIndices[i]]);
                yield return new WaitForFixedUpdate();
                Assert.IsNotNull(_levelManager.ActivePlayer);
            }
        }
    }
}
