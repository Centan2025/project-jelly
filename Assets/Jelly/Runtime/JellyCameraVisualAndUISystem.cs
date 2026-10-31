using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Jelly
{
    public class JellyCameraController : MonoBehaviour
    {
        public Transform primaryTarget;
        public JellyFragmentRegistry fragmentRegistry;
        public JellyMassController massController;

        public Vector3 baseOffset = new Vector3(0f, 5.2f, -6.8f);
        public float lookAheadFactor = 0.25f;
        public float maxLookAhead = 2.4f;
        public float positionSmoothTime = 0.14f;
        public float rotationLerpSpeed = 10f;
        public float fragmentSpreadZoomFactor = 0.55f;
        public float massZoomFactor = 0.012f;
        public LayerMask obstructionMask = ~0;

        private Vector3 _velocityRef;
        private Vector3 _smoothedLookTarget;

        public void SnapImmediate()
        {
            Vector3 focus = ComputeFocusPoint();
            _smoothedLookTarget = focus;
            transform.position = ComputeDesiredCameraPosition(focus);
            transform.LookAt(focus + Vector3.up * 0.4f);
        }

        public Vector3 ComputeFocusPoint()
        {
            if (fragmentRegistry != null && fragmentRegistry.ActiveCount > 0)
                return fragmentRegistry.GetCentroid();
            if (primaryTarget != null)
                return primaryTarget.position;
            return Vector3.zero;
        }

        public Vector3 ComputeDesiredCameraPosition(Vector3 focus)
        {
            float spread = fragmentRegistry != null ? fragmentRegistry.GetMaxSpreadDistance() : 0f;
            float massDelta = massController != null ? (massController.CurrentMass - 100f) : 0f;
            float zoomScale = 1f + Mathf.Max(0f, spread * fragmentSpreadZoomFactor) + Mathf.Clamp(massDelta * massZoomFactor, -0.2f, 0.45f);

            Vector3 lookAhead = Vector3.zero;
            if (primaryTarget != null)
            {
                var rb = primaryTarget.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    lookAhead = Vector3.ClampMagnitude(flatVel * lookAheadFactor, maxLookAhead);
                }
            }

            Vector3 desired = focus + lookAhead + baseOffset * zoomScale;

            // Prevent wall obstruction
            Vector3 rayOrigin = focus + Vector3.up * 0.5f;
            Vector3 dir = desired - rayOrigin;
            float dist = dir.magnitude;
            if (dist > 0.2f && Physics.SphereCast(rayOrigin, 0.22f, dir.normalized, out RaycastHit hit, dist, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.GetComponentInParent<JellyController>() == null && !hit.collider.isTrigger)
                {
                    desired = rayOrigin + dir.normalized * Mathf.Max(1.5f, hit.distance - 0.25f);
                }
            }
            return desired;
        }

        public void StepCamera(float dt)
        {
            if (dt <= 0f) return;
            Vector3 focus = ComputeFocusPoint();
            Vector3 desiredPos = ComputeDesiredCameraPosition(focus);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocityRef, positionSmoothTime, Mathf.Infinity, dt);
            _smoothedLookTarget = Vector3.Lerp(_smoothedLookTarget, focus + Vector3.up * 0.4f, Mathf.Clamp01(dt * rotationLerpSpeed));
            Vector3 lookDir = _smoothedLookTarget - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized, Vector3.up), Mathf.Clamp01(dt * rotationLerpSpeed));
        }

        private void LateUpdate() => StepCamera(Time.deltaTime);
    }

    public class JellyFXPool : MonoBehaviour
    {
        public static JellyFXPool Instance { get; private set; }
        private readonly Queue<ParticleSystem> _pool = new Queue<ParticleSystem>();

        private void Awake() => Instance = this;

        public ParticleSystem SpawnBurst(Vector3 pos, Color color, int count = 12)
        {
            ParticleSystem ps = null;
            while (_pool.Count > 0 && ps == null)
                ps = _pool.Dequeue();

            if (ps == null)
            {
                var go = new GameObject("PooledJellyFX");
                go.transform.SetParent(transform, false);
                ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.duration = 0.45f;
                main.startLifetime = 0.4f;
                main.startSpeed = 3.5f;
                main.startSize = 0.16f;
                main.maxParticles = 32;
            }

            ps.transform.position = pos;
            var m = ps.main;
            m.startColor = color;
            ps.gameObject.SetActive(true);
            ps.Emit(count);
            return ps;
        }

        public void ReturnToPool(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            if (!_pool.Contains(ps))
                _pool.Enqueue(ps);
        }
    }

    public class JellySafeAreaFitter : MonoBehaviour
    {
        public RectTransform targetRect;
        private Rect _lastSafeArea;

        private void Awake()
        {
            if (targetRect == null) targetRect = GetComponent<RectTransform>();
            ApplySafeArea(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea)
                ApplySafeArea(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        public void ApplySafeArea(Rect safeArea, Vector2Int screenSize)
        {
            if (targetRect == null || screenSize.x <= 0 || screenSize.y <= 0) return;
            _lastSafeArea = safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;
            targetRect.anchorMin = anchorMin;
            targetRect.anchorMax = anchorMax;
        }
    }

    public static class JellyMobileQualityManager
    {
        public static int CurrentTargetFPS { get; private set; } = 60;

        public static void ApplyProfile(bool highPerformance60Fps)
        {
            CurrentTargetFPS = highPerformance60Fps ? 60 : 30;
            Application.targetFrameRate = CurrentTargetFPS;
            QualitySettings.vSyncCount = 0;
        }
    }

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
                massLabel.text = $"Mass: {m:F0} | Parts: {parts}";
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
            Time.timeScale = 1f;
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
