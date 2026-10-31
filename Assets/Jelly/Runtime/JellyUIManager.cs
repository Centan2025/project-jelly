using UnityEngine;
using UnityEngine.UI;

namespace Jelly
{
    public class JellyUIManager : MonoBehaviour
    {
        public LevelManager levelManager;
        public GameObject mainMenuPanel;
        public GameObject hudPanel;
        public GameObject pausePanel;
        public GameObject levelCompletePanel;
        public Text massLabel;
        public Text levelTitleLabel;
        public Text completionSummaryLabel;

        private void Start()
        {
            JellyMobileQualityManager.ApplyProfile(true);
            if (levelManager == null) levelManager = FindAnyObjectByType<LevelManager>();
            if (levelManager != null)
            {
                levelManager.OnLevelLoaded += HandleLevelLoaded;
                levelManager.OnLevelCompleted += HandleLevelCompleted;
                if (levelManager.CurrentLevel != null)
                    HandleLevelLoaded(levelManager.CurrentLevel);
            }
            ShowHUD();
        }

        private void OnDestroy()
        {
            if (levelManager != null)
            {
                levelManager.OnLevelLoaded -= HandleLevelLoaded;
                levelManager.OnLevelCompleted -= HandleLevelCompleted;
            }
        }

        private void Update()
        {
            if (levelManager != null && levelManager.ActivePlayer != null && massLabel != null)
            {
                var reg = levelManager.ActivePlayer.GetComponent<JellyFragmentRegistry>();
                float m = reg != null && reg.ActiveCount > 0
                    ? reg.GetTotalActiveMass()
                    : levelManager.ActivePlayer.massController.CurrentMass;
                int parts = reg != null && reg.ActiveCount > 0 ? reg.ActiveCount : 1;
                massLabel.text = $"Mass: {m:F0} | Parts: {parts}  [WASD/Drag: Roll | Space: Split | E: Merge | R: Restart]";
            }
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
            {
                OnClickRestart();
            }
        }

        private void HandleLevelLoaded(JellyLevelDefinition def)
        {
            if (levelTitleLabel != null && def != null)
                levelTitleLabel.text = $"{def.levelId.Replace('_', ' ')} — {def.displayName}";
            ShowHUD();
        }

        private void HandleLevelCompleted(LevelCompletionResult res)
        {
            if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
            if (completionSummaryLabel != null)
                completionSummaryLabel.text = $"COMPLETED!\nTime: {res.ElapsedSeconds:F1}s | Mass: {res.FinalMass:F0} | Stars: {res.StarsEarned}/3";
        }

        public void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (hudPanel != null) hudPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        }

        public void ShowHUD()
        {
            if (Application.isPlaying) Time.timeScale = 1f;
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(true);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        }

        public void TogglePause()
        {
            bool paused = Time.timeScale < 0.5f;
            Time.timeScale = paused ? 1f : 0f;
            if (pausePanel != null) pausePanel.SetActive(!paused);
        }

        public void OnClickSplit()
        {
            if (levelManager?.ActivePlayer == null) return;
            var split = levelManager.ActivePlayer.GetComponent<JellySplitController>();
            var merge = levelManager.ActivePlayer.GetComponent<JellyMergeController>();
            if (split != null && split.IsSplit && merge != null)
                merge.TryMergeIfEligible(true);
            else if (split != null)
                split.TrySplit();
        }

        public void OnClickRestart()
        {
            Time.timeScale = 1f;
            levelManager?.RestartCurrentLevel();
        }

        public void OnClickNextLevel()
        {
            Time.timeScale = 1f;
            levelManager?.LoadNextLevel();
        }
    }
}
