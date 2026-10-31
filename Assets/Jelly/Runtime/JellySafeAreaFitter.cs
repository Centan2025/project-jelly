using UnityEngine;

namespace Jelly
{
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
}
