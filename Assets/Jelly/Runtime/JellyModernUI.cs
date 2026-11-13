using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Jelly
{
    /// <summary>
    /// Modern, Safe-Area-aware uGUI interface for Project Jelly using procedural 9-slice rounded sprites.
    /// Replaces all legacy OnGUI() debug boxes and overflowing scene text.
    /// </summary>
    [ExecuteAlways]
    public class JellyModernUI : MonoBehaviour
    {
        private EndlessTrackManager _mgr;
        private Canvas _canvas;
        private RectTransform _safeRoot;
        private Font _uiFont;
        private Sprite _roundedSprite;
        private Sprite _circleSprite;

        // Gameplay HUD
        private GameObject _hudGroup;
        private Slider _progressSlider;
        private Text _levelBadgeText;
        private Text _massValueText;
        private Text _scoreValueText;
        private Button _splitMergeBtn;
        private Text _splitMergeText;
        private Image _splitMergeBg;

        // Main Menu (Swipe-to-Start Overlay)
        private GameObject _menuGroup;
        private Text _menuLevelText;
        private Text _menuHighScoreText;
        private RectTransform _swipeHintHand;

        // End-of-Run Overlay
        private GameObject _endGroup;
        private Text _endTitleText;
        private Text _endScoreText;
        private Text _endBestText;
        private Text _endNextBtnText;
        private Image _endCardBorder;

        // Pause / Settings Overlay
        private GameObject _settingsGroup;
        private Text _soundStateText;
        private Text _hapticStateText;
        private Text _conceptStateText;

        private bool _built;

        public void Bind(EndlessTrackManager manager)
        {
            _mgr = manager;
            var existingCanvas = transform.Find("JellyModernCanvas");
            if (existingCanvas != null)
            {
                if (Application.isPlaying) Destroy(existingCanvas.gameObject);
                else DestroyImmediate(existingCanvas.gameObject);
            }
            _built = false;
            BuildUI();
            RefreshUIState();
        }

        private void BuildUI()
        {
            _built = true;
            EnsureEventSystem();
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _roundedSprite = CreateRoundedRectSprite(64, 64, 28);
            _circleSprite = CreateCircleSprite(96);

            var canvasGo = new GameObject("JellyModernCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var safeGo = new GameObject("SafeAreaRoot");
            safeGo.transform.SetParent(canvasGo.transform, false);
            _safeRoot = safeGo.AddComponent<RectTransform>();
            _safeRoot.anchorMin = Vector2.zero;
            _safeRoot.anchorMax = Vector2.one;
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
            var fitter = safeGo.AddComponent<JellySafeAreaFitter>();
            fitter.targetRect = _safeRoot;

            BuildGameplayHUD();
            BuildMainMenuOverlay();
            BuildEndOverlay();
            BuildSettingsOverlay();
        }

        private static void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
                return;
            }

            var oldModule = es.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                if (Application.isPlaying) Destroy(oldModule);
                else DestroyImmediate(oldModule);
            }

            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildGameplayHUD()
        {
            _hudGroup = CreateContainer(_safeRoot, "HUDGroup");

            Color outerRimCol = new Color(0.95f, 0.98f, 1.00f, 0.96f);
            Color innerFrostCol = new Color(0.76f, 0.89f, 0.98f, 0.92f);
            Color darkOutline = new Color(0.14f, 0.28f, 0.44f, 0.95f);

            // 1. Top-Left: Glossy White-Rimmed Frosted Pause Pill ('||') — positioned cleanly below camera notch!
            var pauseOuter = CreateRoundedPanel(_hudGroup.transform, "PauseOuter",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(82f, -142f), new Vector2(98f, 98f), outerRimCol);
            var pauseBtn = CreateRoundedButton(pauseOuter.transform, "PauseBtn",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f),
                innerFrostCol, "||", 38, () =>
                {
                    if (_mgr == null) return;
                    _mgr.CurrentScreen = _mgr.CurrentScreen == EndlessTrackManager.JellyScreenMode.Settings
                        ? EndlessTrackManager.JellyScreenMode.Playing
                        : EndlessTrackManager.JellyScreenMode.Settings;
                });
            AddOutline(pauseBtn.GetComponentInChildren<Text>().gameObject, darkOutline, 2.5f);

            // 2. Top-Center: Unified White-Rimmed Pill [ (SEVİYE 1 progress bar) | KÜTLE: 90 ] — zero overlap with ScoreOuter!
            var centerOuter = CreateRoundedPanel(_hudGroup.transform, "CenterTopOuter",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-28f, -142f), new Vector2(424f, 96f), outerRimCol);
            var centerInner = CreateRoundedPanel(centerOuter.transform, "CenterTopInner",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(410f, 82f), innerFrostCol);

            // Left sub-pill: Progress Track + Turquoise Fill + 'SEVİYE 1' centered inside the bar
            var sliderGo = new GameObject("ProgressSlider");
            sliderGo.transform.SetParent(centerInner.transform, false);
            var sRect = sliderGo.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0.5f);
            sRect.anchorMax = new Vector2(0f, 0.5f);
            sRect.anchoredPosition = new Vector2(144f, 0f);
            sRect.sizeDelta = new Vector2(272f, 64f);

            CreateRoundedPanel(sliderGo.transform, "Track",
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                new Color(0.36f, 0.52f, 0.68f, 0.95f));
            var fillArea = CreateContainer(sliderGo.transform, "FillArea").GetComponent<RectTransform>();
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;

            var fillGo = CreateRoundedPanel(fillArea, "Fill",
                new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero,
                new Color(0.18f, 0.86f, 0.82f, 1f));

            _progressSlider = sliderGo.AddComponent<Slider>();
            _progressSlider.interactable = false;
            _progressSlider.fillRect = fillGo.GetComponent<RectTransform>();
            _progressSlider.minValue = 0f;
            _progressSlider.maxValue = 1f;

            _levelBadgeText = CreateText(sliderGo.transform, "LevelText", "SEVİYE 1", 28, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(256f, 52f));
            AddOutline(_levelBadgeText.gameObject, darkOutline, 2.5f);

            // Right section inside unified pill: 'KÜTLE:' + '90'
            var kutleHdr = CreateText(centerInner.transform, "MassHeader", "KÜTLE:", 18, FontStyle.Bold, new Color(0.18f, 0.30f, 0.44f, 1f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-64f, 19f), new Vector2(114f, 26f));
            _ = kutleHdr;
            _massValueText = CreateText(centerInner.transform, "MassValue", "90", 34, FontStyle.Bold, Color.white,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-64f, -13f), new Vector2(114f, 40f));
            AddOutline(_massValueText.gameObject, darkOutline, 2.5f);

            // 3. Top-Right: White-Rimmed Score Pill ('★ 0') — separated cleanly with 134px gap!
            var scoreOuter = CreateRoundedPanel(_hudGroup.transform, "ScoreOuter",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-120f, -142f), new Vector2(196f, 94f), outerRimCol);
            var scoreInner = CreateRoundedPanel(scoreOuter.transform, "ScoreInner",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 80f), innerFrostCol);
            var starIcon = CreateText(scoreInner.transform, "StarIcon", "★", 42, FontStyle.Bold, new Color(1.00f, 0.78f, 0.14f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 2f), new Vector2(58f, 58f));
            AddOutline(starIcon.gameObject, new Color(0.68f, 0.40f, 0.04f, 0.95f), 2.5f);
            _scoreValueText = CreateText(scoreInner.transform, "ScoreValue", "0", 34, FontStyle.Bold, Color.white,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-64f, 0f), new Vector2(112f, 52f));
            AddOutline(_scoreValueText.gameObject, darkOutline, 2.5f);

            // 4. Bottom-Right: Circular White-Rimmed Glossy Purple '÷2' Jelly Action Button!
            var splitOuter = new GameObject("SplitOuterRing");
            splitOuter.transform.SetParent(_hudGroup.transform, false);
            var soRt = splitOuter.AddComponent<RectTransform>();
            soRt.anchorMin = new Vector2(1f, 0f);
            soRt.anchorMax = new Vector2(1f, 0f);
            soRt.anchoredPosition = new Vector2(-136f, 156f);
            soRt.sizeDelta = new Vector2(196f, 196f);
            var soImg = splitOuter.AddComponent<Image>();
            soImg.sprite = _circleSprite;
            soImg.color = outerRimCol;

            var splitInnerGo = new GameObject("SplitMergeBtn");
            splitInnerGo.transform.SetParent(splitOuter.transform, false);
            var siRt = splitInnerGo.AddComponent<RectTransform>();
            siRt.anchorMin = new Vector2(0.5f, 0.5f);
            siRt.anchorMax = new Vector2(0.5f, 0.5f);
            siRt.anchoredPosition = Vector2.zero;
            siRt.sizeDelta = new Vector2(174f, 174f);
            _splitMergeBg = splitInnerGo.AddComponent<Image>();
            _splitMergeBg.sprite = _circleSprite;
            _splitMergeBg.color = new Color(0.68f, 0.26f, 0.98f, 0.98f);
            _splitMergeBtn = splitInnerGo.AddComponent<Button>();
            _splitMergeBtn.onClick.AddListener(() => _mgr?.ToggleManualSplitMerge());

            var glossArc = new GameObject("GlossHighlight");
            glossArc.transform.SetParent(splitInnerGo.transform, false);
            var gaRt = glossArc.AddComponent<RectTransform>();
            gaRt.anchorMin = new Vector2(0.5f, 1f);
            gaRt.anchorMax = new Vector2(0.5f, 1f);
            gaRt.anchoredPosition = new Vector2(0f, -36f);
            gaRt.sizeDelta = new Vector2(106f, 40f);
            var gaImg = glossArc.AddComponent<Image>();
            gaImg.sprite = _circleSprite;
            gaImg.color = new Color(1f, 1f, 1f, 0.34f);
            gaImg.raycastTarget = false;

            _splitMergeText = CreateText(splitInnerGo.transform, "Label", "÷2", 58, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 96f));
            AddOutline(_splitMergeText.gameObject, new Color(0.32f, 0.08f, 0.56f, 0.95f), 3f);
        }

        private void BuildMainMenuOverlay()
        {
            _menuGroup = CreateContainer(_safeRoot, "MainMenuGroup");

            // Clean floating logo at top
            var title = CreateText(_menuGroup.transform, "LogoTitle", "PROJECT JELLY", 74, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(900f, 110f));
            AddOutline(title.gameObject, new Color(0.18f, 0.10f, 0.45f, 0.95f), 4f);

            var subCard = CreateRoundedPanel(_menuGroup.transform, "MenuInfoCard",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(560f, 92f),
                new Color(0.10f, 0.08f, 0.28f, 0.78f));
            _menuLevelText = CreateText(subCard.transform, "MenuLevel", "SEVİYE 1", 32, FontStyle.Bold, new Color(0.38f, 0.94f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(520f, 40f));
            _menuHighScoreText = CreateText(subCard.transform, "MenuBest", "EN İYİ: 0", 26, FontStyle.Bold, new Color(1f, 0.88f, 0.36f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(520f, 36f));

            // Bottom animated "KAYDIR VE BAŞLA" pill
            var swipePill = CreateRoundedButton(_menuGroup.transform, "SwipeToPlayBtn",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 240f), new Vector2(540f, 116f),
                new Color(0.16f, 0.88f, 0.48f, 0.96f), "KAYDIR VE BAŞLA ▶", 38, () =>
                {
                    if (_mgr != null) _mgr.CurrentScreen = EndlessTrackManager.JellyScreenMode.Playing;
                });
            _swipeHintHand = swipePill.GetComponent<RectTransform>();

            // Settings button
            CreateRoundedButton(_menuGroup.transform, "MenuSettingsBtn",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-88f, -88f), new Vector2(104f, 104f),
                new Color(0.20f, 0.16f, 0.48f, 0.90f), "⚙", 44, () =>
                {
                    if (_mgr != null) _mgr.CurrentScreen = EndlessTrackManager.JellyScreenMode.Settings;
                });
        }

        private void BuildEndOverlay()
        {
            _endGroup = CreateContainer(_safeRoot, "EndOverlayGroup");
            var dim = _endGroup.AddComponent<Image>();
            dim.color = new Color(0.04f, 0.03f, 0.12f, 0.62f);

            var card = CreateRoundedPanel(_endGroup.transform, "EndCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 680f),
                new Color(0.12f, 0.09f, 0.30f, 0.96f));
            _endCardBorder = card.GetComponent<Image>();

            _endTitleText = CreateText(card.transform, "EndTitle", "SEVİYE TAMAMLANDI!", 48, FontStyle.Bold, new Color(0.32f, 1f, 0.62f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(720f, 76f));

            var scoreBox = CreateRoundedPanel(card.transform, "ScoreBox",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(640f, 200f),
                new Color(0.18f, 0.13f, 0.40f, 0.95f));

            _endScoreText = CreateText(scoreBox.transform, "EndScore", "PUAN: 0", 44, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(580f, 60f));
            _endBestText = CreateText(scoreBox.transform, "EndBest", "REKOR: 0", 30, FontStyle.Bold, new Color(1f, 0.86f, 0.30f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(580f, 48f));

            var nextBtn = CreateRoundedButton(card.transform, "EndPrimaryBtn",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 146f), new Vector2(560f, 112f),
                new Color(0.16f, 0.88f, 0.48f, 1f), "SONRAKİ SEVİYE ▶", 38, () =>
                {
                    if (_mgr == null) return;
                    if (_mgr.IsLevelCompleted) _mgr.LoadNextProgressiveLevel();
                    else _mgr.StartLevel(_mgr.CurrentLevelNumber);
                });
            _endNextBtnText = nextBtn.GetComponentInChildren<Text>();

            CreateRoundedButton(card.transform, "EndMenuBtn",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(320f, 62f),
                new Color(0.32f, 0.24f, 0.62f, 0.95f), "ANA MENÜ", 28, () =>
                {
                    if (_mgr == null) return;
                    _mgr.IsGameOver = false;
                    _mgr.IsLevelCompleted = false;
                    _mgr.CurrentScreen = EndlessTrackManager.JellyScreenMode.MainMenu;
                });
        }

        private void BuildSettingsOverlay()
        {
            _settingsGroup = CreateContainer(_safeRoot, "SettingsGroup");
            var dim = _settingsGroup.AddComponent<Image>();
            dim.color = new Color(0.04f, 0.03f, 0.12f, 0.62f);

            var card = CreateRoundedPanel(_settingsGroup.transform, "SettingsCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 680f),
                new Color(0.12f, 0.09f, 0.30f, 0.96f));

            CreateText(card.transform, "SettingsTitle", "AYARLAR", 46, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(640f, 68f));

            var soundBtn = CreateRoundedButton(card.transform, "SoundRow",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(620f, 92f),
                new Color(0.20f, 0.15f, 0.44f, 1f), "SES EFEKTLERİ: AÇIK", 32, () =>
                {
                    if (_mgr != null) _mgr.SoundEnabled = !_mgr.SoundEnabled;
                });
            _soundStateText = soundBtn.GetComponentInChildren<Text>();

            var hapBtn = CreateRoundedButton(card.transform, "HapticRow",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(620f, 92f),
                new Color(0.20f, 0.15f, 0.44f, 1f), "TİTREŞİM: AÇIK", 32, () =>
                {
                    if (_mgr != null) _mgr.VibrationEnabled = !_mgr.VibrationEnabled;
                });
            _hapticStateText = hapBtn.GetComponentInChildren<Text>();

            var conBtn = CreateRoundedButton(card.transform, "ConceptRow",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(620f, 92f),
                new Color(0.36f, 0.22f, 0.68f, 1f), "TEMA: RENKLİ ARCADE", 30, () =>
                {
                    if (_mgr != null)
                    {
                        _mgr.VisualConceptIndex = (_mgr.VisualConceptIndex + 1) % 3;
                        _mgr.StartLevel(_mgr.CurrentLevelNumber);
                    }
                });
            _conceptStateText = conBtn.GetComponentInChildren<Text>();

            CreateRoundedButton(card.transform, "ResumeBtn",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(460f, 96f),
                new Color(0.16f, 0.88f, 0.48f, 1f), "DEVAM ET ▶", 36, () =>
                {
                    if (_mgr != null) _mgr.CurrentScreen = EndlessTrackManager.JellyScreenMode.Playing;
                });
        }

        private void Update()
        {
            if (_mgr == null) _mgr = GetComponent<EndlessTrackManager>();
            if (!_built && _mgr != null && transform.Find("JellyModernCanvas") == null)
            {
                BuildUI();
            }
            RefreshUIState();
        }

        public void RefreshUIState()
        {
            if (!_built || _mgr == null || _hudGroup == null) return;

            bool isEnd = _mgr.IsLevelCompleted || _mgr.IsGameOver;
            bool isMenu = !isEnd && _mgr.CurrentScreen == EndlessTrackManager.JellyScreenMode.MainMenu;
            bool isSettings = !isEnd && (_mgr.CurrentScreen == EndlessTrackManager.JellyScreenMode.Settings ||
                                         _mgr.CurrentScreen == EndlessTrackManager.JellyScreenMode.Paused);
            bool isPlaying = !isEnd && !isMenu && !isSettings;

            if (_hudGroup.activeSelf != isPlaying) _hudGroup.SetActive(isPlaying);
            if (_menuGroup.activeSelf != isMenu) _menuGroup.SetActive(isMenu);
            if (_endGroup.activeSelf != isEnd) _endGroup.SetActive(isEnd);
            if (_settingsGroup.activeSelf != isSettings) _settingsGroup.SetActive(isSettings);

            if (isPlaying)
            {
                float totalLen = _mgr.CurrentLevelTotalLength;
                float prog = totalLen > 1f ? Mathf.Clamp01(_mgr.CurrentDistance / totalLen) : 0f;
                _progressSlider.value = Mathf.Max(0.45f, prog);
                _levelBadgeText.text = $"SEVİYE {_mgr.CurrentLevelNumber}";

                float effective = _mgr.IsSplit ? _mgr.CurrentMass * 0.5f : _mgr.CurrentMass;
                _massValueText.text = _mgr.IsSplit ? $"{effective:F0}×2" : $"{_mgr.CurrentMass:F0}";
                _scoreValueText.text = $"{_mgr.Score:N0}";

                _splitMergeText.text = _mgr.IsSplit ? "><" : "÷2";
                _splitMergeBg.color = _mgr.IsSplit
                    ? new Color(0.98f, 0.68f, 0.14f, 0.98f)
                    : new Color(0.68f, 0.26f, 0.98f, 0.98f);
            }
            else if (isMenu)
            {
                _menuLevelText.text = $"SEVİYE {_mgr.CurrentLevelNumber}";
                _menuHighScoreText.text = $"EN İYİ REKOR: {_mgr.HighScore:N0}";
                if (_swipeHintHand != null)
                {
                    float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.04f;
                    _swipeHintHand.localScale = new Vector3(pulse, pulse, 1f);
                }
            }
            else if (isEnd)
            {
                _endTitleText.text = _mgr.IsLevelCompleted ? $"SEVİYE {_mgr.CurrentLevelNumber} TAMAM!" : "OYUN BİTTİ!";
                _endTitleText.color = _mgr.IsLevelCompleted ? new Color(0.32f, 1f, 0.62f) : new Color(1f, 0.38f, 0.62f);
                _endScoreText.text = $"PUAN: {_mgr.Score:N0}";
                _endBestText.text = $"EN İYİ REKOR: {_mgr.HighScore:N0}";
                _endNextBtnText.text = _mgr.IsLevelCompleted ? $"SONRAKİ SEVİYE ({_mgr.CurrentLevelNumber + 1}) ▶" : "TEKRAR DENE ↻";
            }
            else if (isSettings)
            {
                _soundStateText.text = $"SES EFEKTLERİ: {(_mgr.SoundEnabled ? "AÇIK" : "KAPALI")}";
                _hapticStateText.text = $"TİTREŞİM: {(_mgr.VibrationEnabled ? "AÇIK" : "KAPALI")}";
                string[] cNames = { "RENKLİ ARCADE", "MİNİMALİST", "FUTURİSTİK NEON" };
                _conceptStateText.text = $"TEMA: {cNames[_mgr.VisualConceptIndex % 3]}";
            }
        }

        private GameObject CreateContainer(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private GameObject CreateRoundedPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = col;
            return go;
        }

        private Button CreateRoundedButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color bg, string label, int fontSize, Action onClick)
        {
            var go = CreateRoundedPanel(parent, name, anchorMin, anchorMax, pos, size, bg);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            CreateText(go.transform, "Label", label, fontSize, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16f, 8f));
            return btn;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, Color col, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = _uiFont;
            t.text = content;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        private static void AddOutline(GameObject go, Color col, float dist)
        {
            var ol = go.AddComponent<Outline>();
            ol.effectColor = col;
            ol.effectDistance = new Vector2(dist, -dist);
        }

        private static Sprite CreateRoundedRectSprite(int w, int h, int radius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            float r = radius;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (w - 1 - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y, y - (h - 1 - r)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite CreateCircleSprite(int diameter)
        {
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[diameter * diameter];
            float r = diameter * 0.5f - 1f;
            Vector2 c = new Vector2((diameter - 1) * 0.5f, (diameter - 1) * 0.5f);
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    float alpha = Mathf.Clamp01(r - dist + 0.75f);
                    px[y * diameter + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
