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

        [Header("Endless Flow Settings")]
        public float baseForwardSpeed = 9.5f;
        public float maxForwardSpeed = 16.0f;
        public float speedRampPerHundredMeters = 0.8f;
        public float lateralSteerSpeed = 4.2f;
        public float steerAcceleration = 7.5f;
        public float steerDeceleration = 11.5f;
        public float maxLateralOffset = 2.45f;
        public float roadWidth = 6.4f;
        public float sliceLength = 12.0f; // 12m smooth extruded spline mesh chunks
        public float lookAheadSpawnDistance = 108f;
        public float cleanupBehindDistance = 24f;

        [Header("Live State")]
        public float CurrentDistance;
        public float CurrentLateralOffset;
        public float CurrentMass = 90f;
        public bool IsSplit;
        public int Score;
        public int HighScore;
        public int ComboCount;
        public bool IsGameOver;
        public string StatusBanner = "Engellere göre Büyü (+), Küçül (-) veya Bölün (÷2)!";
        public float StatusBannerTimer = 6f;

        private GameObject _secondarySplitVisual;
        private GameObject _jellyBridgeVisual;
        private GameObject _primaryShadowDisc;
        private GameObject _secondaryShadowDisc;
        private float _spawnedSliceUpToZ;
        private float _nextObstacleZ = 26f;
        private int _obstacleCounter;
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

        private Texture2D _flatTex;
        private readonly List<GameObject> _spawnedChunks = new List<GameObject>();
        private readonly List<EndlessGateObstacle> _activeObstacles = new List<EndlessGateObstacle>();
        private readonly List<EndlessPickupItem> _activePickups = new List<EndlessPickupItem>();

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
                Name = "BÖLGE 1: GÖKYÜZÜ VADİSİ",
                SkyColor = new Color(0.32f, 0.62f, 0.92f),
                RoadSurfaceColor = new Color(0.92f, 0.96f, 1.00f),
                CurbColor = new Color(0.98f, 0.42f, 0.36f),
                StripeColor = new Color(0.18f, 0.78f, 0.98f),
                PropColor = new Color(0.28f, 0.82f, 0.98f),
                CloudColor = new Color(0.96f, 0.98f, 1.00f)
            },
            new BiomePalette
            {
                Name = "BÖLGE 2: GÜN BATIMI KANYONU",
                SkyColor = new Color(0.28f, 0.20f, 0.48f),
                RoadSurfaceColor = new Color(0.96f, 0.90f, 0.94f),
                CurbColor = new Color(0.98f, 0.28f, 0.58f),
                StripeColor = new Color(1.00f, 0.62f, 0.20f),
                PropColor = new Color(0.92f, 0.38f, 0.86f),
                CloudColor = new Color(1.00f, 0.82f, 0.88f)
            },
            new BiomePalette
            {
                Name = "BÖLGE 3: ZÜMRÜT ADALARI",
                SkyColor = new Color(0.16f, 0.52f, 0.56f),
                RoadSurfaceColor = new Color(0.90f, 0.98f, 0.94f),
                CurbColor = new Color(0.98f, 0.74f, 0.18f),
                StripeColor = new Color(0.16f, 0.86f, 0.54f),
                PropColor = new Color(0.22f, 0.92f, 0.60f),
                CloudColor = new Color(0.90f, 1.00f, 0.96f)
            }
        };

        private void Awake()
        {
            if (lateralSteerSpeed > 4.5f) lateralSteerSpeed = 4.2f;
            if (baseForwardSpeed > 10f) baseForwardSpeed = 9.5f;
            sliceLength = 12.0f;
            HighScore = PlayerPrefs.GetInt("JellyEndlessHighScore", 0);
        }

        private void Start()
        {
            if (lateralSteerSpeed > 4.5f) lateralSteerSpeed = 4.2f;
            sliceLength = 12.0f;
            if (mainCamera == null) mainCamera = Camera.main;
            StartNewEndlessRun();
        }

        public void StartNewEndlessRun()
        {
            ClearTrack();
            CurrentDistance = 0f;
            CurrentLateralOffset = 0f;
            CurrentMass = 90f;
            _displayedMass = 90f;
            IsSplit = false;
            _currentSplitBlend = 0f;
            _splitBlendVelocity = 0f;
            _wobbleAmplitude = 0f;
            Score = 0;
            ComboCount = 0;
            IsGameOver = false;
            _spawnedSliceUpToZ = -12f;
            _nextObstacleZ = 26f;
            _obstacleCounter = 0;
            _bounceCooldown = 0f;
            _speedBoostTimer = 0f;
            _manualSplitCooldown = 0f;
            SetBanner("Parmağınla / Fareyle kaydır | Deliklerden süzül, cam duvarları devleşip kır!", 4.5f);

            EnsurePlayerObjects();
            SyncJellyVisualsAndPosition(0f);

            while (_spawnedSliceUpToZ < lookAheadSpawnDistance)
            {
                SpawnNextRoadChunk();
            }
            UpdateCameraImmediate();
            UpdateEnvironmentVisuals(0f);
        }

        public void BuildInitialPreviewInEditor(JellyController existingPlayer)
        {
            if (existingPlayer != null) primaryPlayer = existingPlayer;
            if (mainCamera == null) mainCamera = Camera.main;
            StartNewEndlessRun();
        }

        private BiomePalette GetBiomeAtZ(float z)
        {
            int idx = Mathf.Abs(Mathf.FloorToInt(z / 220f)) % Biomes.Length;
            return Biomes[idx];
        }

        private void UpdateEnvironmentVisuals(float z)
        {
            if (mainCamera == null) return;
            float biomePos = z / 220f;
            int idxA = Mathf.Abs(Mathf.FloorToInt(biomePos)) % Biomes.Length;
            int idxB = (idxA + 1) % Biomes.Length;
            float frac = Mathf.Clamp01(((z % 220f) - 185f) / 35f);
            Color sky = Color.Lerp(Biomes[idxA].SkyColor, Biomes[idxB].SkyColor, frac);

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = sky;
            mainCamera.fieldOfView = 54f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 48f;
            RenderSettings.fogEndDistance = 105f;
            RenderSettings.fogColor = sky;
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

                // Remove old back dot if present
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

            if (_primaryShadowDisc == null)
            {
                _primaryShadowDisc = CreateShadowDisc("PrimaryShadowDisc");
            }
            if (_secondaryShadowDisc == null)
            {
                _secondaryShadowDisc = CreateShadowDisc("SecondaryShadowDisc");
            }
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
            float blend = Mathf.Clamp01((z - 4f) / 14f);
            float x = blend * (Mathf.Sin(z * 0.048f) * 6.0f + Mathf.Sin(z * 0.105f) * 2.2f);
            float y = blend * (Mathf.Sin(z * 0.058f) * 1.9f + Mathf.Cos(z * 0.115f) * 0.75f - 0.75f);

            float dz = 0.25f;
            float z2 = z + dz;
            float blend2 = Mathf.Clamp01((z2 - 4f) / 14f);
            float x2 = blend2 * (Mathf.Sin(z2 * 0.048f) * 6.0f + Mathf.Sin(z2 * 0.105f) * 2.2f);
            float y2 = blend2 * (Mathf.Sin(z2 * 0.058f) * 1.9f + Mathf.Cos(z2 * 0.115f) * 0.75f - 0.75f);

            Vector3 tangent = new Vector3(x2 - x, y2 - y, dz).normalized;
            float bankAngle = Mathf.Clamp(-(x2 - x) / dz * 7f, -10f, 10f);
            rot = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0f, 0f, bankAngle);
            right = rot * Vector3.right;
            pos = new Vector3(x, y, z);
        }

        public void ToggleManualSplitMerge()
        {
            if (_manualSplitCooldown > 0f || IsGameOver) return;
            _manualSplitCooldown = 0.22f;
            IsSplit = !IsSplit;
            _wobbleAmplitude = 0.26f;
            _wobblePhase = 0f;
            SetBanner(IsSplit
                ? "🟣 İKİYE BÖLÜNDÜN (÷2)! İkiz tünellerden geçebilirsin!"
                : "🟡 TEKRAR BİRLEŞTİN (><)! Tek parça halinde ilerliyorsun!", 2.2f);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                StartNewEndlessRun();
                return;
            }

            if (IsGameOver)
            {
                if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                    (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    StartNewEndlessRun();
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

            // 1. Sample horizontal steering (A/D, Arrow keys, Mouse/Touch drag)
            float rawKeySteer = 0f;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawKeySteer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawKeySteer += 1f;
            }

            float accelRate = Mathf.Abs(rawKeySteer) > 0.01f ? steerAcceleration : steerDeceleration;
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
            float nextOffset = CurrentLateralOffset + (_smoothedSteerInput * lateralSteerSpeed * dt) + dragDeltaOffset;

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

            // 2. Advance along the winding road
            float speed = Mathf.Min(maxForwardSpeed, baseForwardSpeed + (CurrentDistance * 0.01f) * speedRampPerHundredMeters);
            if (_speedBoostTimer > 0f)
                speed *= 1.35f;
            if (_bounceCooldown > 0f)
                speed = -7.2f; // Crisp physical rebound backward from solid walls!

            CurrentDistance = Mathf.Max(0f, CurrentDistance + speed * dt);

            // 3. Position & animate Jelly (single or split twin) on the 3D spline road
            SyncJellyVisualsAndPosition(dt * speed);

            // 4. Check pickup collection for both primary and secondary split pieces
            CheckPickups();

            // 5. Evaluate cutout and moving obstacles as player crosses their Z plane
            CheckObstacles();

            // 6. Generate new smooth extruded road chunks & obstacles ahead, cleanup behind
            while (_spawnedSliceUpToZ < CurrentDistance + lookAheadSpawnDistance)
            {
                SpawnNextRoadChunk();
            }
            CleanupBehindSlices();

            // 7. Smoothly follow the winding road with the camera & update biome atmosphere
            UpdateCameraSmooth(dt);
            UpdateEnvironmentVisuals(CurrentDistance);
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

            Color stateTint = CurrentMass >= 115f
                ? new Color(0.16f, 0.94f, 0.52f, 0.94f)
                : (CurrentMass <= 75f ? new Color(0.28f, 0.88f, 1.00f, 0.94f) : new Color(0.14f, 0.74f, 0.98f, 0.94f));
            LevelLoader.ApplyMaterialOrColor(primaryPlayer.gameObject, jellyMaterial, stateTint);
            if (_secondarySplitVisual != null)
                LevelLoader.ApplyMaterialOrColor(_secondarySplitVisual, jellyMaterial, stateTint);

            if (primaryPlayer.massController != null)
                primaryPlayer.massController.SetMassImmediate(effectiveSphereMass);

            if (_currentSplitBlend <= 0.005f && !IsSplit)
            {
                Vector3 groundPos = roadCenter + roadRight * CurrentLateralOffset;
                Vector3 worldPos = groundPos + roadUp * (scaleY * 0.5f + 0.02f);
                primaryPlayer.transform.position = worldPos;
                primaryPlayer.transform.localScale = jellyScale;
                primaryPlayer.transform.rotation = roadRot;

                if (_primaryShadowDisc != null)
                {
                    _primaryShadowDisc.SetActive(true);
                    _primaryShadowDisc.transform.position = groundPos + roadUp * 0.02f;
                    _primaryShadowDisc.transform.rotation = roadRot;
                    _primaryShadowDisc.transform.localScale = new Vector3(scaleX * 0.92f, 0.015f, scaleZ * 0.92f);
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

                Vector3 centerGround = roadCenter + roadRight * CurrentLateralOffset;
                Vector3 centerWorld = centerGround + roadUp * (scaleY * 0.5f + 0.02f);
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
                        SetBanner("🟣 İKİYE BÖLÜNDÜN! (÷2) İkiz tünelden geçebilirsin!", 2.5f);
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
            float catchRadius = jellyRadius + 0.72f;

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
            if (_bounceCooldown > 0f) return;

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
                    // HeavySmashWall has a breakable crystal center!
                    bool alignedCenter = Mathf.Abs(CurrentLateralOffset) <= 1.55f;
                    bool heavyEnough = CurrentMass >= obs.minRequiredMassForSmash - 8f;
                    if (alignedCenter && heavyEnough)
                    {
                        obs.ShatterBreakablePiece();
                        RegisterCleanGatePass(obs, 45, "💥 DEV JÖLEYLE CAM DUVARI PARÇALADIN! (+45 Puan)");
                    }
                    else if (alignedCenter && obs.hasBouncedOnce)
                    {
                        // Second impact against cracked glass breaks through at heavy mass cost!
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
            // Notice: The solid wall stays intact! Only the piece that entered the opening passes through!
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
            // Solid walls NEVER break! The player bounces backward off the wall, loses 22 mass, and must steer through the opening!
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

        private void SpawnNextRoadChunk()
        {
            float zStart = _spawnedSliceUpToZ;
            float zEnd = zStart + sliceLength;
            _spawnedSliceUpToZ = zEnd;

            BiomePalette biome = GetBiomeAtZ(zStart);
            var chunkRoot = BuildSmoothRoadChunkMesh(zStart, zEnd, 24, biome);
            _spawnedChunks.Add(chunkRoot);

            int chunkIdx = Mathf.FloorToInt(zStart / sliceLength);
            if (chunkIdx >= 1)
            {
                SpawnChunkSceneryAndClouds(chunkRoot.transform, zStart + sliceLength * 0.5f, chunkIdx, biome);
            }

            while (_nextObstacleZ <= zEnd)
            {
                SpawnObstacleAndPreparationPickups(_nextObstacleZ, _obstacleCounter);
                _obstacleCounter++;
                _nextObstacleZ += 26f;
            }
        }

        /// <summary>
        /// Builds a 100% seamless, continuous extruded 3D spline road mesh + curb meshes + center stripe mesh!
        /// Eliminates all jagged staircase steps and overlapping box gaps on curves and hills.
        /// </summary>
        private GameObject BuildSmoothRoadChunkMesh(float zStart, float zEnd, int steps, BiomePalette biome)
        {
            var chunkGo = new GameObject($"SmoothRoadChunk_{zStart:F0}");
            chunkGo.transform.SetParent(trackRoot, false);
            chunkGo.transform.position = Vector3.zero;
            chunkGo.transform.rotation = Quaternion.identity;

            float halfRoad = roadWidth * 0.5f;
            float curbWidth = 0.42f;
            float curbHeight = 0.32f;
            float roadThickness = 0.45f;

            // 1. Extrude Road Deck Mesh
            var roadGo = new GameObject("RoadSurfaceMesh");
            roadGo.transform.SetParent(chunkGo.transform, false);
            var roadMf = roadGo.AddComponent<MeshFilter>();
            roadGo.AddComponent<MeshRenderer>();
            roadMf.sharedMesh = CreateExtrudedRibbonMesh(zStart, zEnd, steps, -halfRoad, +halfRoad, -roadThickness, 0f);
            LevelLoader.ApplyMaterialOrColor(roadGo, trackMaterial, biome.RoadSurfaceColor);

            // 2. Extrude Center Glowing Stripe Mesh
            var stripeGo = new GameObject("CenterStripeMesh");
            stripeGo.transform.SetParent(chunkGo.transform, false);
            var stripeMf = stripeGo.AddComponent<MeshFilter>();
            stripeGo.AddComponent<MeshRenderer>();
            stripeMf.sharedMesh = CreateExtrudedRibbonMesh(zStart, zEnd, steps, -0.10f, +0.10f, 0.0f, 0.025f);
            LevelLoader.ApplyMaterialOrColor(stripeGo, null, biome.StripeColor);

            // 3. Extrude Seamless Left Curb Mesh
            var leftCurbGo = new GameObject("LeftCurbMesh");
            leftCurbGo.transform.SetParent(chunkGo.transform, false);
            var leftMf = leftCurbGo.AddComponent<MeshFilter>();
            leftCurbGo.AddComponent<MeshRenderer>();
            leftMf.sharedMesh = CreateExtrudedRibbonMesh(zStart, zEnd, steps, -halfRoad - curbWidth, -halfRoad, -0.1f, curbHeight);
            LevelLoader.ApplyMaterialOrColor(leftCurbGo, gateMaterial, biome.CurbColor);

            // 4. Extrude Seamless Right Curb Mesh
            var rightCurbGo = new GameObject("RightCurbMesh");
            rightCurbGo.transform.SetParent(chunkGo.transform, false);
            var rightMf = rightCurbGo.AddComponent<MeshFilter>();
            rightCurbGo.AddComponent<MeshRenderer>();
            rightMf.sharedMesh = CreateExtrudedRibbonMesh(zStart, zEnd, steps, +halfRoad, +halfRoad + curbWidth, -0.1f, curbHeight);
            LevelLoader.ApplyMaterialOrColor(rightCurbGo, gateMaterial, biome.CurbColor);

            return chunkGo;
        }

        private static Mesh CreateExtrudedRibbonMesh(float zStart, float zEnd, int steps, float xMin, float xMax, float yMin, float yMax)
        {
            int ringCount = steps + 1;
            // Each ring has 4 vertices: 0=BottomLeft, 1=TopLeft, 2=TopRight, 3=BottomRight
            Vector3[] verts = new Vector3[ringCount * 4];
            Vector2[] uvs = new Vector2[ringCount * 4];
            int[] tris = new int[steps * 24]; // 4 quads per step (Top, Left, Right, Bottom) * 6 indices

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

                // Top face (1 -> n+1 -> n+2 -> 2)
                tris[ti++] = c + 1; tris[ti++] = n + 1; tris[ti++] = n + 2;
                tris[ti++] = c + 1; tris[ti++] = n + 2; tris[ti++] = c + 2;

                // Left face (0 -> n+0 -> n+1 -> 1)
                tris[ti++] = c + 0; tris[ti++] = n + 0; tris[ti++] = n + 1;
                tris[ti++] = c + 0; tris[ti++] = n + 1; tris[ti++] = c + 1;

                // Right face (2 -> n+2 -> n+3 -> 3)
                tris[ti++] = c + 2; tris[ti++] = n + 2; tris[ti++] = n + 3;
                tris[ti++] = c + 2; tris[ti++] = n + 3; tris[ti++] = c + 3;

                // Bottom face (3 -> n+3 -> n+0 -> 0)
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

            // Left & Right stylized roadside crystal pylons
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

            // Fluffy stylized low-poly sky clouds floating on left/right horizon so upper portrait screen looks rich
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

        private void SpawnObstacleAndPreparationPickups(float gateZ, int index)
        {
            int pattern = index % 9;
            switch (pattern)
            {
                case 0:
                    SpawnPickupOnRoad(gateZ - 14f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ - 10f, -1.1f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 10f, +1.1f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 6f, 0f, EndlessPickupType.Shrink, 25f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.SmallCenterHole);
                    break;

                case 1:
                    SpawnPickupOnRoad(gateZ - 13f, -1.2f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 13f, +1.2f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 7.5f, 0f, EndlessPickupType.SpeedBoost, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.SlidingDoorWall);
                    break;

                case 2:
                    SpawnPickupOnRoad(gateZ - 12f, -1.35f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 12f, 0f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 12f, +1.35f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 6.5f, 0f, EndlessPickupType.Split, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.TwinSplitTunnels);
                    SpawnPickupOnRoad(gateZ + 7.5f, -1.2f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 7.5f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 7.5f, +1.2f, EndlessPickupType.Merge, 0f);
                    break;

                case 3:
                    SpawnPickupOnRoad(gateZ - 12f, -1.2f, EndlessPickupType.Shrink, 20f);
                    SpawnPickupOnRoad(gateZ - 12f, +1.2f, EndlessPickupType.Grow, 25f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.PincerShutters);
                    break;

                case 4:
                    SpawnPickupOnRoad(gateZ - 15f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ - 11.5f, -1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 11.5f, +1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 6.5f, 0f, EndlessPickupType.Grow, 35f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.HeavySmashWall);
                    break;

                case 5:
                    SpawnPickupOnRoad(gateZ - 11f, -1.2f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 11f, +1.2f, EndlessPickupType.SpeedBoost, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.VerticalCrusherGate);
                    break;

                case 6:
                    SpawnPickupOnRoad(gateZ - 11f, -1.4f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 11f, +1.4f, EndlessPickupType.Grow, 25f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.SwingingPendulum);
                    break;

                case 7:
                    SpawnPickupOnRoad(gateZ - 12f, -1.3f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 12f, 0f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 12f, +1.3f, EndlessPickupType.Split, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.MovingTwinTunnels);
                    SpawnPickupOnRoad(gateZ + 7.5f, -1.1f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 7.5f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 7.5f, +1.1f, EndlessPickupType.Merge, 0f);
                    break;

                case 8:
                    bool leftOpen = (index / 9) % 2 == 0;
                    SpawnPickupOnRoad(gateZ - 11f, leftOpen ? -1.4f : +1.4f, EndlessPickupType.SpeedBoost, 0f);
                    BuildCutoutGate(gateZ, leftOpen ? EndlessObstacleType.LeftOpeningWall : EndlessObstacleType.RightOpeningWall);
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

        private void CleanupBehindSlices()
        {
            float cutoffZ = CurrentDistance - cleanupBehindDistance;
            for (int i = _spawnedChunks.Count - 1; i >= 0; i--)
            {
                var ch = _spawnedChunks[i];
                if (ch == null)
                {
                    _spawnedChunks.RemoveAt(i);
                    continue;
                }
                if (ch.name.StartsWith("SmoothRoadChunk_"))
                {
                    if (float.TryParse(ch.name.Substring("SmoothRoadChunk_".Length), out float zStart) && zStart + sliceLength < cutoffZ)
                    {
                        Destroy(ch);
                        _spawnedChunks.RemoveAt(i);
                    }
                    continue;
                }
                if (ch.transform.position.z < cutoffZ)
                {
                    Destroy(ch);
                    _spawnedChunks.RemoveAt(i);
                }
            }
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (_activeObstacles[i] == null || _activeObstacles[i].trackZ < cutoffZ)
                    _activeObstacles.RemoveAt(i);
            }
            for (int i = _activePickups.Count - 1; i >= 0; i--)
            {
                if (_activePickups[i] == null || _activePickups[i].transform.position.z < cutoffZ)
                    _activePickups.RemoveAt(i);
            }
        }

        private void UpdateCameraImmediate()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out _, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 10.5f, out Vector3 lookAheadPt, out _, out _);

            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 camPos = roadCenter - horizForward * 6.5f + Vector3.up * 4.4f + roadRight * (CurrentLateralOffset * 0.20f);
            mainCamera.transform.position = camPos;
            mainCamera.transform.rotation = Quaternion.LookRotation((lookAheadPt - Vector3.up * 0.25f) - camPos, Vector3.up);
        }

        private void UpdateCameraSmooth(float dt)
        {
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out _, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 10.5f, out Vector3 lookAheadPt, out _, out _);

            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 desiredPos = roadCenter - horizForward * 6.5f + Vector3.up * 4.4f + roadRight * (CurrentLateralOffset * 0.20f);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPos, Mathf.Clamp01(dt * 8f));

            Quaternion desiredRot = Quaternion.LookRotation((lookAheadPt - Vector3.up * 0.25f) - mainCamera.transform.position, Vector3.up);
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

            // Responsive DPI & SafeArea scaling for both Mobile Portrait Simulator (with Notch) and Desktop 16:9!
            Rect safe = Screen.safeArea;
            float safeLeft = safe.xMin;
            float safeRight = safe.xMax;
            float safeTop = Screen.height - safe.yMax;   // GUI Y from top that clears iPhone Dynamic Island / Notch!
            float safeBottom = safe.yMin;                // Bottom home indicator clearance
            float safeWidth = safe.width;

            // Scale factor relative to 720p width reference
            float s = Mathf.Clamp(safeWidth / 680f, 0.85f, 2.25f);

            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(16f * s),
                fontStyle = FontStyle.Bold
            };

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(22f * s),
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(15f * s),
                fontStyle = FontStyle.Bold
            };
            subStyle.normal.textColor = new Color(0.86f, 0.94f, 1.00f);

            // 1. 3D World-Space Floating Badges above upcoming Gates & Pickups (DPI-scaled!)
            if (mainCamera != null && !IsGameOver)
            {
                for (int i = 0; i < _activeObstacles.Count; i++)
                {
                    var obs = _activeObstacles[i];
                    if (obs == null || obs.resolved) continue;
                    float dist = obs.trackZ - CurrentDistance;
                    if (dist < -1f || dist > 36f) continue;

                    Vector3 worldTop = obs.transform.position + Vector3.up * 2.95f;
                    Vector3 sp = mainCamera.WorldToScreenPoint(worldTop);
                    if (sp.z > 0.5f)
                    {
                        float guiY = Screen.height - sp.y;
                        string txt = obs.GetGateHeaderText();
                        float bw = Mathf.Max(220f * s, (txt.Length * 10.5f + 32f) * s);
                        float bh = 36f * s;
                        Rect br = new Rect(sp.x - bw * 0.5f, guiY - bh * 0.5f, bw, bh);
                        DrawRect(new Rect(br.x - 3f * s, br.y - 3f * s, br.width + 6f * s, br.height + 6f * s), new Color(0.05f, 0.08f, 0.14f, 0.92f));
                        DrawRect(br, obs.GetGateHeaderColor());
                        badgeStyle.normal.textColor = new Color(0.06f, 0.08f, 0.14f);
                        GUI.Label(br, txt, badgeStyle);
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

            // 2. Top Safe-Area Header Bar (Strictly below Notch/Dynamic Island!)
            BiomePalette biome = GetBiomeAtZ(CurrentDistance);
            float topY = safeTop + 14f * s;
            float headerW = Mathf.Min(safeWidth - 28f * s, 620f * s);
            float headerH = 88f * s;
            Rect topBar = new Rect(safeLeft + (safeWidth - headerW) * 0.5f, topY, headerW, headerH);

            DrawRect(new Rect(topBar.x - 2f * s, topBar.y - 2f * s, topBar.width + 4f * s, topBar.height + 4f * s), biome.StripeColor * new Color(1f, 1f, 1f, 0.65f));
            DrawRect(topBar, new Color(0.06f, 0.10f, 0.18f, 0.90f));

            string comboStr = ComboCount > 1 ? $"   •   🔥 x{Mathf.Clamp(1 + ComboCount / 2, 1, 5)} KOMBO" : "";
            GUI.Label(new Rect(topBar.x + 12f * s, topBar.y + 6f * s, topBar.width - 24f * s, 30f * s),
                $"{biome.Name}   |   🏆 SKOR: {Score:N0}", titleStyle);

            GUI.Label(new Rect(topBar.x + 12f * s, topBar.y + 38f * s, topBar.width - 24f * s, 24f * s),
                $"Mesafe: {CurrentDistance:F0} m   •   Rekor: {HighScore:N0}{comboStr}", subStyle);

            // Zone progress bar inside top card
            float biomeProgress = (CurrentDistance % 220f) / 220f;
            Rect progBg = new Rect(topBar.x + 20f * s, topBar.y + 68f * s, topBar.width - 40f * s, 10f * s);
            DrawRect(progBg, new Color(1f, 1f, 1f, 0.18f));
            DrawRect(new Rect(progBg.x, progBg.y, progBg.width * biomeProgress, progBg.height), biome.StripeColor);

            // 3. Bottom Safe-Area Jelly Mass Gauge + Interactive Touch Split/Merge Button
            float bottomH = 128f * s;
            float bottomY = Screen.height - safeBottom - bottomH - 14f * s;
            Rect bottomBar = new Rect(safeLeft + (safeWidth - headerW) * 0.5f, bottomY, headerW, bottomH);

            DrawRect(new Rect(bottomBar.x - 2f * s, bottomBar.y - 2f * s, bottomBar.width + 4f * s, bottomBar.height + 4f * s), new Color(1f, 1f, 1f, 0.25f));
            DrawRect(bottomBar, new Color(0.06f, 0.10f, 0.18f, 0.92f));

            string sizeCategory = CurrentMass <= 75f
                ? "KÜÇÜK (Dar Geçide Sığar)"
                : (CurrentMass >= 115f ? "DEV (Cam Duvarı Kırar!)" : "NORMAL BOYUT");

            badgeStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(bottomBar.x + 16f * s, bottomBar.y + 8f * s, bottomBar.width - 190f * s, 28f * s),
                $"KÜTLE: {CurrentMass:F0}  —  {sizeCategory}", badgeStyle);

            // Mass Bar
            Rect barBg = new Rect(bottomBar.x + 20f * s, bottomBar.y + 42f * s, bottomBar.width - 200f * s, 20f * s);
            DrawRect(barBg, new Color(0.16f, 0.22f, 0.32f, 1f));
            float fillRatio = Mathf.Clamp01(CurrentMass / 180f);
            Color barCol = CurrentMass <= 75f
                ? new Color(0.25f, 0.88f, 1.00f)
                : (CurrentMass >= 115f ? new Color(0.20f, 0.96f, 0.52f) : new Color(0.35f, 0.72f, 1.00f));
            DrawRect(new Rect(barBg.x, barBg.y, barBg.width * fillRatio, barBg.height), barCol);

            // Threshold markers at 75 and 115
            float mark75X = barBg.x + barBg.width * (75f / 180f);
            float mark115X = barBg.x + barBg.width * (115f / 180f);
            DrawRect(new Rect(mark75X, barBg.y - 3f * s, 3f * s, barBg.height + 6f * s), new Color(1f, 0.38f, 0.44f));
            DrawRect(new Rect(mark115X, barBg.y - 3f * s, 3f * s, barBg.height + 6f * s), new Color(0.22f, 1f, 0.55f));

            // Interactive On-Screen Split/Merge Touch Button on the right side of Bottom Bar!
            Rect actionBtn = new Rect(bottomBar.x + bottomBar.width - 168f * s, bottomBar.y + 12f * s, 152f * s, 56f * s);
            Color btnCol = IsSplit ? new Color(0.98f, 0.78f, 0.18f) : new Color(0.68f, 0.32f, 0.98f);
            DrawRect(actionBtn, btnCol);
            badgeStyle.normal.textColor = IsSplit ? new Color(0.08f, 0.08f, 0.12f) : Color.white;
            string btnLabel = IsSplit ? ">< BİRLEŞ\n(Boşluk)" : "÷2 BÖLÜN\n(Boşluk)";
            if (GUI.Button(actionBtn, btnLabel, badgeStyle))
            {
                ToggleManualSplitMerge();
            }

            // Live status banner along bottom of card
            subStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
            GUI.Label(new Rect(bottomBar.x + 14f * s, bottomBar.y + 74f * s, bottomBar.width - 28f * s, 46f * s), StatusBanner, subStyle);

            // 4. Game Over Modal
            if (IsGameOver)
            {
                float mw = Mathf.Min(safeWidth - 36f * s, 540f * s);
                float mh = 250f * s;
                Rect boxRect = new Rect(safeLeft + (safeWidth - mw) * 0.5f, (Screen.height - mh) * 0.5f, mw, mh);

                DrawRect(new Rect(boxRect.x - 4f * s, boxRect.y - 4f * s, boxRect.width + 8f * s, boxRect.height + 8f * s), new Color(1f, 0.32f, 0.40f, 0.95f));
                DrawRect(boxRect, new Color(0.06f, 0.10f, 0.18f, 0.97f));

                titleStyle.normal.textColor = new Color(1f, 0.38f, 0.44f);
                GUI.Label(new Rect(boxRect.x + 20f * s, boxRect.y + 18f * s, mw - 40f * s, 40f * s), "OYUN BİTTİ!", titleStyle);

                subStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(boxRect.x + 24f * s, boxRect.y + 68f * s, mw - 48f * s, 80f * s),
                    $"{StatusBanner}\nMesafe: {CurrentDistance:F0} m   |   Skor: {Score:N0}   |   Rekor: {HighScore:N0}", subStyle);

                Rect retryRect = new Rect(boxRect.x + (mw - 260f * s) * 0.5f, boxRect.y + 166f * s, 260f * s, 56f * s);
                DrawRect(retryRect, new Color(0.20f, 0.88f, 0.52f));
                badgeStyle.normal.textColor = new Color(0.05f, 0.08f, 0.12f);
                if (GUI.Button(retryRect, "TEKRAR OYNA", badgeStyle))
                {
                    StartNewEndlessRun();
                }
            }
        }
    }
}
