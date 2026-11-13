using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jelly
{
    public class JellyUIManager : MonoBehaviour
    {
        public LevelManager levelManager;
        public EndlessTrackManager endlessManager;
        public GameObject mainMenuPanel;
        public GameObject hudPanel;
        public GameObject pausePanel;
        public GameObject levelCompletePanel;
        public Text massLabel;
        public Text levelTitleLabel;
        public Text completionSummaryLabel;

        private string _rejectionHint;
        private float _rejectionHintTimer;

        private void Start()
        {
            JellyMobileQualityManager.ApplyProfile(true);
            if (endlessManager == null) endlessManager = FindAnyObjectByType<EndlessTrackManager>();
            if (levelManager == null) levelManager = FindAnyObjectByType<LevelManager>();
            if (levelManager != null)
            {
                levelManager.OnLevelLoaded += HandleLevelLoaded;
                levelManager.OnLevelCompleted += HandleLevelCompleted;
                levelManager.OnGoalRejected += HandleGoalRejected;
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
                levelManager.OnGoalRejected -= HandleGoalRejected;
            }
        }

        private void Update()
        {
            if (endlessManager == null)
                endlessManager = FindAnyObjectByType<EndlessTrackManager>();

            if (endlessManager != null && endlessManager.enabled)
            {
                if (hudPanel != null && hudPanel.activeSelf)
                    hudPanel.SetActive(false);
                if (mainMenuPanel != null && mainMenuPanel.activeSelf)
                    mainMenuPanel.SetActive(false);
                gameObject.SetActive(false);
                return;
            }

            if (_rejectionHintTimer > 0f)
                _rejectionHintTimer -= Time.deltaTime;

            if (levelManager != null && levelManager.ActivePlayer != null && massLabel != null)
            {
                var reg = levelManager.ActivePlayer.GetComponent<JellyFragmentRegistry>();
                float m = reg != null && reg.ActiveCount > 0
                    ? reg.GetTotalActiveMass()
                    : levelManager.ActivePlayer.massController.CurrentMass;
                int parts = reg != null && reg.ActiveCount > 0 ? reg.ActiveCount : 1;
                float reqMass = levelManager.CurrentLevel != null ? levelManager.CurrentLevel.requiredMinMassAtGoal : 60f;
                string baseStatus = $"Kütle: {m:F0} (Hedef: {reqMass:F0}+) | Parça: {parts}  [WASD: Hareket | Space: Bölün | E: Birleş | R: Yeniden | N/P: Bölüm Geç]";
                massLabel.text = _rejectionHintTimer > 0f ? $"{baseStatus}\n⚠ {_rejectionHint}" : baseStatus;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame)
                    OnClickRestart();
                else if (kb.nKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                    OnClickNextLevel();
                else if (kb.pKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame)
                    OnClickPrevLevel();
                else if (kb.digit1Key.wasPressedThisFrame) JumpToLevel(0);
                else if (kb.digit2Key.wasPressedThisFrame) JumpToLevel(1);
                else if (kb.digit3Key.wasPressedThisFrame) JumpToLevel(2);
                else if (kb.digit4Key.wasPressedThisFrame) JumpToLevel(3);
                else if (kb.digit5Key.wasPressedThisFrame) JumpToLevel(4);
                else if (kb.digit6Key.wasPressedThisFrame) JumpToLevel(5);
                else if (kb.digit7Key.wasPressedThisFrame) JumpToLevel(6);
                else if (kb.digit8Key.wasPressedThisFrame) JumpToLevel(7);
                else if (kb.digit9Key.wasPressedThisFrame) JumpToLevel(8);
                else if (kb.digit0Key.wasPressedThisFrame) JumpToLevel(9);
            }
        }

        private void JumpToLevel(int index)
        {
            CancelInvoke(nameof(AutoAdvanceToNextLevel));
            if (Application.isPlaying) Time.timeScale = 1f;
            levelManager?.LoadLevelByIndex(index);
        }

        private void HandleLevelLoaded(JellyLevelDefinition def)
        {
            CancelInvoke(nameof(AutoAdvanceToNextLevel));
            _rejectionHintTimer = 0f;
            if (levelTitleLabel != null && def != null)
                levelTitleLabel.text = $"{def.levelId.Replace('_', ' ')} — {def.displayName}";
            ShowHUD();
        }

        private void HandleLevelCompleted(LevelCompletionResult res)
        {
            if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
            if (completionSummaryLabel != null)
                completionSummaryLabel.text = $"BÖLÜM TAMAMLANDI! ({res.StarsEarned}/3 Yıldız)\nSüre: {res.ElapsedSeconds:F1}sn | Kütle: {res.FinalMass:F0}\nSonraki bölüme geçiliyor...";

            CancelInvoke(nameof(AutoAdvanceToNextLevel));
            if (Application.isPlaying)
                Invoke(nameof(AutoAdvanceToNextLevel), 1.15f);
        }

        private void HandleGoalRejected(LevelCompletionResult res)
        {
            _rejectionHint = res.Reason;
            _rejectionHintTimer = 2.5f;
        }

        private void AutoAdvanceToNextLevel()
        {
            OnClickNextLevel();
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
            CancelInvoke(nameof(AutoAdvanceToNextLevel));
            if (Application.isPlaying) Time.timeScale = 1f;
            levelManager?.RestartCurrentLevel();
        }

        public void OnClickNextLevel()
        {
            CancelInvoke(nameof(AutoAdvanceToNextLevel));
            if (Application.isPlaying) Time.timeScale = 1f;
            levelManager?.LoadNextLevel();
        }

        public void OnClickPrevLevel()
        {
            CancelInvoke(nameof(AutoAdvanceToNextLevel));
            if (Application.isPlaying) Time.timeScale = 1f;
            levelManager?.LoadPreviousLevel();
        }
    }
}
