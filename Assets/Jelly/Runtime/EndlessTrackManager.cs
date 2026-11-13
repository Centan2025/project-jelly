using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Jelly
{
    public class EndlessTrackManager : MonoBehaviour
    {
        [Header("References")]
        public Transform trackRoot;
        public JellyController primaryPlayer;
        public Camera mainCamera;
        public Material jellyMaterial;
        public Material trackMaterial;
        public Material gateMaterial;
        public Material goalMaterial;
        public Material pickupMaterial;
        public Material drainMaterial;

        [Header("Progressive Map & Flow Settings")]
        public int CurrentLevelNumber = 1;
        public float baseForwardSpeed = 9.5f;
        public float maxForwardSpeed = 16.5f;
        public float lateralSteerSpeed = 4.2f;
        public float steerAcceleration = 7.5f;
        public float steerDeceleration = 11.5f;
        public float maxLateralOffset = 2.45f;
        public float roadWidth = 6.4f;
        public float sliceLength = 12.0f;

        public enum JellyScreenMode
        {
            MainMenu,
            Playing,
            Paused,
            Quests,
            Shop,
            Settings
        }

        [Header("Live State")]
        public JellyScreenMode CurrentScreen = JellyScreenMode.Playing;
        public float CurrentDistance;
        public float CurrentLateralOffset;
        public float CurrentMass = 90f;
        public bool IsSplit;
        public int Score;
        public int HighScore;
        public int ComboCount;
        public int HighestUnlockedLevel = 1;
        public int LastMultiplierReached = 1;
        public bool IsGameOver;
        public bool IsLevelCompleted;
        public bool HasShield;
        public float MagnetTimer;
        public float SlowTimeTimer;
        public string StatusBanner = "Engellere göre Büyü (+), Küçül (-) veya Bölün (÷2)!";
        public float StatusBannerTimer = 6f;

        // Quests & Customization & Settings State
        public int QuestSplitsCount;
        public int QuestReach150Count;
        public int QuestSmashWallsCount;
        public int SelectedSkinIndex;
        public int SelectedTrailIndex;
        public int SelectedEffectIndex;
        public int ShopActiveTab; // 0=Jöleler, 1=İzler, 2=Efektler
        public bool SoundEnabled = true;
        public bool MusicEnabled = true;
        public bool VibrationEnabled = true;
        public bool ColorblindMode;
        public int QualityLevelIndex = 1; // 0=Düşük, 1=Orta, 2=Yüksek
        public int VisualConceptIndex;    // 0=Renkli Arcade, 1=Premium Minimalist, 2=Futuristik Neon
        private bool _onStickyFloor;

        private JellyLevelBlueprint _currentBlueprint;
        private GameObject _secondarySplitVisual;
        private GameObject _jellyBridgeVisual;
        private GameObject _primaryShadowDisc;
        private GameObject _secondaryShadowDisc;
        private GameObject _shieldBubbleVisual;

        // 3D Multi-Layer Parallax Background Roots
        private Transform _parallaxFarRoot;
        private Transform _parallaxMidRoot;
        private readonly List<Transform> _floatingIslands = new List<Transform>();

        // Vertical jump / double-decker elevation physics
        private float _airborneY;
        private float _verticalVelocity;
        private float _deckBaseY;
        private int _currentDyeColorIndex = -1; // -1=Default, 0=Cyan, 1=Gold, 2=Magenta

        private Vector2 _dragPrevPos;
        private bool _dragging;
        private float _uiSteerHoldDir;
        private float _bounceCooldown;
        private float _speedBoostTimer;
        private float _manualSplitCooldown;
        private float _smoothedSteerInput;
        private float _currentSplitBlend;
        private float _splitBlendVelocity;
        private float _displayedMass = 90f;
        private float _wobbleAmplitude;
        private float _wobblePhase;
        private float _trailSpawnTimer;
        private float _cameraFovPunch;
        private float _cameraShakeTimer;
        private float _cameraShakeAmp;
        private float _lastTapTime = -1f;
        private Vector2 _pointerDownPos;
        private Material _skyboxMat;
        private Volume _globalPostVolume;
        private static float _activeCurveScale = 1.0f;

        private Texture2D _flatTex;
        private readonly List<GameObject> _spawnedChunks = new List<GameObject>();
        private readonly List<EndlessGateObstacle> _activeObstacles = new List<EndlessGateObstacle>();
        private readonly List<EndlessPickupItem> _activePickups = new List<EndlessPickupItem>();
        private readonly List<JellyTrackSegmentNode> _activeSegmentNodes = new List<JellyTrackSegmentNode>();

        private struct BiomePalette
        {
            public string Name;
            public Color SkyColor;
            public Color RoadSurfaceColor;
            public Color CurbColor;
            public Color StripeColor;
            public Color PropColor;
            public Color CloudColor;
        }

        private static readonly BiomePalette[] Biomes = new BiomePalette[]
        {
            new BiomePalette // 0: Oyuncak Su Parkı (Alternatif 1 Ana Tema)
            {
                Name = "SU PARKI",
                SkyColor = new Color(0.24f, 0.76f, 1.00f),
                RoadSurfaceColor = Color.white,
                CurbColor = new Color(0.26f, 0.84f, 0.94f),
                StripeColor = new Color(0.04f, 0.74f, 0.72f),
                PropColor = new Color(0.98f, 0.42f, 0.28f),
                CloudColor = Color.white
            },
            new BiomePalette // 1: Şeker Ülkesi (Candy)
            {
                Name = "ŞEKER ÜLKESİ",
                SkyColor = new Color(0.58f, 0.34f, 0.78f),
                RoadSurfaceColor = new Color(0.44f, 0.24f, 0.64f),
                CurbColor = new Color(1.00f, 0.42f, 0.74f),
                StripeColor = new Color(1.00f, 0.84f, 0.28f),
                PropColor = new Color(1.00f, 0.36f, 0.64f),
                CloudColor = new Color(1.00f, 0.88f, 0.96f)
            },
            new BiomePalette // 2: Buz Kristal (Ice)
            {
                Name = "BUZ KRİSTAL",
                SkyColor = new Color(0.20f, 0.54f, 0.82f),
                RoadSurfaceColor = new Color(0.24f, 0.38f, 0.70f),
                CurbColor = new Color(0.40f, 0.96f, 1.00f),
                StripeColor = new Color(0.55f, 0.94f, 1.00f),
                PropColor = new Color(0.36f, 0.88f, 0.98f),
                CloudColor = new Color(0.90f, 0.98f, 1.00f)
            },
            new BiomePalette // 3: Volkanik Jel (Lava-Jelly)
            {
                Name = "VOLKANİK JEL",
                SkyColor = new Color(0.28f, 0.16f, 0.38f),
                RoadSurfaceColor = new Color(0.26f, 0.18f, 0.42f),
                CurbColor = new Color(1.00f, 0.52f, 0.16f),
                StripeColor = new Color(1.00f, 0.34f, 0.22f),
                PropColor = new Color(1.00f, 0.64f, 0.18f),
                CloudColor = new Color(0.96f, 0.76f, 0.68f)
            },
            new BiomePalette // 4: Uzay Jel (Neon/Galaksi)
            {
                Name = "UZAY JEL",
                SkyColor = new Color(0.14f, 0.12f, 0.34f),
                RoadSurfaceColor = new Color(0.20f, 0.16f, 0.46f),
                CurbColor = new Color(0.86f, 0.28f, 1.00f),
                StripeColor = new Color(0.18f, 0.94f, 0.98f),
                PropColor = new Color(0.68f, 0.34f, 1.00f),
                CloudColor = new Color(0.82f, 0.78f, 1.00f)
            }
        };

        private static readonly Color[] SkinPaletteColors = new Color[]
        {
            new Color(0.12f, 0.78f, 1.00f, 0.94f), // 0: Mavi Jöle (Varsayılan)
            new Color(0.16f, 0.95f, 0.48f, 0.94f), // 1: Zümrüt Jöle
            new Color(0.76f, 0.32f, 1.00f, 0.94f), // 2: Kristal Mor
            new Color(1.00f, 0.76f, 0.16f, 0.94f), // 3: Altın Jöle
            new Color(1.00f, 0.36f, 0.72f, 0.94f), // 4: Pembe Şeker
            new Color(0.24f, 0.96f, 0.88f, 0.94f)  // 5: Galaksi Neon
        };

        private static readonly string[] SkinNames = new string[]
        {
            "Mavi Jöle", "Zümrüt Jöle", "Mor Kristal", "Altın Jöle", "Pembe Şeker", "Neon Galaksi"
        };

        private static readonly string[] TrailNames = new string[]
        {
            "Klasik Damla", "Neon Çizgi", "Yıldız Tozu", "Buz Kristali", "Alev İzi", "Gökkuşağı"
        };

        private static readonly string[] EffectNames = new string[]
        {
            "Jöle Sıçrama", "Mor Şok Dalgası", "Kristal Patlama", "Altın Konfeti", "Plazma Halka", "Süpernova"
        };

        private static readonly Color[] DyeColors = new Color[]
        {
            new Color(0.15f, 0.88f, 1.00f), // 0 = Mavi (Cyan)
            new Color(1.00f, 0.80f, 0.16f), // 1 = Altın (Gold)
            new Color(0.88f, 0.28f, 0.98f)  // 2 = Mor (Magenta)
        };

        private static readonly string[] DyeColorNames = new string[] { "MAVİ", "ALTIN", "MOR" };

        private void Awake()
        {
            if (lateralSteerSpeed > 4.5f) lateralSteerSpeed = 4.2f;
            if (baseForwardSpeed > 10f) baseForwardSpeed = 9.5f;
            sliceLength = 12.0f;
            HighScore = PlayerPrefs.GetInt("JellyEndlessHighScore", 0);
            HighestUnlockedLevel = Mathf.Max(1, PlayerPrefs.GetInt("JellyHighestLevel", 1));
            QuestSplitsCount = PlayerPrefs.GetInt("JellyQuestSplits", 7);
            QuestReach150Count = PlayerPrefs.GetInt("JellyQuestMass150", 0);
            QuestSmashWallsCount = PlayerPrefs.GetInt("JellyQuestSmash", 2);
            SelectedSkinIndex = PlayerPrefs.GetInt("JellySkinIdx", 0);
            SelectedTrailIndex = PlayerPrefs.GetInt("JellyTrailIdx", 0);
            SelectedEffectIndex = PlayerPrefs.GetInt("JellyEffectIdx", 0);
            CurrentLevelNumber = 1;
        }

        public float CurrentLevelTotalLength => _currentBlueprint != null ? _currentBlueprint.Chunks.Count * sliceLength : 72f;
        public string CurrentLevelDisplayTitle => _currentBlueprint != null ? _currentBlueprint.LevelTitle : $"LVL {CurrentLevelNumber}";

        private JellyModernUI _modernUI;
        private JellyAudioAndHaptics _audio;

        private void Start()
        {
            if (lateralSteerSpeed > 4.5f) lateralSteerSpeed = 4.2f;
            sliceLength = 12.0f;
            if (mainCamera == null) mainCamera = Camera.main;
            CurrentLevelNumber = 1;
            StartLevel(1);

            DestroyLegacySceneCanvases();

            _audio = GetComponent<JellyAudioAndHaptics>();
            if (_audio == null) _audio = gameObject.AddComponent<JellyAudioAndHaptics>();

            EnsureModernUI();
        }

        private void DestroyLegacySceneCanvases()
        {
            var legacyUi = FindAnyObjectByType<JellyUIManager>();
            if (legacyUi != null && legacyUi.gameObject != gameObject)
            {
                if (Application.isPlaying) Destroy(legacyUi.gameObject);
                else DestroyImmediate(legacyUi.gameObject);
            }
            var oldCanvas = GameObject.Find("JellyUICanvas");
            if (oldCanvas != null && oldCanvas != gameObject)
            {
                if (Application.isPlaying) Destroy(oldCanvas);
                else DestroyImmediate(oldCanvas);
            }
        }

        public void EnsureModernUI()
        {
            _modernUI = GetComponent<JellyModernUI>();
            if (_modernUI == null) _modernUI = gameObject.AddComponent<JellyModernUI>();
            _modernUI.Bind(this);
        }

        public void StartNewEndlessRun()
        {
            StartLevel(CurrentLevelNumber);
        }

        public void StartLevel(int levelNumber)
        {
            CurrentLevelNumber = Mathf.Max(1, levelNumber);
            _currentBlueprint = JellyProgressiveMapGenerator.GenerateLevel(CurrentLevelNumber);
            _activeCurveScale = _currentBlueprint.CurveIntensity;

            ClearTrack();
            CurrentDistance = 0f;
            CurrentLateralOffset = 0f;
            CurrentMass = 90f;
            _displayedMass = 90f;
            IsSplit = false;
            _currentSplitBlend = 0f;
            _splitBlendVelocity = 0f;
            _wobbleAmplitude = 0f;
            _airborneY = 0f;
            _verticalVelocity = 0f;
            _deckBaseY = 0f;
            _currentDyeColorIndex = -1;
            ComboCount = 0;
            LastMultiplierReached = 1;
            IsGameOver = false;
            IsLevelCompleted = false;
            HasShield = false;
            MagnetTimer = 0f;
            SlowTimeTimer = 0f;
            EndlessGateObstacle.GlobalObstacleTimeScale = 1.0f;
            _bounceCooldown = 0f;
            _speedBoostTimer = 0f;
            _manualSplitCooldown = 0f;

            SetBanner($"{_currentBlueprint.LevelTitle} — Büyü (+), Küçül (-), Bölün (÷2)!", 4.5f);

            EnsurePlayerObjects();
            BuildEntireLevelMap(_currentBlueprint);
            BuildParallax3DBackground(GetCurrentBiome(), _currentBlueprint.TotalTrackLength);
            SyncJellyVisualsAndPosition(0f);
            UpdateCameraImmediate();
            UpdateEnvironmentVisuals();
        }

        public void LoadNextProgressiveLevel()
        {
            CurrentLevelNumber++;
            if (CurrentLevelNumber > HighestUnlockedLevel)
            {
                HighestUnlockedLevel = CurrentLevelNumber;
                PlayerPrefs.SetInt("JellyHighestLevel", HighestUnlockedLevel);
            }
            StartLevel(CurrentLevelNumber);
        }

        public void LoadPreviousProgressiveLevel()
        {
            CurrentLevelNumber = Mathf.Max(1, CurrentLevelNumber - 1);
            StartLevel(CurrentLevelNumber);
        }

        public void BuildInitialPreviewInEditor(JellyController existingPlayer)
        {
            if (existingPlayer != null) primaryPlayer = existingPlayer;
            if (mainCamera == null) mainCamera = Camera.main;
            BlenderKitModelSpawner.ClearCaches();
            _cachedRoadGridTex = null;
            _cachedRoadGridMats.Clear();
            _cachedWaterCausticsTex = null;
            DestroyLegacySceneCanvases();
            CurrentScreen = JellyScreenMode.Playing;
            StartLevel(1);
            EnsureModernUI();
        }

        public void RenderComparisonSnapshotsToDisk(string outputDir)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;
            if (!System.IO.Directory.Exists(outputDir))
                System.IO.Directory.CreateDirectory(outputDir);

            Vector3 origPos = mainCamera.transform.position;
            Quaternion origRot = mainCamera.transform.rotation;
            float origFov = mainCamera.fieldOfView;

            // 1. Main Track + Jelly + Pickups + Floating Islands view
            UpdateCameraImmediate();
            CaptureCameraToPng(mainCamera, System.IO.Path.Combine(outputDir, "unity_track_and_jelly.png"), 900, 700);

            // Spawn a temporary isolated showcase rig high in the sky so we get clean studio-style 3D shots of individual obstacles
            float savedCurve = _activeCurveScale;
            _activeCurveScale = 0f;

            RenderObstacleStudioShot(EndlessObstacleType.VerticalCrusherGate, System.IO.Path.Combine(outputDir, "unity_piston_gate.png"));
            RenderObstacleStudioShot(EndlessObstacleType.HeavySmashWall, System.IO.Path.Combine(outputDir, "unity_glass_wall.png"));
            RenderObstacleStudioShot(EndlessObstacleType.SpinnerBar, System.IO.Path.Combine(outputDir, "unity_spinner_bar.png"));

            _activeCurveScale = savedCurve;
            mainCamera.transform.SetPositionAndRotation(origPos, origRot);
            mainCamera.fieldOfView = origFov;
        }

        private void RenderObstacleStudioShot(EndlessObstacleType type, string filePath)
        {
            float studioZ = -200f;
            var studioDeck = BuildSmoothRoadChunkMesh(studioZ - 5f, studioZ + 6f, 8, Biomes[0], 0f);
            BuildCutoutGate(studioZ, type);
            GameObject gateGo = _spawnedChunks[_spawnedChunks.Count - 1];

            EvaluateRoadCurve(studioZ, out Vector3 center, out _, out _);
            mainCamera.fieldOfView = 38f;
            mainCamera.transform.position = center + new Vector3(0f, 1.85f, -5.8f);
            mainCamera.transform.LookAt(center + new Vector3(0f, 1.25f, 0f));

            CaptureCameraToPng(mainCamera, filePath, 800, 560);

            _spawnedChunks.Remove(gateGo);
            if (_activeObstacles.Count > 0) _activeObstacles.RemoveAt(_activeObstacles.Count - 1);
            DestroyImmediate(gateGo);
            DestroyImmediate(studioDeck);
        }

        private static void CaptureCameraToPng(Camera cam, string filePath, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            Color32[] pixels = tex.GetPixels32();
            int rowStride = (width * 3 + 3) & ~3;
            int dataSize = rowStride * height;
            byte[] bmp = new byte[54 + dataSize];
            bmp[0] = (byte)'B';
            bmp[1] = (byte)'M';
            System.BitConverter.GetBytes(54 + dataSize).CopyTo(bmp, 2);
            System.BitConverter.GetBytes(54).CopyTo(bmp, 10);
            System.BitConverter.GetBytes(40).CopyTo(bmp, 14);
            System.BitConverter.GetBytes(width).CopyTo(bmp, 18);
            System.BitConverter.GetBytes(height).CopyTo(bmp, 22);
            bmp[26] = 1;
            bmp[28] = 24;
            System.BitConverter.GetBytes(dataSize).CopyTo(bmp, 34);

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * width;
                int dstRow = 54 + y * rowStride;
                for (int x = 0; x < width; x++)
                {
                    Color32 c = pixels[srcRow + x];
                    int idx = dstRow + x * 3;
                    bmp[idx + 0] = c.b;
                    bmp[idx + 1] = c.g;
                    bmp[idx + 2] = c.r;
                }
            }

            System.IO.File.WriteAllBytes(filePath, bmp);

            DestroyImmediate(tex);
            DestroyImmediate(rt);
        }

        private BiomePalette GetCurrentBiome()
        {
            int idx = _currentBlueprint != null ? (_currentBlueprint.BiomeIndex % Biomes.Length) : 0;
            return ApplyVisualConceptToBiome(Biomes[idx], VisualConceptIndex);
        }

        private static BiomePalette ApplyVisualConceptToBiome(BiomePalette b, int conceptIndex)
        {
            switch (conceptIndex % 3)
            {
                case 1: // Premium Minimalist (Clean Soft Pastel Studio)
                    return new BiomePalette
                    {
                        Name = b.Name + " · MİNİMALİST",
                        SkyColor = Color.Lerp(b.SkyColor, new Color(0.84f, 0.89f, 0.96f), 0.72f),
                        RoadSurfaceColor = Color.Lerp(b.RoadSurfaceColor, new Color(0.76f, 0.80f, 0.90f), 0.65f),
                        CurbColor = Color.Lerp(b.CurbColor, new Color(0.32f, 0.68f, 0.88f), 0.55f),
                        StripeColor = Color.Lerp(b.StripeColor, new Color(0.96f, 0.98f, 1.00f), 0.60f),
                        PropColor = Color.Lerp(b.PropColor, new Color(0.70f, 0.78f, 0.92f), 0.55f),
                        CloudColor = new Color(0.98f, 0.99f, 1.00f)
                    };
                case 2: // Futuristik Neon (Deep Space Obsidian + High-Contrast Cyber Glow)
                    return new BiomePalette
                    {
                        Name = b.Name + " · FUTURİSTİK NEON",
                        SkyColor = new Color(0.03f, 0.04f, 0.11f),
                        RoadSurfaceColor = new Color(0.07f, 0.08f, 0.18f),
                        CurbColor = new Color(0.08f, 0.98f, 1.00f),
                        StripeColor = new Color(1.00f, 0.18f, 0.86f),
                        PropColor = new Color(0.18f, 0.96f, 0.88f),
                        CloudColor = new Color(0.32f, 0.56f, 0.95f)
                    };
                default: // 0: Renkli Arcade (Vibrant Hyper-Casual Default)
                    return b;
            }
        }

        private void UpdateEnvironmentVisuals()
        {
            if (mainCamera == null) return;
            BiomePalette b = GetCurrentBiome();

            // 1. Gradient Skybox instead of flat SolidColor
            Shader skySh = Resources.Load<Shader>("Shaders/ProceduralSkyGradient") ?? Shader.Find("ProjectJelly/ProceduralSkyGradient");
            if (skySh != null)
            {
                if (_skyboxMat == null || _skyboxMat.shader != skySh)
                    _skyboxMat = new Material(skySh) { name = "RuntimeJellyGradientSky" };

                Color zenith = Color.Lerp(b.SkyColor, new Color(0.14f, 0.34f, 0.86f), 0.35f);
                Color horizon = Color.Lerp(b.SkyColor, Color.white, 0.42f);
                Color ground = Color.Lerp(b.RoadSurfaceColor, b.SkyColor, 0.45f);
                _skyboxMat.SetColor("_TopColor", zenith);
                _skyboxMat.SetColor("_HorizonColor", horizon);
                _skyboxMat.SetColor("_BottomColor", ground);
                _skyboxMat.SetFloat("_HorizonSharpness", 1.15f);
                RenderSettings.skybox = _skyboxMat;
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = b.SkyColor;
            }

            mainCamera.fieldOfView = 54f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 68f;
            RenderSettings.fogEndDistance = 155f;
            RenderSettings.fogColor = Color.Lerp(b.SkyColor, Color.white, 0.34f);

            // Studio Trilight ambient lighting & balanced key directional light (prevents white blow-out)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(b.SkyColor, Color.white, 0.25f);
            RenderSettings.ambientEquatorColor = Color.Lerp(b.SkyColor, b.CurbColor, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.56f, 0.68f);

            Light keyLight = RenderSettings.sun;
            if (keyLight == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional) { keyLight = lights[i]; break; }
                }
            }
            if (keyLight != null)
            {
                keyLight.intensity = 1.08f;
                keyLight.color = new Color(1.00f, 0.97f, 0.93f);
                keyLight.shadows = LightShadows.Soft;
                keyLight.shadowStrength = 0.52f;
                keyLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            EnsureWaterparkOceanPlane(b);

            // 2. Enable URP Post-Processing (Bloom + ACES Tonemapping + Vignette)
            var camData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
            }
            EnsureGlobalPostProcessingVolume();
        }

        private void EnsureGlobalPostProcessingVolume()
        {
            if (_globalPostVolume != null) return;
            var existing = FindAnyObjectByType<Volume>();
            if (existing != null && existing.isGlobal)
            {
                _globalPostVolume = existing;
                return;
            }

            var volGo = new GameObject("JellyPostProcessingVolume");
            volGo.transform.SetParent(trackRoot != null ? trackRoot : transform, false);
            _globalPostVolume = volGo.AddComponent<Volume>();
            _globalPostVolume.isGlobal = true;
            _globalPostVolume.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.16f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.48f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.16f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.35f;

            _globalPostVolume.sharedProfile = profile;
        }

        private void EnsurePlayerObjects()
        {
            EnsureTrackRoot();
            if (primaryPlayer == null)
                primaryPlayer = FindAnyObjectByType<JellyController>();

            if (primaryPlayer != null)
            {
                primaryPlayer.enabled = false;
                if (primaryPlayer.motor != null) primaryPlayer.motor.enabled = false;
                if (primaryPlayer.inputAdapter != null) primaryPlayer.inputAdapter.enabled = false;
                var splitCtrl = primaryPlayer.GetComponent<JellySplitController>();
                if (splitCtrl != null) splitCtrl.enabled = false;
                var mergeCtrl = primaryPlayer.GetComponent<JellyMergeController>();
                if (mergeCtrl != null) mergeCtrl.enabled = false;
                var deformCtrl = primaryPlayer.GetComponent<JellyDeformationController>();
                if (deformCtrl != null) deformCtrl.enabled = false;
                var morphCtrl = primaryPlayer.GetComponent<JellyMorphController>();
                if (morphCtrl != null) morphCtrl.enabled = false;

                var childVis = primaryPlayer.transform.Find("Visual");
                if (childVis != null)
                {
                    childVis.localPosition = Vector3.zero;
                    childVis.localRotation = Quaternion.identity;
                    childVis.localScale = Vector3.one;
                    BlenderKitModelSpawner.ApplyBlenderJellyMesh(childVis.gameObject);
                }
                BlenderKitModelSpawner.ApplyBlenderJellyMesh(primaryPlayer.gameObject);

                EnsureJellyGlossDecorations(primaryPlayer.transform);

                var rb = primaryPlayer.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
                var camCtrl = mainCamera != null ? mainCamera.GetComponent<JellyCameraController>() : null;
                if (camCtrl != null) camCtrl.enabled = false;
            }

            if (_secondarySplitVisual == null)
            {
                var existingSec = trackRoot.Find("JellySplitTwin");
                if (existingSec != null)
                {
                    _secondarySplitVisual = existingSec.gameObject;
                    BlenderKitModelSpawner.ApplyBlenderJellyMesh(_secondarySplitVisual);
                }
                else
                {
                    _secondarySplitVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _secondarySplitVisual.name = "JellySplitTwin";
                    _secondarySplitVisual.transform.SetParent(trackRoot, false);
                    var col = _secondarySplitVisual.GetComponent<Collider>();
                    if (col != null) col.isTrigger = true;
                    var rb = _secondarySplitVisual.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    LevelLoader.ApplyMaterialOrColor(_secondarySplitVisual, jellyMaterial, new Color(0.18f, 0.82f, 1.00f, 0.92f));
                    BlenderKitModelSpawner.ApplyBlenderJellyMesh(_secondarySplitVisual);
                    EnsureJellyGlossDecorations(_secondarySplitVisual.transform);
                }
            }
            else
            {
                BlenderKitModelSpawner.ApplyBlenderJellyMesh(_secondarySplitVisual);
            }
            _secondarySplitVisual.SetActive(IsSplit || _currentSplitBlend > 0.01f);

            if (_jellyBridgeVisual == null)
            {
                var existingBridge = trackRoot.Find("JellySplitBridge");
                if (existingBridge != null)
                {
                    _jellyBridgeVisual = existingBridge.gameObject;
                }
                else
                {
                    _jellyBridgeVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _jellyBridgeVisual.name = "JellySplitBridge";
                    _jellyBridgeVisual.transform.SetParent(trackRoot, false);
                    DestroyCollider(_jellyBridgeVisual);
                    LevelLoader.ApplyMaterialOrColor(_jellyBridgeVisual, jellyMaterial, new Color(0.22f, 0.85f, 1.00f, 0.88f));
                }
            }
            _jellyBridgeVisual.SetActive(false);

            if (_shieldBubbleVisual == null && primaryPlayer != null)
            {
                var existingShield = primaryPlayer.transform.Find("JellyShieldBubble");
                if (existingShield != null)
                {
                    _shieldBubbleVisual = existingShield.gameObject;
                }
                else
                {
                    _shieldBubbleVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _shieldBubbleVisual.name = "JellyShieldBubble";
                    DestroyCollider(_shieldBubbleVisual);
                    _shieldBubbleVisual.transform.SetParent(primaryPlayer.transform, false);
                    _shieldBubbleVisual.transform.localPosition = Vector3.zero;
                    _shieldBubbleVisual.transform.localScale = Vector3.one * 1.28f;
                    LevelLoader.ApplyMaterialOrColor(_shieldBubbleVisual, null, new Color(0.88f, 0.38f, 1.00f, 0.38f));
                }
            }
            if (_shieldBubbleVisual != null) _shieldBubbleVisual.SetActive(HasShield);

            if (_primaryShadowDisc == null) _primaryShadowDisc = CreateShadowDisc("PrimaryShadowDisc");
            if (_secondaryShadowDisc == null) _secondaryShadowDisc = CreateShadowDisc("SecondaryShadowDisc");
        }

        private static void EnsureJellyGlossDecorations(Transform jellyRoot)
        {
            if (jellyRoot == null) return;
            for (int i = jellyRoot.childCount - 1; i >= 0; i--)
            {
                var ch = jellyRoot.GetChild(i);
                string n = ch.name;
                if (n == "GlossHighlight" || n == "JellyInnerCore" || n == "CuteFaceRig" ||
                    n.StartsWith("WetTrail", System.StringComparison.Ordinal))
                {
                    if (Application.isPlaying) Destroy(ch.gameObject);
                    else DestroyImmediate(ch.gameObject);
                }
            }

            // Cute expressive eyes & curved smile placed high on the upper dome arc so they never sink below the jelly contour
            var faceRig = new GameObject("CuteFaceRig").transform;
            faceRig.SetParent(jellyRoot, false);
            faceRig.localPosition = Vector3.zero;
            faceRig.localRotation = Quaternion.identity;

            Color eyeDark = new Color(0.05f, 0.07f, 0.11f, 1f);
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = s < 0 ? "EyeL" : "EyeR";
                DestroyCollider(eye);
                eye.transform.SetParent(faceRig, false);
                eye.transform.localPosition = new Vector3(s * 0.145f, 0.205f, -0.425f);
                eye.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                eye.transform.localScale = new Vector3(0.115f, 0.125f, 0.075f);
                LevelLoader.ApplyMaterialOrColor(eye, null, eyeDark);

                var glint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glint.name = "EyeGlint";
                DestroyCollider(glint);
                glint.transform.SetParent(eye.transform, false);
                glint.transform.localPosition = new Vector3(-0.22f, 0.24f, -0.48f);
                glint.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
                LevelLoader.ApplyMaterialOrColor(glint, null, Color.white);
            }

            // Cute 3-dot curved smile arc ('◡')
            Vector3[] smilePts =
            {
                new Vector3(-0.048f, 0.135f, -0.452f),
                new Vector3( 0.000f, 0.118f, -0.458f),
                new Vector3( 0.048f, 0.135f, -0.452f)
            };
            for (int m = 0; m < smilePts.Length; m++)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = $"SmileArc_{m}";
                DestroyCollider(dot);
                dot.transform.SetParent(faceRig, false);
                dot.transform.localPosition = smilePts[m];
                dot.transform.localScale = new Vector3(0.036f, 0.032f, 0.032f);
                LevelLoader.ApplyMaterialOrColor(dot, null, eyeDark);
            }
        }

        private GameObject CreateShadowDisc(string name)
        {
            var existing = trackRoot.Find(name);
            if (existing != null)
            {
                BlenderKitModelSpawner.ApplySoftShadowDiscMaterial(existing.gameObject, new Color(0.18f, 0.88f, 1.00f, 0.55f));
                return existing.gameObject;
            }

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            DestroyCollider(disc);
            disc.transform.SetParent(trackRoot, false);
            BlenderKitModelSpawner.ApplySoftShadowDiscMaterial(disc, new Color(0.18f, 0.88f, 1.00f, 0.55f));
            return disc;
        }

        private void EnsureTrackRoot()
        {
            if (trackRoot != null) return;
            var found = GameObject.Find("LevelRuntimeRoot");
            if (found != null) trackRoot = found.transform;
            else trackRoot = new GameObject("LevelRuntimeRoot").transform;
        }

        public void ClearTrack()
        {
            EnsureTrackRoot();
            _spawnedChunks.Clear();
            _activeObstacles.Clear();
            _activePickups.Clear();
            _activeSegmentNodes.Clear();
            _floatingIslands.Clear();
            _parallaxFarRoot = null;
            _parallaxMidRoot = null;
            for (int i = trackRoot.childCount - 1; i >= 0; i--)
            {
                var ch = trackRoot.GetChild(i);
                if (primaryPlayer != null && ch == primaryPlayer.transform) continue;
                if (Application.isPlaying) Destroy(ch.gameObject);
                else DestroyImmediate(ch.gameObject);
            }
            _secondarySplitVisual = null;
            _jellyBridgeVisual = null;
            _primaryShadowDisc = null;
            _secondaryShadowDisc = null;
            _waterOceanPlane = null;
            _waterOceanMat = null;
        }

        public static void EvaluateRoadCurve(float z, out Vector3 pos, out Quaternion rot, out Vector3 right)
        {
            float blend = Mathf.Clamp01((z - 3f) / 11f) * _activeCurveScale;
            float x = blend * (-Mathf.Sin((z - 3f) * 0.082f) * 4.4f + Mathf.Sin(z * 0.038f) * 1.4f);
            float y = blend * (Mathf.Sin(z * 0.055f) * 0.14f);

            float dz = 0.25f;
            float z2 = z + dz;
            float blend2 = Mathf.Clamp01((z2 - 3f) / 11f) * _activeCurveScale;
            float x2 = blend2 * (-Mathf.Sin((z2 - 3f) * 0.082f) * 4.4f + Mathf.Sin(z2 * 0.038f) * 1.4f);
            float y2 = blend2 * (Mathf.Sin(z2 * 0.055f) * 0.14f);

            Vector3 tangent = new Vector3(x2 - x, y2 - y, dz).normalized;
            float bankAngle = Mathf.Clamp(-(x2 - x) / dz * 6.5f, -9f, 9f);
            rot = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0f, 0f, bankAngle);
            right = rot * Vector3.right;
            pos = new Vector3(x, y, z);
        }

        public void ToggleManualSplitMerge()
        {
            SetSplitState(!IsSplit);
        }

        public void SetSplitState(bool targetSplit)
        {
            if (_manualSplitCooldown > 0f || IsGameOver || IsLevelCompleted) return;
            if (IsSplit == targetSplit) return;
            _manualSplitCooldown = 0.20f;
            IsSplit = targetSplit;
            _wobbleAmplitude = 0.26f;
            _wobblePhase = 0f;
            _audio?.PlaySplitMerge(SoundEnabled, VibrationEnabled);
            if (IsSplit)
            {
                QuestSplitsCount++;
                PlayerPrefs.SetInt("JellyQuestSplits", QuestSplitsCount);
            }
            if (primaryPlayer != null)
                SpawnJellySplash(primaryPlayer.transform.position, IsSplit ? new Color(0.75f, 0.38f, 1.00f) : new Color(1.00f, 0.82f, 0.20f), 7);
            SetBanner(IsSplit
                ? "🟣 İKİYE BÖLÜNDÜN (÷2)! İkiz Parçalar (%50 kütle) ile tünellerden geç!"
                : "🟡 BİRLEŞTİN (><)! Tek parça halinde ilerliyorsun!", 2.2f);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) { CurrentScreen = JellyScreenMode.Playing; StartLevel(CurrentLevelNumber); return; }
                if (kb.nKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame) { CurrentScreen = JellyScreenMode.Playing; LoadNextProgressiveLevel(); return; }
                if (kb.pKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) { CurrentScreen = JellyScreenMode.Playing; LoadPreviousProgressiveLevel(); return; }
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    CurrentScreen = CurrentScreen == JellyScreenMode.Playing ? JellyScreenMode.MainMenu : JellyScreenMode.Playing;
                    return;
                }
            }

            UpdateParallaxLayers(Time.deltaTime);

            // Pause forward movement when viewing MainMenu, Settings, or Paused; Swipe immediately starts from MainMenu!
            if (CurrentScreen != JellyScreenMode.Playing)
            {
                if (CurrentScreen == JellyScreenMode.MainMenu)
                {
                    var tsMenu = Touchscreen.current;
                    bool swipeStart = (tsMenu != null && tsMenu.primaryTouch.press.isPressed && tsMenu.primaryTouch.delta.ReadValue().sqrMagnitude > 16f) ||
                                      (Mouse.current != null && Mouse.current.leftButton.isPressed && Mouse.current.delta.ReadValue().sqrMagnitude > 16f);
                    if (swipeStart)
                    {
                        CurrentScreen = JellyScreenMode.Playing;
                        return;
                    }
                }
                SyncJellyVisualsAndPosition(0f);
                return;
            }

            if (IsLevelCompleted || IsGameOver)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (StatusBannerTimer > 0f) StatusBannerTimer -= dt;
            if (_bounceCooldown > 0f) _bounceCooldown -= dt;
            if (_speedBoostTimer > 0f) _speedBoostTimer -= dt;
            if (_manualSplitCooldown > 0f) _manualSplitCooldown -= dt;
            if (MagnetTimer > 0f) MagnetTimer -= dt;
            if (SlowTimeTimer > 0f)
            {
                SlowTimeTimer -= dt;
                EndlessGateObstacle.GlobalObstacleTimeScale = 0.35f;
            }
            else
            {
                EndlessGateObstacle.GlobalObstacleTimeScale = 1.0f;
            }

            bool manualTogglePressed = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) ||
                                       (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
            if (manualTogglePressed)
            {
                ToggleManualSplitMerge();
            }

            // 1. Sample horizontal steering + Bottom UI `<` / `>` buttons + Surface Hazard modifiers
            float rawKeySteer = _uiSteerHoldDir;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawKeySteer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawKeySteer += 1f;
            }
            rawKeySteer = Mathf.Clamp(rawKeySteer, -1f, 1f);
            _uiSteerHoldDir = 0f;

            bool onIce = false;
            float conveyorDelta = 0f;
            EvaluateActiveSegmentHazards(dt, ref onIce, ref conveyorDelta);

            float accelRate = onIce
                ? 2.8f
                : (Mathf.Abs(rawKeySteer) > 0.01f ? steerAcceleration : steerDeceleration);
            _smoothedSteerInput = Mathf.MoveTowards(_smoothedSteerInput, rawKeySteer, accelRate * dt);

            float dragDeltaOffset = 0f;
            float screenWidth = Mathf.Max(400f, Screen.width);
            float topSafePauseZone = Screen.height * 0.90f; // Only exclude top-most HUD bar
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                Vector2 p = ts.primaryTouch.position.ReadValue();
                if (ts.primaryTouch.press.wasPressedThisFrame && p.y < topSafePauseZone)
                {
                    float now = Time.unscaledTime;
                    if (now - _lastTapTime < 0.28f && Vector2.Distance(p, _pointerDownPos) < screenWidth * 0.08f)
                    {
                        ToggleManualSplitMerge();
                        _lastTapTime = -1f;
                    }
                    else
                    {
                        _lastTapTime = now;
                        _pointerDownPos = p;
                    }
                }
                if (p.y < topSafePauseZone)
                {
                    if (!_dragging) { _dragging = true; _dragPrevPos = p; }
                    dragDeltaOffset = ((p.x - _dragPrevPos.x) / screenWidth) * roadWidth * 1.18f;
                    _dragPrevPos = p;
                }
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                if (Mouse.current.leftButton.wasPressedThisFrame && p.y < topSafePauseZone)
                {
                    float now = Time.unscaledTime;
                    if (now - _lastTapTime < 0.28f && Vector2.Distance(p, _pointerDownPos) < screenWidth * 0.08f)
                    {
                        ToggleManualSplitMerge();
                        _lastTapTime = -1f;
                    }
                    else
                    {
                        _lastTapTime = now;
                        _pointerDownPos = p;
                    }
                }
                if (p.y < topSafePauseZone)
                {
                    if (!_dragging) { _dragging = true; _dragPrevPos = p; }
                    dragDeltaOffset = ((p.x - _dragPrevPos.x) / screenWidth) * roadWidth * 1.18f;
                    _dragPrevPos = p;
                }
            }
            else
            {
                _dragging = false;
            }

            // Mass-responsive steering agility: lighter jelly is snappier, giant jelly feels heavier
            float massAgility = Mathf.Lerp(1.22f, 0.80f, Mathf.InverseLerp(45f, 160f, CurrentMass));
            float maxOffset = IsSplit ? maxLateralOffset - 0.80f : maxLateralOffset;
            float nextOffset = CurrentLateralOffset + (_smoothedSteerInput * lateralSteerSpeed * massAgility * dt) + (dragDeltaOffset * massAgility) + conveyorDelta;

            // Gentle magnetic funnel assist when approaching TwinSplitTunnels or MovingTwinTunnels while split
            if (IsSplit)
            {
                for (int i = 0; i < _activeObstacles.Count; i++)
                {
                    var o = _activeObstacles[i];
                    if (o == null || o.resolved) continue;
                    if (o.obstacleType == EndlessObstacleType.TwinSplitTunnels || o.obstacleType == EndlessObstacleType.MovingTwinTunnels)
                    {
                        float distToGate = o.trackZ - CurrentDistance;
                        float targetCenter = o.obstacleType == EndlessObstacleType.MovingTwinTunnels ? o.CurrentOpeningCenterX : 0f;
                        if (distToGate > 0f && distToGate < 8.5f && Mathf.Abs(nextOffset - targetCenter) < 1.55f)
                        {
                            nextOffset = Mathf.MoveTowards(nextOffset, targetCenter, dt * 3.0f);
                        }
                    }
                }
            }

            CurrentLateralOffset = Mathf.Clamp(nextOffset, -maxOffset, maxOffset);

            // 2. Advance along the course + Update Vertical Jump / Double-Decker Elevation
            float levelSpeedMult = _currentBlueprint != null ? _currentBlueprint.SpeedMultiplier : 1f;
            float speed = Mathf.Min(maxForwardSpeed, baseForwardSpeed * levelSpeedMult);
            if (_speedBoostTimer > 0f)
                speed *= 1.35f;
            if (SlowTimeTimer > 0f)
                speed *= 0.80f;
            if (_onStickyFloor)
                speed *= 0.68f;
            if (_bounceCooldown > 0f)
                speed = -7.2f;

            CurrentDistance = Mathf.Max(0f, CurrentDistance + speed * dt);

            UpdateVerticalAndSegmentMechanics(dt);

            // 3. Position & animate Jelly (single or split twin) on the 3D spline road
            SyncJellyVisualsAndPosition(dt * speed);

            // 4. Check pickup collection (+ Magnet attraction)
            CheckPickups(dt);

            // 5. Evaluate cutout and moving obstacles as player crosses their Z plane
            CheckObstacles();

            // 6. Smoothly follow the winding road with the camera
            UpdateCameraSmooth(dt);
        }

        private void EvaluateActiveSegmentHazards(float dt, ref bool onIce, ref float conveyorDelta)
        {
            _onStickyFloor = false;
            for (int i = 0; i < _activeSegmentNodes.Count; i++)
            {
                var node = _activeSegmentNodes[i];
                if (node == null) continue;
                if (CurrentDistance < node.zStart || CurrentDistance > node.zEnd) continue;

                if (node.moduleType == TrackSegmentModuleType.SurfaceHazardZone && _airborneY <= 0.1f)
                {
                    switch (node.hazardKind)
                    {
                        case SurfaceHazardKind.IceDrift:
                            onIce = true;
                            break;
                        case SurfaceHazardKind.ConveyorPushLeft:
                            conveyorDelta -= node.conveyorPushForce * dt;
                            break;
                        case SurfaceHazardKind.ConveyorPushRight:
                            conveyorDelta += node.conveyorPushForce * dt;
                            break;
                        case SurfaceHazardKind.SlimeMassDrain:
                            if (Mathf.Abs(CurrentLateralOffset) > 1.15f)
                            {
                                CurrentMass = Mathf.Max(22f, CurrentMass - 14f * dt);
                            }
                            break;
                        case SurfaceHazardKind.MagneticField:
                            float pullDir = Mathf.Sin(node.zStart * 0.37f) >= 0f ? 1f : -1f;
                            conveyorDelta += pullDir * 2.4f * dt;
                            break;
                        case SurfaceHazardKind.StickyFloor:
                            _onStickyFloor = true;
                            break;
                        case SurfaceHazardKind.JumpPad:
                            if (Mathf.Abs(CurrentDistance - (node.zStart + 5.5f)) <= 0.85f && _verticalVelocity <= 0.1f)
                            {
                                _verticalVelocity = 11.8f;
                                _wobbleAmplitude = 0.30f;
                                _wobblePhase = 0f;
                                AddScoreWithCombo(30);
                                SetBanner("🟢 ZIPLAMA PEDİ! Üst seviyeye fırladın! (+30 Puan)", 2.0f);
                            }
                            break;
                    }
                }
            }
        }

        private void UpdateVerticalAndSegmentMechanics(float dt)
        {
            float targetDeckY = 0f;

            for (int i = 0; i < _activeSegmentNodes.Count; i++)
            {
                var node = _activeSegmentNodes[i];
                if (node == null) continue;

                // 1. Jump Ramp Trigger (JumpRampOverAbyss or DoubleDeckerOverpass entrance ramp)
                if ((node.moduleType == TrackSegmentModuleType.JumpRampOverAbyss ||
                     node.moduleType == TrackSegmentModuleType.DoubleDeckerOverpass) &&
                    Mathf.Abs(CurrentDistance - node.rampTriggerZ) <= 0.85f &&
                    _verticalVelocity <= 0.1f && _airborneY <= 0.25f)
                {
                    // Lighter jelly jumps higher; heavy jelly jumps lower
                    float weightFactor = CurrentMass <= 80f ? 1.12f : (CurrentMass >= 115f ? 0.92f : 1.0f);
                    _verticalVelocity = node.launchUpVelocity * weightFactor;
                    _wobbleAmplitude = 0.28f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(25);
                    SetBanner("🚀 RAMPADAN HAVALANDIN! (+25 Puan)", 2.0f);
                }

                // 2. Check if currently over DoubleDeckerOverpass Upper Deck
                if (node.moduleType == TrackSegmentModuleType.DoubleDeckerOverpass &&
                    CurrentDistance >= node.zStart + 2.5f && CurrentDistance <= node.zEnd - 1.0f)
                {
                    if (_deckBaseY + _airborneY >= node.upperDeckHeight - 0.55f)
                    {
                        targetDeckY = node.upperDeckHeight;

                        // Check Breakable Glass Floor section on the Upper Deck!
                        if (!node.glassBroken && CurrentDistance >= node.glassFloorZStart && CurrentDistance <= node.glassFloorZEnd)
                        {
                            if (CurrentMass >= 112f)
                            {
                                node.glassBroken = true;
                                if (node.breakableGlassFloor != null)
                                    node.breakableGlassFloor.gameObject.SetActive(false);
                                targetDeckY = 0f;
                                _airborneY = node.upperDeckHeight;
                                _deckBaseY = 0f;
                                _verticalVelocity = -4.5f;
                                AddScoreWithCombo(40);
                                if (primaryPlayer != null)
                                    SpawnJellySplash(primaryPlayer.transform.position, new Color(0.28f, 0.95f, 1.00f), 12);
                                SetBanner("💥 AĞIR JÖLEYLE ÜST KAT CAMINI KIRIP ALT KATA İNDİN! (+40 Puan)", 2.6f);
                            }
                        }
                    }
                }

                // 3. SplitTwinBridge Center Chasm Check
                if (node.moduleType == TrackSegmentModuleType.SplitTwinBridge &&
                    CurrentDistance >= node.abyssZStart && CurrentDistance <= node.abyssZEnd &&
                    _airborneY <= 0.15f && _bounceCooldown <= 0f)
                {
                    if (!IsSplit && Mathf.Abs(CurrentLateralOffset) < node.centerGapHalfWidth)
                    {
                        // Single jelly tried to walk over the open center chasm between the twin bridges!
                        CurrentMass -= 20f;
                        if (CurrentMass < 22f)
                        {
                            TriggerGameOver("💥 ÇATAL KÖPRÜ ORTASINDAKİ UÇURUMA DÜŞTÜN!");
                            return;
                        }
                        // Nudge onto nearest bridge deck
                        CurrentLateralOffset = CurrentLateralOffset >= 0f ? 1.55f : -1.55f;
                        _wobbleAmplitude = 0.34f;
                        _wobblePhase = 0f;
                        SetBanner("⚠ ORTASI UÇURUM (-20 Kütle)! Bölün (÷2) veya yan köprüde kal!", 2.5f);
                    }
                }

                // 4. Laser Slicer Gate (Blue Auto-Splitter or Red Mass Trimmer)
                if (node.moduleType == TrackSegmentModuleType.LaserSlicerGate && !node.laserTriggered &&
                    Mathf.Abs(CurrentDistance - node.laserTriggerZ) <= 0.75f)
                {
                    node.laserTriggered = true;
                    if (node.laserKind == LaserGateKind.BlueVerticalAutoSplitter)
                    {
                        if (!IsSplit)
                        {
                            IsSplit = true;
                            _wobbleAmplitude = 0.30f;
                            _wobblePhase = 0f;
                            AddScoreWithCombo(30);
                            SetBanner("✂ MAVİ LAZER JÖLEYİ ORTADAN İKİYE (÷2) BÖLDÜ! (+30 Puan)", 2.5f);
                        }
                    }
                    else
                    {
                        if (CurrentMass > 72f)
                        {
                            CurrentMass = 70f;
                            _wobbleAmplitude = 0.28f;
                            _wobblePhase = 0f;
                            AddScoreWithCombo(25);
                            SetBanner("✂ KIRMIZI LAZER FAZLA KÜTLEYİ 70'E TIRAŞLADI! Dar geçide hazırsın!", 2.5f);
                        }
                    }
                }

                // 5. Color Match Section (Dye Pool + Color Barrier Gate)
                if (node.moduleType == TrackSegmentModuleType.ColorMatchSection)
                {
                    if (!node.dyeApplied && Mathf.Abs(CurrentDistance - node.dyePadZ) <= 0.9f)
                    {
                        node.dyeApplied = true;
                        // Determine which lane dye pool player stepped into (-1.5=0 Cyan, 0=1 Gold, +1.5=2 Magenta)
                        if (CurrentLateralOffset < -0.65f) _currentDyeColorIndex = 0;
                        else if (CurrentLateralOffset > 0.65f) _currentDyeColorIndex = 2;
                        else _currentDyeColorIndex = 1;

                        SetBanner($"🎨 JÖLE RENGİ {DyeColorNames[_currentDyeColorIndex]} OLDU!", 2.0f);
                    }

                    if (!node.colorGateResolved && Mathf.Abs(CurrentDistance - node.colorGateZ) <= 0.75f)
                    {
                        node.colorGateResolved = true;
                        if (_currentDyeColorIndex == node.requiredColorIndex)
                        {
                            ComboCount++;
                            AddScoreWithCombo(45);
                            SetBanner($"✓ {DyeColorNames[node.requiredColorIndex]} ENERJİ KAPISINDAN UYUMLA GEÇTİN! (+45 Puan)", 2.4f);
                        }
                        else
                        {
                            CurrentMass -= 20f;
                            if (CurrentMass < 22f)
                            {
                                TriggerGameOver("💥 YANLIŞ RENKLİ ENERJİ BARİYERİNDE ERİDİN!");
                                return;
                            }
                            SetBanner($"⚠ YANLIŞ RENK (-20 Kütle)! {DyeColorNames[node.requiredColorIndex]} boyadan geçmeliydin!", 2.5f);
                        }
                    }
                }

                // 6. Level Finale Multiplier Walls (x2, x3, x5, x10) & Level Complete!
                if (node.moduleType == TrackSegmentModuleType.LevelFinaleMultiplier && !node.wallResolved &&
                    CurrentDistance >= node.wallZ - 0.45f)
                {
                    node.wallResolved = true;
                    if (CurrentMass >= node.requiredMassToBreak || node.isFinalGoalLine)
                    {
                        LastMultiplierReached = Mathf.Max(LastMultiplierReached, node.multiplierValue);
                        if (node.breakableWallVisual != null)
                            node.breakableWallVisual.gameObject.SetActive(false);
                        if (primaryPlayer != null)
                            SpawnJellySplash(primaryPlayer.transform.position, new Color(1f, 0.84f, 0.20f), 14);

                        Score += 50 * node.multiplierValue;
                        if (Score > HighScore)
                        {
                            HighScore = Score;
                            PlayerPrefs.SetInt("JellyEndlessHighScore", HighScore);
                        }

                        if (node.isFinalGoalLine)
                        {
                            CompleteCurrentLevel();
                            return;
                        }
                        else
                        {
                            SetBanner($"💥 x{node.multiplierValue} FİNAL DUVARINI YIKTIN! (+{50 * node.multiplierValue} Puan)", 2.0f);
                        }
                    }
                    else
                    {
                        // Not enough mass to break this higher multiplier wall -> level finishes here with the multiplier reached so far!
                        CompleteCurrentLevel();
                        return;
                    }
                }
            }

            // Smoothly transition deck base height & airborne gravity parabola
            if (targetDeckY < _deckBaseY && _airborneY <= 0.05f)
            {
                // Stepped off the end of the upper deck -> fall smoothly back to ground deck
                _airborneY = _deckBaseY;
                _deckBaseY = 0f;
            }
            else if (targetDeckY > _deckBaseY && _deckBaseY + _airborneY >= targetDeckY - 0.25f)
            {
                _deckBaseY = targetDeckY;
                _airborneY = Mathf.Max(0f, (_deckBaseY + _airborneY) - targetDeckY);
            }

            if (_airborneY > 0f || _verticalVelocity > 0f)
            {
                _airborneY += _verticalVelocity * dt;
                _verticalVelocity -= 24.5f * dt;
                if (_airborneY <= 0f)
                {
                    _airborneY = 0f;
                    _verticalVelocity = 0f;
                    _wobbleAmplitude = 0.22f;
                    _wobblePhase = 0f;
                }
            }
        }

        private void CompleteCurrentLevel()
        {
            IsLevelCompleted = true;
            _audio?.PlayLevelWin(SoundEnabled, VibrationEnabled);
            if (CurrentLevelNumber >= HighestUnlockedLevel)
            {
                HighestUnlockedLevel = CurrentLevelNumber + 1;
                PlayerPrefs.SetInt("JellyHighestLevel", HighestUnlockedLevel);
            }
            SetBanner($"🎉 SEVİYE {CurrentLevelNumber} TAMAMLANDI! (x{LastMultiplierReached} Çarpan Bonusu!)", 999f);
        }

        public static float ComputeVisualDiameter(float mass)
        {
            // Reference Section 1:
            // Küçük Jöle (≤ 75): Ø 0.62 - 0.85 m
            // Normal Jöle (76 - 114): Ø ~1.0 m
            // Dev Jöle (≥ 115): Ø 1.16 - 1.65 m
            if (mass <= 75f)
                return Mathf.Lerp(0.62f, 0.85f, Mathf.InverseLerp(30f, 75f, mass));
            if (mass < 115f)
                return Mathf.Lerp(0.88f, 1.12f, Mathf.InverseLerp(76f, 114f, mass));
            return Mathf.Lerp(1.16f, 1.65f, Mathf.InverseLerp(115f, 195f, mass));
        }

        private void SyncJellyVisualsAndPosition(float forwardDelta)
        {
            EnsurePlayerObjects();
            if (primaryPlayer == null) return;

            float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
            float targetBlend = IsSplit ? 1f : 0f;
            if (Application.isPlaying)
            {
                _currentSplitBlend = Mathf.SmoothDamp(_currentSplitBlend, targetBlend, ref _splitBlendVelocity, 0.20f, 10f, dt);
                _displayedMass = Mathf.Lerp(_displayedMass, CurrentMass, Mathf.Clamp01(dt * 9.5f));
                if (_wobbleAmplitude > 0.001f)
                {
                    _wobblePhase += dt * 22f;
                    _wobbleAmplitude = Mathf.MoveTowards(_wobbleAmplitude, 0f, dt * 0.85f);
                }
            }
            else
            {
                _currentSplitBlend = targetBlend;
                _displayedMass = CurrentMass;
            }

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out Quaternion roadRot, out Vector3 roadRight);
            Vector3 roadUp = roadRot * Vector3.up;

            float effectiveSphereMass = Mathf.Lerp(_displayedMass, _displayedMass * 0.5f, _currentSplitBlend);
            float diameter = ComputeVisualDiameter(effectiveSphereMass);

            float stretchPhase = 4f * _currentSplitBlend * (1f - _currentSplitBlend);
            float liveTime = Application.isPlaying ? Time.unscaledTime : (float)System.DateTime.Now.TimeOfDay.TotalSeconds;
            float idlePulse = Mathf.Sin(liveTime * 5.2f) * 0.032f;
            float moveWave = forwardDelta > 0.001f ? Mathf.Sin(CurrentDistance * 2.4f) * 0.038f : 0f;
            float wobble = Mathf.Sin(_wobblePhase) * _wobbleAmplitude + idlePulse + moveWave;
            float scaleX = diameter * (0.98f + stretchPhase * 0.32f + wobble);
            float scaleY = diameter * Mathf.Max(0.68f, 1.02f - stretchPhase * 0.18f - wobble * 0.55f);
            float scaleZ = diameter * (0.98f - stretchPhase * 0.10f - wobble * 0.20f);
            Vector3 jellyScale = new Vector3(scaleX, scaleY, scaleZ);
            Quaternion bankRot = roadRot * Quaternion.Euler(0f, 0f, -_smoothedSteerInput * 11f);

            Color baseSkin = SkinPaletteColors[Mathf.Clamp(SelectedSkinIndex, 0, SkinPaletteColors.Length - 1)];
            Color stateTint;
            if (_currentDyeColorIndex >= 0 && _currentDyeColorIndex < DyeColors.Length)
            {
                stateTint = DyeColors[_currentDyeColorIndex];
            }
            else if (SelectedSkinIndex == 0)
            {
                // Reference Section 1: Toy Waterpark saturated aqua-turquoise jelly (never pale white!)
                stateTint = CurrentMass >= 115f
                    ? new Color(0.10f, 0.88f, 0.48f, 0.96f)
                    : (CurrentMass <= 75f ? new Color(0.08f, 0.82f, 0.92f, 0.96f) : new Color(0.04f, 0.78f, 0.90f, 0.96f));
            }
            else
            {
                stateTint = CurrentMass >= 115f
                    ? Color.Lerp(baseSkin, new Color(0.16f, 0.98f, 0.52f, 0.95f), 0.45f)
                    : baseSkin;
            }

            LevelLoader.ApplyMaterialOrColor(primaryPlayer.gameObject, jellyMaterial, stateTint);
            BlenderKitModelSpawner.ApplyJellySkinMaterial(primaryPlayer.gameObject, stateTint);
            if (_secondarySplitVisual != null)
            {
                LevelLoader.ApplyMaterialOrColor(_secondarySplitVisual, jellyMaterial, stateTint);
                BlenderKitModelSpawner.ApplyJellySkinMaterial(_secondarySplitVisual, stateTint);
            }
            Color contactShadowCol = new Color(0.05f, 0.26f, 0.32f, 0.58f);
            if (_primaryShadowDisc != null)
                BlenderKitModelSpawner.ApplySoftShadowDiscMaterial(_primaryShadowDisc, contactShadowCol);
            if (_secondaryShadowDisc != null)
                BlenderKitModelSpawner.ApplySoftShadowDiscMaterial(_secondaryShadowDisc, contactShadowCol);
            if (_shieldBubbleVisual != null && _shieldBubbleVisual.activeSelf != HasShield)
                _shieldBubbleVisual.SetActive(HasShield);

            if (primaryPlayer.massController != null)
                primaryPlayer.massController.SetMassImmediate(effectiveSphereMass);

            if (_currentSplitBlend <= 0.005f && !IsSplit)
            {
                Vector3 deckGroundPos = roadCenter + roadRight * CurrentLateralOffset + roadUp * _deckBaseY;
                Vector3 worldPos = deckGroundPos + roadUp * (_airborneY + scaleY * 0.42f + 0.015f);
                primaryPlayer.transform.position = worldPos;
                primaryPlayer.transform.localScale = jellyScale;
                primaryPlayer.transform.rotation = bankRot;

                if (_primaryShadowDisc != null)
                {
                    _primaryShadowDisc.SetActive(true);
                    _primaryShadowDisc.transform.position = deckGroundPos + roadUp * 0.02f;
                    _primaryShadowDisc.transform.rotation = roadRot;
                    float shadowShrink = Mathf.Clamp(1f - _airborneY * 0.18f, 0.4f, 1f);
                    _primaryShadowDisc.transform.localScale = new Vector3(scaleX * 1.05f * shadowShrink, 0.018f, scaleZ * 1.05f * shadowShrink);
                }
                if (_secondaryShadowDisc != null && _secondaryShadowDisc.activeSelf)
                    _secondaryShadowDisc.SetActive(false);

                if (_secondarySplitVisual != null && _secondarySplitVisual.activeSelf)
                    _secondarySplitVisual.SetActive(false);
                if (_jellyBridgeVisual != null && _jellyBridgeVisual.activeSelf)
                    _jellyBridgeVisual.SetActive(false);
            }
            else
            {
                float smoothT = Mathf.SmoothStep(0f, 1f, _currentSplitBlend);
                float maxSpread = 1.32f;
                float currentSpread = maxSpread * smoothT;

                Vector3 centerGround = roadCenter + roadRight * CurrentLateralOffset + roadUp * _deckBaseY;
                Vector3 centerWorld = centerGround + roadUp * (_airborneY + scaleY * 0.42f + 0.015f);
                Vector3 leftPos = centerWorld - roadRight * currentSpread;
                Vector3 rightPos = centerWorld + roadRight * currentSpread;

                primaryPlayer.transform.position = leftPos;
                primaryPlayer.transform.localScale = jellyScale;
                primaryPlayer.transform.rotation = bankRot;

                if (_secondarySplitVisual != null)
                {
                    if (!_secondarySplitVisual.activeSelf) _secondarySplitVisual.SetActive(true);
                    _secondarySplitVisual.transform.position = rightPos;
                    _secondarySplitVisual.transform.localScale = jellyScale;
                    _secondarySplitVisual.transform.rotation = bankRot;
                }

                if (_primaryShadowDisc != null)
                {
                    _primaryShadowDisc.SetActive(true);
                    _primaryShadowDisc.transform.position = (centerGround - roadRight * currentSpread) + roadUp * 0.02f;
                    _primaryShadowDisc.transform.rotation = roadRot;
                    _primaryShadowDisc.transform.localScale = new Vector3(scaleX * 1.05f, 0.018f, scaleZ * 1.05f);
                }
                if (_secondaryShadowDisc != null)
                {
                    _secondaryShadowDisc.SetActive(true);
                    _secondaryShadowDisc.transform.position = (centerGround + roadRight * currentSpread) + roadUp * 0.02f;
                    _secondaryShadowDisc.transform.rotation = roadRot;
                    _secondaryShadowDisc.transform.localScale = new Vector3(scaleX * 1.05f, 0.018f, scaleZ * 1.05f);
                }

                if (_jellyBridgeVisual != null)
                {
                    if (_currentSplitBlend > 0.01f && _currentSplitBlend < 0.72f)
                    {
                        if (!_jellyBridgeVisual.activeSelf) _jellyBridgeVisual.SetActive(true);
                        float bridgeThickness = Mathf.Clamp01(1f - (_currentSplitBlend / 0.72f)) * diameter * 0.62f;
                        _jellyBridgeVisual.transform.position = centerWorld;
                        _jellyBridgeVisual.transform.rotation = roadRot;
                        _jellyBridgeVisual.transform.localScale = new Vector3(currentSpread * 2.1f, bridgeThickness, bridgeThickness);
                    }
                    else if (_jellyBridgeVisual.activeSelf)
                    {
                        _jellyBridgeVisual.SetActive(false);
                    }
                }
            }

            // Spawn subtle trail droplets behind the player when moving forward
            if (Application.isPlaying && forwardDelta > 0.01f && SelectedTrailIndex > 0)
            {
                _trailSpawnTimer -= dt;
                if (_trailSpawnTimer <= 0f)
                {
                    _trailSpawnTimer = 0.09f;
                    var trailDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    DestroyCollider(trailDot);
                    trailDot.transform.position = primaryPlayer.transform.position - (roadRot * Vector3.forward) * (diameter * 0.45f);
                    trailDot.transform.localScale = Vector3.one * 0.18f;
                    LevelLoader.ApplyMaterialOrColor(trailDot, null, stateTint);
                    Destroy(trailDot, 0.32f);
                }
            }
        }

        public void ApplyPickup(EndlessPickupType type, float amount, Vector3 pickupWorldPos = default)
        {
            if (pickupWorldPos == default && primaryPlayer != null)
                pickupWorldPos = primaryPlayer.transform.position;

            _audio?.PlayPickup(SoundEnabled, VibrationEnabled, ComboCount, type == EndlessPickupType.Shrink);

            switch (type)
            {
                case EndlessPickupType.Grow:
                    CurrentMass = Mathf.Clamp(CurrentMass + amount, 25f, 220f);
                    if (CurrentMass >= 150f && QuestReach150Count < 1)
                    {
                        QuestReach150Count = 1;
                        PlayerPrefs.SetInt("JellyQuestMass150", 1);
                    }
                    _wobbleAmplitude = 0.24f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(15);
                    SpawnJellySplash(pickupWorldPos, new Color(0.22f, 0.95f, 0.48f), 7);
                    SetBanner($"🟢 BÜYÜME KÜRESİ (+{amount:F0})! Kütle: {CurrentMass:F0}", 2.0f);
                    break;

                case EndlessPickupType.Shrink:
                    CurrentMass = Mathf.Clamp(CurrentMass - amount, 35f, 220f);
                    _wobbleAmplitude = 0.22f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(15);
                    SpawnJellySplash(pickupWorldPos, new Color(0.98f, 0.30f, 0.38f), 7);
                    SetBanner($"🔴 KÜÇÜLME KÜRESİ (-{amount:F0})! Kütle: {CurrentMass:F0} (Dar Geçide Hazır!)", 2.0f);
                    break;

                case EndlessPickupType.Split:
                    if (!IsSplit)
                    {
                        IsSplit = true;
                        QuestSplitsCount++;
                        PlayerPrefs.SetInt("JellyQuestSplits", QuestSplitsCount);
                        _wobbleAmplitude = 0.28f;
                        _wobblePhase = 0f;
                        AddScoreWithCombo(20);
                        SpawnJellySplash(pickupWorldPos, new Color(0.75f, 0.35f, 1.00f), 9);
                        SetBanner("🟣 BÖLÜNME KRİSTALİ (÷2)! Jöle 2 ikiz parçaya bölündü!", 2.5f);
                    }
                    break;

                case EndlessPickupType.Merge:
                    if (IsSplit)
                    {
                        IsSplit = false;
                        _wobbleAmplitude = 0.26f;
                        _wobblePhase = 0f;
                        AddScoreWithCombo(20);
                        SpawnJellySplash(pickupWorldPos, new Color(1.00f, 0.84f, 0.22f), 9);
                        SetBanner("🟡 BİRLEŞME HALKASI (><)! 2 parça tekrar birleşti!", 2.5f);
                    }
                    break;

                case EndlessPickupType.SpeedBoost:
                    _speedBoostTimer = 2.2f;
                    _wobbleAmplitude = 0.25f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(25);
                    SpawnJellySplash(pickupWorldPos, new Color(0.20f, 0.92f, 1.00f), 10);
                    SetBanner("⚡ SÜPER HIZ BANDI! (2.2 sn / +%35 Hız)", 2.0f);
                    break;

                case EndlessPickupType.Shield:
                    HasShield = true;
                    _wobbleAmplitude = 0.22f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(30);
                    SpawnJellySplash(pickupWorldPos, new Color(0.88f, 0.38f, 1.00f), 8);
                    SetBanner("🛡 KALKAN AKTİF! (1 kez hasarı tamamen engeller!)", 2.4f);
                    break;

                case EndlessPickupType.Magnet:
                    MagnetTimer = 3.2f;
                    _wobbleAmplitude = 0.22f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(30);
                    SpawnJellySplash(pickupWorldPos, new Color(0.28f, 0.72f, 1.00f), 8);
                    SetBanner("🧲 MIKNATIS AKTİF! (~3 sn tüm yararlı nesneleri çeker!)", 2.4f);
                    break;

                case EndlessPickupType.StarScore:
                    AddScoreWithCombo(500);
                    _wobbleAmplitude = 0.24f;
                    _wobblePhase = 0f;
                    SpawnJellySplash(pickupWorldPos, new Color(1.00f, 0.88f, 0.20f), 12);
                    SetBanner("⭐ PUAN YILDIZI! (+500 Puan Bonusu!)", 2.2f);
                    break;

                case EndlessPickupType.SlowTime:
                    SlowTimeTimer = 2.2f;
                    _wobbleAmplitude = 0.20f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(30);
                    SpawnJellySplash(pickupWorldPos, new Color(0.98f, 0.72f, 0.28f), 8);
                    SetBanner("⏳ YAVAŞ ZAMAN! (2 sn engeller yavaşladı!)", 2.2f);
                    break;
            }
        }

        private void AddScoreWithCombo(int basePoints)
        {
            int mult = Mathf.Clamp(1 + ComboCount / 2, 1, 5);
            Score += basePoints * mult;
            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt("JellyEndlessHighScore", HighScore);
            }
        }

        private static void SpawnCrystalShatterBurst(Vector3 centerPos)
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < 14; i++)
            {
                var shard = CreateWallBlock(null, "CrystalShard", centerPos + Random.insideUnitSphere * 0.9f,
                    new Vector3(Random.Range(0.18f, 0.42f), Random.Range(0.22f, 0.55f), Random.Range(0.14f, 0.32f)),
                    null, new Color(0.32f, 0.94f, 1.00f, 0.85f));
                shard.transform.rotation = Random.rotation;
                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.12f;
                rb.linearVelocity = new Vector3(Random.Range(-5.5f, 5.5f), Random.Range(3.2f, 8.2f), Random.Range(4.0f, 11.5f));
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                Destroy(shard, 1.15f);
            }
        }

        private static void SpawnJellySplash(Vector3 pos, Color col, int count)
        {
            if (!Application.isPlaying) return;

            // Shockwave / Sıçrama Ring on ground
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "JellyImpactWaveRing";
            var rc = ring.GetComponent<Collider>();
            if (rc != null) Destroy(rc);
            ring.transform.position = pos + Vector3.down * 0.2f;
            ring.transform.localScale = new Vector3(1.45f, 0.02f, 1.45f);
            LevelLoader.ApplyMaterialOrColor(ring, null, new Color(col.r, col.g, col.b, 0.75f));
            Destroy(ring, 0.35f);

            for (int i = 0; i < count; i++)
            {
                var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                drop.name = "JellySplashDrop";
                var c = drop.GetComponent<Collider>();
                if (c != null) Destroy(c);
                drop.transform.position = pos + Random.insideUnitSphere * 0.35f;
                drop.transform.localScale = Vector3.one * Random.Range(0.12f, 0.26f);
                LevelLoader.ApplyMaterialOrColor(drop, null, col);
                var rb = drop.AddComponent<Rigidbody>();
                rb.mass = 0.08f;
                rb.linearVelocity = new Vector3(Random.Range(-3.5f, 3.5f), Random.Range(2.5f, 5.5f), Random.Range(1f, 6f));
                Destroy(drop, 0.75f);
            }
        }

        private void CheckPickups(float dt)
        {
            if (primaryPlayer == null) return;

            float singleMass = IsSplit ? _displayedMass * 0.5f : _displayedMass;
            float jellyRadius = ComputeVisualDiameter(singleMass) * 0.5f;
            float catchRadius = jellyRadius + 0.75f;

            Vector3 posA = primaryPlayer.transform.position;
            bool hasTwin = (IsSplit || _currentSplitBlend > 0.15f) && _secondarySplitVisual != null && _secondarySplitVisual.activeSelf;
            Vector3 posB = hasTwin ? _secondarySplitVisual.transform.position : posA;
            Vector3 midPos = (posA + posB) * 0.5f;

            for (int i = 0; i < _activePickups.Count; i++)
            {
                var p = _activePickups[i];
                if (p == null || p.IsConsumed || !p.gameObject.activeInHierarchy) continue;

                // Magnet pulls beneficial pickups toward player!
                if (MagnetTimer > 0f && p.pickupType != EndlessPickupType.Shrink)
                {
                    float dz = p.trackZ - CurrentDistance;
                    if (dz > -1f && dz < 11f)
                    {
                        Vector3 nextPos = Vector3.MoveTowards(p.transform.position, midPos, dt * 16f);
                        p.SetWorldPosition(nextPos);
                    }
                }

                Vector3 pPos = p.transform.position;
                bool hitA = (pPos - posA).sqrMagnitude <= catchRadius * catchRadius;
                bool hitB = hasTwin && (pPos - posB).sqrMagnitude <= catchRadius * catchRadius;
                bool hitMid = hasTwin && (p.pickupType == EndlessPickupType.Merge || p.pickupType == EndlessPickupType.SpeedBoost) &&
                              (pPos - midPos).sqrMagnitude <= (catchRadius + 0.7f) * (catchRadius + 0.7f);

                if (hitA || hitB || hitMid)
                {
                    p.TryCollect(this);
                }
            }
        }

        private void CheckObstacles()
        {
            if (_bounceCooldown > 0f || _deckBaseY > 1.0f || _airborneY > 1.2f) return;

            for (int i = 0; i < _activeObstacles.Count; i++)
            {
                var obs = _activeObstacles[i];
                if (obs == null || obs.resolved) continue;

                float dz = CurrentDistance - obs.trackZ;
                if (dz >= -0.45f && dz <= 0.85f)
                {
                    EvaluateObstaclePassage(obs);
                }
            }
        }

        private void EvaluateObstaclePassage(EndlessGateObstacle obs)
        {
            float leftPieceX = CurrentLateralOffset - 1.32f;
            float rightPieceX = CurrentLateralOffset + 1.32f;

            switch (obs.obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole:
                case EndlessObstacleType.VerticalCrusherGate:
                case EndlessObstacleType.HeavySmashWall:
                case EndlessObstacleType.TwinSplitTunnels:
                case EndlessObstacleType.MultiHoleDecisionWall:
                case EndlessObstacleType.FlexibleSqueezeGate:
                case EndlessObstacleType.MassScaleGate:
                case EndlessObstacleType.SlidingHoleWall:
                case EndlessObstacleType.MicroShatterGate:
                {
                    Phase2ObstacleEvaluation eval = obs.EvaluateCoreObstacle(CurrentMass, CurrentLateralOffset, IsSplit);
                    switch (eval.Outcome)
                    {
                        case Phase2ObstacleOutcome.CleanPass:
                        case Phase2ObstacleOutcome.BonusRiskPass:
                            if (obs.obstacleType == EndlessObstacleType.MicroShatterGate && !IsSplit)
                            {
                                IsSplit = true;
                            }
                            if (eval.ComboIncrement > 1)
                            {
                                ComboCount += (eval.ComboIncrement - 1);
                            }
                            RegisterCleanGatePass(obs, eval.ScoreAwarded, eval.FeedbackBanner);
                            break;

                        case Phase2ObstacleOutcome.CrystalShatterPass:
                            obs.ShatterBreakablePiece();
                            QuestSmashWallsCount++;
                            PlayerPrefs.SetInt("JellyQuestSmash", QuestSmashWallsCount);
                            RegisterCleanGatePass(obs, eval.ScoreAwarded, eval.FeedbackBanner);
                            break;

                        case Phase2ObstacleOutcome.PartialSplitSacrifice:
                            SacrificeOneSplitPieceAndContinue(obs, eval.SurvivingLateralX, eval.FeedbackBanner);
                            break;

                        default:
                            TriggerSolidWallBounce(obs, eval.FeedbackBanner);
                            break;
                    }
                    break;
                }

                case EndlessObstacleType.MovingTwinTunnels:
                {
                    float shift = obs.CurrentOpeningCenterX;
                    if (IsSplit)
                    {
                        bool leftInTunnel = leftPieceX >= (-2.35f + shift) && leftPieceX <= (-0.42f + shift);
                        bool rightInTunnel = rightPieceX >= (0.42f + shift) && rightPieceX <= (2.35f + shift);

                        if (leftInTunnel && rightInTunnel)
                        {
                            RegisterCleanGatePass(obs, 40, "✓ İKİZ TÜNELDEN BÖLÜNEREK GEÇTİN! (+40 Puan)");
                        }
                        else if (leftInTunnel || rightInTunnel)
                        {
                            float survivingX = leftInTunnel ? leftPieceX : rightPieceX;
                            SacrificeOneSplitPieceAndContinue(obs, survivingX,
                                "⚠ BİR PARÇA DİREĞE ÇARPTI! Tünelden geçen parçayla devam (-%50 Kütle)!");
                        }
                        else
                        {
                            bool leftInRight = leftPieceX >= (0.42f + shift) && leftPieceX <= (2.35f + shift);
                            bool rightInLeft = rightPieceX >= (-2.35f + shift) && rightPieceX <= (-0.42f + shift);
                            if (leftInRight || rightInLeft)
                            {
                                float survivingX = leftInRight ? leftPieceX : rightPieceX;
                                SacrificeOneSplitPieceAndContinue(obs, survivingX,
                                    "⚠ SADECE TEK PARÇA TÜNELE GİRDİ! Kalan parçayla devam (-%50 Kütle)!");
                            }
                            else
                            {
                                TriggerSolidWallBounce(obs, "💥 DİREĞE ÇARPTIN (-22 Kütle)! Tünellere hizalan!");
                            }
                        }
                    }
                    else
                    {
                        float relX = CurrentLateralOffset - shift;
                        if (Mathf.Abs(relX) >= 0.72f && Mathf.Abs(relX) <= 2.25f && CurrentMass <= 95f)
                        {
                            RegisterCleanGatePass(obs, 30, "✓ YAN TÜNELDEN TEK PARÇA GEÇTİN! (+30 Puan)");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 ORTADAKİ DİREĞE ÇARPTIN (-22 Kütle)! Bölün (÷2) veya yan tünele gir!");
                        }
                    }
                    break;
                }

                case EndlessObstacleType.SlidingDoorWall:
                {
                    float doorX = obs.CurrentOpeningCenterX;
                    float halfGap = 1.05f;
                    if (IsSplit)
                    {
                        bool leftOk = Mathf.Abs(leftPieceX - doorX) <= halfGap;
                        bool rightOk = Mathf.Abs(rightPieceX - doorX) <= halfGap;
                        if (leftOk && rightOk)
                        {
                            RegisterCleanGatePass(obs, 40, "✓ KAYAR KAPIDAN GEÇTİN! (+40 Puan)");
                        }
                        else if (leftOk || rightOk)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, leftOk ? leftPieceX : rightPieceX,
                                "⚠ KAYAR KAPI BİR PARÇAYI ÇARPTI! Açıklıktan geçenle devam!");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 KAYAR DUVARA ÇARPTIN (-22 Kütle)! Açıklığı takip et!");
                        }
                        return;
                    }

                    if (Mathf.Abs(CurrentLateralOffset - doorX) <= halfGap)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ KAYAR KAPIDAN TAM ZAMANINDA GEÇTİN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 KAYAR DUVARA ÇARPTIN (-22 Kütle)! Açıklığın kaydığı yöne geç!");
                    }
                    break;
                }

                case EndlessObstacleType.PincerShutters:
                {
                    float gapHalf = obs.CurrentPincerGapHalfWidth;
                    if (IsSplit)
                    {
                        bool leftOk = Mathf.Abs(leftPieceX) <= gapHalf + 0.18f;
                        bool rightOk = Mathf.Abs(rightPieceX) <= gapHalf + 0.18f;
                        if (leftOk && rightOk)
                        {
                            RegisterCleanGatePass(obs, 40, "✓ MAKAS KAPIDAN GEÇTİN! (+40 Puan)");
                        }
                        else if (leftOk || rightOk)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, leftOk ? leftPieceX : rightPieceX,
                                "⚠ MAKAS KAPI BİR PARÇAYI KISTIRDI! Kalan parçayla devam!");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 KAPALI MAKAS DUVARA ÇARPTIN (-22 Kütle)! Açılmasını bekle!");
                        }
                        return;
                    }

                    float jellyRad = ComputeVisualDiameter(CurrentMass) * 0.40f;
                    if (Mathf.Abs(CurrentLateralOffset) + jellyRad <= gapHalf + 0.28f)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ AÇILIR MAKAS KAPIDAN GEÇTİN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 MAKAS KAPI KAPALIYKEN ÇARPTIN (-22 Kütle)! Açıldığı an geç!");
                    }
                    break;
                }

                case EndlessObstacleType.SwingingPendulum:
                {
                    float headX = obs.CurrentHazardCenterX;
                    float hitRadius = 0.85f;
                    if (IsSplit)
                    {
                        bool leftHit = Mathf.Abs(leftPieceX - headX) < hitRadius;
                        bool rightHit = Mathf.Abs(rightPieceX - headX) < hitRadius;
                        if (!leftHit && !rightHit)
                        {
                            RegisterCleanGatePass(obs, 40, "✓ SARKAÇTAN BÖLÜNEREK SIYRILDIN! (+40 Puan)");
                        }
                        else if (!leftHit || !rightHit)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, !leftHit ? leftPieceX : rightPieceX,
                                "⚠ SARKAÇ BİR PARÇAYA ÇARPTI! Sağlam parçayla devam!");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 SARKACA ÇARPTIN (-22 Kütle)!");
                        }
                        return;
                    }

                    if (Mathf.Abs(CurrentLateralOffset - headX) >= hitRadius)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ SARKAÇTAN ZAMANLAMAYLA KAÇTIN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 SARKACA ÇARPTIN (-22 Kütle)! Ters yöne kaç veya bölün!");
                    }
                    break;
                }

                case EndlessObstacleType.SpinnerBar:
                {
                    // Dönen Engel (Spinner Bar): Center pedestal (|X| < 0.48) or rotating arm sweep when sin(angle) is broadside
                    float rad = obs.CurrentSpinnerAngleDeg * Mathf.Deg2Rad;
                    float armHalfSpan = Mathf.Abs(Mathf.Cos(rad)) * 2.25f;
                    float armDepthBlocking = Mathf.Abs(Mathf.Sin(rad));

                    if (IsSplit)
                    {
                        bool hitBar = armDepthBlocking < 0.42f && (Mathf.Abs(leftPieceX) <= armHalfSpan || Mathf.Abs(rightPieceX) <= armHalfSpan);
                        if (!hitBar)
                        {
                            RegisterCleanGatePass(obs, 45, "✓ DÖNEN ENGELDEN BÖLÜNEREK GEÇTİN! (+45 Puan)");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 DÖNEN KOLA ÇARPTIN (-22 Kütle)! Kol paralelken geç!");
                        }
                        return;
                    }

                    bool hitCenterHub = Mathf.Abs(CurrentLateralOffset) < 0.48f;
                    bool hitSweepingArm = armDepthBlocking < 0.42f && Mathf.Abs(CurrentLateralOffset) <= armHalfSpan;
                    if (!hitCenterHub && !hitSweepingArm)
                    {
                        RegisterCleanGatePass(obs, 45, "✓ DÖNEN ENGELDEN ZAMANLAMAYLA GEÇTİN! (+45 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 DÖNEN ENGELE ÇARPTIN (-22 Kütle)! Yanlardan veya kol dönerken geç!");
                    }
                    break;
                }

                case EndlessObstacleType.LeftOpeningWall:
                {
                    if (IsSplit)
                    {
                        bool leftPassed = leftPieceX <= -0.52f;
                        bool rightPassed = rightPieceX <= -0.52f;
                        if (leftPassed && rightPassed)
                        {
                            RegisterCleanGatePass(obs, 30, "✓ İKİ PARÇA DA SOL KORİDORDAN GEÇTİ! (+30 Puan)");
                        }
                        else if (leftPassed)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, leftPieceX,
                                "⚠ SAĞ PARÇA DUVARA ÇARPTI! Sol koridordan geçenle devam (-%50 Kütle)!");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 SAĞ DUVAR KAPALI (-22 Kütle)! Sol koridora kay!");
                        }
                        return;
                    }

                    if (CurrentLateralOffset <= -0.52f)
                    {
                        RegisterCleanGatePass(obs, 30, "✓ SOL KORİDORDAN GEÇTİN! (+30 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 SAĞ DUVAR KAPALI (-22 Kütle)! Sol koridora yönel!");
                    }
                    break;
                }

                case EndlessObstacleType.RightOpeningWall:
                {
                    if (IsSplit)
                    {
                        bool leftPassed = leftPieceX >= 0.52f;
                        bool rightPassed = rightPieceX >= 0.52f;
                        if (leftPassed && rightPassed)
                        {
                            RegisterCleanGatePass(obs, 30, "✓ İKİ PARÇA DA SAĞ KORİDORDAN GEÇTİ! (+30 Puan)");
                        }
                        else if (rightPassed)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, rightPieceX,
                                "⚠ SOL PARÇA DUVARA ÇARPTI! Sağ koridordan geçenle devam (-%50 Kütle)!");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 SOL DUVAR KAPALI (-22 Kütle)! Sağ koridora kay!");
                        }
                        return;
                    }

                    if (CurrentLateralOffset >= 0.52f)
                    {
                        RegisterCleanGatePass(obs, 30, "✓ SAĞ KORİDORDAN GEÇTİN! (+30 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 SOL DUVAR KAPALI (-22 Kütle)! Sağ koridora yönel!");
                    }
                    break;
                }
            }
        }

        private void RegisterCleanGatePass(EndlessGateObstacle obs, int basePoints, string bannerMsg)
        {
            obs.resolved = true;
            ComboCount++;
            AddScoreWithCombo(basePoints);
            _wobbleAmplitude = 0.24f;
            _wobblePhase = 0f;

            // Squish & Pop squeeze animation on narrow gates & flexible squeeze corridors + FOV pop!
            if (obs.obstacleType == EndlessObstacleType.SmallCenterHole ||
                obs.obstacleType == EndlessObstacleType.FlexibleSqueezeGate ||
                obs.obstacleType == EndlessObstacleType.VerticalCrusherGate ||
                obs.obstacleType == EndlessObstacleType.SlidingHoleWall)
            {
                TriggerDeformerSqueeze(0.42f, 0.30f);
                _cameraFovPunch = Mathf.Max(_cameraFovPunch, 3.8f);
                _audio?.PlayGateSqueeze(SoundEnabled, VibrationEnabled, ComboCount);
            }
            else if (obs.obstacleType == EndlessObstacleType.HeavySmashWall)
            {
                _cameraFovPunch = Mathf.Max(_cameraFovPunch, 5.2f);
                _cameraShakeTimer = 0.22f;
                _cameraShakeAmp = 0.16f;
                _audio?.PlayCrystalSmash(SoundEnabled, VibrationEnabled);
                SpawnCrystalShatterBurst(obs.transform.position + Vector3.up * 1.1f);
            }
            else
            {
                _audio?.PlayGateSqueeze(SoundEnabled, VibrationEnabled, ComboCount);
            }

            if (primaryPlayer != null)
                SpawnJellySplash(primaryPlayer.transform.position, new Color(0.25f, 0.98f, 0.65f), 8);
            int mult = Mathf.Clamp(1 + ComboCount / 2, 1, 5);
            SetBanner(mult > 1 ? $"{bannerMsg} [🔥 x{mult} KOMBO!]" : bannerMsg, 2.2f);
        }

        private void TriggerDeformerSqueeze(float intensity, float duration)
        {
            if (primaryPlayer != null)
            {
                var def = primaryPlayer.GetComponentInChildren<JellyRealtimeDeformer>();
                if (def != null) def.TriggerGateSqueeze(intensity, duration);
            }
            if (_secondarySplitVisual != null && _secondarySplitVisual.activeSelf)
            {
                var def2 = _secondarySplitVisual.GetComponentInChildren<JellyRealtimeDeformer>();
                if (def2 != null) def2.TriggerGateSqueeze(intensity, duration);
            }
        }

        private void TriggerDeformerWallImpact()
        {
            if (primaryPlayer != null)
            {
                var def = primaryPlayer.GetComponentInChildren<JellyRealtimeDeformer>();
                if (def != null) def.TriggerWallImpact();
            }
            if (_secondarySplitVisual != null && _secondarySplitVisual.activeSelf)
            {
                var def2 = _secondarySplitVisual.GetComponentInChildren<JellyRealtimeDeformer>();
                if (def2 != null) def2.TriggerWallImpact();
            }
        }

        private void SacrificeOneSplitPieceAndContinue(EndlessGateObstacle obs, float survivingLateralX, string message)
        {
            if (HasShield)
            {
                HasShield = false;
                obs.resolved = true;
                _wobbleAmplitude = 0.24f;
                _wobblePhase = 0f;
                if (primaryPlayer != null)
                    SpawnJellySplash(primaryPlayer.transform.position, new Color(0.88f, 0.38f, 1.00f), 10);
                SetBanner("🛡 KALKAN PARÇAYI KORUDU! Hasar almadan geçtin!", 2.4f);
                return;
            }

            obs.resolved = true;
            ComboCount = 0;
            IsSplit = false;
            CurrentLateralOffset = Mathf.Clamp(survivingLateralX, -maxLateralOffset, maxLateralOffset);
            _currentSplitBlend = 0f;
            CurrentMass *= 0.5f;
            _displayedMass = CurrentMass;
            _wobbleAmplitude = 0.32f;
            _wobblePhase = 0f;

            if (primaryPlayer != null)
                SpawnJellySplash(primaryPlayer.transform.position, new Color(0.32f, 0.88f, 1.00f), 12);

            if (CurrentMass < 22f)
            {
                TriggerGameOver("💥 JÖLE ÇOK KÜÇÜLDÜ VE ERİDİ! (Kütle Tükendi)");
                return;
            }
            SetBanner(message, 2.8f);
        }

        private void TriggerSolidWallBounce(EndlessGateObstacle obs, string reason)
        {
            if (HasShield)
            {
                HasShield = false;
                obs.resolved = true;
                _wobbleAmplitude = 0.26f;
                _wobblePhase = 0f;
                if (primaryPlayer != null)
                    SpawnJellySplash(primaryPlayer.transform.position, new Color(0.88f, 0.38f, 1.00f), 12);
                SetBanner("🛡 KALKAN DARBEYİ ENGELLEDİ! Kütle kaybetmeden geçtin!", 2.5f);
                return;
            }

            ComboCount = 0;
            obs.hasBouncedOnce = true;
            _wobbleAmplitude = 0.42f;
            _wobblePhase = 0f;
            _cameraShakeTimer = 0.24f;
            _cameraShakeAmp = 0.18f;
            _audio?.PlayWallThud(SoundEnabled, VibrationEnabled);
            TriggerDeformerWallImpact();
            CurrentMass -= 22f;

            if (primaryPlayer != null)
                SpawnJellySplash(primaryPlayer.transform.position, new Color(0.78f, 0.28f, 0.98f), 11);

            if (CurrentMass < 22f)
            {
                TriggerGameOver("💥 DUVARA ÇARPA ÇARPA JÖLE TÜKENDİ!");
                return;
            }

            _bounceCooldown = 0.46f;
            SetBanner(reason, 2.8f);
        }

        private void TriggerGameOver(string reason)
        {
            IsGameOver = true;
            CurrentMass = 0f;
            _displayedMass = 10f;
            SetBanner(reason, 999f);
        }

        private void SetBanner(string msg, float duration)
        {
            StatusBanner = msg;
            StatusBannerTimer = duration;
        }

        // =====================================================================
        // MODULAR MAP DEVELOPMENT ENGINE: Builds all 8 Track Segment Architectures
        // =====================================================================
        private void BuildEntireLevelMap(JellyLevelBlueprint bp)
        {
            BiomePalette biome = ApplyVisualConceptToBiome(Biomes[bp.BiomeIndex % Biomes.Length], VisualConceptIndex);

            // Pre-start runway (-12m .. 0m)
            _spawnedChunks.Add(BuildSmoothRoadChunkMesh(-12f, 0f, 16, biome, 0f));

            int finaleChunkIndex = 0;
            for (int i = 0; i < bp.Chunks.Count; i++)
            {
                float zStart = i * sliceLength;
                float zEnd = zStart + sliceLength;
                TrackChunkSpec spec = bp.Chunks[i];

                switch (spec.ModuleType)
                {
                    case TrackSegmentModuleType.SplitTwinBridge:
                        BuildSplitTwinBridgeModule(zStart, zEnd, biome);
                        break;

                    case TrackSegmentModuleType.JumpRampOverAbyss:
                        BuildJumpRampOverAbyssModule(zStart, zEnd, biome);
                        break;

                    case TrackSegmentModuleType.DoubleDeckerOverpass:
                        BuildDoubleDeckerOverpassModule(zStart, zEnd, biome, spec.GateType);
                        break;

                    case TrackSegmentModuleType.LaserSlicerGate:
                        BuildLaserSlicerModule(zStart, zEnd, biome, spec.LaserKind);
                        break;

                    case TrackSegmentModuleType.SurfaceHazardZone:
                        BuildSurfaceHazardModule(zStart, zEnd, biome, spec.HazardKind, spec.HasGateObstacle, spec.GateType);
                        break;

                    case TrackSegmentModuleType.ColorMatchSection:
                        BuildColorMatchModule(zStart, zEnd, biome, spec.ColorIndex);
                        break;

                    case TrackSegmentModuleType.LevelFinaleMultiplier:
                        BuildLevelFinaleModule(zStart, zEnd, biome, finaleChunkIndex);
                        finaleChunkIndex++;
                        break;

                    default: // StandardRoad
                        var stdChunk = BuildSmoothRoadChunkMesh(zStart, zEnd, 20, biome, 0f);
                        _spawnedChunks.Add(stdChunk);
                        if (i >= 1)
                            SpawnChunkSceneryAndClouds(stdChunk.transform, zStart + sliceLength * 0.5f, i, biome);
                        if (spec.HasGateObstacle)
                            SpawnObstacleAndPreparationPickups(zStart + 8.8f, spec.GateType, spec.MultiHoleVariant, spec.HasBonusPickup, spec.BonusPickup);
                        break;
                }
            }
        }

        private void BuildSplitTwinBridgeModule(float zStart, float zEnd, BiomePalette biome)
        {
            var root = new GameObject($"Module_SplitTwinBridge_{zStart:F0}");
            root.transform.SetParent(trackRoot, false);
            _spawnedChunks.Add(root);

            var node = root.AddComponent<JellyTrackSegmentNode>();
            node.moduleType = TrackSegmentModuleType.SplitTwinBridge;
            node.zStart = zStart;
            node.zEnd = zEnd;
            node.abyssZStart = zStart + 2.0f;
            node.abyssZEnd = zEnd - 1.0f;
            node.centerGapHalfWidth = 0.68f;
            _activeSegmentNodes.Add(node);

            // Approach Lead-in (zStart .. zStart + 2m) + Split (÷2) crystals
            CreateSubRibbonObject(root.transform, "ApproachDeck", zStart, zStart + 2f, 4, -3.2f, +3.2f, -0.4f, 0f, 0f, biome.RoadSurfaceColor);
            SpawnPickupOnRoad(zStart - 3.5f, -1.2f, EndlessPickupType.Split, 0f);
            SpawnPickupOnRoad(zStart - 3.5f, 0f, EndlessPickupType.Split, 0f);
            SpawnPickupOnRoad(zStart - 3.5f, +1.2f, EndlessPickupType.Split, 0f);

            // LEFT BRIDGE (-2.85m .. -0.72m) & RIGHT BRIDGE (+0.72m .. +2.85m) with open abyss chasm in the center!
            Color bridgeCol = Color.Lerp(biome.RoadSurfaceColor, biome.StripeColor, 0.25f);
            CreateSubRibbonObject(root.transform, "LeftTwinBridge", zStart + 2f, zEnd - 1f, 16, -2.85f, -0.72f, -0.35f, 0f, 0f, bridgeCol);
            CreateSubRibbonObject(root.transform, "LeftBridgeOuterRail", zStart + 2f, zEnd - 1f, 16, -3.10f, -2.85f, -0.1f, 0.28f, 0f, biome.CurbColor);
            CreateSubRibbonObject(root.transform, "LeftBridgeInnerRail", zStart + 2f, zEnd - 1f, 16, -0.72f, -0.58f, -0.05f, 0.16f, 0f, new Color(0.75f, 0.32f, 0.98f));

            CreateSubRibbonObject(root.transform, "RightTwinBridge", zStart + 2f, zEnd - 1f, 16, +0.72f, +2.85f, -0.35f, 0f, 0f, bridgeCol);
            CreateSubRibbonObject(root.transform, "RightBridgeOuterRail", zStart + 2f, zEnd - 1f, 16, +2.85f, +3.10f, -0.1f, 0.28f, 0f, biome.CurbColor);
            CreateSubRibbonObject(root.transform, "RightBridgeInnerRail", zStart + 2f, zEnd - 1f, 16, +0.58f, +0.72f, -0.05f, 0.16f, 0f, new Color(0.75f, 0.32f, 0.98f));

            // Exit reconnect deck
            CreateSubRibbonObject(root.transform, "ExitDeck", zEnd - 1f, zEnd, 4, -3.2f, +3.2f, -0.4f, 0f, 0f, biome.RoadSurfaceColor);

            // Reward Grow (+) orbs on BOTH left and right bridges simultaneously!
            SpawnPickupOnRoad(zStart + 6.5f, -1.55f, EndlessPickupType.Grow, 25f);
            SpawnPickupOnRoad(zStart + 6.5f, +1.55f, EndlessPickupType.Grow, 25f);
            SpawnPickupOnRoad(zEnd + 2.5f, 0f, EndlessPickupType.Merge, 0f);
        }

        private void BuildJumpRampOverAbyssModule(float zStart, float zEnd, BiomePalette biome)
        {
            var root = new GameObject($"Module_JumpRampAbyss_{zStart:F0}");
            root.transform.SetParent(trackRoot, false);
            _spawnedChunks.Add(root);

            var node = root.AddComponent<JellyTrackSegmentNode>();
            node.moduleType = TrackSegmentModuleType.JumpRampOverAbyss;
            node.zStart = zStart;
            node.zEnd = zEnd;
            node.rampTriggerZ = zStart + 3.2f;
            node.launchUpVelocity = 11.2f;
            _activeSegmentNodes.Add(node);

            // Road up to the chasm (zStart .. zStart + 3.5m)
            CreateSubRibbonObject(root.transform, "PreRampDeck", zStart, zStart + 3.5f, 6, -3.2f, +3.2f, -0.45f, 0f, 0f, biome.RoadSurfaceColor);

            // 3D Inclined Launch Ramp Wedge at zStart + 2.2m
            EvaluateRoadCurve(zStart + 2.2f, out Vector3 rampPos, out Quaternion rampRot, out _);
            var rampGo = CreateWallBlock(root.transform, "LaunchRampWedge", Vector3.zero, new Vector3(4.6f, 0.45f, 2.6f), null, new Color(1.00f, 0.78f, 0.16f));
            rampGo.transform.position = rampPos + (rampRot * Vector3.up) * 0.32f;
            rampGo.transform.rotation = rampRot * Quaternion.Euler(-16f, 0f, 0f);

            // Floating Golden Aerial Ring above the chasm at zStart + 6.5m!
            EvaluateRoadCurve(zStart + 6.5f, out Vector3 ringCenter, out Quaternion ringRot, out _);
            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "AerialBonusRing";
            DestroyCollider(ringGo);
            ringGo.transform.SetParent(root.transform, false);
            ringGo.transform.position = ringCenter + Vector3.up * 2.35f;
            ringGo.transform.rotation = ringRot * Quaternion.Euler(90f, 0f, 0f);
            ringGo.transform.localScale = new Vector3(2.6f, 0.14f, 2.6f);
            LevelLoader.ApplyMaterialOrColor(ringGo, null, new Color(0.20f, 0.94f, 1.00f));

            // Landing road deck after the 4.5m open abyss chasm (zStart + 8.0m .. zEnd)
            CreateSubRibbonObject(root.transform, "LandingDeck", zStart + 8.0f, zEnd, 8, -3.2f, +3.2f, -0.45f, 0f, 0f, biome.RoadSurfaceColor);
            CreateSubRibbonObject(root.transform, "LandingCurbL", zStart + 8.0f, zEnd, 8, -3.6f, -3.2f, -0.1f, 0.32f, 0f, biome.CurbColor);
            CreateSubRibbonObject(root.transform, "LandingCurbR", zStart + 8.0f, zEnd, 8, +3.2f, +3.6f, -0.1f, 0.32f, 0f, biome.CurbColor);
        }

        private void BuildDoubleDeckerOverpassModule(float zStart, float zEnd, BiomePalette biome, EndlessObstacleType lowerGateType)
        {
            var root = new GameObject($"Module_DoubleDecker_{zStart:F0}");
            root.transform.SetParent(trackRoot, false);
            _spawnedChunks.Add(root);

            var node = root.AddComponent<JellyTrackSegmentNode>();
            node.moduleType = TrackSegmentModuleType.DoubleDeckerOverpass;
            node.zStart = zStart;
            node.zEnd = zEnd;
            node.rampTriggerZ = zStart + 1.5f;
            node.launchUpVelocity = 12.2f;
            node.upperDeckHeight = 2.4f;
            node.glassFloorZStart = zStart + 5.0f;
            node.glassFloorZEnd = zStart + 9.2f;
            _activeSegmentNodes.Add(node);

            // 1. Full Lower Deck (Y = 0)
            var lowerChunk = BuildSmoothRoadChunkMesh(zStart, zEnd, 20, biome, 0f);
            lowerChunk.transform.SetParent(root.transform, false);

            // Launch Ramp at entrance (zStart + 0.8m) to reach the 2nd-floor Overpass!
            EvaluateRoadCurve(zStart + 0.8f, out Vector3 rPos, out Quaternion rRot, out _);
            var upRamp = CreateWallBlock(root.transform, "OverpassEntryRamp", Vector3.zero, new Vector3(3.4f, 0.45f, 2.8f), null, new Color(0.22f, 0.92f, 0.62f));
            upRamp.transform.position = rPos + (rRot * Vector3.up) * 0.42f;
            upRamp.transform.rotation = rRot * Quaternion.Euler(-20f, 0f, 0f);

            // 2. Elevated 2nd-Floor Overpass Bridge (Y = +2.4m) with Breakable Glass Middle Section!
            CreateSubRibbonObject(root.transform, "UpperDeckEntry", zStart + 2.4f, node.glassFloorZStart, 6, -2.5f, +2.5f, -0.22f, 0f, node.upperDeckHeight, biome.StripeColor);
            var glassGo = CreateSubRibbonObject(root.transform, "UpperDeckBreakableGlass", node.glassFloorZStart, node.glassFloorZEnd, 8, -2.5f, +2.5f, -0.16f, 0f, node.upperDeckHeight, new Color(0.45f, 0.95f, 1.00f, 0.85f));
            node.breakableGlassFloor = glassGo.transform;
            CreateSubRibbonObject(root.transform, "UpperDeckExit", node.glassFloorZEnd, zEnd - 0.8f, 6, -2.5f, +2.5f, -0.22f, 0f, node.upperDeckHeight, biome.StripeColor);

            // Gate on the lower deck so falling through the glass or staying low has an obstacle challenge!
            BuildCutoutGate(zStart + 9.5f, lowerGateType);
        }

        private void BuildLaserSlicerModule(float zStart, float zEnd, BiomePalette biome, LaserGateKind laserKind)
        {
            var chunk = BuildSmoothRoadChunkMesh(zStart, zEnd, 20, biome, 0f);
            _spawnedChunks.Add(chunk);

            var node = chunk.AddComponent<JellyTrackSegmentNode>();
            node.moduleType = TrackSegmentModuleType.LaserSlicerGate;
            node.zStart = zStart;
            node.zEnd = zEnd;
            node.laserKind = laserKind;
            node.laserTriggerZ = zStart + 6.0f;
            _activeSegmentNodes.Add(node);

            EvaluateRoadCurve(node.laserTriggerZ, out Vector3 center, out Quaternion rot, out _);
            var gateRoot = new GameObject($"LaserGate_{laserKind}").transform;
            gateRoot.SetParent(chunk.transform, false);
            gateRoot.SetPositionAndRotation(center, rot);

            Color frameCol = new Color(0.18f, 0.22f, 0.34f);
            CreateWallBlock(gateRoot, "LaserPostL", new Vector3(-2.95f, 1.3f, 0f), new Vector3(0.45f, 2.6f, 0.45f), null, frameCol);
            CreateWallBlock(gateRoot, "LaserPostR", new Vector3(+2.95f, 1.3f, 0f), new Vector3(0.45f, 2.6f, 0.45f), null, frameCol);
            CreateWallBlock(gateRoot, "LaserTopArch", new Vector3(0f, 2.55f, 0f), new Vector3(6.3f, 0.32f, 0.5f), null, frameCol);

            if (laserKind == LaserGateKind.BlueVerticalAutoSplitter)
            {
                // Glowing Vertical Blue Laser Blade in the center
                CreateWallBlock(gateRoot, "BlueVerticalLaserBlade", new Vector3(0f, 1.25f, 0f), new Vector3(0.16f, 2.4f, 0.25f), null, new Color(0.18f, 0.92f, 1.00f));
                // Put TwinSplitTunnels right after so the sliced jelly glides straight through both tunnels!
                BuildCutoutGate(zStart + 10.5f, EndlessObstacleType.TwinSplitTunnels);
            }
            else
            {
                // Glowing Horizontal Red Trimmer Laser at Y = 1.05m
                CreateWallBlock(gateRoot, "RedHorizontalTrimmerBeam", new Vector3(0f, 1.08f, 0f), new Vector3(5.6f, 0.16f, 0.25f), null, new Color(1.00f, 0.25f, 0.35f));
                // Put SmallCenterHole right after so the trimmed jelly fits right through!
                BuildCutoutGate(zStart + 10.5f, EndlessObstacleType.SmallCenterHole);
            }
        }

        private void BuildSurfaceHazardModule(float zStart, float zEnd, BiomePalette biome, SurfaceHazardKind kind, bool hasGate, EndlessObstacleType gateType)
        {
            Color surfaceTint = biome.RoadSurfaceColor;
            if (kind == SurfaceHazardKind.IceDrift)
                surfaceTint = new Color(0.62f, 0.92f, 1.00f);
            else if (kind == SurfaceHazardKind.ConveyorPushLeft || kind == SurfaceHazardKind.ConveyorPushRight)
                surfaceTint = new Color(0.96f, 0.82f, 0.42f);

            var chunk = BuildSmoothRoadChunkMesh(zStart, zEnd, 20, biome, 0f, surfaceTint);
            _spawnedChunks.Add(chunk);

            var node = chunk.AddComponent<JellyTrackSegmentNode>();
            node.moduleType = TrackSegmentModuleType.SurfaceHazardZone;
            node.zStart = zStart;
            node.zEnd = zEnd;
            node.hazardKind = kind;
            node.conveyorPushForce = 2.8f;
            _activeSegmentNodes.Add(node);

            if (kind == SurfaceHazardKind.SlimeMassDrain)
            {
                // Toxic purple/green slime strips along outer lanes
                CreateSubRibbonObject(chunk.transform, "SlimeStripLeft", zStart + 1f, zEnd - 1f, 12, -2.9f, -1.25f, 0.0f, 0.05f, 0f, new Color(0.65f, 0.96f, 0.18f));
                CreateSubRibbonObject(chunk.transform, "SlimeStripRight", zStart + 1f, zEnd - 1f, 12, +1.25f, +2.9f, 0.0f, 0.05f, 0f, new Color(0.65f, 0.96f, 0.18f));
            }
            else if (kind == SurfaceHazardKind.MagneticField)
            {
                EvaluateRoadCurve(zStart + 5.5f, out Vector3 mPos, out Quaternion mRot, out _);
                var blenderMag = BlenderKitModelSpawner.InstantiateModelGroup("Hazard_MagneticField", chunk.transform, alignBottomToZero: true, uniformScale: 1.0f);
                if (blenderMag != null)
                {
                    blenderMag.transform.SetPositionAndRotation(mPos, mRot);
                }
                else
                {
                    var magGo = new GameObject("Hazard_MagneticField").transform;
                    magGo.SetParent(chunk.transform, false);
                    magGo.SetPositionAndRotation(mPos, mRot);
                    CreateWallBlock(magGo, "MagCoilL", new Vector3(-2.65f, 1.0f, 0f), new Vector3(0.65f, 1.9f, 0.65f), null, new Color(0.95f, 0.30f, 0.90f));
                    CreateWallBlock(magGo, "MagCoilR", new Vector3(+2.65f, 1.0f, 0f), new Vector3(0.65f, 1.9f, 0.65f), null, new Color(0.20f, 0.92f, 1.00f));
                }
            }
            else if (kind == SurfaceHazardKind.StickyFloor)
            {
                EvaluateRoadCurve(zStart + 5.5f, out Vector3 sPos, out Quaternion sRot, out _);
                var blenderSticky = BlenderKitModelSpawner.InstantiateModelGroup("Hazard_StickyFloor", chunk.transform, alignBottomToZero: true, uniformScale: 1.0f);
                if (blenderSticky != null)
                {
                    blenderSticky.transform.SetPositionAndRotation(sPos, sRot);
                }
                else
                {
                    CreateSubRibbonObject(chunk.transform, "StickyHoneyPad", zStart + 1.5f, zEnd - 1.5f, 10, -2.4f, +2.4f, 0.0f, 0.07f, 0f, new Color(1.00f, 0.72f, 0.14f));
                }
            }
            else if (kind == SurfaceHazardKind.JumpPad)
            {
                EvaluateRoadCurve(zStart + 5.5f, out Vector3 jPos, out Quaternion jRot, out _);
                var blenderJump = BlenderKitModelSpawner.InstantiateModelGroup("Hazard_JumpPad", chunk.transform, alignBottomToZero: true, uniformScale: 1.0f);
                if (blenderJump != null)
                {
                    blenderJump.transform.SetPositionAndRotation(jPos, jRot);
                }
                else
                {
                    var padGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(padGo);
                    padGo.name = "Hazard_JumpPad";
                    padGo.transform.SetParent(chunk.transform, false);
                    padGo.transform.SetPositionAndRotation(jPos + Vector3.up * 0.18f, jRot);
                    padGo.transform.localScale = new Vector3(2.1f, 0.18f, 2.1f);
                    LevelLoader.ApplyMaterialOrColor(padGo, null, new Color(0.22f, 0.98f, 0.56f));
                }
            }

            if (hasGate)
            {
                SpawnObstacleAndPreparationPickups(zStart + 9.5f, gateType);
            }
        }

        private void BuildColorMatchModule(float zStart, float zEnd, BiomePalette biome, int requiredColorIdx)
        {
            var chunk = BuildSmoothRoadChunkMesh(zStart, zEnd, 20, biome, 0f);
            _spawnedChunks.Add(chunk);

            var node = chunk.AddComponent<JellyTrackSegmentNode>();
            node.moduleType = TrackSegmentModuleType.ColorMatchSection;
            node.zStart = zStart;
            node.zEnd = zEnd;
            node.requiredColorIndex = requiredColorIdx % 3;
            node.dyePadZ = zStart + 3.0f;
            node.colorGateZ = zStart + 9.5f;
            _activeSegmentNodes.Add(node);

            // 3 Dye Pools across the road at zStart + 3.0m: Left=Cyan(0), Center=Gold(1), Right=Magenta(2)
            CreateSubRibbonObject(chunk.transform, "DyePool_Cyan", zStart + 1.8f, zStart + 4.2f, 4, -2.9f, -0.95f, 0.0f, 0.06f, 0f, DyeColors[0]);
            CreateSubRibbonObject(chunk.transform, "DyePool_Gold", zStart + 1.8f, zStart + 4.2f, 4, -0.90f, +0.90f, 0.0f, 0.06f, 0f, DyeColors[1]);
            CreateSubRibbonObject(chunk.transform, "DyePool_Magenta", zStart + 1.8f, zStart + 4.2f, 4, +0.95f, +2.9f, 0.0f, 0.06f, 0f, DyeColors[2]);

            // Matching Colored Energy Shield Gate at zStart + 9.5m
            EvaluateRoadCurve(node.colorGateZ, out Vector3 gCenter, out Quaternion gRot, out _);
            var gateGo = new GameObject("ColorEnergyGate").transform;
            gateGo.SetParent(chunk.transform, false);
            gateGo.SetPositionAndRotation(gCenter, gRot);

            Color reqCol = DyeColors[node.requiredColorIndex];
            CreateWallBlock(gateGo, "PostL", new Vector3(-2.9f, 1.2f, 0f), new Vector3(0.5f, 2.4f, 0.5f), null, reqCol);
            CreateWallBlock(gateGo, "PostR", new Vector3(+2.9f, 1.2f, 0f), new Vector3(0.5f, 2.4f, 0.5f), null, reqCol);
            CreateWallBlock(gateGo, "TopBar", new Vector3(0f, 2.4f, 0f), new Vector3(6.3f, 0.4f, 0.55f), null, reqCol);
            CreateWallBlock(gateGo, "EnergyField", new Vector3(0f, 1.15f, 0f), new Vector3(5.3f, 2.1f, 0.15f), null, new Color(reqCol.r, reqCol.g, reqCol.b, 0.55f));
        }

        private void BuildLevelFinaleModule(float zStart, float zEnd, BiomePalette biome, int finaleChunkIdx)
        {
            Color goldFloor = (finaleChunkIdx % 2 == 0) ? new Color(0.98f, 0.94f, 0.78f) : new Color(0.94f, 0.88f, 0.65f);
            var chunk = BuildSmoothRoadChunkMesh(zStart, zEnd, 20, biome, 0f, goldFloor);
            _spawnedChunks.Add(chunk);

            int[] mults = finaleChunkIdx == 0 ? new int[] { 2, 3 } : new int[] { 5, 10 };
            float[] reqMasses = finaleChunkIdx == 0 ? new float[] { 45f, 70f } : new float[] { 92f, 115f };
            Color[] wallCols = finaleChunkIdx == 0
                ? new Color[] { new Color(0.22f, 0.88f, 0.98f), new Color(0.25f, 0.95f, 0.52f) }
                : new Color[] { new Color(0.98f, 0.68f, 0.18f), new Color(0.96f, 0.28f, 0.62f) };

            for (int k = 0; k < 2; k++)
            {
                float wz = zStart + 4.5f + k * 6.0f;
                bool isFinal = (k == 1);

                var nodeGo = new GameObject($"FinaleMultiplierWall_x{mults[k]}");
                nodeGo.transform.SetParent(chunk.transform, false);
                EvaluateRoadCurve(wz, out Vector3 wCenter, out Quaternion wRot, out _);
                nodeGo.transform.SetPositionAndRotation(wCenter, wRot);

                var node = nodeGo.AddComponent<JellyTrackSegmentNode>();
                node.moduleType = TrackSegmentModuleType.LevelFinaleMultiplier;
                node.zStart = wz - 2f;
                node.zEnd = wz + 2f;
                node.wallZ = wz;
                node.multiplierValue = mults[k];
                node.requiredMassToBreak = reqMasses[k];
                node.isFinalGoalLine = isFinal;

                CreateWallBlock(nodeGo.transform, "PillarL", new Vector3(-2.85f, 1.3f, 0f), new Vector3(0.6f, 2.6f, 0.55f), null, new Color(0.34f, 0.24f, 0.72f));
                CreateWallBlock(nodeGo.transform, "PillarR", new Vector3(+2.85f, 1.3f, 0f), new Vector3(0.6f, 2.6f, 0.55f), null, new Color(0.34f, 0.24f, 0.72f));
                var breakable = CreateWallBlock(nodeGo.transform, $"Wall_x{mults[k]}", new Vector3(0f, 1.15f, 0f), new Vector3(5.1f, 2.2f, 0.38f), null, wallCols[k]);
                node.breakableWallVisual = breakable.transform;
                _activeSegmentNodes.Add(node);
            }
        }

        private GameObject BuildSmoothRoadChunkMesh(float zStart, float zEnd, int steps, BiomePalette biome, float yOffset, Color? customRoadColor = null)
        {
            var chunkGo = new GameObject($"SmoothRoadChunk_{zStart:F0}");
            chunkGo.transform.SetParent(trackRoot, false);
            chunkGo.transform.position = Vector3.zero;
            chunkGo.transform.rotation = Quaternion.identity;

            float halfRoad = roadWidth * 0.5f;
            float curbWidth = 0.28f;
            float barrierHeight = 0.62f;
            float roadThickness = 0.45f;

            // Main ceramic tile track surface (3-lane warm-cream & mint tiles)
            CreateSubRibbonObject(chunkGo.transform, "RoadSurfaceMesh", zStart, zEnd, steps, -halfRoad, +halfRoad, -roadThickness, 0f, yOffset, customRoadColor ?? biome.RoadSurfaceColor);
            // Turquoise outer track trim skirt
            CreateSubRibbonObject(chunkGo.transform, "LeftSideSkirt", zStart, zEnd, steps, -halfRoad - 0.16f, -halfRoad, -roadThickness - 0.06f, 0.08f, yOffset, new Color(0.22f, 0.80f, 0.90f));
            CreateSubRibbonObject(chunkGo.transform, "RightSideSkirt", zStart, zEnd, steps, +halfRoad, +halfRoad + 0.16f, -roadThickness - 0.06f, 0.08f, yOffset, new Color(0.22f, 0.80f, 0.90f));

            // Crisp dashed teal center stripe
            int dashCount = 4;
            float segLen = (zEnd - zStart) / dashCount;
            for (int d = 0; d < dashCount; d++)
            {
                float dStart = zStart + d * segLen + segLen * 0.18f;
                float dEnd = zStart + (d + 1) * segLen - segLen * 0.18f;
                CreateSubRibbonObject(chunkGo.transform, "CenterStripeMesh", dStart, dEnd, Mathf.Max(2, steps / dashCount), -0.065f, +0.065f, 0.0f, 0.016f, yOffset, biome.StripeColor);
            }

            // Chunky Cylindrical Waterpark Bumper Tubes (Alternating Coral-Orange & Aqua every 6m, matching Alternative 1!)
            int railSegs = 2;
            float rLen = (zEnd - zStart) / railSegs;
            for (int r = 0; r < railSegs; r++)
            {
                float rz0 = zStart + r * rLen + 0.05f;
                float rz1 = zStart + (r + 1) * rLen - 0.05f;
                int subSteps = Mathf.Max(4, steps / railSegs);
                bool leftCoral = ((Mathf.FloorToInt(rz0 / 6f) + 0) % 2) == 0;
                Color leftCol = leftCoral ? biome.PropColor : biome.CurbColor;
                Color rightCol = leftCoral ? biome.CurbColor : biome.PropColor;
                CreateExtrudedTubeObject(chunkGo.transform, "LeftBumperTube", rz0, rz1, subSteps, -halfRoad - 0.14f, yOffset + 0.28f, 0.28f, leftCol);
                CreateExtrudedTubeObject(chunkGo.transform, "RightBumperTube", rz0, rz1, subSteps, +halfRoad + 0.14f, yOffset + 0.28f, 0.28f, rightCol);
            }

            // Stacked Double Forward Chevron Pairs (>>) left & right of centerline
            float zMid = (zStart + zEnd) * 0.5f;
            Color chevCol = new Color(0.16f, 0.88f, 0.96f);
            for (int pair = 0; pair < 2; pair++)
            {
                EvaluateRoadCurve(zMid - 0.26f + pair * 0.52f, out Vector3 cPos, out Quaternion cRot, out Vector3 cRight);
                Vector3 cUp = cRot * Vector3.up;
                for (int lane = -1; lane <= 1; lane += 2)
                {
                    Vector3 laneCenter = cPos + cRight * (lane * 1.38f) + cUp * (yOffset + 0.018f);
                    var chevL = CreateWallBlock(chunkGo.transform, "RoadChevronL", Vector3.zero, new Vector3(0.46f, 0.015f, 0.11f), null, chevCol);
                    chevL.transform.position = laneCenter - cRight * 0.15f;
                    chevL.transform.rotation = cRot * Quaternion.Euler(0f, -34f, 0f);
                    var chevR = CreateWallBlock(chunkGo.transform, "RoadChevronR", Vector3.zero, new Vector3(0.46f, 0.015f, 0.11f), null, chevCol);
                    chevR.transform.position = laneCenter + cRight * 0.15f;
                    chevR.transform.rotation = cRot * Quaternion.Euler(0f, 34f, 0f);
                }
            }

            return chunkGo;
        }

        // =====================================================================
        // 3D PARALLAX BACKGROUND & FLOATING SKY ISLANDS SYSTEM (Reference Section 4)
        // =====================================================================
        private void BuildParallax3DBackground(BiomePalette biome, float totalTrackLen)
        {
            _floatingIslands.Clear();

            var farGo = new GameObject("ParallaxFarLayer");
            farGo.transform.SetParent(trackRoot, false);
            _parallaxFarRoot = farGo.transform;

            var midGo = new GameObject("ParallaxMidLayer");
            midGo.transform.SetParent(trackRoot, false);
            _parallaxMidRoot = midGo.transform;

            // 1. Far Parallax Layer: Stacked Translucent Pastel Jelly Towers on the Horizon & Big Cartoon Clouds
            Color[] jellyTowerCols = new[]
            {
                new Color(0.32f, 0.90f, 0.88f), // Pastel Aqua Jelly
                new Color(0.62f, 0.94f, 0.80f), // Pastel Mint Jelly
                new Color(0.98f, 0.66f, 0.86f), // Pastel Pink Jelly
                new Color(1.00f, 0.78f, 0.58f)  // Pastel Peach Jelly
            };
            int farCount = Mathf.CeilToInt(totalTrackLen / 22f) + 4;
            for (int i = 0; i < farCount; i++)
            {
                float z = i * 22f + 16f;
                float side = (i % 2 == 0) ? -1f : 1f;
                float x = side * (16f + (i % 3) * 5.5f);

                // 3-Tier Stacked Pastel Jelly Mould Tower rising from the pool horizon
                var tower = new GameObject($"PastelJellyTower_{i}").transform;
                tower.SetParent(_parallaxFarRoot, false);
                tower.localPosition = new Vector3(x, -1.35f, z);
                int tiers = (i % 2 == 0) ? 3 : 2;
                for (int t = 0; t < tiers; t++)
                {
                    var tierCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(tierCyl);
                    tierCyl.name = $"JellyTier_{t}";
                    tierCyl.transform.SetParent(tower, false);
                    float w = 5.4f - t * 0.85f;
                    tierCyl.transform.localPosition = new Vector3(0f, 1.3f + t * 2.4f, 0f);
                    tierCyl.transform.localScale = new Vector3(w, 1.25f, w);
                    LevelLoader.ApplyMaterialOrColor(tierCyl, null, jellyTowerCols[(i + t) % jellyTowerCols.Length]);
                }

                // Big puffy white cartoon cloud clusters well above the horizon towers!
                var farCloud = new GameObject($"FarCloud_{i}").transform;
                farCloud.SetParent(_parallaxFarRoot, false);
                farCloud.localPosition = new Vector3(-side * (16f + (i % 2) * 8f), 12.8f + (i % 3) * 2.8f, z + 10f);
                Vector3[] puffOffsets = new[]
                {
                    Vector3.zero,
                    new Vector3(2.8f, -0.5f, 0.3f),
                    new Vector3(-2.7f, -0.6f, -0.2f),
                    new Vector3(0.8f, 1.2f, 0f)
                };
                Vector3[] puffScales = new[]
                {
                    new Vector3(6.4f, 3.8f, 4.2f),
                    new Vector3(4.8f, 3.0f, 3.6f),
                    new Vector3(4.6f, 2.8f, 3.4f),
                    new Vector3(4.2f, 3.2f, 3.4f)
                };
                for (int p = 0; p < puffOffsets.Length; p++)
                {
                    var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    DestroyCollider(puff);
                    puff.transform.SetParent(farCloud, false);
                    puff.transform.localPosition = puffOffsets[p];
                    puff.transform.localScale = puffScales[p];
                    LevelLoader.ApplyMaterialOrColor(puff, null, Color.white);
                }
            }

            // 2. Waterpark Pool Layer: Inflatable Frosted Donut Rings (Torus) & Tilted Candy Buoys floating at y = -1.30f!
            int propCount = Mathf.CeilToInt(totalTrackLen / 11f) + 4;
            Color[] buoyColors = new[]
            {
                new Color(0.98f, 0.34f, 0.56f), // Hot Pink Buoy
                new Color(0.68f, 0.46f, 0.96f), // Lavender Buoy
                new Color(1.00f, 0.80f, 0.20f)  // Sunny Yellow Buoy
            };
            Mesh donutMesh = GetOrCreateTorusMesh(1.35f, 0.52f, 28, 16);
            Mesh icingMesh = GetOrCreateTorusMesh(1.35f, 0.46f, 28, 12);

            for (int i = 0; i < propCount; i++)
            {
                float z = i * 11f + 7f;
                EvaluateRoadCurve(Mathf.Min(z, totalTrackLen), out Vector3 roadPt, out _, out Vector3 roadRight);
                float side = (i % 2 == 0) ? -1f : 1f;
                // Keep buoys & donut rings well outside the curving road (11.5m..18m out in open pool water!)
                float distOut = 11.5f + (i % 3) * 3.4f;
                Vector3 waterPos = new Vector3(roadPt.x + roadRight.x * (side * distOut), -1.32f, roadPt.z + roadRight.z * (side * distOut));

                if (i % 2 == 1)
                {
                    // Inflatable Frosted Pink Donut Ring floating flat or tilted in the pool!
                    var donutRoot = new GameObject($"WaterparkDonut_{i}").transform;
                    donutRoot.SetParent(_parallaxMidRoot, false);
                    donutRoot.position = waterPos + Vector3.up * (i % 3 == 0 ? 0.85f : 0.12f);
                    donutRoot.rotation = (i % 3 == 0)
                        ? Quaternion.Euler(72f, side * -28f, 14f) // Upright floating life-ring pose!
                        : Quaternion.Euler(6f, i * 35f, -5f);
                    _floatingIslands.Add(donutRoot);

                    var ringGo = new GameObject("DonutDough");
                    ringGo.transform.SetParent(donutRoot, false);
                    ringGo.AddComponent<MeshFilter>().sharedMesh = donutMesh;
                    ringGo.AddComponent<MeshRenderer>();
                    LevelLoader.ApplyMaterialOrColor(ringGo, null, new Color(0.96f, 0.72f, 0.48f));

                    var iceGo = new GameObject("DonutPinkIcing");
                    iceGo.transform.SetParent(donutRoot, false);
                    iceGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                    iceGo.AddComponent<MeshFilter>().sharedMesh = icingMesh;
                    iceGo.AddComponent<MeshRenderer>();
                    LevelLoader.ApplyMaterialOrColor(iceGo, null, new Color(0.98f, 0.52f, 0.74f));
                }
                else
                {
                    // Playful Tilted Candy Buoy (matching the pink/purple antenna buoys in Alternative 1!)
                    var buoy = new GameObject($"WaterparkBuoy_{i}").transform;
                    buoy.SetParent(_parallaxMidRoot, false);
                    buoy.position = waterPos;
                    buoy.rotation = Quaternion.Euler(8f, i * 40f, side * -12f);
                    _floatingIslands.Add(buoy);

                    Color bCol = buoyColors[(i / 2) % buoyColors.Length];
                    var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(collar);
                    collar.name = "BuoyRim";
                    collar.transform.SetParent(buoy, false);
                    collar.transform.localScale = new Vector3(2.8f, 0.24f, 2.8f);
                    LevelLoader.ApplyMaterialOrColor(collar, null, new Color(1.00f, 0.46f, 0.28f));

                    var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    DestroyCollider(dome);
                    dome.name = "BuoyBell";
                    dome.transform.SetParent(buoy, false);
                    dome.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                    dome.transform.localScale = new Vector3(2.3f, 1.45f, 2.3f);
                    LevelLoader.ApplyMaterialOrColor(dome, null, bCol);

                    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(ring);
                    ring.name = "BuoyCapRing";
                    ring.transform.SetParent(buoy, false);
                    ring.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                    ring.transform.localScale = new Vector3(0.95f, 0.14f, 0.95f);
                    LevelLoader.ApplyMaterialOrColor(ring, null, new Color(1f, 0.84f, 0.88f));

                    var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(mast);
                    mast.name = "BuoyMast";
                    mast.transform.SetParent(buoy, false);
                    mast.transform.localPosition = new Vector3(0f, 1.55f, 0f);
                    mast.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
                    LevelLoader.ApplyMaterialOrColor(mast, null, Color.Lerp(bCol, Color.white, 0.35f));

                    var topper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    DestroyCollider(topper);
                    topper.name = "BuoyTopper";
                    topper.transform.SetParent(buoy, false);
                    topper.transform.localPosition = new Vector3(0f, 2.15f, 0f);
                    topper.transform.localScale = new Vector3(0.68f, 0.68f, 0.68f);
                    LevelLoader.ApplyMaterialOrColor(topper, null, bCol);
                }
            }
        }

        private void UpdateParallaxLayers(float dt)
        {
            if (_parallaxFarRoot != null)
            {
                // Far background moves at 35% of player Z progress -> deep parallax horizon feel!
                _parallaxFarRoot.position = new Vector3(CurrentLateralOffset * 0.35f, 0f, CurrentDistance * 0.35f);
            }
            if (_parallaxMidRoot != null)
            {
                // Mid floating islands move at 14% parallax shift + gentle vertical bobbing
                _parallaxMidRoot.position = new Vector3(CurrentLateralOffset * 0.15f, 0f, CurrentDistance * 0.14f);
            }
            float t = Time.time;
            if (_waterOceanPlane != null)
            {
                _waterOceanPlane.position = new Vector3(CurrentLateralOffset * 0.4f, -1.35f, CurrentDistance + 140f);
                if (_waterOceanMat != null)
                {
                    Vector2 off = new Vector2(Mathf.Sin(t * 0.25f) * 0.06f, (t * 0.045f) % 1f);
                    _waterOceanMat.mainTextureOffset = off;
                    if (_waterOceanMat.HasProperty("_BaseMap"))
                        _waterOceanMat.SetTextureOffset("_BaseMap", off);
                }
            }
            for (int i = 0; i < _floatingIslands.Count; i++)
            {
                var isl = _floatingIslands[i];
                if (isl == null) continue;
                Vector3 p = isl.localPosition;
                float bob = Mathf.Sin(t * 1.4f + i * 0.9f) * 0.22f * dt;
                isl.localPosition = new Vector3(p.x, p.y + bob, p.z);
            }
        }

        private Transform _waterOceanPlane;
        private Material _waterOceanMat;
        private static Texture2D _cachedWaterCausticsTex;

        private static Texture2D GetOrCreateWaterCausticsTexture()
        {
            if (_cachedWaterCausticsTex != null) return _cachedWaterCausticsTex;
            const int size = 512;
            _cachedWaterCausticsTex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "ProceduralWaterCaustics",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            // Large organic Voronoi F2-F1 swimming-pool caustics (no small brick grid repetition!)
            const int cells = 8;
            Vector2[,] pts = new Vector2[cells, cells];
            for (int cy = 0; cy < cells; cy++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    float hx = Mathf.Abs(Mathf.Sin((cx * 127.1f + cy * 311.7f) * 0.19f));
                    float hy = Mathf.Abs(Mathf.Cos((cx * 269.5f + cy * 183.3f) * 0.23f));
                    pts[cx, cy] = new Vector2(cx + 0.12f + hx * 0.76f, cy + 0.12f + hy * 0.76f);
                }
            }

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float uvy = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float uvx = x / (float)size;
                    // Gentle organic domain warp so cells curve softly like sunlit water ripples
                    float warpX = Mathf.Sin(uvy * Mathf.PI * 4f) * 0.18f;
                    float warpY = Mathf.Cos(uvx * Mathf.PI * 4f) * 0.18f;
                    float px = Mathf.Repeat(uvx * cells + warpX, cells);
                    float py = Mathf.Repeat(uvy * cells + warpY, cells);
                    int ix = Mathf.FloorToInt(px);
                    int iy = Mathf.FloorToInt(py);

                    float d1 = 99f;
                    float d2 = 99f;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int wy = (iy + oy + cells) % cells;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int wx = (ix + ox + cells) % cells;
                            Vector2 fp = pts[wx, wy] + new Vector2(ix + ox - wx, iy + oy - wy);
                            float dist = Vector2.Distance(new Vector2(px, py), fp);
                            if (dist < d1) { d2 = d1; d1 = dist; }
                            else if (dist < d2) { d2 = dist; }
                        }
                    }

                    float edge = Mathf.Clamp01(1f - (d2 - d1) * 5.4f);
                    float caustic = Mathf.Pow(edge, 2.5f);
                    byte r = (byte)Mathf.Lerp(24f, 232f, caustic);
                    byte g = (byte)Mathf.Lerp(198f, 253f, caustic);
                    byte b = (byte)Mathf.Lerp(242f, 255f, caustic);
                    pixels[y * size + x] = new Color32(r, g, b, 255);
                }
            }
            _cachedWaterCausticsTex.SetPixels32(pixels);
            _cachedWaterCausticsTex.Apply(true);
            return _cachedWaterCausticsTex;
        }

        private void EnsureWaterparkOceanPlane(BiomePalette biome)
        {
            if (_waterOceanPlane == null)
            {
                var waterGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                waterGo.name = "WaterparkOceanPlane";
                DestroyCollider(waterGo);
                waterGo.transform.SetParent(trackRoot != null ? trackRoot : transform, false);
                waterGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                waterGo.transform.localScale = new Vector3(420f, 900f, 1f);
                _waterOceanPlane = waterGo.transform;

                var mr = waterGo.GetComponent<MeshRenderer>();
                LevelLoader.ApplyMaterialOrColor(waterGo, null, Color.white);
                if (mr != null && mr.sharedMaterial != null)
                {
                    _waterOceanMat = new Material(mr.sharedMaterial) { name = "WaterparkOceanMat" };
                    var tex = GetOrCreateWaterCausticsTexture();
                    _waterOceanMat.mainTexture = tex;
                    _waterOceanMat.mainTextureScale = new Vector2(6f, 13f);
                    if (_waterOceanMat.HasProperty("_BaseMap"))
                    {
                        _waterOceanMat.SetTexture("_BaseMap", tex);
                        _waterOceanMat.SetTextureScale("_BaseMap", new Vector2(6f, 13f));
                    }
                    if (_waterOceanMat.HasProperty("_Smoothness")) _waterOceanMat.SetFloat("_Smoothness", 0.55f);
                    mr.sharedMaterial = _waterOceanMat;
                }
            }
            _waterOceanPlane.position = new Vector3(CurrentLateralOffset * 0.4f, -1.35f, CurrentDistance + 140f);
        }

        private static Texture2D _cachedRoadGridTex;

        private static Texture2D GetOrCreateRoadGridTexture()
        {
            if (_cachedRoadGridTex != null) return _cachedRoadGridTex;
            const int size = 256;
            _cachedRoadGridTex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "ProceduralRoadGrid",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            // Top surface maps to UV X in [0.25 .. 0.75] (pixels 64..192):
            // Left Mint Lane = 64..105 | Center Warm Vanilla-Cream Lane = 108..148 | Right Mint Lane = 151..192
            Color32 creamTile = new Color32(255, 248, 228, 255);
            Color32 mintTile = new Color32(208, 242, 220, 255);
            Color32 mintAlt = new Color32(200, 236, 214, 255);
            Color32 groutLine = new Color32(168, 204, 188, 255);
            Color32 sideWall = new Color32(186, 226, 208, 255);

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                bool yGrout = (y % 85) < 2;
                int rowIdx = (y / 85) & 1;
                for (int x = 0; x < size; x++)
                {
                    if (x < 64 || x > 192)
                    {
                        pixels[y * size + x] = sideWall;
                        continue;
                    }
                    bool xGrout = (x == 64) || (x >= 106 && x <= 107) || (x >= 149 && x <= 150) || (x == 192);
                    if (xGrout || yGrout)
                    {
                        pixels[y * size + x] = groutLine;
                    }
                    else if (x > 107 && x < 149)
                    {
                        pixels[y * size + x] = creamTile;
                    }
                    else
                    {
                        pixels[y * size + x] = (rowIdx == 0) ? mintTile : mintAlt;
                    }
                }
            }
            _cachedRoadGridTex.SetPixels32(pixels);
            _cachedRoadGridTex.Apply(true);
            return _cachedRoadGridTex;
        }

        private static readonly System.Collections.Generic.Dictionary<Color, Material> _cachedRoadGridMats = new System.Collections.Generic.Dictionary<Color, Material>();

        private GameObject CreateSubRibbonObject(Transform parent, string name, float zStart, float zEnd, int steps, float xMin, float xMax, float yMin, float yMax, float yOffset, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = CreateExtrudedRibbonMesh(zStart, zEnd, steps, xMin, xMax, yMin + yOffset, yMax + yOffset);
            LevelLoader.ApplyMaterialOrColor(go, null, color);
            if (name == "RoadSurfaceMesh" && mr.sharedMaterial != null)
            {
                if (!_cachedRoadGridMats.TryGetValue(color, out Material mat) || mat == null)
                {
                    mat = new Material(mr.sharedMaterial) { name = "RoadGridMat" };
                    var gridTex = GetOrCreateRoadGridTexture();
                    mat.mainTexture = gridTex;
                    mat.mainTextureScale = new Vector2(1f, 1.5f);
                    if (mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", gridTex);
                        mat.SetTextureScale("_BaseMap", new Vector2(1f, 1.5f));
                    }
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.32f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
                    _cachedRoadGridMats[color] = mat;
                }
                mr.sharedMaterial = mat;
            }
            return go;
        }

        private GameObject CreateExtrudedTubeObject(Transform parent, string name, float zStart, float zEnd, int steps, float xOffset, float yCenter, float radius, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            const int radialSegs = 14;
            int rings = steps + 1;
            Vector3[] verts = new Vector3[rings * radialSegs];
            int[] tris = new int[steps * radialSegs * 6];

            for (int i = 0; i < rings; i++)
            {
                float z = Mathf.Lerp(zStart, zEnd, i / (float)steps);
                EvaluateRoadCurve(z, out Vector3 cPos, out Quaternion cRot, out Vector3 cRight);
                Vector3 cUp = cRot * Vector3.up;
                Vector3 ringCenter = cPos + cRight * xOffset + cUp * yCenter;
                for (int r = 0; r < radialSegs; r++)
                {
                    float ang = (r / (float)radialSegs) * Mathf.PI * 2f;
                    verts[i * radialSegs + r] = ringCenter + (cRight * Mathf.Cos(ang) + cUp * Mathf.Sin(ang)) * radius;
                }
            }

            // Outward-facing clockwise winding in Unity's left-handed space (+Z forward, +X right, +Y up)
            int ti = 0;
            for (int i = 0; i < steps; i++)
            {
                int b0 = i * radialSegs;
                int b1 = (i + 1) * radialSegs;
                for (int r = 0; r < radialSegs; r++)
                {
                    int nr = (r + 1) % radialSegs;
                    tris[ti++] = b0 + r; tris[ti++] = b1 + nr; tris[ti++] = b1 + r;
                    tris[ti++] = b0 + r; tris[ti++] = b0 + nr; tris[ti++] = b1 + nr;
                }
            }

            var mesh = new Mesh { name = "SplineBumperTube" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            LevelLoader.ApplyMaterialOrColor(go, null, color);
            if (mr.sharedMaterial != null)
            {
                var tubeMat = new Material(mr.sharedMaterial) { name = "BumperTubeGlossMat" };
                if (tubeMat.HasProperty("_Smoothness")) tubeMat.SetFloat("_Smoothness", 0.72f);
                if (tubeMat.HasProperty("_Metallic")) tubeMat.SetFloat("_Metallic", 0.0f);
                mr.sharedMaterial = tubeMat;
            }

            // Plump rounded sausage endcaps at zStart and zEnd (matching Alternative 1 toy bumper segments!)
            EvaluateRoadCurve(zStart, out Vector3 sPos, out Quaternion sRot, out Vector3 sRight);
            var capStart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            capStart.name = "TubeCapStart";
            DestroyCollider(capStart);
            capStart.transform.SetParent(go.transform, false);
            capStart.transform.position = sPos + sRight * xOffset + (sRot * Vector3.up) * yCenter;
            capStart.transform.localScale = Vector3.one * (radius * 2.0f);
            if (mr.sharedMaterial != null) capStart.GetComponent<MeshRenderer>().sharedMaterial = mr.sharedMaterial;

            EvaluateRoadCurve(zEnd, out Vector3 ePos, out Quaternion eRot, out Vector3 eRight);
            var capEnd = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            capEnd.name = "TubeCapEnd";
            DestroyCollider(capEnd);
            capEnd.transform.SetParent(go.transform, false);
            capEnd.transform.position = ePos + eRight * xOffset + (eRot * Vector3.up) * yCenter;
            capEnd.transform.localScale = Vector3.one * (radius * 2.0f);
            if (mr.sharedMaterial != null) capEnd.GetComponent<MeshRenderer>().sharedMaterial = mr.sharedMaterial;

            return go;
        }

        private static Mesh GetOrCreateTorusMesh(float majorR, float minorR, int majorSegs, int minorSegs)
        {
            Vector3[] verts = new Vector3[majorSegs * minorSegs];
            int[] tris = new int[majorSegs * minorSegs * 6];
            for (int i = 0; i < majorSegs; i++)
            {
                float u = (i / (float)majorSegs) * Mathf.PI * 2f;
                float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                for (int j = 0; j < minorSegs; j++)
                {
                    float v = (j / (float)minorSegs) * Mathf.PI * 2f;
                    float r = majorR + minorR * Mathf.Cos(v);
                    verts[i * minorSegs + j] = new Vector3(r * cu, minorR * Mathf.Sin(v), r * su);
                }
            }
            int ti = 0;
            for (int i = 0; i < majorSegs; i++)
            {
                int ni = (i + 1) % majorSegs;
                for (int j = 0; j < minorSegs; j++)
                {
                    int nj = (j + 1) % minorSegs;
                    int a = i * minorSegs + j;
                    int b = ni * minorSegs + j;
                    int c = ni * minorSegs + nj;
                    int d = i * minorSegs + nj;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = a; tris[ti++] = d; tris[ti++] = c;
                }
            }
            var mesh = new Mesh { name = "ProceduralTorus" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateExtrudedRibbonMesh(float zStart, float zEnd, int steps, float xMin, float xMax, float yMin, float yMax)
        {
            int ringCount = steps + 1;
            Vector3[] verts = new Vector3[ringCount * 4];
            Vector2[] uvs = new Vector2[ringCount * 4];
            int[] tris = new int[steps * 24];

            for (int i = 0; i < ringCount; i++)
            {
                float t = i / (float)steps;
                float z = Mathf.Lerp(zStart, zEnd, t);
                EvaluateRoadCurve(z, out Vector3 center, out Quaternion rot, out Vector3 right);
                Vector3 up = rot * Vector3.up;

                int vBase = i * 4;
                verts[vBase + 0] = center + right * xMin + up * yMin;
                verts[vBase + 1] = center + right * xMin + up * yMax;
                verts[vBase + 2] = center + right * xMax + up * yMax;
                verts[vBase + 3] = center + right * xMax + up * yMin;

                uvs[vBase + 0] = new Vector2(0f, t);
                uvs[vBase + 1] = new Vector2(0.25f, t);
                uvs[vBase + 2] = new Vector2(0.75f, t);
                uvs[vBase + 3] = new Vector2(1f, t);
            }

            int ti = 0;
            for (int i = 0; i < steps; i++)
            {
                int c = i * 4;
                int n = (i + 1) * 4;

                tris[ti++] = c + 1; tris[ti++] = n + 1; tris[ti++] = n + 2;
                tris[ti++] = c + 1; tris[ti++] = n + 2; tris[ti++] = c + 2;

                tris[ti++] = c + 0; tris[ti++] = n + 0; tris[ti++] = n + 1;
                tris[ti++] = c + 0; tris[ti++] = n + 1; tris[ti++] = c + 1;

                tris[ti++] = c + 2; tris[ti++] = n + 2; tris[ti++] = n + 3;
                tris[ti++] = c + 2; tris[ti++] = n + 3; tris[ti++] = c + 3;

                tris[ti++] = c + 3; tris[ti++] = n + 3; tris[ti++] = n + 0;
                tris[ti++] = c + 3; tris[ti++] = n + 0; tris[ti++] = c + 0;
            }

            var mesh = new Mesh { name = "SplineRibbonMesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void SpawnChunkSceneryAndClouds(Transform chunkRoot, float zMid, int chunkIdx, BiomePalette biome)
        {
            EvaluateRoadCurve(zMid, out Vector3 center, out Quaternion rot, out Vector3 right);
            // Support columns descending from underneath the curved track into the pool water (matching Alternative 1!)
            for (int s = -1; s <= 1; s += 2)
            {
                var colGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCollider(colGo);
                colGo.name = s < 0 ? "TrackWaterColumnL" : "TrackWaterColumnR";
                colGo.transform.SetParent(chunkRoot, false);
                colGo.transform.position = center + right * (s * (roadWidth * 0.36f)) + Vector3.down * 1.05f;
                colGo.transform.localScale = new Vector3(0.48f, 0.85f, 0.48f);
                LevelLoader.ApplyMaterialOrColor(colGo, null, new Color(0.18f, 0.72f, 0.82f));
            }
        }

        private void SpawnObstacleAndPreparationPickups(
            float gateZ,
            EndlessObstacleType gateType,
            int multiHoleVariant = -1,
            bool hasBonusPickup = false,
            EndlessPickupType customBonusPickup = EndlessPickupType.StarScore)
        {
            if (hasBonusPickup)
            {
                SpawnPickupOnRoad(gateZ - 2.6f, 0f, customBonusPickup, 0f);
            }

            switch (gateType)
            {
                case EndlessObstacleType.SmallCenterHole:
                case EndlessObstacleType.VerticalCrusherGate:
                    SpawnPickupOnRoad(gateZ - 6.4f, -1.05f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 4.8f, +1.08f, EndlessPickupType.Shrink, 25f);
                    BuildCutoutGate(gateZ, gateType, multiHoleVariant);
                    break;

                case EndlessObstacleType.TwinSplitTunnels:
                case EndlessObstacleType.MovingTwinTunnels:
                    SpawnPickupOnRoad(gateZ - 6.8f, -1.2f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 6.8f, 0f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 6.8f, +1.2f, EndlessPickupType.Split, 0f);
                    BuildCutoutGate(gateZ, gateType, multiHoleVariant);
                    SpawnPickupOnRoad(gateZ + 2.6f, 0f, EndlessPickupType.Merge, 0f);
                    break;

                case EndlessObstacleType.HeavySmashWall:
                case EndlessObstacleType.MassScaleGate:
                    SpawnPickupOnRoad(gateZ - 7.2f, -1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 7.2f, +1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 4.4f, 0f, EndlessPickupType.Grow, 35f);
                    BuildCutoutGate(gateZ, gateType, multiHoleVariant);
                    break;

                case EndlessObstacleType.FlexibleSqueezeGate:
                case EndlessObstacleType.SlidingHoleWall:
                    SpawnPickupOnRoad(gateZ - 7.0f, -1.2f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 7.0f, +1.2f, EndlessPickupType.Shrink, 25f);
                    BuildCutoutGate(gateZ, gateType, multiHoleVariant);
                    break;

                case EndlessObstacleType.MicroShatterGate:
                    SpawnPickupOnRoad(gateZ - 7.0f, 0f, EndlessPickupType.Grow, 25f);
                    BuildCutoutGate(gateZ, gateType, multiHoleVariant);
                    SpawnPickupOnRoad(gateZ + 2.8f, 0f, EndlessPickupType.Merge, 0f);
                    break;

                case EndlessObstacleType.MultiHoleDecisionWall:
                {
                    int varIdx = multiHoleVariant >= 0
                        ? (multiHoleVariant % 3)
                        : (Mathf.Abs(Mathf.RoundToInt(gateZ / 12f)) % 3);
                    if (varIdx == 0) // SizeChoice: left Shrink (-), right Grow (+)
                    {
                        SpawnPickupOnRoad(gateZ - 7.0f, -1.45f, EndlessPickupType.Shrink, 25f);
                        SpawnPickupOnRoad(gateZ - 7.0f, +1.45f, EndlessPickupType.Grow, 25f);
                    }
                    else if (varIdx == 1) // SplitChoice: center Shrink (-) vs side Split (÷2)
                    {
                        SpawnPickupOnRoad(gateZ - 7.0f, -1.35f, EndlessPickupType.Split, 0f);
                        SpawnPickupOnRoad(gateZ - 7.0f, 0f, EndlessPickupType.Shrink, 25f);
                        SpawnPickupOnRoad(gateZ - 7.0f, +1.35f, EndlessPickupType.Split, 0f);
                        SpawnPickupOnRoad(gateZ + 2.6f, 0f, EndlessPickupType.Merge, 0f);
                    }
                    else // RiskRewardChoice: left safe Grow (+), right tight Shrink (-) for high-risk bonus
                    {
                        SpawnPickupOnRoad(gateZ - 7.0f, -1.40f, EndlessPickupType.Grow, 20f);
                        SpawnPickupOnRoad(gateZ - 7.0f, +1.60f, EndlessPickupType.Shrink, 25f);
                        SpawnPickupOnRoad(gateZ - 4.4f, +1.60f, EndlessPickupType.Shrink, 20f);
                    }
                    BuildCutoutGate(gateZ, gateType, varIdx);
                    break;
                }

                default:
                    SpawnPickupOnRoad(gateZ - 6.8f, -1.3f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 6.8f, +1.3f, EndlessPickupType.SpeedBoost, 0f);
                    if (!hasBonusPickup)
                    {
                        int optIdx = Mathf.Abs(Mathf.RoundToInt(gateZ)) % 4;
                        EndlessPickupType bonusType = optIdx == 0 ? EndlessPickupType.Shield
                            : (optIdx == 1 ? EndlessPickupType.Magnet
                            : (optIdx == 2 ? EndlessPickupType.StarScore : EndlessPickupType.SlowTime));
                        SpawnPickupOnRoad(gateZ - 3.6f, 0f, bonusType, 0f);
                    }
                    BuildCutoutGate(gateZ, gateType, multiHoleVariant);
                    break;
            }
        }

        // =====================================================================
        // 2. TOPLANABİLİR NESNELER (PICKUPS) - Reference Section 2
        // =====================================================================
        private void SpawnPickupOnRoad(float z, float lateralOffset, EndlessPickupType type, float amount)
        {
            EvaluateRoadCurve(z, out Vector3 center, out Quaternion rot, out Vector3 right);
            Vector3 up = rot * Vector3.up;
            Vector3 pos = center + right * lateralOffset + up * (type == EndlessPickupType.SpeedBoost ? 0.05f : 0.86f);

            string grpName = BlenderKitModelSpawner.GetPickupGroupName(type);
            var blenderPickup = BlenderKitModelSpawner.InstantiateModelGroup(
                grpName,
                trackRoot,
                alignBottomToZero: type == EndlessPickupType.SpeedBoost,
                uniformScale: type == EndlessPickupType.SpeedBoost ? 0.92f : 0.72f);

            if (blenderPickup != null)
            {
                blenderPickup.name = $"Pickup_{type}";
                blenderPickup.transform.SetPositionAndRotation(pos, rot);
                var trig = blenderPickup.AddComponent<SphereCollider>();
                trig.isTrigger = true;
                trig.radius = 0.65f;

                var bItem = blenderPickup.AddComponent<EndlessPickupItem>();
                bItem.pickupType = type;
                bItem.amount = amount;
                bItem.trackZ = z;
                if (type == EndlessPickupType.SpeedBoost) bItem.spinSpeed = 0f;
                else
                {
                    // Soft colored ground contact shadow beneath floating glass orbs!
                    var pShadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pShadow.name = "PickupGroundShadow";
                    DestroyCollider(pShadow);
                    pShadow.transform.SetParent(trackRoot, false);
                    pShadow.transform.position = center + right * lateralOffset + up * 0.02f;
                    pShadow.transform.rotation = rot;
                    pShadow.transform.localScale = new Vector3(0.88f, 0.012f, 0.88f);
                    Color shCol = type == EndlessPickupType.Shrink
                        ? new Color(0.58f, 0.14f, 0.20f, 0.45f)
                        : (type == EndlessPickupType.Grow ? new Color(0.08f, 0.44f, 0.24f, 0.45f) : new Color(0.08f, 0.36f, 0.48f, 0.45f));
                    BlenderKitModelSpawner.ApplySoftShadowDiscMaterial(pShadow, shCol);
                    _spawnedChunks.Add(pShadow);
                }
                _spawnedChunks.Add(blenderPickup);
                _activePickups.Add(bItem);
                return;
            }

            GameObject go;
            Material mat = null;
            Color col;

            switch (type)
            {
                case EndlessPickupType.Grow: // Büyüme Küresi (+)
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "Pickup_Grow (+)";
                    go.transform.localScale = Vector3.one * 0.86f;
                    mat = pickupMaterial;
                    col = new Color(0.16f, 0.96f, 0.46f);
                    CreateWallBlock(go.transform, "PlusV", new Vector3(0f, 0f, -0.42f), new Vector3(0.18f, 0.56f, 0.18f), null, Color.white);
                    CreateWallBlock(go.transform, "PlusH", new Vector3(0f, 0f, -0.42f), new Vector3(0.56f, 0.18f, 0.18f), null, Color.white);
                    break;

                case EndlessPickupType.Shrink: // Küçülme Küresi (-)
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "Pickup_Shrink (-)";
                    go.transform.localScale = Vector3.one * 0.82f;
                    mat = drainMaterial;
                    col = new Color(1.00f, 0.24f, 0.34f);
                    CreateWallBlock(go.transform, "MinusH", new Vector3(0f, 0f, -0.42f), new Vector3(0.58f, 0.18f, 0.18f), null, Color.white);
                    break;

                case EndlessPickupType.Split: // Bölünme Kristali (÷2)
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Pickup_Split (÷2)";
                    go.transform.localScale = new Vector3(0.76f, 0.76f, 0.76f);
                    go.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                    col = new Color(0.74f, 0.28f, 1.00f);
                    var facet = CreateWallBlock(go.transform, "CrystalFacet", Vector3.zero, new Vector3(0.85f, 0.85f, 0.85f), null, new Color(0.88f, 0.52f, 1.00f));
                    facet.transform.localRotation = Quaternion.Euler(35f, 0f, 45f);
                    break;

                case EndlessPickupType.Merge: // Birleşme Halkası (><)
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "Pickup_Merge (><)";
                    go.transform.localScale = new Vector3(0.96f, 0.16f, 0.96f);
                    go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    mat = goalMaterial;
                    col = new Color(1.00f, 0.80f, 0.16f);
                    CreateWallBlock(go.transform, "InnerCore", Vector3.zero, new Vector3(0.52f, 1.15f, 0.52f), null, new Color(0.24f, 0.16f, 0.38f));
                    break;

                case EndlessPickupType.SpeedBoost: // Süper Hız Bandı (⚡)
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Pickup_SpeedPad (⚡)";
                    go.transform.localScale = new Vector3(1.55f, 0.10f, 2.3f);
                    go.transform.localRotation = rot;
                    col = new Color(0.12f, 0.88f, 1.00f);
                    for (int a = -1; a <= 1; a++)
                    {
                        var ch = CreateWallBlock(go.transform, $"BoostArrow_{a}", new Vector3(0f, 0.65f, a * 0.28f), new Vector3(0.58f, 0.25f, 0.12f), null, new Color(0.45f, 1.00f, 0.65f));
                        ch.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
                    }
                    break;

                case EndlessPickupType.Shield: // Kalkan (🛡)
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Pickup_Shield (🛡)";
                    go.transform.localScale = new Vector3(0.75f, 0.88f, 0.26f);
                    col = new Color(0.84f, 0.32f, 0.98f);
                    CreateWallBlock(go.transform, "ShieldEmblem", new Vector3(0f, 0f, -0.58f), new Vector3(0.45f, 0.45f, 0.25f), null, Color.white);
                    break;

                case EndlessPickupType.Magnet: // Mıknatıs (🧲)
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Pickup_Magnet (🧲)";
                    go.transform.localScale = new Vector3(0.75f, 0.25f, 0.28f);
                    col = new Color(0.22f, 0.62f, 1.00f);
                    CreateWallBlock(go.transform, "MagnetLegL", new Vector3(-0.36f, 1.2f, 0f), new Vector3(0.28f, 2.2f, 1.0f), null, new Color(0.98f, 0.26f, 0.34f));
                    CreateWallBlock(go.transform, "MagnetLegR", new Vector3(+0.36f, 1.2f, 0f), new Vector3(0.28f, 2.2f, 1.0f), null, new Color(0.22f, 0.62f, 1.00f));
                    break;

                case EndlessPickupType.StarScore: // Puan Yıldızı (⭐ +500)
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Pickup_Star (⭐)";
                    go.transform.localScale = new Vector3(0.72f, 0.72f, 0.28f);
                    col = new Color(1.00f, 0.84f, 0.16f);
                    var starRot = CreateWallBlock(go.transform, "StarPoints", Vector3.zero, Vector3.one, null, new Color(1.00f, 0.92f, 0.35f));
                    starRot.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;

                default: // SlowTime: Yavaş Zaman (⏳)
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "Pickup_SlowTime (⏳)";
                    go.transform.localScale = new Vector3(0.68f, 0.48f, 0.68f);
                    col = new Color(0.98f, 0.72f, 0.24f);
                    CreateWallBlock(go.transform, "HourglassTop", new Vector3(0f, 0.95f, 0f), new Vector3(1.15f, 0.22f, 1.15f), null, new Color(0.68f, 0.32f, 0.96f));
                    CreateWallBlock(go.transform, "HourglassBot", new Vector3(0f, -0.95f, 0f), new Vector3(1.15f, 0.22f, 1.15f), null, new Color(0.68f, 0.32f, 0.96f));
                    break;
            }

            go.transform.SetParent(trackRoot, false);
            go.transform.position = pos;
            var c = go.GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
            LevelLoader.ApplyMaterialOrColor(go, mat, col);

            var item = go.AddComponent<EndlessPickupItem>();
            item.pickupType = type;
            item.amount = amount;
            item.trackZ = z;
            if (type == EndlessPickupType.SpeedBoost) item.spinSpeed = 0f;
            _spawnedChunks.Add(go);
            _activePickups.Add(item);
        }

        // =====================================================================
        // 3. ENGELLER ve GEÇİT MEKANİKLERİ (Pist: 6.4m, Yük: ~2.3m, Kal: 0.55m)
        // =====================================================================
        private void BuildCutoutGate(float z, EndlessObstacleType type, int multiHoleVariant = -1)
        {
            EvaluateRoadCurve(z, out Vector3 center, out Quaternion rot, out _);

            var gateRoot = new GameObject($"CutoutGate_{type}_{z:F0}");
            gateRoot.transform.SetParent(trackRoot, false);
            gateRoot.transform.SetPositionAndRotation(center, rot);
            var obs = gateRoot.AddComponent<EndlessGateObstacle>();
            obs.obstacleType = type;
            obs.trackZ = z;
            obs.animPhaseOffset = z * 0.21f;

            if (type == EndlessObstacleType.MultiHoleDecisionWall)
            {
                int vIdx = multiHoleVariant >= 0 ? (multiHoleVariant % 3) : (Mathf.Abs(Mathf.RoundToInt(z / 12f)) % 3);
                MultiHoleVariantType vType = vIdx == 0
                    ? MultiHoleVariantType.SizeChoice
                    : (vIdx == 1 ? MultiHoleVariantType.SplitChoice : MultiHoleVariantType.RiskRewardChoice);
                obs.multiHoleConfig = MultiHoleWallConfig.CreatePreset(vType);
            }

            _spawnedChunks.Add(gateRoot);
            _activeObstacles.Add(obs);

            if (BlenderKitModelSpawner.PopulateObstacleWithBlenderModel(gateRoot.transform, obs))
            {
                return;
            }

            Color purpleFrame = new Color(0.46f, 0.28f, 0.78f);
            Color darkIndigo = new Color(0.24f, 0.16f, 0.48f);
            Color neonMagenta = new Color(0.88f, 0.34f, 1.00f);
            Color hazardGold = new Color(1.00f, 0.78f, 0.16f);
            Color iceCrystal = new Color(0.42f, 0.88f, 1.00f, 0.82f);

            switch (type)
            {
                case EndlessObstacleType.SmallCenterHole: // Dar Geçit (Açıklık Ø 0.72-0.85m, Sadece Küçük Jöle)
                    CreateWallBlock(gateRoot.transform, "LeftWall", new Vector3(-1.82f, 1.15f, 0f), new Vector3(2.6f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "RightWall", new Vector3(+1.82f, 1.15f, 0f), new Vector3(2.6f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "TopHoleArch", new Vector3(0f, 1.62f, 0f), new Vector3(1.1f, 1.35f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "TopHeaderBeam", new Vector3(0f, 2.35f, 0f), new Vector3(6.4f, 0.24f, 0.62f), null, neonMagenta);
                    // Glowing circular rim around the small center hole
                    var holeRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(holeRim);
                    holeRim.name = "SmallHoleNeonRim";
                    holeRim.transform.SetParent(gateRoot.transform, false);
                    holeRim.transform.localPosition = new Vector3(0f, 0.52f, 0f);
                    holeRim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    holeRim.transform.localScale = new Vector3(1.05f, 0.30f, 1.05f);
                    LevelLoader.ApplyMaterialOrColor(holeRim, null, darkIndigo);
                    break;

                case EndlessObstacleType.VerticalCrusherGate: // Piston Kapı (Sarı-Siyah İkaz Şeritli + ↓ Ok)
                    CreateWallBlock(gateRoot.transform, "LeftPost", new Vector3(-2.85f, 1.35f, 0f), new Vector3(0.65f, 2.7f, 0.58f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "RightPost", new Vector3(+2.85f, 1.35f, 0f), new Vector3(0.65f, 2.7f, 0.58f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "TopCrossbar", new Vector3(0f, 2.65f, 0f), new Vector3(6.4f, 0.38f, 0.62f), null, darkIndigo);
                    var piston = CreateWallBlock(gateRoot.transform, "CrusherPiston", new Vector3(0f, 1.8f, 0f), new Vector3(4.9f, 1.5f, 0.55f), null, purpleFrame);
                    // Yellow/Black Hazard Bar on bottom of Piston + Down Arrow ↓
                    CreateWallBlock(piston.transform, "HazardBarYellow", new Vector3(0f, -0.36f, -0.05f), new Vector3(0.98f, 0.26f, 1.06f), null, hazardGold);
                    for (int s = -2; s <= 2; s++)
                    {
                        CreateWallBlock(piston.transform, $"HazardStripe_{s}", new Vector3(s * 0.18f, -0.36f, -0.08f), new Vector3(0.08f, 0.26f, 1.08f), null, new Color(0.12f, 0.12f, 0.16f));
                    }
                    CreateWallBlock(piston.transform, "DownArrowBadge", new Vector3(0f, 0.15f, -0.52f), new Vector3(0.18f, 0.42f, 0.12f), null, hazardGold);
                    obs.movingPartA = piston.transform;
                    break;

                case EndlessObstacleType.HeavySmashWall: // Kırılabilir Cam Duvar (Sadece Dev Jöle ≥ 115, Çatlak Kristal Cam)
                    CreateWallBlock(gateRoot.transform, "LeftFrame", new Vector3(-2.35f, 1.15f, 0f), new Vector3(1.65f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "RightFrame", new Vector3(+2.35f, 1.15f, 0f), new Vector3(1.65f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "TopHeaderBeam", new Vector3(0f, 2.35f, 0f), new Vector3(6.4f, 0.26f, 0.62f), null, new Color(0.28f, 0.78f, 1.00f));
                    var smashCenter = CreateWallBlock(gateRoot.transform, "BreakableSmashCenter", new Vector3(0f, 1.10f, 0f), new Vector3(3.0f, 2.15f, 0.36f), null, iceCrystal);
                    // 3D Crystal Crack Lines across the glass panel
                    var c1 = CreateWallBlock(smashCenter.transform, "Crack1", new Vector3(-0.1f, 0.05f, -0.52f), new Vector3(0.85f, 0.04f, 0.1f), null, Color.white);
                    c1.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                    var c2 = CreateWallBlock(smashCenter.transform, "Crack2", new Vector3(0.12f, -0.08f, -0.52f), new Vector3(0.75f, 0.04f, 0.1f), null, Color.white);
                    c2.transform.localRotation = Quaternion.Euler(0f, 0f, -36f);
                    obs.breakableCenterPiece = smashCenter.transform;
                    break;

                case EndlessObstacleType.TwinSplitTunnels: // İkiz Tünel (Sol/Sağ ~2.5m, Ortası kapalı, Bölünerek geçilir)
                    CreateWallBlock(gateRoot.transform, "OuterLeftPost", new Vector3(-2.85f, 1.15f, 0f), new Vector3(0.7f, 2.3f, 0.55f), null, purpleFrame);
                    var centerPillar = CreateWallBlock(gateRoot.transform, "CenterSplitPillar", new Vector3(0f, 1.15f, 0f), new Vector3(1.05f, 2.3f, 0.60f), null, purpleFrame);
                    CreateWallBlock(centerPillar.transform, "SplitBadgeBar", new Vector3(0f, 0.1f, -0.54f), new Vector3(0.55f, 0.10f, 0.1f), null, neonMagenta);
                    CreateWallBlock(gateRoot.transform, "OuterRightPost", new Vector3(+2.85f, 1.15f, 0f), new Vector3(0.7f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "TopTwinArch", new Vector3(0f, 2.15f, 0f), new Vector3(6.4f, 0.45f, 0.58f), null, neonMagenta);
                    break;

                case EndlessObstacleType.MultiHoleDecisionWall: // 5. Çoklu Delik Duvarı (Boyut / Bölünme / Risk–Ödül Seçimi)
                {
                    int vIdx = Mathf.Abs(Mathf.RoundToInt(z / 12f)) % 3;
                    MultiHoleVariantType vType = vIdx == 0
                        ? MultiHoleVariantType.SizeChoice
                        : (vIdx == 1 ? MultiHoleVariantType.SplitChoice : MultiHoleVariantType.RiskRewardChoice);
                    obs.multiHoleConfig = MultiHoleWallConfig.CreatePreset(vType);

                    // Solid unbreakable frame pillars & header beam
                    CreateWallBlock(gateRoot.transform, "MH_OuterLeft", new Vector3(-2.85f, 1.15f, 0f), new Vector3(0.70f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "MH_OuterRight", new Vector3(+2.85f, 1.15f, 0f), new Vector3(0.70f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "MH_TopHeader", new Vector3(0f, 2.25f, 0f), new Vector3(6.4f, 0.36f, 0.62f), null,
                        vType == MultiHoleVariantType.RiskRewardChoice ? hazardGold : neonMagenta);

                    if (vType == MultiHoleVariantType.SizeChoice || vType == MultiHoleVariantType.RiskRewardChoice)
                    {
                        CreateWallBlock(gateRoot.transform, "MH_CenterDivider", new Vector3(0.05f, 1.10f, 0f), new Vector3(1.15f, 2.1f, 0.55f), null, purpleFrame);
                    }
                    else
                    {
                        CreateWallBlock(gateRoot.transform, "MH_SplitDivL", new Vector3(-0.68f, 1.10f, 0f), new Vector3(0.46f, 2.1f, 0.55f), null, purpleFrame);
                        CreateWallBlock(gateRoot.transform, "MH_SplitDivR", new Vector3(+0.68f, 1.10f, 0f), new Vector3(0.46f, 2.1f, 0.55f), null, purpleFrame);
                    }

                    // Build glowing aperture rings from data-driven MultiHoleApertureSpec[]
                    var apertures = obs.multiHoleConfig.Apertures;
                    for (int h = 0; h < apertures.Length; h++)
                    {
                        MultiHoleApertureSpec ap = apertures[h];
                        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        DestroyCollider(ring);
                        ring.name = $"MH_Ring_{ap.HoleId}";
                        ring.transform.SetParent(gateRoot.transform, false);
                        ring.transform.localPosition = new Vector3(ap.CenterX, ap.CenterY * 0.65f, 0f);
                        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        ring.transform.localScale = new Vector3(ap.Diameter * 1.08f, 0.30f, ap.Diameter * 1.08f);
                        LevelLoader.ApplyMaterialOrColor(ring, null, ap.RimColor);
                    }
                    break;
                }

                case EndlessObstacleType.MovingTwinTunnels: // Hareketli İkiz Tünel (← → Oklarla Açılıp Kapanan Çift Tünel)
                    var movingRig = new GameObject("MovingTwinRig").transform;
                    movingRig.SetParent(gateRoot.transform, false);
                    var mLeft = CreateWallBlock(movingRig, "OuterLeftPost", new Vector3(-2.45f, 1.15f, 0f), new Vector3(0.65f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(mLeft.transform, "ArrowLeft", new Vector3(0f, 0f, -0.54f), new Vector3(0.55f, 0.16f, 0.1f), null, hazardGold);
                    CreateWallBlock(movingRig, "CenterSplitPillar", new Vector3(0f, 1.15f, 0f), new Vector3(0.95f, 2.3f, 0.62f), null, neonMagenta);
                    var mRight = CreateWallBlock(movingRig, "OuterRightPost", new Vector3(+2.45f, 1.15f, 0f), new Vector3(0.65f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(mRight.transform, "ArrowRight", new Vector3(0f, 0f, -0.54f), new Vector3(0.55f, 0.16f, 0.1f), null, hazardGold);
                    CreateWallBlock(movingRig, "TopTwinArch", new Vector3(0f, 2.15f, 0f), new Vector3(5.6f, 0.45f, 0.58f), null, hazardGold);
                    obs.movingPartA = movingRig;
                    break;

                case EndlessObstacleType.SwingingPendulum: // Sarkaç (Dikenli Mor/Altın Topuzlu Sarkaç)
                    CreateWallBlock(gateRoot.transform, "ArchPostL", new Vector3(-2.95f, 1.9f, 0f), new Vector3(0.55f, 3.8f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "ArchPostR", new Vector3(+2.95f, 1.9f, 0f), new Vector3(0.55f, 3.8f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "ArchTop", new Vector3(0f, 3.8f, 0f), new Vector3(6.4f, 0.42f, 0.58f), null, neonMagenta);

                    var pivot = new GameObject("PendulumPivot").transform;
                    pivot.SetParent(gateRoot.transform, false);
                    pivot.localPosition = new Vector3(0f, 3.8f, 0f);
                    CreateWallBlock(pivot, "PendulumRod", new Vector3(0f, -1.5f, 0f), new Vector3(0.18f, 3.0f, 0.18f), null, hazardGold);
                    var hammer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    hammer.name = "PendulumSpikedMace";
                    DestroyCollider(hammer);
                    hammer.transform.SetParent(pivot, false);
                    hammer.transform.localPosition = new Vector3(0f, -3.0f, 0f);
                    hammer.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
                    LevelLoader.ApplyMaterialOrColor(hammer, null, purpleFrame);
                    // Golden spikes on the pendulum ball
                    CreateWallBlock(hammer.transform, "SpikesX", Vector3.zero, new Vector3(1.38f, 0.22f, 0.22f), null, hazardGold);
                    CreateWallBlock(hammer.transform, "SpikesY", Vector3.zero, new Vector3(0.22f, 1.38f, 0.22f), null, hazardGold);
                    obs.movingPartA = pivot;
                    break;

                case EndlessObstacleType.SpinnerBar: // Dönen Engel (Spinner Bar: Çap ~4.6m, Dönen Kollar)
                    var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(hub);
                    hub.name = "SpinnerCenterPedestal";
                    hub.transform.SetParent(gateRoot.transform, false);
                    hub.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                    hub.transform.localScale = new Vector3(0.95f, 0.85f, 0.95f);
                    LevelLoader.ApplyMaterialOrColor(hub, null, hazardGold);

                    var rotor = new GameObject("SpinnerRotor").transform;
                    rotor.SetParent(gateRoot.transform, false);
                    rotor.localPosition = new Vector3(0f, 0.78f, 0f);
                    CreateWallBlock(rotor, "SpinnerMainArm", Vector3.zero, new Vector3(4.6f, 0.36f, 0.36f), null, neonMagenta);
                    CreateWallBlock(rotor, "SpinnerCapL", new Vector3(-2.15f, 0f, 0f), new Vector3(0.45f, 0.48f, 0.48f), null, hazardGold);
                    CreateWallBlock(rotor, "SpinnerCapR", new Vector3(+2.15f, 0f, 0f), new Vector3(0.45f, 0.48f, 0.48f), null, hazardGold);
                    obs.movingPartA = rotor;
                    break;

                case EndlessObstacleType.SlidingDoorWall:
                    var slideLeft = CreateWallBlock(gateRoot.transform, "SlidingDoorLeft", new Vector3(-1.8f, 1.15f, 0f), new Vector3(2.2f, 2.3f, 0.55f), null, purpleFrame);
                    var slideRight = CreateWallBlock(gateRoot.transform, "SlidingDoorRight", new Vector3(+1.8f, 1.15f, 0f), new Vector3(2.2f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "SlidingTopRail", new Vector3(0f, 2.35f, 0f), new Vector3(6.4f, 0.35f, 0.62f), null, neonMagenta);
                    obs.movingPartA = slideLeft.transform;
                    obs.movingPartB = slideRight.transform;
                    break;

                case EndlessObstacleType.PincerShutters:
                    var pincerL = CreateWallBlock(gateRoot.transform, "PincerLeft", new Vector3(-1.8f, 1.15f, 0f), new Vector3(2.4f, 2.3f, 0.55f), null, purpleFrame);
                    var pincerR = CreateWallBlock(gateRoot.transform, "PincerRight", new Vector3(+1.8f, 1.15f, 0f), new Vector3(2.4f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "PincerTopRail", new Vector3(0f, 2.35f, 0f), new Vector3(6.4f, 0.35f, 0.62f), null, hazardGold);
                    obs.movingPartA = pincerL.transform;
                    obs.movingPartB = pincerR.transform;
                    break;

                case EndlessObstacleType.LeftOpeningWall:
                    CreateWallBlock(gateRoot.transform, "BlockedRightSide", new Vector3(+1.25f, 1.15f, 0f), new Vector3(3.7f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "LeftArchTop", new Vector3(-1.65f, 1.95f, 0f), new Vector3(2.2f, 0.7f, 0.55f), null, neonMagenta);
                    break;

                case EndlessObstacleType.RightOpeningWall:
                    CreateWallBlock(gateRoot.transform, "BlockedLeftSide", new Vector3(-1.25f, 1.15f, 0f), new Vector3(3.7f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "RightArchTop", new Vector3(+1.65f, 1.95f, 0f), new Vector3(2.2f, 0.7f, 0.55f), null, neonMagenta);
                    break;

                case EndlessObstacleType.FlexibleSqueezeGate: // Faz 4: Esnek Geçit (Uzun dar koridor)
                    CreateWallBlock(gateRoot.transform, "FlexLeftWall", new Vector3(-1.82f, 1.15f, 0f), new Vector3(2.6f, 2.3f, 1.85f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "FlexRightWall", new Vector3(+1.82f, 1.15f, 0f), new Vector3(2.6f, 2.3f, 1.85f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "FlexTopArch", new Vector3(0f, 2.15f, 0f), new Vector3(6.4f, 0.45f, 1.90f), null, new Color(0.20f, 0.92f, 1.00f));
                    break;

                case EndlessObstacleType.MassScaleGate: // Faz 4: Kütle Terazisi (Baskı plakası + ağırlık kapısı)
                    CreateWallBlock(gateRoot.transform, "ScalePad", new Vector3(0f, 0.08f, -1.55f), new Vector3(2.8f, 0.16f, 1.4f), null, hazardGold);
                    CreateWallBlock(gateRoot.transform, "ScalePostL", new Vector3(-2.65f, 1.25f, 0f), new Vector3(0.65f, 2.5f, 0.55f), null, darkIndigo);
                    CreateWallBlock(gateRoot.transform, "ScalePostR", new Vector3(+2.65f, 1.25f, 0f), new Vector3(0.65f, 2.5f, 0.55f), null, darkIndigo);
                    CreateWallBlock(gateRoot.transform, "ScaleTopBeam", new Vector3(0f, 2.38f, 0f), new Vector3(6.4f, 0.36f, 0.62f), null, hazardGold);
                    CreateWallBlock(gateRoot.transform, "ScaleEnergyDoor", new Vector3(0f, 1.15f, 0f), new Vector3(4.5f, 2.1f, 0.28f), null, new Color(1.00f, 0.80f, 0.22f, 0.62f));
                    break;

                case EndlessObstacleType.SlidingHoleWall: // Faz 4: Hareketli Delik Duvarı
                    CreateWallBlock(gateRoot.transform, "SlideOuterL", new Vector3(-2.85f, 1.15f, 0f), new Vector3(0.65f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "SlideOuterR", new Vector3(+2.85f, 1.15f, 0f), new Vector3(0.65f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "SlideTopBeam", new Vector3(0f, 2.25f, 0f), new Vector3(6.4f, 0.35f, 0.62f), null, hazardGold);
                    var movingHoleRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(movingHoleRing);
                    movingHoleRing.name = "MovingApertureRing";
                    movingHoleRing.transform.SetParent(gateRoot.transform, false);
                    movingHoleRing.transform.localPosition = new Vector3(0f, 0.72f, 0f);
                    movingHoleRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    movingHoleRing.transform.localScale = new Vector3(1.25f, 0.32f, 1.25f);
                    LevelLoader.ApplyMaterialOrColor(movingHoleRing, null, new Color(1.00f, 0.58f, 0.16f));
                    obs.movingPartA = movingHoleRing.transform;
                    break;

                case EndlessObstacleType.MicroShatterGate: // Faz 6: Parçalanma Kapısı (Çoklu lazer ızgarası)
                    CreateWallBlock(gateRoot.transform, "ShatterPostL", new Vector3(-2.65f, 1.15f, 0f), new Vector3(0.8f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "ShatterPostR", new Vector3(+2.65f, 1.15f, 0f), new Vector3(0.8f, 2.3f, 0.55f), null, purpleFrame);
                    CreateWallBlock(gateRoot.transform, "ShatterTop", new Vector3(0f, 2.25f, 0f), new Vector3(6.4f, 0.36f, 0.62f), null, new Color(0.20f, 0.94f, 1.00f));
                    for (int gx = -1; gx <= 1; gx++)
                    {
                        CreateWallBlock(gateRoot.transform, $"GridV_{gx}", new Vector3(gx * 1.1f, 1.10f, 0f), new Vector3(0.08f, 2.0f, 0.22f), null, new Color(0.20f, 0.94f, 1.00f));
                    }
                    for (int gy = 0; gy < 2; gy++)
                    {
                        CreateWallBlock(gateRoot.transform, $"GridH_{gy}", new Vector3(0f, 0.75f + gy * 0.70f, 0f), new Vector3(4.5f, 0.08f, 0.22f), null, neonMagenta);
                    }
                    break;
            }
        }

        private static Mesh _beveledBoxMesh;

        private static Mesh GetOrCreateBeveledBoxMesh()
        {
            if (_beveledBoxMesh != null) return _beveledBoxMesh;

            // Chamfered 1x1x1 box (Octagonal bevel profile extruded along Y with beveled top/bottom caps)
            // Gives studio-rounded edges that catch specular highlights cleanly instead of sharp PrimitiveType.Cube!
            const float c = 0.09f; // Chamfer inset
            const float h = 0.5f;
            float inner = h - c;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Build 3 rings along Y: bottom (-h), lower-bevel (-inner), upper-bevel (+inner), top (+h)
            // Using 8-point chamfered square in XZ:
            Vector2[] ringXZ = new Vector2[]
            {
                new Vector2(-inner, -h), new Vector2( inner, -h),
                new Vector2( h, -inner), new Vector2( h,  inner),
                new Vector2( inner,  h), new Vector2(-inner,  h),
                new Vector2(-h,  inner), new Vector2(-h, -inner)
            };
            Vector2[] capXZ = new Vector2[]
            {
                new Vector2(-inner * 0.92f, -inner * 0.92f), new Vector2( inner * 0.92f, -inner * 0.92f),
                new Vector2( inner * 0.92f, -inner * 0.92f), new Vector2( inner * 0.92f,  inner * 0.92f),
                new Vector2( inner * 0.92f,  inner * 0.92f), new Vector2(-inner * 0.92f,  inner * 0.92f),
                new Vector2(-inner * 0.92f,  inner * 0.92f), new Vector2(-inner * 0.92f, -inner * 0.92f)
            };

            float[] ys = { -h, -inner, inner, h };
            Vector2[][] xzRings = { capXZ, ringXZ, ringXZ, capXZ };

            for (int r = 0; r < 4; r++)
            {
                for (int i = 0; i < 8; i++)
                    verts.Add(new Vector3(xzRings[r][i].x, ys[r], xzRings[r][i].y));
            }

            // Connect rings
            for (int r = 0; r < 3; r++)
            {
                int rowA = r * 8;
                int rowB = (r + 1) * 8;
                for (int i = 0; i < 8; i++)
                {
                    int ni = (i + 1) % 8;
                    tris.Add(rowA + i); tris.Add(rowB + i); tris.Add(rowB + ni);
                    tris.Add(rowA + i); tris.Add(rowB + ni); tris.Add(rowA + ni);
                }
            }

            // Top cap (ring 3) & Bottom cap (ring 0)
            int topCenter = verts.Count; verts.Add(new Vector3(0f, h, 0f));
            int botCenter = verts.Count; verts.Add(new Vector3(0f, -h, 0f));
            for (int i = 0; i < 8; i++)
            {
                int ni = (i + 1) % 8;
                tris.Add(topCenter); tris.Add(24 + ni); tris.Add(24 + i);
                tris.Add(botCenter); tris.Add(i); tris.Add(ni);
            }

            _beveledBoxMesh = new Mesh { name = "StudioBeveledBoxMesh" };
            _beveledBoxMesh.SetVertices(verts);
            _beveledBoxMesh.SetTriangles(tris, 0);
            _beveledBoxMesh.RecalculateNormals();
            _beveledBoxMesh.RecalculateBounds();
            return _beveledBoxMesh;
        }

        private static GameObject CreateWallBlock(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, Color col)
        {
            var b = new GameObject(name);
            b.transform.SetParent(parent, false);
            b.transform.localPosition = localPos;
            b.transform.localScale = localScale;
            var mf = b.AddComponent<MeshFilter>();
            mf.sharedMesh = GetOrCreateBeveledBoxMesh();
            b.AddComponent<MeshRenderer>();
            LevelLoader.ApplyMaterialOrColor(b, mat, col);
            var mr = b.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null && mat == null)
            {
                if (mr.sharedMaterial.HasProperty("_Smoothness"))
                    mr.sharedMaterial.SetFloat("_Smoothness", 0.76f);
            }
            return b;
        }

        private static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        private void UpdateCameraImmediate()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out _, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 9.6f, out Vector3 lookAheadPt, out _, out _);

            float elev = _deckBaseY + _airborneY * 0.45f;
            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 camPos = roadCenter - horizForward * 5.9f + Vector3.up * (5.45f + elev) + roadRight * (CurrentLateralOffset * 0.24f);
            mainCamera.transform.position = camPos;
            mainCamera.transform.rotation = Quaternion.LookRotation((lookAheadPt + Vector3.up * (elev - 0.30f)) - camPos, Vector3.up);
            mainCamera.fieldOfView = 49f;
        }

        private void UpdateCameraSmooth(float dt)
        {
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out _, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 9.6f, out Vector3 lookAheadPt, out _, out _);

            float elev = _deckBaseY + _airborneY * 0.45f;
            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 desiredPos = roadCenter - horizForward * 5.9f + Vector3.up * (5.45f + elev) + roadRight * (CurrentLateralOffset * 0.24f);

            if (_cameraShakeTimer > 0f)
            {
                _cameraShakeTimer = Mathf.Max(0f, _cameraShakeTimer - dt);
                desiredPos += Random.insideUnitSphere * (_cameraShakeAmp * (_cameraShakeTimer / 0.24f));
            }

            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPos, Mathf.Clamp01(dt * 8.5f));

            Quaternion desiredRot = Quaternion.LookRotation((lookAheadPt + Vector3.up * (elev - 0.30f)) - mainCamera.transform.position, Vector3.up);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, desiredRot, Mathf.Clamp01(dt * 8.5f));

            _cameraFovPunch = Mathf.Lerp(_cameraFovPunch, 0f, Mathf.Clamp01(dt * 7.5f));
            float speedFov = _speedBoostTimer > 0f ? 5.5f : 0f;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, 49f + speedFov + _cameraFovPunch, Mathf.Clamp01(dt * 10f));
        }
    }
}
