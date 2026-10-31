# Project Jelly — Automated Test Report

**Test Runner:** Unity Test Framework (`1.4.2`) on Unity `6000.5.6f1`  
**Execution Mode:** Real Unity Batchmode (`EditMode` & `PlayMode`)  
**Overall Automated Test Status:** `PASS` (`14 / 14` tests passed, `0` failed)  
**Hardware Device Verification Status:** `NOT VERIFIED` (No physical Android device attached to `adb devices`)

---

## 1. EditMode Test Suite (`Logs/EditModeResults.xml`)
- **Total:** `7` | **Passed:** `7` | **Failed:** `0` | **Duration:** `0.0609s`

| Test ID | Test Name | Result | Duration | Verified Requirement |
|---|---|---|---|---|
| `1002` | `MassFormula_CubeRootRadiusScaling_IsAccurate` | `Passed` | `0.0004s` | $r = r_0 (m/m_0)^{1/3}$ exact cube-root scaling |
| `1003` | `MassModel_MinMaxBounds_AreClampedCorrectly` | `Passed` | `0.0004s` | Clamps mass within `[20, 200]` |
| `1004` | `MassConservation_SplitAndMerge_ConservesTotalMass` | `Passed` | `0.0005s` | $100 \to 50+50$, $100 \to 70+30$, $50+50 \to 100$, and fragment loss conservation |
| `1005` | `GateClearanceEvaluator_ThresholdsAndBoundaryConditions_AreCorrect` | `Passed` | `0.0009s` | Circular, NarrowSlot, and SplitGate boundary math |
| `1006` | `LevelValidator_ValidatesAll50LevelsAndRejectsInvalidData` | `Passed` | `0.0218s` | Validates all 50 levels (`Level_01`..`Level_50`) and catches duplicate IDs |
| `1007` | `CheckpointManager_StateTransitions_UpdateRespawnState` | `Passed` | `0.0138s` | Checkpoint activation and respawn state persistence |
| `1008` | `SaveGameService_PersistsAndValidatesProgression` | `Passed` | `0.0068s` | JSON save/load, validation, star rating, and next level unlock |

---

## 2. PlayMode Test Suite (`Logs/PlayModeResults.xml`)
- **Total:** `7` | **Passed:** `7` | **Failed:** `0` | **Duration:** `1.8098s`

| Test ID | Test Name | Result | Duration | Verified Requirement |
|---|---|---|---|---|
| `1002` | `PlayerMovement_RollsAndBrakesOnFlatAndSlopedGround` | `Passed` | `0.8864s` | Physics rolling in `FixedUpdate` and controlled braking deceleration |
| `1003` | `MassPickupAndDrain_UpdateMassAndRadiusWithoutGroundPenetration` | `Passed` | `0.2897s` | `+40` pickup and `-60` drain with safe vertical elevation |
| `1004` | `GateClearance_BlocksOversizedAndAllowsShrunkOrSqueezedJelly` | `Passed` | `0.0099s` | `CircularGate` blocks `100` mass, passes `50` mass; `NarrowSlotGate` squeezes morph |
| `1005` | `SplitMergeAndFragmentLoss_EnforcesConservationAndReportsLoss` | `Passed` | `0.0214s` | Runtime split, Level 06 unmerged rejection, proximity merge, and fragment loss |
| `1006` | `CheckpointRestartAndProgression_WorkEndToEndAcrossAll10Levels` | `Passed` | `0.3949s` | End-to-end runtime load & goal completion across `Level_01`..`Level_10` |
| `1007` | `PerformanceAndStability_MeasuresFramePhysicsAndActiveBodies` | `Passed` | `0.0127s` | Frame timing, active Rigidbody/Collider count, and memory profiling |
| `1008` | `AdvancedMechanics_PressurePlateMomentumBridgeAndSpring_WorkCorrectly` | `Passed` | `0.1754s` | `WeightedPressurePlate`, `SpringLauncher`, `MomentumBreakableBarrier`, and Levels 11–50 runtime loading |

---

## 3. Level 01–10 Independent Verification Matrix

| Level ID | Name | Core Mechanics Tested | Validation | PlayMode Load & Completion | Status |
|---|---|---|---|---|---|
| `Level_01` | First Roll | Flat/slope movement, `CircularGate`, goal | `PASS` | `PASS` | `PASS` |
| `Level_02` | Grow | `JellyMassPickup`, `WeightedPressurePlate`, `MassThresholdGate` | `PASS` | `PASS` | `PASS` |
| `Level_03` | Shrink | `JellyMassDrain`, small `CircularGate` (`0.43m`) | `PASS` | `PASS` | `PASS` |
| `Level_04` | Choose Your Size | Dual-path grow vs. shrink routing | `PASS` | `PASS` | `PASS` |
| `Level_05` | Split | `100 -> 50 + 50` split + `SplitGate` | `PASS` | `PASS` | `PASS` |
| `Level_06` | Reunite | `SplitGate` + merge + `requireSingleMergedJellyAtGoal` | `PASS` | `PASS` | `PASS` |
| `Level_07` | Heavy Impact | Pickup growth + `SpringLauncher` + `MomentumBreakableBarrier` | `PASS` | `PASS` | `PASS` |
| `Level_08` | Squeeze | `NarrowSlotGate` (`0.35m` half-width) + lateral morph | `PASS` | `PASS` | `PASS` |
| `Level_09` | Mass Survival | Alternating pickups and drains + mass preservation goal | `PASS` | `PASS` | `PASS` |
| `Level_10` | The Jelly Trial | Combined squeeze, split, reunite, boost, and momentum barrier | `PASS` | `PASS` | `PASS` |
