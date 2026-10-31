using System;
using System.Collections.Generic;
using System.IO;
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

    public struct ValidationReport
    {
        public bool IsValid;
        public List<string> Errors;
        public List<string> Warnings;
    }

    public static class LevelValidator
    {
        public static ValidationReport ValidateSingle(JellyLevelDefinition def)
        {
            var report = new ValidationReport
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            if (def == null)
            {
                report.IsValid = false;
                report.Errors.Add("LevelDefinition is null.");
                return report;
            }

            if (string.IsNullOrWhiteSpace(def.levelId))
                AddError(ref report, "levelId is empty.");
            if (string.IsNullOrWhiteSpace(def.displayName))
                AddError(ref report, $"[{def.levelId}] displayName is empty.");
            if (def.startingMass < 20f || def.startingMass > 250f)
                AddError(ref report, $"[{def.levelId}] startingMass ({def.startingMass}) out of valid bounds [20, 250].");
            if (def.requiredMinMassAtGoal < 0f || def.requiredMinMassAtGoal > 250f)
                AddError(ref report, $"[{def.levelId}] requiredMinMassAtGoal ({def.requiredMinMassAtGoal}) is invalid.");
            if (def.targetTimeSeconds <= 0f)
                AddError(ref report, $"[{def.levelId}] targetTimeSeconds must be positive.");
            if (def.goalScale.x <= 0.1f || def.goalScale.y <= 0.1f || def.goalScale.z <= 0.1f)
                AddError(ref report, $"[{def.levelId}] goalScale is invalid.");
            if (Vector3.Distance(def.startPosition, def.goalPosition) < 2f)
                AddError(ref report, $"[{def.levelId}] startPosition and goalPosition are too close.");

            for (int i = 0; i < def.gates.Count; i++)
            {
                var g = def.gates[i];
                if (g.apertureOrHalfWidth <= 0.08f || g.apertureOrHalfWidth > 5f)
                    AddError(ref report, $"[{def.levelId}] Gate '{g.gateId}' aperture {g.apertureOrHalfWidth} out of physical bounds.");
                if (g.minMass < 0f || g.maxMass < g.minMass)
                    AddError(ref report, $"[{def.levelId}] Gate '{g.gateId}' mass range [{g.minMass}, {g.maxMass}] invalid.");
            }

            for (int i = 0; i < def.objects.Count; i++)
            {
                var o = def.objects[i];
                if ((o.type == LevelObjectType.MassPickup || o.type == LevelObjectType.MassDrain) && o.paramA < 0f)
                    AddError(ref report, $"[{def.levelId}] Object '{o.id}' has negative mass parameter {o.paramA}.");
            }

            return report;
        }

        public static ValidationReport ValidateCatalog(IEnumerable<JellyLevelDefinition> levels)
        {
            var report = new ValidationReport
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            if (levels == null)
            {
                AddError(ref report, "Catalog is null.");
                return report;
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lvl in levels)
            {
                var single = ValidateSingle(lvl);
                if (!single.IsValid)
                {
                    report.IsValid = false;
                    report.Errors.AddRange(single.Errors);
                }
                report.Warnings.AddRange(single.Warnings);

                if (lvl != null && !string.IsNullOrWhiteSpace(lvl.levelId))
                {
                    if (!seenIds.Add(lvl.levelId))
                        AddError(ref report, $"Duplicate Level ID detected: '{lvl.levelId}'.");
                }
            }

            return report;
        }

        private static void AddError(ref ValidationReport report, string msg)
        {
            report.IsValid = false;
            report.Errors.Add(msg);
        }
    }

    public struct LevelCompletionResult
    {
        public bool Completed;
        public string Reason;
        public float ElapsedSeconds;
        public float FinalMass;
        public int StarsEarned;
    }

    public static class LevelGoalEvaluator
    {
        public static LevelCompletionResult Evaluate(
            JellyLevelDefinition definition,
            float currentTotalMass,
            int activeFragmentCount,
            float elapsedSeconds)
        {
            if (definition == null)
                return new LevelCompletionResult { Completed = false, Reason = "Missing definition" };

            if (definition.requireSingleMergedJellyAtGoal && activeFragmentCount > 1)
            {
                return new LevelCompletionResult
                {
                    Completed = false,
                    Reason = "Fragments must reunite before completing the level",
                    ElapsedSeconds = elapsedSeconds,
                    FinalMass = currentTotalMass,
                    StarsEarned = 0
                };
            }

            if (currentTotalMass + 0.01f < definition.requiredMinMassAtGoal)
            {
                return new LevelCompletionResult
                {
                    Completed = false,
                    Reason = $"Insufficient mass ({currentTotalMass:F1} < {definition.requiredMinMassAtGoal:F1})",
                    ElapsedSeconds = elapsedSeconds,
                    FinalMass = currentTotalMass,
                    StarsEarned = 0
                };
            }

            int stars = 1;
            var r = definition.ratingRules;
            if (elapsedSeconds <= r.threeStarMaxTime && currentTotalMass >= r.threeStarMinMass - 0.01f)
                stars = 3;
            else if (elapsedSeconds <= r.twoStarMaxTime && currentTotalMass >= r.twoStarMinMass - 0.01f)
                stars = 2;

            return new LevelCompletionResult
            {
                Completed = true,
                Reason = "Goal reached",
                ElapsedSeconds = elapsedSeconds,
                FinalMass = currentTotalMass,
                StarsEarned = stars
            };
        }
    }

    public class CheckpointTrigger : MonoBehaviour
    {
        public string checkpointId;
        public CheckpointManager manager;
        public bool Activated { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            var jc = other.GetComponentInParent<JellyController>();
            if (jc != null && manager != null)
            {
                Activated = true;
                float m = jc.massController != null ? jc.massController.CurrentMass : 100f;
                manager.ActivateCheckpoint(checkpointId, transform.position + Vector3.up * 0.6f, m);
            }
        }
    }

    public class CheckpointManager : MonoBehaviour
    {
        public string ActiveCheckpointId { get; private set; }
        public Vector3 RespawnPosition { get; private set; }
        public float RespawnMass { get; private set; } = 100f;

        public void InitializeStart(Vector3 startPos, float startMass)
        {
            ActiveCheckpointId = "START";
            RespawnPosition = startPos;
            RespawnMass = startMass;
        }

        public void ActivateCheckpoint(string id, Vector3 pos, float mass)
        {
            ActiveCheckpointId = id;
            RespawnPosition = pos;
            RespawnMass = Mathf.Max(20f, mass);
        }

        public void RespawnPlayer(JellyController player)
        {
            if (player == null) return;
            player.ResetState(RespawnPosition, Quaternion.identity, RespawnMass);
        }
    }

    [Serializable]
    public class LevelProgressRecord
    {
        public string levelId;
        public bool unlocked;
        public bool completed;
        public int bestStars;
        public float bestTimeSeconds;
        public float bestRemainingMass;
    }

    [Serializable]
    public class SaveGameData
    {
        public int version = 1;
        public string lastPlayedLevelId = "Level_01";
        public bool soundEnabled = true;
        public bool hapticsEnabled = true;
        public int qualityTier = 1; // 0 = 30FPS mobile low, 1 = 60FPS default
        public List<LevelProgressRecord> levels = new List<LevelProgressRecord>();
    }

    public class SaveGameService
    {
        private readonly string _customFilePath;

        public SaveGameService(string customFilePath = null)
        {
            _customFilePath = customFilePath;
        }

        public string SaveFilePath =>
            !string.IsNullOrEmpty(_customFilePath)
                ? _customFilePath
                : Path.Combine(Application.persistentDataPath, "jelly_save_v1.json");

        public SaveGameData LoadOrCreate()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    var data = JsonUtility.FromJson<SaveGameData>(json);
                    if (data != null && ValidateSaveData(data))
                        return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveGameService] Failed to load save, creating default: {e.Message}");
            }

            var fresh = new SaveGameData();
            fresh.levels.Add(new LevelProgressRecord
            {
                levelId = "Level_01",
                unlocked = true,
                completed = false,
                bestStars = 0,
                bestTimeSeconds = 0f,
                bestRemainingMass = 0f
            });
            return fresh;
        }

        public bool Save(SaveGameData data)
        {
            if (!ValidateSaveData(data)) return false;
            try
            {
                string dir = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SaveFilePath, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameService] Failed to save: {e.Message}");
                return false;
            }
        }

        public static bool ValidateSaveData(SaveGameData data)
        {
            if (data == null || data.version <= 0 || data.levels == null) return false;
            for (int i = 0; i < data.levels.Count; i++)
            {
                var r = data.levels[i];
                if (string.IsNullOrWhiteSpace(r.levelId)) return false;
                if (r.bestStars < 0 || r.bestStars > 3) return false;
                if (r.bestTimeSeconds < 0f || r.bestRemainingMass < 0f) return false;
            }
            return true;
        }
    }

    public class LevelProgressionService
    {
        private readonly SaveGameService _saveService;
        public SaveGameData Data { get; private set; }

        public LevelProgressionService(SaveGameService saveService)
        {
            _saveService = saveService;
            Data = _saveService.LoadOrCreate();
        }

        public LevelProgressRecord GetOrCreateRecord(string levelId)
        {
            for (int i = 0; i < Data.levels.Count; i++)
            {
                if (string.Equals(Data.levels[i].levelId, levelId, StringComparison.OrdinalIgnoreCase))
                    return Data.levels[i];
            }
            var rec = new LevelProgressRecord
            {
                levelId = levelId,
                unlocked = string.Equals(levelId, "Level_01", StringComparison.OrdinalIgnoreCase)
            };
            Data.levels.Add(rec);
            return rec;
        }

        public void RecordCompletion(JellyLevelDefinition level, LevelCompletionResult result)
        {
            if (level == null || !result.Completed) return;
            var rec = GetOrCreateRecord(level.levelId);
            rec.unlocked = true;
            rec.completed = true;
            rec.bestStars = Mathf.Max(rec.bestStars, result.StarsEarned);
            if (rec.bestTimeSeconds <= 0.001f || result.ElapsedSeconds < rec.bestTimeSeconds)
                rec.bestTimeSeconds = result.ElapsedSeconds;
            rec.bestRemainingMass = Mathf.Max(rec.bestRemainingMass, result.FinalMass);

            string nextId = level.nextLevelReference != null ? level.nextLevelReference.levelId : level.nextLevelId;
            if (!string.IsNullOrWhiteSpace(nextId))
            {
                var nextRec = GetOrCreateRecord(nextId);
                nextRec.unlocked = true;
                Data.lastPlayedLevelId = nextId;
            }
            _saveService.Save(Data);
        }
    }

    public class LevelGoalZone : MonoBehaviour
    {
        public LevelManager levelManager;

        private void OnTriggerEnter(Collider other)
        {
            var jc = other.GetComponentInParent<JellyController>();
            if (jc != null && levelManager != null)
                levelManager.NotifyGoalZoneEntered();
        }

        private void OnTriggerStay(Collider other)
        {
            var jc = other.GetComponentInParent<JellyController>();
            if (jc != null && levelManager != null && !levelManager.IsLevelCompleted)
                levelManager.NotifyGoalZoneEntered();
        }
    }

    public class LevelLoader : MonoBehaviour
    {
        public Transform levelRoot;
        private readonly Dictionary<string, ObstacleStateController> _obstaclesById = new Dictionary<string, ObstacleStateController>();

        public void ClearCurrentLevel()
        {
            _obstaclesById.Clear();
            if (levelRoot == null) return;
            for (int i = levelRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(levelRoot.GetChild(i).gameObject);
            }
        }

        public JellyController BuildLevelRuntime(JellyLevelDefinition def, LevelManager manager, CheckpointManager checkpointManager)
        {
            if (levelRoot == null)
            {
                var rootGo = new GameObject("LevelRuntimeRoot");
                levelRoot = rootGo.transform;
            }
            ClearCurrentLevel();

            // Base Track Floor if objects list does not specify custom floor
            bool hasCustomTrack = false;
            for (int i = 0; i < def.objects.Count; i++)
            {
                if (def.objects[i].type == LevelObjectType.Platform && def.objects[i].id.StartsWith("Track"))
                    hasCustomTrack = true;
            }

            if (!hasCustomTrack)
            {
                float dist = Mathf.Max(28f, def.goalPosition.z + 6f);
                CreateBox("MainTrackFloor", new Vector3(0f, -0.5f, dist * 0.5f - 3f), new Vector3(7f, 1f, dist), new Color(0.92f, 0.94f, 0.98f));
                CreateBox("LeftRail", new Vector3(-3.65f, 0.35f, dist * 0.5f - 3f), new Vector3(0.3f, 0.7f, dist), new Color(0.82f, 0.86f, 0.92f));
                CreateBox("RightRail", new Vector3(3.65f, 0.35f, dist * 0.5f - 3f), new Vector3(0.3f, 0.7f, dist), new Color(0.82f, 0.86f, 0.92f));
            }

            // Gates first so switches can link to them
            for (int i = 0; i < def.gates.Count; i++)
            {
                var g = def.gates[i];
                var gateGo = CreateBox($"Gate_{g.gateId}", g.position, new Vector3(6.8f, 2.2f, 0.5f), new Color(0.95f, 0.58f, 0.45f));
                var stateCtrl = gateGo.AddComponent<ObstacleStateController>();
                stateCtrl.visualDoor = gateGo.transform;
                _obstaclesById[g.gateId] = stateCtrl;

                switch (g.gateType)
                {
                    case LevelObjectType.CircularGate:
                        var cg = gateGo.AddComponent<CircularGate>();
                        cg.apertureRadius = g.apertureOrHalfWidth;
                        break;
                    case LevelObjectType.NarrowSlotGate:
                        var ng = gateGo.AddComponent<NarrowSlotGate>();
                        ng.slotHalfWidth = g.apertureOrHalfWidth;
                        ng.slotHalfHeight = 0.6f;
                        break;
                    case LevelObjectType.MassThresholdGate:
                        var mg = gateGo.AddComponent<MassThresholdGate>();
                        mg.minAllowedMass = g.minMass;
                        mg.maxAllowedMass = g.maxMass;
                        mg.apertureRadius = g.apertureOrHalfWidth;
                        break;
                    case LevelObjectType.SplitGate:
                        var sg = gateGo.AddComponent<SplitGate>();
                        sg.maxFragmentMass = g.maxMass > 0f ? g.maxMass : 60f;
                        sg.channelHalfWidth = g.apertureOrHalfWidth;
                        sg.requireSplitState = g.requireSplit;
                        break;
                    case LevelObjectType.TimedGate:
                        var tg = gateGo.AddComponent<TimedGate>();
                        tg.openDuration = Mathf.Max(0.5f, g.minMass);
                        tg.closedDuration = Mathf.Max(0.5f, g.maxMass);
                        break;
                }
            }

            // Level objects
            for (int i = 0; i < def.objects.Count; i++)
            {
                SpawnPlacement(def.objects[i]);
            }

            // Checkpoints
            for (int i = 0; i < def.checkpoints.Count; i++)
            {
                var cp = def.checkpoints[i];
                var cpGo = new GameObject($"Checkpoint_{cp.checkpointId}");
                cpGo.transform.SetParent(levelRoot, false);
                cpGo.transform.position = cp.position;
                var sc = cpGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = Mathf.Max(0.8f, cp.radius);
                var trig = cpGo.AddComponent<CheckpointTrigger>();
                trig.checkpointId = cp.checkpointId;
                trig.manager = checkpointManager;
            }

            // Goal Zone
            var goalGo = CreateBox("GoalZone", def.goalPosition, def.goalScale, new Color(0.45f, 0.92f, 0.65f));
            goalGo.GetComponent<Collider>().isTrigger = true;
            var goalZone = goalGo.AddComponent<LevelGoalZone>();
            goalZone.levelManager = manager;

            // Player Jelly
            JellyController player = SpawnPlayer(def.startPosition, Quaternion.Euler(def.startEulerRotation), def.startingMass);
            return player;
        }

        private void SpawnPlacement(LevelObjectPlacement p)
        {
            Vector3 scale = p.scale.sqrMagnitude > 0.001f ? p.scale : Vector3.one;
            Quaternion rot = Quaternion.Euler(p.eulerRotation);

            switch (p.type)
            {
                case LevelObjectType.Platform:
                case LevelObjectType.Ramp:
                case LevelObjectType.Wall:
                    var plat = CreateBox(p.id, p.position, scale, new Color(0.86f, 0.90f, 0.96f));
                    plat.transform.rotation = rot;
                    break;

                case LevelObjectType.MassPickup:
                    var pick = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    pick.name = $"Pickup_{p.id}";
                    pick.transform.SetParent(levelRoot, false);
                    pick.transform.SetPositionAndRotation(p.position, rot);
                    pick.transform.localScale = Vector3.one * 0.65f;
                    pick.GetComponent<Collider>().isTrigger = true;
                    ApplyColor(pick, new Color(0.35f, 0.95f, 0.55f));
                    var mp = pick.AddComponent<JellyMassPickup>();
                    mp.massGain = p.paramA > 0f ? p.paramA : 20f;
                    break;

                case LevelObjectType.MassDrain:
                    var drain = CreateBox($"Drain_{p.id}", p.position, scale, new Color(0.95f, 0.35f, 0.42f));
                    drain.GetComponent<Collider>().isTrigger = true;
                    var md = drain.AddComponent<JellyMassDrain>();
                    md.massLoss = p.paramA > 0f ? p.paramA : 20f;
                    md.continuousDrain = p.paramB > 0.5f;
                    break;

                case LevelObjectType.PressurePlate:
                    var plate = CreateBox($"PressurePlate_{p.id}", p.position, new Vector3(2.2f, 0.35f, 2.2f), new Color(0.98f, 0.82f, 0.32f));
                    plate.GetComponent<Collider>().isTrigger = true;
                    var wp = plate.AddComponent<WeightedPressurePlate>();
                    wp.requiredMass = p.paramA > 0f ? p.paramA : 110f;
                    if (!string.IsNullOrEmpty(p.linkedTargetId) && _obstaclesById.TryGetValue(p.linkedTargetId, out var targetObs))
                        wp.targetObstacle = targetObs;
                    break;

                case LevelObjectType.MomentumBarrier:
                    var bar = CreateBox($"MomentumBarrier_{p.id}", p.position, scale, new Color(0.95f, 0.48f, 0.25f));
                    var obs = bar.AddComponent<ObstacleStateController>();
                    obs.visualDoor = bar.transform;
                    var mb = bar.AddComponent<MomentumBreakableBarrier>();
                    mb.requiredMomentum = p.paramA > 0f ? p.paramA : 750f;
                    mb.stateController = obs;
                    break;

                case LevelObjectType.PushableBlock:
                    var blk = CreateBox($"PushBlock_{p.id}", p.position, scale, new Color(0.65f, 0.75f, 0.95f));
                    blk.AddComponent<Rigidbody>();
                    var pb = blk.AddComponent<PushableBlock>();
                    pb.minimumPlayerMassToPush = p.paramA > 0f ? p.paramA : 90f;
                    break;

                case LevelObjectType.MassSensitiveBridge:
                    var brg = CreateBox($"Bridge_{p.id}", p.position, scale, new Color(0.92f, 0.78f, 0.62f));
                    var msb = brg.AddComponent<MassSensitiveBridge>();
                    msb.maxSafeMass = p.paramA > 0f ? p.paramA : 120f;
                    break;

                case LevelObjectType.MovingPlatform:
                    var mov = CreateBox($"MovingPlatform_{p.id}", p.position, scale, new Color(0.55f, 0.85f, 0.95f));
                    var mpComp = mov.AddComponent<MovingPlatform>();
                    mpComp.pointA = p.position;
                    mpComp.pointB = p.position + new Vector3(p.paramA != 0f ? p.paramA : 3f, 0f, 0f);
                    mpComp.speed = p.paramB > 0f ? p.paramB : 1.5f;
                    break;

                case LevelObjectType.RotatingObstacle:
                    var rotGo = CreateBox($"RotatingObstacle_{p.id}", p.position, scale, new Color(0.95f, 0.45f, 0.55f));
                    var ro = rotGo.AddComponent<RotatingObstacle>();
                    ro.angularVelocityEuler = new Vector3(0f, p.paramA != 0f ? p.paramA : 90f, 0f);
                    break;

                case LevelObjectType.SpringLauncher:
                    var spr = CreateBox($"SpringLauncher_{p.id}", p.position, scale, new Color(0.4f, 0.95f, 0.85f));
                    spr.GetComponent<Collider>().isTrigger = true;
                    var sl = spr.AddComponent<SpringLauncher>();
                    sl.launchVelocity = new Vector3(0f, p.paramA > 0f ? p.paramA : 6.5f, p.paramB > 0f ? p.paramB : 11f);
                    break;

                case LevelObjectType.FrictionZone:
                    var fz = CreateBox($"FrictionZone_{p.id}", p.position, scale, new Color(0.70f, 0.92f, 0.99f));
                    var sfz = fz.AddComponent<SurfaceFrictionZone>();
                    sfz.frictionMultiplier = p.paramA > 0f ? p.paramA : 0.25f;
                    break;
            }
        }

        private GameObject CreateBox(string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(levelRoot, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            ApplyColor(go, color);
            return go;
        }

        public static void ApplyColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh != null)
            {
                var mat = new Material(sh) { color = c };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                r.sharedMaterial = mat;
            }
        }

        public JellyController SpawnPlayer(Vector3 pos, Quaternion rot, float startingMass)
        {
            GameObject playerGo = new GameObject("JellyPlayer");
            playerGo.transform.SetParent(levelRoot, false);
            playerGo.transform.SetPositionAndRotation(pos, rot);

            var rb = playerGo.AddComponent<Rigidbody>();
            var sc = playerGo.AddComponent<SphereCollider>();
            sc.radius = 0.5f;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Visual";
            DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(playerGo.transform, false);
            ApplyColor(vis, new Color(0.32f, 0.78f, 0.98f, 0.88f));

            var phys = playerGo.AddComponent<JellyPhysicsBody>();
            var gd = playerGo.AddComponent<JellyGroundDetector>();
            var input = playerGo.AddComponent<JellyInputAdapter>();
            var motor = playerGo.AddComponent<JellyMotor>();
            var morph = playerGo.AddComponent<JellyMorphController>();
            var deform = playerGo.AddComponent<JellyDeformationController>();
            deform.visualRoot = vis.transform;
            var massCtrl = playerGo.AddComponent<JellyMassController>();
            var controller = playerGo.AddComponent<JellyController>();
            var frag = playerGo.AddComponent<JellyFragment>();
            var reg = playerGo.AddComponent<JellyFragmentRegistry>();
            var pool = playerGo.AddComponent<JellyFragmentPool>();
            var router = playerGo.AddComponent<JellyFragmentInputRouter>();
            var split = playerGo.AddComponent<JellySplitController>();
            var merge = playerGo.AddComponent<JellyMergeController>();

            controller.ResetState(pos, rot, startingMass);
            split.EnsureSetup();
            return controller;
        }
    }

    public class LevelManager : MonoBehaviour
    {
        public List<JellyLevelDefinition> levelCatalog = new List<JellyLevelDefinition>();
        public int currentLevelIndex;
        public JellyLevelDefinition CurrentLevel { get; private set; }
        public JellyController ActivePlayer { get; private set; }
        public LevelLoader loader;
        public CheckpointManager checkpointManager;
        public JellyCameraController cameraController;

        public float ElapsedLevelTime { get; private set; }
        public bool IsLevelCompleted { get; private set; }
        public LevelCompletionResult LastCompletionResult { get; private set; }

        public SaveGameService SaveService { get; private set; }
        public LevelProgressionService ProgressionService { get; private set; }

        public event Action<JellyLevelDefinition> OnLevelLoaded;
        public event Action<LevelCompletionResult> OnLevelCompleted;

        private void Awake()
        {
            SaveService = new SaveGameService();
            ProgressionService = new LevelProgressionService(SaveService);
            if (loader == null) loader = GetComponent<LevelLoader>() ?? gameObject.AddComponent<LevelLoader>();
            if (checkpointManager == null) checkpointManager = GetComponent<CheckpointManager>() ?? gameObject.AddComponent<CheckpointManager>();
        }

        private void Start()
        {
            if (CurrentLevel == null && levelCatalog.Count > 0)
                LoadLevelByIndex(Mathf.Clamp(currentLevelIndex, 0, levelCatalog.Count - 1));
        }

        private void Update()
        {
            if (CurrentLevel != null && !IsLevelCompleted)
                ElapsedLevelTime += Time.deltaTime;
        }

        public void LoadLevel(JellyLevelDefinition def)
        {
            if (def == null) return;
            CurrentLevel = def;
            ElapsedLevelTime = 0f;
            IsLevelCompleted = false;
            LastCompletionResult = default;

            ActivePlayer = loader.BuildLevelRuntime(def, this, checkpointManager);
            checkpointManager.InitializeStart(def.startPosition, def.startingMass);
            ActivePlayer.OnFellIntoAbyss += HandlePlayerAbyss;

            if (cameraController == null)
                cameraController = FindAnyObjectByType<JellyCameraController>();
            if (cameraController != null)
            {
                cameraController.primaryTarget = ActivePlayer.transform;
                cameraController.fragmentRegistry = ActivePlayer.GetComponent<JellyFragmentRegistry>();
                cameraController.massController = ActivePlayer.massController;
                cameraController.SnapImmediate();
            }

            OnLevelLoaded?.Invoke(def);
        }

        public void LoadLevelByIndex(int index)
        {
            if (levelCatalog == null || levelCatalog.Count == 0) return;
            currentLevelIndex = Mathf.Clamp(index, 0, levelCatalog.Count - 1);
            LoadLevel(levelCatalog[currentLevelIndex]);
        }

        public void RestartCurrentLevel()
        {
            if (CurrentLevel != null)
                LoadLevel(CurrentLevel);
        }

        public void LoadNextLevel()
        {
            if (CurrentLevel != null && CurrentLevel.nextLevelReference != null)
            {
                int idx = levelCatalog.IndexOf(CurrentLevel.nextLevelReference);
                if (idx >= 0) currentLevelIndex = idx;
                LoadLevel(CurrentLevel.nextLevelReference);
                return;
            }
            if (levelCatalog.Count > 0)
            {
                currentLevelIndex = (currentLevelIndex + 1) % levelCatalog.Count;
                LoadLevel(levelCatalog[currentLevelIndex]);
            }
        }

        private void HandlePlayerAbyss(JellyController player)
        {
            checkpointManager.RespawnPlayer(player);
        }

        public LevelCompletionResult NotifyGoalZoneEntered()
        {
            if (CurrentLevel == null || IsLevelCompleted) return LastCompletionResult;
            var reg = ActivePlayer != null ? ActivePlayer.GetComponent<JellyFragmentRegistry>() : null;
            float totalMass = reg != null && reg.ActiveCount > 0
                ? reg.GetTotalActiveMass()
                : (ActivePlayer != null && ActivePlayer.massController != null ? ActivePlayer.massController.CurrentMass : 100f);
            int fragCount = reg != null && reg.ActiveCount > 0 ? reg.ActiveCount : 1;

            var res = LevelGoalEvaluator.Evaluate(CurrentLevel, totalMass, fragCount, ElapsedLevelTime);
            LastCompletionResult = res;
            if (res.Completed)
            {
                IsLevelCompleted = true;
                ProgressionService?.RecordCompletion(CurrentLevel, res);
                OnLevelCompleted?.Invoke(res);
            }
            return res;
        }
    }
}
