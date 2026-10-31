using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Jelly
{
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
        public int qualityTier = 1;
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
}
