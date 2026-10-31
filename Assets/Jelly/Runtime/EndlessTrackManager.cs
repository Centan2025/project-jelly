using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Live State")]
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
        public string StatusBanner = "Engellere göre Büyü (+), Küçül (-) veya Bölün (÷2)!";
        public float StatusBannerTimer = 6f;

        private JellyLevelBlueprint _currentBlueprint;
        private GameObject _secondarySplitVisual;
        private GameObject _jellyBridgeVisual;
        private GameObject _primaryShadowDisc;
        private GameObject _secondaryShadowDisc;

        // Vertical jump / double-decker elevation physics
        private float _airborneY;
        private float _verticalVelocity;
        private float _deckBaseY;
        private int _currentDyeColorIndex = -1; // -1=Default, 0=Cyan, 1=Gold, 2=Magenta

        private Vector2 _dragPrevPos;
        private bool _dragging;
        private float _bounceCooldown;
        private float _speedBoostTimer;
        private float _manualSplitCooldown;
        private float _smoothedSteerInput;
        private float _currentSplitBlend;
        private float _splitBlendVelocity;
        private float _displayedMass = 90f;
        private float _wobbleAmplitude;
        private float _wobblePhase;
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
            new BiomePalette
            {
                Name = "GÖKYÜZÜ VADİSİ",
                SkyColor = new Color(0.32f, 0.62f, 0.92f),
                RoadSurfaceColor = new Color(0.92f, 0.96f, 1.00f),
                CurbColor = new Color(0.98f, 0.42f, 0.36f),
                StripeColor = new Color(0.18f, 0.78f, 0.98f),
                PropColor = new Color(0.28f, 0.82f, 0.98f),
                CloudColor = new Color(0.96f, 0.98f, 1.00f)
            },
            new BiomePalette
            {
                Name = "GÜN BATIMI KANYONU",
                SkyColor = new Color(0.28f, 0.20f, 0.48f),
                RoadSurfaceColor = new Color(0.96f, 0.90f, 0.94f),
                CurbColor = new Color(0.98f, 0.28f, 0.58f),
                StripeColor = new Color(1.00f, 0.62f, 0.20f),
                PropColor = new Color(0.92f, 0.38f, 0.86f),
                CloudColor = new Color(1.00f, 0.82f, 0.88f)
            },
            new BiomePalette
            {
                Name = "ZÜMRÜT ADALARI",
                SkyColor = new Color(0.16f, 0.52f, 0.56f),
                RoadSurfaceColor = new Color(0.90f, 0.98f, 0.94f),
                CurbColor = new Color(0.98f, 0.74f, 0.18f),
                StripeColor = new Color(0.16f, 0.86f, 0.54f),
                PropColor = new Color(0.22f, 0.92f, 0.60f),
                CloudColor = new Color(0.90f, 1.00f, 0.96f)
            }
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
            CurrentLevelNumber = HighestUnlockedLevel;
        }

        private void Start()
        {
            if (lateralSteerSpeed > 4.5f) lateralSteerSpeed = 4.2f;
            sliceLength = 12.0f;
            if (mainCamera == null) mainCamera = Camera.main;
            StartLevel(CurrentLevelNumber);
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
            _bounceCooldown = 0f;
            _speedBoostTimer = 0f;
            _manualSplitCooldown = 0f;

            SetBanner($"{_currentBlueprint.LevelTitle} — Rampa, Çatal Köprü ve Lazerlere Dikkat!", 4.5f);

            EnsurePlayerObjects();
            BuildEntireLevelMap(_currentBlueprint);
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
            StartLevel(1);
        }

        private BiomePalette GetCurrentBiome()
        {
            int idx = _currentBlueprint != null ? (_currentBlueprint.BiomeIndex % Biomes.Length) : 0;
            return Biomes[idx];
        }

        private void UpdateEnvironmentVisuals()
        {
            if (mainCamera == null) return;
            BiomePalette b = GetCurrentBiome();
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = b.SkyColor;
            mainCamera.fieldOfView = 54f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 52f;
            RenderSettings.fogEndDistance = 115f;
            RenderSettings.fogColor = b.SkyColor;
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
                }

                var oldHighlight = primaryPlayer.transform.Find("GlossHighlight");
                if (oldHighlight != null) Destroy(oldHighlight.gameObject);
                var oldCore = primaryPlayer.transform.Find("JellyInnerCore");
                if (oldCore != null) Destroy(oldCore.gameObject);

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
                }
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

            if (_primaryShadowDisc == null) _primaryShadowDisc = CreateShadowDisc("PrimaryShadowDisc");
            if (_secondaryShadowDisc == null) _secondaryShadowDisc = CreateShadowDisc("SecondaryShadowDisc");
        }

        private GameObject CreateShadowDisc(string name)
        {
            var existing = trackRoot.Find(name);
            if (existing != null) return existing.gameObject;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            DestroyCollider(disc);
            disc.transform.SetParent(trackRoot, false);
            LevelLoader.ApplyMaterialOrColor(disc, null, new Color(0.08f, 0.12f, 0.22f, 0.65f));
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
        }

        public static void EvaluateRoadCurve(float z, out Vector3 pos, out Quaternion rot, out Vector3 right)
        {
            float blend = Mathf.Clamp01((z - 6f) / 14f) * _activeCurveScale;
            float x = blend * (Mathf.Sin(z * 0.045f) * 5.8f + Mathf.Sin(z * 0.095f) * 2.0f);
            float y = blend * (Mathf.Sin(z * 0.052f) * 1.7f + Mathf.Cos(z * 0.105f) * 0.65f - 0.65f);

            float dz = 0.25f;
            float z2 = z + dz;
            float blend2 = Mathf.Clamp01((z2 - 6f) / 14f) * _activeCurveScale;
            float x2 = blend2 * (Mathf.Sin(z2 * 0.045f) * 5.8f + Mathf.Sin(z2 * 0.095f) * 2.0f);
            float y2 = blend2 * (Mathf.Sin(z2 * 0.052f) * 1.7f + Mathf.Cos(z2 * 0.105f) * 0.65f - 0.65f);

            Vector3 tangent = new Vector3(x2 - x, y2 - y, dz).normalized;
            float bankAngle = Mathf.Clamp(-(x2 - x) / dz * 6.5f, -9f, 9f);
            rot = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0f, 0f, bankAngle);
            right = rot * Vector3.right;
            pos = new Vector3(x, y, z);
        }

        public void ToggleManualSplitMerge()
        {
            if (_manualSplitCooldown > 0f || IsGameOver || IsLevelCompleted) return;
            _manualSplitCooldown = 0.22f;
            IsSplit = !IsSplit;
            _wobbleAmplitude = 0.26f;
            _wobblePhase = 0f;
            SetBanner(IsSplit
                ? "🟣 İKİYE BÖLÜNDÜN (÷2)! Çatal köprülerden ve ikiz tünellerden geçebilirsin!"
                : "🟡 TEKRAR BİRLEŞTİN (><)! Tek parça halinde ilerliyorsun!", 2.2f);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) { StartLevel(CurrentLevelNumber); return; }
                if (kb.nKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame) { LoadNextProgressiveLevel(); return; }
                if (kb.pKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) { LoadPreviousProgressiveLevel(); return; }
            }

            if (IsLevelCompleted)
            {
                if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                    (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    LoadNextProgressiveLevel();
                }
                return;
            }

            if (IsGameOver)
            {
                if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                    (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    StartLevel(CurrentLevelNumber);
                }
                return;
            }

            float dt = Time.deltaTime;
            if (StatusBannerTimer > 0f) StatusBannerTimer -= dt;
            if (_bounceCooldown > 0f) _bounceCooldown -= dt;
            if (_speedBoostTimer > 0f) _speedBoostTimer -= dt;
            if (_manualSplitCooldown > 0f) _manualSplitCooldown -= dt;

            bool manualTogglePressed = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) ||
                                       (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
            if (manualTogglePressed)
            {
                ToggleManualSplitMerge();
            }

            // 1. Sample horizontal steering + Surface Hazard modifiers (Ice Drift / Conveyor Push)
            float rawKeySteer = 0f;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawKeySteer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawKeySteer += 1f;
            }

            bool onIce = false;
            float conveyorDelta = 0f;
            EvaluateActiveSegmentHazards(dt, ref onIce, ref conveyorDelta);

            float accelRate = onIce
                ? 2.8f
                : (Mathf.Abs(rawKeySteer) > 0.01f ? steerAcceleration : steerDeceleration);
            _smoothedSteerInput = Mathf.MoveTowards(_smoothedSteerInput, rawKeySteer, accelRate * dt);

            float dragDeltaOffset = 0f;
            float screenWidth = Mathf.Max(400f, Screen.width);
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                Vector2 p = ts.primaryTouch.position.ReadValue();
                if (!_dragging) { _dragging = true; _dragPrevPos = p; }
                dragDeltaOffset = ((p.x - _dragPrevPos.x) / screenWidth) * roadWidth * 0.90f;
                _dragPrevPos = p;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                if (!_dragging) { _dragging = true; _dragPrevPos = p; }
                dragDeltaOffset = ((p.x - _dragPrevPos.x) / screenWidth) * roadWidth * 0.90f;
                _dragPrevPos = p;
            }
            else
            {
                _dragging = false;
            }

            float maxOffset = IsSplit ? maxLateralOffset - 0.80f : maxLateralOffset;
            float nextOffset = CurrentLateralOffset + (_smoothedSteerInput * lateralSteerSpeed * dt) + dragDeltaOffset + conveyorDelta;

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
            if (_bounceCooldown > 0f)
                speed = -7.2f;

            CurrentDistance = Mathf.Max(0f, CurrentDistance + speed * dt);

            UpdateVerticalAndSegmentMechanics(dt);

            // 3. Position & animate Jelly (single or split twin) on the 3D spline road
            SyncJellyVisualsAndPosition(dt * speed);

            // 4. Check pickup collection for both primary and secondary split pieces
            CheckPickups();

            // 5. Evaluate cutout and moving obstacles as player crosses their Z plane
            CheckObstacles();

            // 6. Smoothly follow the winding road with the camera
            UpdateCameraSmooth(dt);
        }

        private void EvaluateActiveSegmentHazards(float dt, ref bool onIce, ref float conveyorDelta)
        {
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
            if (CurrentLevelNumber >= HighestUnlockedLevel)
            {
                HighestUnlockedLevel = CurrentLevelNumber + 1;
                PlayerPrefs.SetInt("JellyHighestLevel", HighestUnlockedLevel);
            }
            SetBanner($"🎉 SEVİYE {CurrentLevelNumber} TAMAMLANDI! (x{LastMultiplierReached} Çarpan Bonusu!)", 999f);
        }

        public static float ComputeVisualDiameter(float mass)
        {
            float normalized = Mathf.Clamp(mass, 25f, 220f) / 90f;
            return 1.02f * Mathf.Pow(normalized, 0.58f);
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
            float wobble = Mathf.Sin(_wobblePhase) * _wobbleAmplitude;
            float scaleX = diameter * (1f + stretchPhase * 0.36f + wobble);
            float scaleY = diameter * Mathf.Max(0.55f, 1f - stretchPhase * 0.22f - wobble * 0.8f);
            float scaleZ = diameter * (1f - stretchPhase * 0.12f + wobble * 0.4f);
            Vector3 jellyScale = new Vector3(scaleX, scaleY, scaleZ);

            Color stateTint;
            if (_currentDyeColorIndex >= 0 && _currentDyeColorIndex < DyeColors.Length)
            {
                stateTint = DyeColors[_currentDyeColorIndex];
            }
            else
            {
                stateTint = CurrentMass >= 115f
                    ? new Color(0.16f, 0.94f, 0.52f, 0.94f)
                    : (CurrentMass <= 75f ? new Color(0.28f, 0.88f, 1.00f, 0.94f) : new Color(0.14f, 0.74f, 0.98f, 0.94f));
            }
            LevelLoader.ApplyMaterialOrColor(primaryPlayer.gameObject, jellyMaterial, stateTint);
            if (_secondarySplitVisual != null)
                LevelLoader.ApplyMaterialOrColor(_secondarySplitVisual, jellyMaterial, stateTint);

            if (primaryPlayer.massController != null)
                primaryPlayer.massController.SetMassImmediate(effectiveSphereMass);

            float totalElevation = _deckBaseY + _airborneY;

            if (_currentSplitBlend <= 0.005f && !IsSplit)
            {
                Vector3 deckGroundPos = roadCenter + roadRight * CurrentLateralOffset + roadUp * _deckBaseY;
                Vector3 worldPos = deckGroundPos + roadUp * (_airborneY + scaleY * 0.5f + 0.02f);
                primaryPlayer.transform.position = worldPos;
                primaryPlayer.transform.localScale = jellyScale;
                primaryPlayer.transform.rotation = roadRot;

                if (_primaryShadowDisc != null)
                {
                    _primaryShadowDisc.SetActive(true);
                    _primaryShadowDisc.transform.position = deckGroundPos + roadUp * 0.02f;
                    _primaryShadowDisc.transform.rotation = roadRot;
                    float shadowShrink = Mathf.Clamp(1f - _airborneY * 0.18f, 0.4f, 1f);
                    _primaryShadowDisc.transform.localScale = new Vector3(scaleX * 0.92f * shadowShrink, 0.015f, scaleZ * 0.92f * shadowShrink);
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
                Vector3 centerWorld = centerGround + roadUp * (_airborneY + scaleY * 0.5f + 0.02f);
                Vector3 leftPos = centerWorld - roadRight * currentSpread;
                Vector3 rightPos = centerWorld + roadRight * currentSpread;

                primaryPlayer.transform.position = leftPos;
                primaryPlayer.transform.localScale = jellyScale;
                primaryPlayer.transform.rotation = roadRot;

                if (_secondarySplitVisual != null)
                {
                    if (!_secondarySplitVisual.activeSelf) _secondarySplitVisual.SetActive(true);
                    _secondarySplitVisual.transform.position = rightPos;
                    _secondarySplitVisual.transform.localScale = jellyScale;
                    _secondarySplitVisual.transform.rotation = roadRot;
                }

                if (_primaryShadowDisc != null)
                {
                    _primaryShadowDisc.SetActive(true);
                    _primaryShadowDisc.transform.position = (centerGround - roadRight * currentSpread) + roadUp * 0.02f;
                    _primaryShadowDisc.transform.rotation = roadRot;
                    _primaryShadowDisc.transform.localScale = new Vector3(scaleX * 0.92f, 0.015f, scaleZ * 0.92f);
                }
                if (_secondaryShadowDisc != null)
                {
                    _secondaryShadowDisc.SetActive(true);
                    _secondaryShadowDisc.transform.position = (centerGround + roadRight * currentSpread) + roadUp * 0.02f;
                    _secondaryShadowDisc.transform.rotation = roadRot;
                    _secondaryShadowDisc.transform.localScale = new Vector3(scaleX * 0.92f, 0.015f, scaleZ * 0.92f);
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
        }

        public void ApplyPickup(EndlessPickupType type, float amount, Vector3 pickupWorldPos = default)
        {
            if (pickupWorldPos == default && primaryPlayer != null)
                pickupWorldPos = primaryPlayer.transform.position;

            switch (type)
            {
                case EndlessPickupType.Grow:
                    CurrentMass = Mathf.Clamp(CurrentMass + amount, 25f, 220f);
                    _wobbleAmplitude = 0.24f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(15);
                    SpawnJellySplash(pickupWorldPos, new Color(0.22f, 0.95f, 0.48f), 7);
                    SetBanner($"🟢 BÜYÜDÜN! (+{amount:F0} Kütle -> {CurrentMass:F0})", 2.0f);
                    break;

                case EndlessPickupType.Shrink:
                    CurrentMass = Mathf.Clamp(CurrentMass - amount, 35f, 220f);
                    _wobbleAmplitude = 0.22f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(15);
                    SpawnJellySplash(pickupWorldPos, new Color(0.98f, 0.30f, 0.38f), 7);
                    SetBanner($"🔴 KÜÇÜLDÜN! (-{amount:F0} Kütle -> {CurrentMass:F0}) Dar geçitten sığabilirsin!", 2.0f);
                    break;

                case EndlessPickupType.Split:
                    if (!IsSplit)
                    {
                        IsSplit = true;
                        _wobbleAmplitude = 0.28f;
                        _wobblePhase = 0f;
                        AddScoreWithCombo(20);
                        SpawnJellySplash(pickupWorldPos, new Color(0.75f, 0.35f, 1.00f), 9);
                        SetBanner("🟣 İKİYE BÖLÜNDÜN! (÷2) Çatal köprüden ve ikiz tünelden geçebilirsin!", 2.5f);
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
                        SetBanner("🟡 TEKRAR BİRLEŞTİN! (><) Tek büyük jöle oldun!", 2.5f);
                    }
                    break;

                case EndlessPickupType.SpeedBoost:
                    _speedBoostTimer = 2.2f;
                    _wobbleAmplitude = 0.25f;
                    _wobblePhase = 0f;
                    AddScoreWithCombo(25);
                    SpawnJellySplash(pickupWorldPos, new Color(0.20f, 0.92f, 1.00f), 10);
                    SetBanner("⚡ SÜPER HIZ BANDI! (+25 Puan)", 2.0f);
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

        private static void SpawnJellySplash(Vector3 pos, Color col, int count)
        {
            if (!Application.isPlaying) return;
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

        private void CheckPickups()
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
                {
                    if (IsSplit)
                    {
                        bool leftInHole = Mathf.Abs(leftPieceX) <= 0.88f;
                        bool rightInHole = Mathf.Abs(rightPieceX) <= 0.88f;
                        if (leftInHole || rightInHole)
                        {
                            float survivingX = leftInHole ? leftPieceX : rightPieceX;
                            SacrificeOneSplitPieceAndContinue(obs, survivingX,
                                "⚠ TEK DELİK! Delikten geçen parçayla devam (-%50 Kütle)!");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 DUVARA ÇARPTIN (-22 Kütle)! Ortadaki deliğe hizalan!");
                        }
                        return;
                    }

                    bool alignedCenter = Mathf.Abs(CurrentLateralOffset) <= 0.92f;
                    bool smallEnough = CurrentMass <= obs.maxAllowedMassForSmallHole + 8f;
                    if (alignedCenter && smallEnough)
                    {
                        RegisterCleanGatePass(obs, 35, "✓ DAR GEÇİTTEN SÜZÜLDÜN! (+35 Puan)");
                    }
                    else
                    {
                        string reason = !alignedCenter
                            ? "💥 DUVARA ÇARPTIN (-22 Kütle)! Ortadaki deliğe yönel!"
                            : $"💥 DELİK İÇİN BÜYÜKTÜN (-22 Kütle eridi -> {Mathf.Max(0f, CurrentMass - 22f):F0})! Şimdi delikten geç!";
                        TriggerSolidWallBounce(obs, reason);
                    }
                    break;
                }

                case EndlessObstacleType.TwinSplitTunnels:
                case EndlessObstacleType.MovingTwinTunnels:
                {
                    float shift = obs.obstacleType == EndlessObstacleType.MovingTwinTunnels ? obs.CurrentOpeningCenterX : 0f;
                    if (IsSplit)
                    {
                        bool leftInTunnel = leftPieceX >= (-2.35f + shift) && leftPieceX <= (-0.42f + shift);
                        bool rightInTunnel = rightPieceX >= (0.42f + shift) && rightPieceX <= (2.35f + shift);

                        if (leftInTunnel && rightInTunnel)
                        {
                            RegisterCleanGatePass(obs, 40, "✓ İKİLİ TÜNELDEN BÖLÜNEREK GEÇTİN! (+40 Puan)");
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

                case EndlessObstacleType.HeavySmashWall:
                {
                    bool alignedCenter = Mathf.Abs(CurrentLateralOffset) <= 1.55f;
                    bool heavyEnough = CurrentMass >= obs.minRequiredMassForSmash - 8f;
                    if (alignedCenter && heavyEnough)
                    {
                        obs.ShatterBreakablePiece();
                        RegisterCleanGatePass(obs, 45, "💥 DEV JÖLEYLE CAM DUVARI PARÇALADIN! (+45 Puan)");
                    }
                    else if (alignedCenter && obs.hasBouncedOnce)
                    {
                        CurrentMass -= 22f;
                        if (CurrentMass < 22f)
                        {
                            TriggerGameOver("💥 CAM DUVARI KIRARKEN TÜM KÜTLEN TÜKENDİ!");
                            return;
                        }
                        obs.ShatterBreakablePiece();
                        obs.resolved = true;
                        SetBanner("⚠ CAM DUVARI ZORLA ÇATLATIP GEÇTİN (-22 Kütle)!", 2.5f);
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, $"💥 CAM DUVAR İÇİN HAFİFTİN (Kütle {CurrentMass:F0} < {obs.minRequiredMassForSmash:F0})! (-22 Kütle)");
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

                case EndlessObstacleType.VerticalCrusherGate:
                {
                    float crusherY = obs.CurrentCrusherBottomY;
                    float effectiveMass = IsSplit ? CurrentMass * 0.5f : CurrentMass;
                    float jellyHeight = ComputeVisualDiameter(effectiveMass);

                    if (crusherY >= jellyHeight - 0.14f || effectiveMass <= 78f)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ PİSTON KAPININ ALTINDAN GEÇTİN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 İNEN PİSTONA ÇARPTIN (-22 Kütle)! Piston kalkınca geç!");
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
                            RegisterCleanGatePass(obs, 40, "✓ DEV SARKAÇTAN SIYRILDIN! (+40 Puan)");
                        }
                        else if (!leftHit || !rightHit)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, !leftHit ? leftPieceX : rightPieceX,
                                "⚠ SARKAÇ BİR PARÇAYA ÇARPTI! Sağlam parçayla devam!");
                        }
                        else
                        {
                            TriggerSolidWallBounce(obs, "💥 DEV SARKACA ÇARPTIN (-22 Kütle)!");
                        }
                        return;
                    }

                    if (Mathf.Abs(CurrentLateralOffset - headX) >= hitRadius)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ DEV SARKAÇTAN KAÇTIN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerSolidWallBounce(obs, "💥 DEV SARKACA ÇARPTIN (-22 Kütle)! Ters yöne kaç!");
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
            _wobbleAmplitude = 0.18f;
            _wobblePhase = 0f;
            if (primaryPlayer != null)
                SpawnJellySplash(primaryPlayer.transform.position, new Color(0.25f, 0.98f, 0.65f), 8);
            int mult = Mathf.Clamp(1 + ComboCount / 2, 1, 5);
            SetBanner(mult > 1 ? $"{bannerMsg} [🔥 x{mult} KOMBO!]" : bannerMsg, 2.2f);
        }

        private void SacrificeOneSplitPieceAndContinue(EndlessGateObstacle obs, float survivingLateralX, string message)
        {
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
                SpawnJellySplash(primaryPlayer.transform.position, new Color(1f, 0.40f, 0.40f), 10);

            if (CurrentMass < 22f)
            {
                TriggerGameOver("💥 JÖLE ÇOK KÜÇÜLDÜ VE ERİDİ! (Kütle Tükendi)");
                return;
            }
            SetBanner(message, 2.8f);
        }

        private void TriggerSolidWallBounce(EndlessGateObstacle obs, string reason)
        {
            ComboCount = 0;
            obs.hasBouncedOnce = true;
            _wobbleAmplitude = 0.38f;
            _wobblePhase = 0f;
            CurrentMass -= 22f;

            if (primaryPlayer != null)
                SpawnJellySplash(primaryPlayer.transform.position, new Color(1f, 0.35f, 0.35f), 10);

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
            BiomePalette biome = Biomes[bp.BiomeIndex % Biomes.Length];

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
                            SpawnObstacleAndPreparationPickups(zStart + 9f, spec.GateType);
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
            var rampGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rampGo.name = "LaunchRampWedge";
            DestroyCollider(rampGo);
            rampGo.transform.SetParent(root.transform, false);
            rampGo.transform.position = rampPos + (rampRot * Vector3.up) * 0.32f;
            rampGo.transform.rotation = rampRot * Quaternion.Euler(-16f, 0f, 0f);
            rampGo.transform.localScale = new Vector3(4.6f, 0.45f, 2.6f);
            LevelLoader.ApplyMaterialOrColor(rampGo, null, new Color(1.00f, 0.78f, 0.16f));

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
            var upRamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            upRamp.name = "OverpassEntryRamp";
            DestroyCollider(upRamp);
            upRamp.transform.SetParent(root.transform, false);
            upRamp.transform.position = rPos + (rRot * Vector3.up) * 0.42f;
            upRamp.transform.rotation = rRot * Quaternion.Euler(-20f, 0f, 0f);
            upRamp.transform.localScale = new Vector3(3.4f, 0.45f, 2.8f);
            LevelLoader.ApplyMaterialOrColor(upRamp, null, new Color(0.22f, 0.92f, 0.62f));

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
                bool isFinal = (finaleChunkIdx == 1 && k == 1);

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

                CreateWallBlock(nodeGo.transform, "PillarL", new Vector3(-2.85f, 1.3f, 0f), new Vector3(0.6f, 2.6f, 0.55f), null, new Color(0.18f, 0.22f, 0.34f));
                CreateWallBlock(nodeGo.transform, "PillarR", new Vector3(+2.85f, 1.3f, 0f), new Vector3(0.6f, 2.6f, 0.55f), null, new Color(0.18f, 0.22f, 0.34f));
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
            float curbWidth = 0.42f;
            float curbHeight = 0.32f;
            float roadThickness = 0.45f;

            CreateSubRibbonObject(chunkGo.transform, "RoadSurfaceMesh", zStart, zEnd, steps, -halfRoad, +halfRoad, -roadThickness, 0f, yOffset, customRoadColor ?? biome.RoadSurfaceColor);
            CreateSubRibbonObject(chunkGo.transform, "CenterStripeMesh", zStart, zEnd, steps, -0.10f, +0.10f, 0.0f, 0.025f, yOffset, biome.StripeColor);
            CreateSubRibbonObject(chunkGo.transform, "LeftCurbMesh", zStart, zEnd, steps, -halfRoad - curbWidth, -halfRoad, -0.1f, curbHeight, yOffset, biome.CurbColor);
            CreateSubRibbonObject(chunkGo.transform, "RightCurbMesh", zStart, zEnd, steps, +halfRoad, +halfRoad + curbWidth, -0.1f, curbHeight, yOffset, biome.CurbColor);

            return chunkGo;
        }

        private GameObject CreateSubRibbonObject(Transform parent, string name, float zStart, float zEnd, int steps, float xMin, float xMax, float yMin, float yMax, float yOffset, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            mf.sharedMesh = CreateExtrudedRibbonMesh(zStart, zEnd, steps, xMin, xMax, yMin + yOffset, yMax + yOffset);
            LevelLoader.ApplyMaterialOrColor(go, null, color);
            return go;
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
            Vector3 up = rot * Vector3.up;
            float sideDist = roadWidth * 0.5f + 2.6f;
            float h = 3.2f + (chunkIdx % 3) * 1.1f;

            var leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPillar.name = "SceneryPillarL";
            DestroyCollider(leftPillar);
            leftPillar.transform.SetParent(chunkRoot, false);
            leftPillar.transform.position = center - right * sideDist + up * (h * 0.35f - 0.5f);
            leftPillar.transform.rotation = rot * Quaternion.Euler(0f, 0f, 6f);
            leftPillar.transform.localScale = new Vector3(0.65f, h, 0.65f);
            LevelLoader.ApplyMaterialOrColor(leftPillar, null, biome.PropColor);

            var rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPillar.name = "SceneryPillarR";
            DestroyCollider(rightPillar);
            rightPillar.transform.SetParent(chunkRoot, false);
            rightPillar.transform.position = center + right * sideDist + up * (h * 0.35f - 0.5f);
            rightPillar.transform.rotation = rot * Quaternion.Euler(0f, 0f, -6f);
            rightPillar.transform.localScale = new Vector3(0.65f, h, 0.65f);
            LevelLoader.ApplyMaterialOrColor(rightPillar, null, biome.PropColor);

            float cloudSide = (chunkIdx % 2 == 0 ? -1f : 1f) * (10.5f + (chunkIdx % 3) * 3.2f);
            float cloudHeight = 5.5f + (chunkIdx % 4) * 1.6f;
            var cloudGroup = new GameObject("SkyCloud").transform;
            cloudGroup.SetParent(chunkRoot, false);
            cloudGroup.position = center + right * cloudSide + Vector3.up * cloudHeight;

            var puffMain = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(puffMain);
            puffMain.transform.SetParent(cloudGroup, false);
            puffMain.transform.localScale = new Vector3(4.2f, 1.6f, 2.6f);
            LevelLoader.ApplyMaterialOrColor(puffMain, null, biome.CloudColor);

            var puffSub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(puffSub);
            puffSub.transform.SetParent(cloudGroup, false);
            puffSub.transform.localPosition = new Vector3(1.4f, 0.4f, -0.3f);
            puffSub.transform.localScale = new Vector3(2.8f, 1.4f, 2.2f);
            LevelLoader.ApplyMaterialOrColor(puffSub, null, biome.CloudColor);
        }

        private void SpawnObstacleAndPreparationPickups(float gateZ, EndlessObstacleType gateType)
        {
            switch (gateType)
            {
                case EndlessObstacleType.SmallCenterHole:
                    SpawnPickupOnRoad(gateZ - 7.5f, -1.1f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 7.5f, +1.1f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 4.2f, 0f, EndlessPickupType.Shrink, 25f);
                    BuildCutoutGate(gateZ, gateType);
                    break;

                case EndlessObstacleType.TwinSplitTunnels:
                case EndlessObstacleType.MovingTwinTunnels:
                    SpawnPickupOnRoad(gateZ - 7.0f, -1.2f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 7.0f, 0f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 7.0f, +1.2f, EndlessPickupType.Split, 0f);
                    BuildCutoutGate(gateZ, gateType);
                    SpawnPickupOnRoad(gateZ + 4.5f, 0f, EndlessPickupType.Merge, 0f);
                    break;

                case EndlessObstacleType.HeavySmashWall:
                    SpawnPickupOnRoad(gateZ - 7.5f, -1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 7.5f, +1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 4.2f, 0f, EndlessPickupType.Grow, 35f);
                    BuildCutoutGate(gateZ, gateType);
                    break;

                default:
                    SpawnPickupOnRoad(gateZ - 6.5f, -1.2f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 6.5f, +1.2f, EndlessPickupType.SpeedBoost, 0f);
                    BuildCutoutGate(gateZ, gateType);
                    break;
            }
        }

        private void SpawnPickupOnRoad(float z, float lateralOffset, EndlessPickupType type, float amount)
        {
            EvaluateRoadCurve(z, out Vector3 center, out Quaternion rot, out Vector3 right);
            Vector3 up = rot * Vector3.up;
            Vector3 pos = center + right * lateralOffset + up * (type == EndlessPickupType.SpeedBoost ? 0.08f : 0.68f);

            GameObject go;
            Material mat;
            Color col;

            switch (type)
            {
                case EndlessPickupType.Grow:
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "Pickup_Grow (+)";
                    go.transform.localScale = Vector3.one * 0.85f;
                    mat = pickupMaterial;
                    col = new Color(0.18f, 0.94f, 0.46f);
                    var vBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCollider(vBar);
                    vBar.transform.SetParent(go.transform, false);
                    vBar.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    vBar.transform.localScale = new Vector3(0.22f, 0.7f, 0.22f);
                    LevelLoader.ApplyMaterialOrColor(vBar, pickupMaterial, col);
                    var hBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCollider(hBar);
                    hBar.transform.SetParent(go.transform, false);
                    hBar.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    hBar.transform.localScale = new Vector3(0.7f, 0.22f, 0.22f);
                    LevelLoader.ApplyMaterialOrColor(hBar, pickupMaterial, col);
                    break;

                case EndlessPickupType.Shrink:
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "Pickup_Shrink (-)";
                    go.transform.localScale = Vector3.one * 0.78f;
                    mat = drainMaterial;
                    col = new Color(0.98f, 0.28f, 0.36f);
                    var mBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCollider(mBar);
                    mBar.transform.SetParent(go.transform, false);
                    mBar.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                    mBar.transform.localScale = new Vector3(0.75f, 0.22f, 0.22f);
                    LevelLoader.ApplyMaterialOrColor(mBar, drainMaterial, col);
                    break;

                case EndlessPickupType.Split:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Pickup_Split (÷2)";
                    go.transform.localScale = new Vector3(0.78f, 0.78f, 0.78f);
                    go.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                    mat = null;
                    col = new Color(0.75f, 0.32f, 0.98f);
                    break;

                case EndlessPickupType.SpeedBoost:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Pickup_SpeedPad (⚡)";
                    go.transform.localScale = new Vector3(1.5f, 0.12f, 2.2f);
                    go.transform.localRotation = rot;
                    mat = null;
                    col = new Color(0.18f, 0.92f, 1.00f);
                    break;

                default: // Merge
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "Pickup_Merge (><)";
                    go.transform.localScale = new Vector3(0.95f, 0.25f, 0.95f);
                    mat = goalMaterial;
                    col = new Color(0.98f, 0.82f, 0.22f);
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

        private void BuildCutoutGate(float z, EndlessObstacleType type)
        {
            EvaluateRoadCurve(z, out Vector3 center, out Quaternion rot, out _);

            var gateRoot = new GameObject($"CutoutGate_{type}_{z:F0}");
            gateRoot.transform.SetParent(trackRoot, false);
            gateRoot.transform.SetPositionAndRotation(center, rot);
            var obs = gateRoot.AddComponent<EndlessGateObstacle>();
            obs.obstacleType = type;
            obs.trackZ = z;
            obs.animPhaseOffset = z * 0.21f;

            _spawnedChunks.Add(gateRoot);
            _activeObstacles.Add(obs);

            Color wallColor = new Color(0.94f, 0.42f, 0.34f);
            Color lintelColor = new Color(0.18f, 0.24f, 0.38f);
            Color neonCyan = new Color(0.18f, 0.84f, 0.98f);

            switch (type)
            {
                case EndlessObstacleType.SmallCenterHole:
                    CreateWallBlock(gateRoot.transform, "LeftWall", new Vector3(-1.82f, 1.15f, 0f), new Vector3(2.6f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightWall", new Vector3(+1.82f, 1.15f, 0f), new Vector3(2.6f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "TopHoleFrame", new Vector3(0f, 1.62f, 0f), new Vector3(1.1f, 1.35f, 0.58f), drainMaterial, new Color(0.96f, 0.28f, 0.36f));
                    CreateWallBlock(gateRoot.transform, "TopHeaderBeam", new Vector3(0f, 2.35f, 0f), new Vector3(6.3f, 0.22f, 0.65f), null, lintelColor);
                    break;

                case EndlessObstacleType.TwinSplitTunnels:
                    CreateWallBlock(gateRoot.transform, "OuterLeftPost", new Vector3(-2.75f, 1.15f, 0f), new Vector3(0.7f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "CenterSplitPillar", new Vector3(0f, 1.15f, 0f), new Vector3(1.05f, 2.3f, 0.65f), null, new Color(0.74f, 0.32f, 0.98f));
                    CreateWallBlock(gateRoot.transform, "OuterRightPost", new Vector3(+2.75f, 1.15f, 0f), new Vector3(0.7f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "TopTwinArch", new Vector3(0f, 2.15f, 0f), new Vector3(6.2f, 0.45f, 0.58f), null, lintelColor);
                    break;

                case EndlessObstacleType.MovingTwinTunnels:
                    var movingRig = new GameObject("MovingTwinRig").transform;
                    movingRig.SetParent(gateRoot.transform, false);
                    CreateWallBlock(movingRig, "OuterLeftPost", new Vector3(-2.45f, 1.15f, 0f), new Vector3(0.6f, 2.3f, 0.55f), null, neonCyan);
                    CreateWallBlock(movingRig, "CenterSplitPillar", new Vector3(0f, 1.15f, 0f), new Vector3(0.95f, 2.3f, 0.65f), null, new Color(0.78f, 0.35f, 1.00f));
                    CreateWallBlock(movingRig, "OuterRightPost", new Vector3(+2.45f, 1.15f, 0f), new Vector3(0.6f, 2.3f, 0.55f), null, neonCyan);
                    CreateWallBlock(movingRig, "TopTwinArch", new Vector3(0f, 2.15f, 0f), new Vector3(5.5f, 0.45f, 0.58f), null, lintelColor);
                    obs.movingPartA = movingRig;
                    break;

                case EndlessObstacleType.HeavySmashWall:
                    CreateWallBlock(gateRoot.transform, "LeftFrame", new Vector3(-2.3f, 1.15f, 0f), new Vector3(1.6f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightFrame", new Vector3(+2.3f, 1.15f, 0f), new Vector3(1.6f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "TopHeaderBeam", new Vector3(0f, 2.35f, 0f), new Vector3(6.2f, 0.25f, 0.65f), null, lintelColor);
                    var smashCenter = CreateWallBlock(gateRoot.transform, "BreakableSmashCenter", new Vector3(0f, 1.05f, 0f), new Vector3(2.8f, 2.1f, 0.38f), pickupMaterial, new Color(0.24f, 0.94f, 0.54f));
                    obs.breakableCenterPiece = smashCenter.transform;
                    break;

                case EndlessObstacleType.SlidingDoorWall:
                    var slideLeft = CreateWallBlock(gateRoot.transform, "SlidingDoorLeft", new Vector3(-1.8f, 1.15f, 0f), new Vector3(2.2f, 2.3f, 0.58f), null, neonCyan);
                    var slideRight = CreateWallBlock(gateRoot.transform, "SlidingDoorRight", new Vector3(+1.8f, 1.15f, 0f), new Vector3(2.2f, 2.3f, 0.58f), null, neonCyan);
                    CreateWallBlock(gateRoot.transform, "SlidingTopRail", new Vector3(0f, 2.35f, 0f), new Vector3(6.4f, 0.35f, 0.65f), null, lintelColor);
                    obs.movingPartA = slideLeft.transform;
                    obs.movingPartB = slideRight.transform;
                    break;

                case EndlessObstacleType.PincerShutters:
                    var pincerL = CreateWallBlock(gateRoot.transform, "PincerLeft", new Vector3(-1.8f, 1.15f, 0f), new Vector3(2.4f, 2.3f, 0.58f), null, new Color(0.98f, 0.58f, 0.20f));
                    var pincerR = CreateWallBlock(gateRoot.transform, "PincerRight", new Vector3(+1.8f, 1.15f, 0f), new Vector3(2.4f, 2.3f, 0.58f), null, new Color(0.98f, 0.58f, 0.20f));
                    CreateWallBlock(gateRoot.transform, "PincerTopRail", new Vector3(0f, 2.35f, 0f), new Vector3(6.4f, 0.35f, 0.65f), null, lintelColor);
                    obs.movingPartA = pincerL.transform;
                    obs.movingPartB = pincerR.transform;
                    break;

                case EndlessObstacleType.VerticalCrusherGate:
                    CreateWallBlock(gateRoot.transform, "LeftPost", new Vector3(-2.85f, 1.5f, 0f), new Vector3(0.55f, 3.0f, 0.58f), null, lintelColor);
                    CreateWallBlock(gateRoot.transform, "RightPost", new Vector3(+2.85f, 1.5f, 0f), new Vector3(0.55f, 3.0f, 0.58f), null, lintelColor);
                    CreateWallBlock(gateRoot.transform, "TopCrossbar", new Vector3(0f, 2.95f, 0f), new Vector3(6.2f, 0.4f, 0.62f), null, lintelColor);
                    var piston = CreateWallBlock(gateRoot.transform, "CrusherPiston", new Vector3(0f, 1.8f, 0f), new Vector3(5.0f, 1.5f, 0.52f), drainMaterial, new Color(0.96f, 0.32f, 0.42f));
                    obs.movingPartA = piston.transform;
                    break;

                case EndlessObstacleType.SwingingPendulum:
                    CreateWallBlock(gateRoot.transform, "ArchPostL", new Vector3(-2.95f, 1.9f, 0f), new Vector3(0.5f, 3.8f, 0.5f), null, lintelColor);
                    CreateWallBlock(gateRoot.transform, "ArchPostR", new Vector3(+2.95f, 1.9f, 0f), new Vector3(0.5f, 3.8f, 0.5f), null, lintelColor);
                    CreateWallBlock(gateRoot.transform, "ArchTop", new Vector3(0f, 3.8f, 0f), new Vector3(6.4f, 0.4f, 0.55f), null, lintelColor);

                    var pivot = new GameObject("PendulumPivot").transform;
                    pivot.SetParent(gateRoot.transform, false);
                    pivot.localPosition = new Vector3(0f, 3.8f, 0f);
                    CreateWallBlock(pivot, "PendulumRod", new Vector3(0f, -1.5f, 0f), new Vector3(0.18f, 3.0f, 0.18f), null, Color.white);
                    var hammer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    hammer.name = "PendulumHead";
                    DestroyCollider(hammer);
                    hammer.transform.SetParent(pivot, false);
                    hammer.transform.localPosition = new Vector3(0f, -3.0f, 0f);
                    hammer.transform.localScale = new Vector3(1.55f, 1.55f, 0.85f);
                    LevelLoader.ApplyMaterialOrColor(hammer, drainMaterial, new Color(1.00f, 0.48f, 0.15f));
                    obs.movingPartA = pivot;
                    break;

                case EndlessObstacleType.LeftOpeningWall:
                    CreateWallBlock(gateRoot.transform, "BlockedRightSide", new Vector3(+1.25f, 1.15f, 0f), new Vector3(3.7f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "LeftArchTop", new Vector3(-1.65f, 1.95f, 0f), new Vector3(2.2f, 0.7f, 0.55f), goalMaterial, lintelColor);
                    break;

                case EndlessObstacleType.RightOpeningWall:
                    CreateWallBlock(gateRoot.transform, "BlockedLeftSide", new Vector3(-1.25f, 1.15f, 0f), new Vector3(3.7f, 2.3f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightArchTop", new Vector3(+1.65f, 1.95f, 0f), new Vector3(2.2f, 0.7f, 0.55f), goalMaterial, lintelColor);
                    break;
            }
        }

        private static GameObject CreateWallBlock(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, Color col)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.localPosition = localPos;
            b.transform.localScale = localScale;
            DestroyCollider(b);
            LevelLoader.ApplyMaterialOrColor(b, mat, col);
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
            EvaluateRoadCurve(CurrentDistance + 10.5f, out Vector3 lookAheadPt, out _, out _);

            float elev = _deckBaseY + _airborneY * 0.45f;
            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 camPos = roadCenter - horizForward * 6.5f + Vector3.up * (4.4f + elev) + roadRight * (CurrentLateralOffset * 0.20f);
            mainCamera.transform.position = camPos;
            mainCamera.transform.rotation = Quaternion.LookRotation((lookAheadPt + Vector3.up * (elev - 0.25f)) - camPos, Vector3.up);
        }

        private void UpdateCameraSmooth(float dt)
        {
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out _, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 10.5f, out Vector3 lookAheadPt, out _, out _);

            float elev = _deckBaseY + _airborneY * 0.45f;
            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 desiredPos = roadCenter - horizForward * 6.5f + Vector3.up * (4.4f + elev) + roadRight * (CurrentLateralOffset * 0.20f);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPos, Mathf.Clamp01(dt * 8f));

            Quaternion desiredRot = Quaternion.LookRotation((lookAheadPt + Vector3.up * (elev - 0.25f)) - mainCamera.transform.position, Vector3.up);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, desiredRot, Mathf.Clamp01(dt * 8f));
        }

        private void EnsureFlatTex()
        {
            if (_flatTex == null)
            {
                _flatTex = new Texture2D(1, 1);
                _flatTex.SetPixel(0, 0, Color.white);
                _flatTex.Apply();
            }
        }

        private void DrawRect(Rect r, Color col)
        {
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(r, _flatTex);
            GUI.color = prev;
        }

        private void OnGUI()
        {
            EnsureFlatTex();

            Rect safe = Screen.safeArea;
            float safeLeft = safe.xMin;
            float safeTop = Screen.height - safe.yMax;
            float safeBottom = safe.yMin;
            float safeWidth = safe.width;

            float s = Mathf.Clamp(safeWidth / 680f, 0.85f, 2.25f);

            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(15f * s),
                fontStyle = FontStyle.Bold
            };

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(20f * s),
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(14f * s),
                fontStyle = FontStyle.Bold
            };
            subStyle.normal.textColor = new Color(0.86f, 0.94f, 1.00f);

            // 1. 3D World-Space Floating Badges for Gates, Special Segment Modules & Pickups
            if (mainCamera != null && !IsGameOver && !IsLevelCompleted)
            {
                for (int i = 0; i < _activeObstacles.Count; i++)
                {
                    var obs = _activeObstacles[i];
                    if (obs == null || obs.resolved) continue;
                    float dist = obs.trackZ - CurrentDistance;
                    if (dist < -1f || dist > 36f) continue;

                    DrawWorldSpaceBadge(obs.transform.position + Vector3.up * 2.95f, obs.GetGateHeaderText(), obs.GetGateHeaderColor(), s, badgeStyle);
                }

                for (int i = 0; i < _activeSegmentNodes.Count; i++)
                {
                    var node = _activeSegmentNodes[i];
                    if (node == null) continue;
                    float refZ = node.zStart + 3f;
                    if (node.moduleType == TrackSegmentModuleType.LevelFinaleMultiplier) refZ = node.wallZ;
                    float dist = refZ - CurrentDistance;
                    if (dist < -1f || dist > 36f) continue;

                    EvaluateRoadCurve(refZ, out Vector3 wPos, out _, out _);
                    switch (node.moduleType)
                    {
                        case TrackSegmentModuleType.SplitTwinBridge:
                            DrawWorldSpaceBadge(wPos + Vector3.up * 2.4f, "🛣 ÇATAL İKİZ KÖPRÜ (Ortası Uçurum! ÷2 Bölün)", new Color(0.80f, 0.42f, 1.00f), s, badgeStyle);
                            break;
                        case TrackSegmentModuleType.JumpRampOverAbyss:
                            DrawWorldSpaceBadge(wPos + Vector3.up * 2.8f, "🚀 UÇURUM RAMPASI & HAVA HALKASI", new Color(1.00f, 0.80f, 0.18f), s, badgeStyle);
                            break;
                        case TrackSegmentModuleType.DoubleDeckerOverpass:
                            DrawWorldSpaceBadge(wPos + Vector3.up * 3.8f, "🎢 2 KATLI ÜST GEÇİT (Ağır Jöle Camı Kırar!)", new Color(0.22f, 0.92f, 0.65f), s, badgeStyle);
                            break;
                        case TrackSegmentModuleType.LaserSlicerGate:
                            string lTxt = node.laserKind == LaserGateKind.BlueVerticalAutoSplitter ? "✂ MAVİ LAZER (Otomatik ÷2 Böler)" : "✂ KIRMIZI LAZER (Fazla Kütleyi Tıraşlar)";
                            DrawWorldSpaceBadge(wPos + Vector3.up * 2.9f, lTxt, new Color(0.25f, 0.88f, 1.00f), s, badgeStyle);
                            break;
                        case TrackSegmentModuleType.ColorMatchSection:
                            DrawWorldSpaceBadge(wPos + Vector3.up * 2.8f, $"🎨 HEDEF RENK: {DyeColorNames[node.requiredColorIndex]} BOYA HAVUZUNDAN GEÇ!", DyeColors[node.requiredColorIndex], s, badgeStyle);
                            break;
                        case TrackSegmentModuleType.LevelFinaleMultiplier:
                            if (!node.wallResolved)
                                DrawWorldSpaceBadge(wPos + Vector3.up * 2.65f, $"🏁 FİNAL x{node.multiplierValue} DUVARI (Kütle ≥ {node.requiredMassToBreak:F0})", new Color(1.00f, 0.84f, 0.22f), s, badgeStyle);
                            break;
                    }
                }

                for (int i = 0; i < _activePickups.Count; i++)
                {
                    var p = _activePickups[i];
                    if (p == null || p.IsConsumed || !p.gameObject.activeInHierarchy) continue;
                    float dist = p.trackZ - CurrentDistance;
                    if (dist < 0f || dist > 20f) continue;

                    Vector3 sp = mainCamera.WorldToScreenPoint(p.transform.position + Vector3.up * 1.05f);
                    if (sp.z > 0.5f)
                    {
                        float guiY = Screen.height - sp.y;
                        string txt = p.GetShortBadgeText();
                        float bw = 112f * s;
                        float bh = 28f * s;
                        Rect br = new Rect(sp.x - bw * 0.5f, guiY - bh * 0.5f, bw, bh);
                        DrawRect(br, new Color(0.06f, 0.09f, 0.16f, 0.88f));
                        badgeStyle.normal.textColor = p.GetBadgeColor();
                        GUI.Label(br, txt, badgeStyle);
                    }
                }
            }

            // 2. Top Safe-Area Level Header + Level Switcher Buttons
            BiomePalette biome = GetCurrentBiome();
            float topY = safeTop + 14f * s;
            float headerW = Mathf.Min(safeWidth - 28f * s, 640f * s);
            float headerH = 92f * s;
            Rect topBar = new Rect(safeLeft + (safeWidth - headerW) * 0.5f, topY, headerW, headerH);

            DrawRect(new Rect(topBar.x - 2f * s, topBar.y - 2f * s, topBar.width + 4f * s, topBar.height + 4f * s), biome.StripeColor * new Color(1f, 1f, 1f, 0.65f));
            DrawRect(topBar, new Color(0.06f, 0.10f, 0.18f, 0.92f));

            // Prev / Next Level quick buttons in Top Bar
            Rect prevBtn = new Rect(topBar.x + 10f * s, topBar.y + 10f * s, 72f * s, 44f * s);
            Rect nextBtn = new Rect(topBar.x + topBar.width - 82f * s, topBar.y + 10f * s, 72f * s, 44f * s);
            DrawRect(prevBtn, new Color(0.18f, 0.26f, 0.42f));
            DrawRect(nextBtn, new Color(0.18f, 0.26f, 0.42f));
            badgeStyle.normal.textColor = Color.white;
            if (GUI.Button(prevBtn, "◀ Svy", badgeStyle)) LoadPreviousProgressiveLevel();
            if (GUI.Button(nextBtn, "Svy ▶", badgeStyle)) LoadNextProgressiveLevel();

            string lvlTitle = _currentBlueprint != null ? _currentBlueprint.LevelTitle : $"SEVİYE {CurrentLevelNumber}";
            GUI.Label(new Rect(topBar.x + 86f * s, topBar.y + 6f * s, topBar.width - 172f * s, 28f * s), lvlTitle, titleStyle);

            float totalLen = _currentBlueprint != null ? Mathf.Max(1f, _currentBlueprint.TotalTrackLength) : 200f;
            float lvlPct = Mathf.Clamp01(CurrentDistance / totalLen) * 100f;
            GUI.Label(new Rect(topBar.x + 86f * s, topBar.y + 36f * s, topBar.width - 172f * s, 24f * s),
                $"İlerleme: %{lvlPct:F0}   •   🏆 Skor: {Score:N0}   •   🔥 x{Mathf.Clamp(1 + ComboCount / 2, 1, 5)}", subStyle);

            // Level course progress bar to Finale
            Rect progBg = new Rect(topBar.x + 16f * s, topBar.y + 70f * s, topBar.width - 32f * s, 10f * s);
            DrawRect(progBg, new Color(1f, 1f, 1f, 0.18f));
            DrawRect(new Rect(progBg.x, progBg.y, progBg.width * (lvlPct * 0.01f), progBg.height), biome.StripeColor);

            // 3. Bottom Safe-Area Jelly Mass Gauge + Interactive Touch Split/Merge Button
            float bottomH = 128f * s;
            float bottomY = Screen.height - safeBottom - bottomH - 14f * s;
            Rect bottomBar = new Rect(safeLeft + (safeWidth - headerW) * 0.5f, bottomY, headerW, bottomH);

            DrawRect(new Rect(bottomBar.x - 2f * s, bottomBar.y - 2f * s, bottomBar.width + 4f * s, bottomBar.height + 4f * s), new Color(1f, 1f, 1f, 0.25f));
            DrawRect(bottomBar, new Color(0.06f, 0.10f, 0.18f, 0.92f));

            string sizeCategory = CurrentMass <= 75f
                ? "KÜÇÜK (Dar Geçit)"
                : (CurrentMass >= 115f ? "DEV (Cam Kırar!)" : "NORMAL BOYUT");

            badgeStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(bottomBar.x + 16f * s, bottomBar.y + 8f * s, bottomBar.width - 190f * s, 28f * s),
                $"KÜTLE: {CurrentMass:F0}  —  {sizeCategory}", badgeStyle);

            Rect barBg = new Rect(bottomBar.x + 20f * s, bottomBar.y + 42f * s, bottomBar.width - 200f * s, 20f * s);
            DrawRect(barBg, new Color(0.16f, 0.22f, 0.32f, 1f));
            float fillRatio = Mathf.Clamp01(CurrentMass / 180f);
            Color barCol = CurrentMass <= 75f
                ? new Color(0.25f, 0.88f, 1.00f)
                : (CurrentMass >= 115f ? new Color(0.20f, 0.96f, 0.52f) : new Color(0.35f, 0.72f, 1.00f));
            DrawRect(new Rect(barBg.x, barBg.y, barBg.width * fillRatio, barBg.height), barCol);

            float mark75X = barBg.x + barBg.width * (75f / 180f);
            float mark115X = barBg.x + barBg.width * (115f / 180f);
            DrawRect(new Rect(mark75X, barBg.y - 3f * s, 3f * s, barBg.height + 6f * s), new Color(1f, 0.38f, 0.44f));
            DrawRect(new Rect(mark115X, barBg.y - 3f * s, 3f * s, barBg.height + 6f * s), new Color(0.22f, 1f, 0.55f));

            Rect actionBtn = new Rect(bottomBar.x + bottomBar.width - 168f * s, bottomBar.y + 12f * s, 152f * s, 56f * s);
            Color btnCol = IsSplit ? new Color(0.98f, 0.78f, 0.18f) : new Color(0.68f, 0.32f, 0.98f);
            DrawRect(actionBtn, btnCol);
            badgeStyle.normal.textColor = IsSplit ? new Color(0.08f, 0.08f, 0.12f) : Color.white;
            string btnLabel = IsSplit ? ">< BİRLEŞ\n(Boşluk)" : "÷2 BÖLÜN\n(Boşluk)";
            if (GUI.Button(actionBtn, btnLabel, badgeStyle))
            {
                ToggleManualSplitMerge();
            }

            subStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
            GUI.Label(new Rect(bottomBar.x + 14f * s, bottomBar.y + 74f * s, bottomBar.width - 28f * s, 46f * s), StatusBanner, subStyle);

            // 4. Level Completed Modal
            if (IsLevelCompleted)
            {
                float mw = Mathf.Min(safeWidth - 36f * s, 540f * s);
                float mh = 260f * s;
                Rect boxRect = new Rect(safeLeft + (safeWidth - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

                DrawRect(new Rect(boxRect.x - 4f * s, boxRect.y - 4f * s, boxRect.width + 8f * s, boxRect.height + 8f * s), new Color(0.22f, 0.96f, 0.55f, 0.95f));
                DrawRect(boxRect, new Color(0.06f, 0.10f, 0.18f, 0.97f));

                titleStyle.normal.textColor = new Color(0.25f, 0.98f, 0.58f);
                GUI.Label(new Rect(boxRect.x + 20f * s, boxRect.y + 18f * s, mw - 40f * s, 40f * s), $"SEVİYE {CurrentLevelNumber} TAMAMLANDI!", titleStyle);

                subStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(boxRect.x + 24f * s, boxRect.y + 68f * s, mw - 48f * s, 85f * s),
                    $"Kırılan Final Çarpanı: x{LastMultiplierReached}   |   Kalan Kütle: {CurrentMass:F0}\nToplam Skor: {Score:N0}   |   Rekor: {HighScore:N0}", subStyle);

                Rect nextLvlRect = new Rect(boxRect.x + (mw - 280f * s) * 0.5f, boxRect.y + 172f * s, 280f * s, 58f * s);
                DrawRect(nextLvlRect, new Color(0.20f, 0.92f, 0.54f));
                badgeStyle.normal.textColor = new Color(0.05f, 0.08f, 0.12f);
                if (GUI.Button(nextLvlRect, $"SONRAKİ SEVİYE (SEVİYE {CurrentLevelNumber + 1}) ▶", badgeStyle))
                {
                    LoadNextProgressiveLevel();
                }
            }

            // 5. Game Over Modal
            if (IsGameOver)
            {
                float mw = Mathf.Min(safeWidth - 36f * s, 540f * s);
                float mh = 250f * s;
                Rect boxRect = new Rect(safeLeft + (safeWidth - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

                DrawRect(new Rect(boxRect.x - 4f * s, boxRect.y - 4f * s, boxRect.width + 8f * s, boxRect.height + 8f * s), new Color(1f, 0.32f, 0.40f, 0.95f));
                DrawRect(boxRect, new Color(0.06f, 0.10f, 0.18f, 0.97f));

                titleStyle.normal.textColor = new Color(1f, 0.38f, 0.44f);
                GUI.Label(new Rect(boxRect.x + 20f * s, boxRect.y + 18f * s, mw - 40f * s, 40f * s), $"SEVİYE {CurrentLevelNumber} — YANDIN!", titleStyle);

                subStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(boxRect.x + 24f * s, boxRect.y + 68f * s, mw - 48f * s, 80f * s),
                    $"{StatusBanner}\nİlerleme: %{lvlPct:F0}   |   Skor: {Score:N0}", subStyle);

                Rect retryRect = new Rect(boxRect.x + (mw - 260f * s) * 0.5f, boxRect.y + 166f * s, 260f * s, 56f * s);
                DrawRect(retryRect, new Color(0.20f, 0.88f, 0.52f));
                badgeStyle.normal.textColor = new Color(0.05f, 0.08f, 0.12f);
                if (GUI.Button(retryRect, "SEVİYEYİ TEKRAR DENE", badgeStyle))
                {
                    StartLevel(CurrentLevelNumber);
                }
            }
        }

        private void DrawWorldSpaceBadge(Vector3 worldPos, string txt, Color bgCol, float s, GUIStyle badgeStyle)
        {
            Vector3 sp = mainCamera.WorldToScreenPoint(worldPos);
            if (sp.z <= 0.5f) return;

            float guiY = Screen.height - sp.y;
            float bw = Mathf.Max(220f * s, (txt.Length * 10.2f + 32f) * s);
            float bh = 36f * s;
            Rect br = new Rect(sp.x - bw * 0.5f, guiY - bh * 0.5f, bw, bh);
            DrawRect(new Rect(br.x - 3f * s, br.y - 3f * s, br.width + 6f * s, br.height + 6f * s), new Color(0.05f, 0.08f, 0.14f, 0.92f));
            DrawRect(br, bgCol);
            badgeStyle.normal.textColor = new Color(0.06f, 0.08f, 0.14f);
            GUI.Label(br, txt, badgeStyle);
        }
    }
}
