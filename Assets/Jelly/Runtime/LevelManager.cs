using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public class LevelManager : MonoBehaviour
    {
        public List<JellyLevelDefinition> levelCatalog = new List<JellyLevelDefinition>();
        public int currentLevelIndex;
        public JellyLevelDefinition CurrentLevel;
        public JellyController ActivePlayer;
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
            if (levelCatalog == null || levelCatalog.Count == 0)
                levelCatalog = JellyLevelCatalogFactory.CreateVerticalSlice10();

            if (ActivePlayer == null)
            {
                var existingPlayer = FindAnyObjectByType<JellyController>();
                if (existingPlayer != null && CurrentLevel != null)
                {
                    ActivePlayer = existingPlayer;
                    checkpointManager.InitializeStart(CurrentLevel.startPosition, CurrentLevel.startingMass);
                    ActivePlayer.OnFellIntoAbyss -= HandlePlayerAbyss;
                    ActivePlayer.OnFellIntoAbyss += HandlePlayerAbyss;
                    BindCamera(ActivePlayer);
                    OnLevelLoaded?.Invoke(CurrentLevel);
                    return;
                }
            }

            if (CurrentLevel == null && levelCatalog.Count > 0)
                LoadLevelByIndex(Mathf.Clamp(currentLevelIndex, 0, levelCatalog.Count - 1));
            else if (CurrentLevel != null && ActivePlayer == null)
                LoadLevel(CurrentLevel);
        }

        private void Update()
        {
            if (CurrentLevel != null && !IsLevelCompleted)
                ElapsedLevelTime += Time.deltaTime;
        }

        public void LoadLevel(JellyLevelDefinition def)
        {
            if (def == null) return;
            if (loader == null) loader = GetComponent<LevelLoader>() ?? gameObject.AddComponent<LevelLoader>();
            if (checkpointManager == null) checkpointManager = GetComponent<CheckpointManager>() ?? gameObject.AddComponent<CheckpointManager>();

            CurrentLevel = def;
            ElapsedLevelTime = 0f;
            IsLevelCompleted = false;
            LastCompletionResult = default;

            ActivePlayer = loader.BuildLevelRuntime(def, this, checkpointManager);
            checkpointManager.InitializeStart(def.startPosition, def.startingMass);
            ActivePlayer.OnFellIntoAbyss -= HandlePlayerAbyss;
            ActivePlayer.OnFellIntoAbyss += HandlePlayerAbyss;

            BindCamera(ActivePlayer);
            OnLevelLoaded?.Invoke(def);
        }

        private void BindCamera(JellyController player)
        {
            if (cameraController == null)
                cameraController = FindAnyObjectByType<JellyCameraController>();
            if (cameraController != null && player != null)
            {
                cameraController.primaryTarget = player.transform;
                cameraController.fragmentRegistry = player.GetComponent<JellyFragmentRegistry>();
                cameraController.massController = player.massController;
                cameraController.SnapImmediate();
            }
        }

        public void LoadLevelByIndex(int index)
        {
            if (levelCatalog == null || levelCatalog.Count == 0)
                levelCatalog = JellyLevelCatalogFactory.CreateVerticalSlice10();
            currentLevelIndex = Mathf.Clamp(index, 0, levelCatalog.Count - 1);
            if (levelCatalog[currentLevelIndex] == null)
                levelCatalog[currentLevelIndex] = JellyLevelCatalogFactory.CreateVerticalSlice10()[Mathf.Clamp(currentLevelIndex, 0, 9)];
            LoadLevel(levelCatalog[currentLevelIndex]);
        }

        public void RestartCurrentLevel()
        {
            if (CurrentLevel != null)
                LoadLevel(CurrentLevel);
            else
                LoadLevelByIndex(currentLevelIndex);
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
                LoadLevelByIndex(currentLevelIndex);
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
