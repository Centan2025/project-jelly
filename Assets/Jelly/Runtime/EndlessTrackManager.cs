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
        public float maxForwardSpeed = 16.5f;
        public float speedRampPerHundredMeters = 0.85f;
        public float lateralSteerSpeed = 4.2f;
        public float steerAcceleration = 7.5f;
        public float steerDeceleration = 11.5f;
        public float maxLateralOffset = 2.45f;
        public float roadWidth = 6.4f;
        public float sliceLength = 2.0f;
        public float lookAheadSpawnDistance = 96f;
        public float cleanupBehindDistance = 18f;

        [Header("Live State")]
        public float CurrentDistance;
        public float CurrentLateralOffset;
        public float CurrentMass = 90f;
        public bool IsSplit;
        public int Score;
        public int HighScore;
        public int ComboCount;
        public bool IsGameOver;
        public string StatusBanner = "Yeşil (+): Büyü | Kırmızı (-): Küçül | Mor (÷2): Bölün | [Boşluk]: Manuel Bölün/Birleş";
        public float StatusBannerTimer = 6f;

        private GameObject _secondarySplitVisual;
        private GameObject _jellyBridgeVisual;
        private float _spawnedSliceUpToZ;
        private float _nextObstacleZ = 24f;
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

        // Cached 1x1 white texture & styles for crisp custom HUD
        private Texture2D _flatTex;
        private GUIStyle _badgeStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subStyle;
        private GUIStyle _bannerStyle;

        private readonly List<GameObject> _spawnedChunks = new List<GameObject>();
        private readonly List<EndlessGateObstacle> _activeObstacles = new List<EndlessGateObstacle>();
        private readonly List<EndlessPickupItem> _activePickups = new List<EndlessPickupItem>();

        private struct BiomePalette
        {
            public string Name;
            public Color SkyColor;
            public Color TrackLight;
            public Color TrackDark;
            public Color CurbColor;
            public Color StripeColor;
            public Color PropColor;
        }

        private static readonly BiomePalette[] Biomes = new BiomePalette[]
        {
            new BiomePalette
            {
                Name = "KRİSTAL VADİ",
                SkyColor = new Color(0.14f, 0.22f, 0.38f),
                TrackLight = new Color(0.88f, 0.93f, 0.99f),
                TrackDark = new Color(0.79f, 0.86f, 0.96f),
                CurbColor = new Color(0.96f, 0.46f, 0.38f),
                StripeColor = new Color(0.20f, 0.82f, 0.98f),
                PropColor = new Color(0.32f, 0.78f, 0.98f)
            },
            new BiomePalette
            {
                Name = "NEON SİBER KÖPRÜ",
                SkyColor = new Color(0.11f, 0.07f, 0.24f),
                TrackLight = new Color(0.20f, 0.18f, 0.34f),
                TrackDark = new Color(0.15f, 0.13f, 0.27f),
                CurbColor = new Color(0.95f, 0.22f, 0.68f),
                StripeColor = new Color(0.18f, 0.95f, 0.92f),
                PropColor = new Color(0.88f, 0.28f, 0.96f)
            },
            new BiomePalette
            {
                Name = "ZÜMRÜT LABORATUVARI",
                SkyColor = new Color(0.08f, 0.24f, 0.22f),
                TrackLight = new Color(0.82f, 0.95f, 0.89f),
                TrackDark = new Color(0.72f, 0.89f, 0.81f),
                CurbColor = new Color(0.98f, 0.76f, 0.20f),
                StripeColor = new Color(0.18f, 0.88f, 0.52f),
                PropColor = new Color(0.25f, 0.92f, 0.58f)
            }
        };

        private void Awake()
        {
            if (lateralSteerSpeed > 4.5f) lateralSteerSpeed = 4.2f;
            if (baseForwardSpeed > 10f) baseForwardSpeed = 9.5f;
            HighScore = PlayerPrefs.GetInt("JellyEndlessHighScore", 0);
        }

        private void Start()
        {
            if (lateralSteerSpeed > 4.5f) lateralSteerSpeed = 4.2f;
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
            _spawnedSliceUpToZ = -6f;
            _nextObstacleZ = 24f;
            _obstacleCounter = 0;
            _bounceCooldown = 0f;
            _speedBoostTimer = 0f;
            _manualSplitCooldown = 0f;
            SetBanner("A/D veya Fare ile yönlendir | [BOŞLUK]: Bölün/Birleş | Engellere uygun boyuta geç!", 4.5f);

            EnsurePlayerObjects();
            SyncJellyVisualsAndPosition(0f);

            while (_spawnedSliceUpToZ < lookAheadSpawnDistance)
            {
                SpawnNextRoadSlice();
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
            float frac = Mathf.Clamp01(( (z % 220f) - 185f ) / 35f);
            Color sky = Color.Lerp(Biomes[idxA].SkyColor, Biomes[idxB].SkyColor, frac);

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 98f;
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

                var rb = primaryPlayer.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
                var camCtrl = mainCamera != null ? mainCamera.GetComponent<JellyCameraController>() : null;
                if (camCtrl != null) camCtrl.enabled = false;

                DecorateJellyCharacter(primaryPlayer.transform, new Color(0.18f, 0.82f, 1.00f));
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
                    LevelLoader.ApplyMaterialOrColor(_secondarySplitVisual, jellyMaterial, new Color(0.22f, 0.84f, 1.00f, 0.92f));
                    DecorateJellyCharacter(_secondarySplitVisual.transform, new Color(0.22f, 0.84f, 1.00f));
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
                    LevelLoader.ApplyMaterialOrColor(_jellyBridgeVisual, jellyMaterial, new Color(0.28f, 0.85f, 1.00f, 0.88f));
                }
            }
            _jellyBridgeVisual.SetActive(false);
        }

        private void DecorateJellyCharacter(Transform jellyRoot, Color baseCol)
        {
            if (jellyRoot == null || jellyRoot.Find("JellyInnerCore") != null) return;

            // Inner glowing nucleus
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "JellyInnerCore";
            DestroyCollider(core);
            core.transform.SetParent(jellyRoot, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = Vector3.one * 0.56f;
            LevelLoader.ApplyMaterialOrColor(core, null, Color.Lerp(baseCol, Color.white, 0.55f));

            // Cute front eyes (visible from behind/side and during turns)
            CreateEye(jellyRoot, "LeftEye", new Vector3(-0.18f, 0.15f, 0.42f));
            CreateEye(jellyRoot, "RightEye", new Vector3(+0.18f, 0.15f, 0.42f));

            // Top glossy highlight speck
            var spec = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spec.name = "GlossHighlight";
            DestroyCollider(spec);
            spec.transform.SetParent(jellyRoot, false);
            spec.transform.localPosition = new Vector3(-0.16f, 0.34f, -0.22f);
            spec.transform.localScale = new Vector3(0.22f, 0.12f, 0.18f);
            LevelLoader.ApplyMaterialOrColor(spec, null, Color.white);
        }

        private static void CreateEye(Transform parent, string name, Vector3 localPos)
        {
            var eyeWhite = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeWhite.name = name;
            DestroyCollider(eyeWhite);
            eyeWhite.transform.SetParent(parent, false);
            eyeWhite.transform.localPosition = localPos;
            eyeWhite.transform.localScale = new Vector3(0.18f, 0.22f, 0.12f);
            LevelLoader.ApplyMaterialOrColor(eyeWhite, null, Color.white);

            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil";
            DestroyCollider(pupil);
            pupil.transform.SetParent(eyeWhite.transform, false);
            pupil.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            pupil.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            LevelLoader.ApplyMaterialOrColor(pupil, null, new Color(0.08f, 0.10f, 0.18f));
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
        }

        public static void EvaluateRoadCurve(float z, out Vector3 pos, out Quaternion rot, out Vector3 right)
        {
            float blend = Mathf.Clamp01((z - 4f) / 14f);
            float x = blend * (Mathf.Sin(z * 0.052f) * 6.4f + Mathf.Sin(z * 0.11f) * 2.5f);
            float y = blend * (Mathf.Sin(z * 0.062f) * 2.2f + Mathf.Cos(z * 0.125f) * 0.85f - 0.85f);

            float dz = 0.25f;
            float z2 = z + dz;
            float blend2 = Mathf.Clamp01((z2 - 4f) / 14f);
            float x2 = blend2 * (Mathf.Sin(z2 * 0.052f) * 6.4f + Mathf.Sin(z2 * 0.11f) * 2.5f);
            float y2 = blend2 * (Mathf.Sin(z2 * 0.062f) * 2.2f + Mathf.Cos(z2 * 0.125f) * 0.85f - 0.85f);

            Vector3 tangent = new Vector3(x2 - x, y2 - y, dz).normalized;
            float bankAngle = Mathf.Clamp(-(x2 - x) / dz * 8f, -12f, 12f);
            rot = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0f, 0f, bankAngle);
            right = rot * Vector3.right;
            pos = new Vector3(x, y, z);
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

            // Optional manual Split/Merge toggle with Space or E or Right Mouse Click so player is never stuck without a pickup!
            bool manualTogglePressed = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) ||
                                       (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
            if (manualTogglePressed && _manualSplitCooldown <= 0f)
            {
                _manualSplitCooldown = 0.25f;
                IsSplit = !IsSplit;
                _wobbleAmplitude = 0.26f;
                _wobblePhase = 0f;
                SetBanner(IsSplit
                    ? "🟣 İKİYE BÖLÜNDÜN (÷2)! Tekrar birleşmek için [Boşluk] veya Altın (><) al!"
                    : "🟡 TEKRAR BİRLEŞTİN (><)! Tek parça halinde ilerliyorsun!", 2.2f);
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
                dragDeltaOffset = ((p.x - _dragPrevPos.x) / screenWidth) * roadWidth * 0.85f;
                _dragPrevPos = p;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                if (!_dragging) { _dragging = true; _dragPrevPos = p; }
                dragDeltaOffset = ((p.x - _dragPrevPos.x) / screenWidth) * roadWidth * 0.85f;
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
                speed *= 1.38f;
            if (_bounceCooldown > 0f)
                speed = -5.8f;

            CurrentDistance = Mathf.Max(0f, CurrentDistance + speed * dt);

            // 3. Position & animate Jelly (single or split twin) on the 3D spline road
            SyncJellyVisualsAndPosition(dt * speed);

            // 4. Check pickup collection for both primary and secondary split pieces
            CheckPickups();

            // 5. Evaluate cutout and moving obstacles as player crosses their Z plane
            CheckObstacles();

            // 6. Generate new curved road slices & obstacles ahead, cleanup behind
            while (_spawnedSliceUpToZ < CurrentDistance + lookAheadSpawnDistance)
            {
                SpawnNextRoadSlice();
            }
            CleanupBehindSlices();

            // 7. Smoothly follow the winding road with the camera & update biome atmosphere
            UpdateCameraSmooth(dt);
            UpdateEnvironmentVisuals(CurrentDistance);
        }

        public static float ComputeVisualDiameter(float mass)
        {
            // Dramatic visual size difference between Small (<75), Normal (90), and Giant (>115)
            float normalized = Mathf.Clamp(mass, 25f, 220f) / 90f;
            return 1.02f * Mathf.Pow(normalized, 0.56f);
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

            // Tint jelly color slightly based on mass state (Cyan normal, Emerald when heavy smash ready, Coral-Pink when tiny)
            Color stateTint = CurrentMass >= 115f
                ? new Color(0.20f, 0.96f, 0.58f, 0.92f)
                : (CurrentMass <= 75f ? new Color(0.32f, 0.90f, 1.00f, 0.92f) : new Color(0.22f, 0.78f, 0.98f, 0.92f));
            LevelLoader.ApplyMaterialOrColor(primaryPlayer.gameObject, jellyMaterial, stateTint);
            if (_secondarySplitVisual != null)
                LevelLoader.ApplyMaterialOrColor(_secondarySplitVisual, jellyMaterial, stateTint);

            if (primaryPlayer.massController != null)
                primaryPlayer.massController.SetMassImmediate(effectiveSphereMass);

            if (_currentSplitBlend <= 0.005f && !IsSplit)
            {
                Vector3 worldPos = roadCenter + roadRight * CurrentLateralOffset + roadUp * (scaleY * 0.5f + 0.02f);
                primaryPlayer.transform.position = worldPos;
                primaryPlayer.transform.localScale = jellyScale;
                primaryPlayer.transform.rotation = roadRot;

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

                Vector3 centerWorld = roadCenter + roadRight * CurrentLateralOffset + roadUp * (scaleY * 0.5f + 0.02f);
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
                    SetBanner($"🔴 KÜÇÜLDÜN! (-{amount:F0} Kütle -> {CurrentMass:F0}) Dar deliklerden sığabilirsin!", 2.0f);
                    break;

                case EndlessPickupType.Split:
                    if (!IsSplit)
                    {
                        IsSplit = true;
                        _wobbleAmplitude = 0.28f;
                        _wobblePhase = 0f;
                        AddScoreWithCombo(20);
                        SpawnJellySplash(pickupWorldPos, new Color(0.75f, 0.35f, 1.00f), 9);
                        SetBanner("🟣 İKİYE BÖLÜNDÜN! (÷2) İkiz tünelden ve orta engellerden geçebilirsin!", 2.5f);
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
                                "⚠ TEK DELİK! Delikten geçen parçayla devam ediyorsun (-%50 Kütle)!");
                        }
                        else
                        {
                            TriggerObstacleCrash(obs, "💥 İKİ PARÇA DA DUVARA ÇARPTI (-30 Kütle)! Ortadaki deliğe hizalan!");
                        }
                        return;
                    }

                    bool alignedCenter = Mathf.Abs(CurrentLateralOffset) <= 0.92f;
                    bool smallEnough = CurrentMass <= obs.maxAllowedMassForSmallHole + 8f;
                    if (alignedCenter && smallEnough)
                    {
                        RegisterCleanGatePass(obs, 35, "✓ DAR DELİKTEN SÜZÜLDÜN! (+35 Puan)");
                    }
                    else
                    {
                        string reason = !alignedCenter
                            ? "💥 DUVARA ÇARPTIN (-30 Kütle)! Ortadaki deliğe yönel!"
                            : $"💥 DELİK İÇİN ÇOK BÜYÜKTÜN (Kütle {CurrentMass:F0} > {obs.maxAllowedMassForSmallHole:F0})! Kırmızı (-) almalıydın!";
                        TriggerObstacleCrash(obs, reason);
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
                                "⚠ PARÇALARDAN BİRİ DİREĞE ÇARPTI! Tünelden geçen parçayla devam (-%50 Kütle)!");
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
                                TriggerObstacleCrash(obs, "💥 İKİ PARÇA DA DUVARA ÇARPTI (-30 Kütle)! Tünellere hizalan!");
                            }
                        }
                    }
                    else
                    {
                        float relX = CurrentLateralOffset - shift;
                        if (Mathf.Abs(relX) >= 0.72f && Mathf.Abs(relX) <= 2.25f && CurrentMass <= 92f)
                        {
                            RegisterCleanGatePass(obs, 30, "✓ YAN TÜNELDEN TEK PARÇA GEÇTİN! (+30 Puan)");
                        }
                        else
                        {
                            TriggerObstacleCrash(obs, "💥 ORTADAKİ DİREĞE ÇARPTIN (-30 Kütle)! [Boşluk] veya Mor (÷2) ile bölün!");
                        }
                    }
                    break;
                }

                case EndlessObstacleType.HeavySmashWall:
                {
                    if (IsSplit)
                    {
                        if (CurrentMass >= obs.minRequiredMassForSmash)
                        {
                            obs.ShatterBreakablePiece();
                            RegisterCleanGatePass(obs, 40, "💥 İKİ PARÇAYLA DUVARI YIKTIN! (+40 Puan)");
                        }
                        else
                        {
                            SacrificeOneSplitPieceAndContinue(obs, CurrentLateralOffset,
                                "💥 BÖLÜNMÜŞ JÖLE DUVARA HAFİF GELDİ! Bir parça kırıldı (-%50 Kütle)!");
                        }
                        return;
                    }

                    bool alignedCenter = Mathf.Abs(CurrentLateralOffset) <= 1.50f;
                    bool heavyEnough = CurrentMass >= obs.minRequiredMassForSmash - 8f;
                    if (alignedCenter && heavyEnough)
                    {
                        obs.ShatterBreakablePiece();
                        RegisterCleanGatePass(obs, 45, "💥 DEV JÖLEYLE DUVARI PARÇALADIN! (+45 Puan)");
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, $"💥 DUVARI KIRAMADIN (Kütle {CurrentMass:F0} < {obs.minRequiredMassForSmash:F0})! Yeşil (+) toplayıp büyümeliydin!");
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
                            RegisterCleanGatePass(obs, 40, "✓ HAREKETLİ KAPIDAN GEÇTİN! (+40 Puan)");
                        }
                        else if (leftOk || rightOk)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, leftOk ? leftPieceX : rightPieceX,
                                "⚠ HAREKETLİ KAPI BİR PARÇAYI YAKALADI! Kalan parçayla devam!");
                        }
                        else
                        {
                            TriggerObstacleCrash(obs, "💥 HAREKETLİ KAPIYA ÇARPTIN (-30 Kütle)! Kayan açıklığı takip et!");
                        }
                        return;
                    }

                    if (Mathf.Abs(CurrentLateralOffset - doorX) <= halfGap)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ HAREKETLİ KAPIDAN TAM ZAMANINDA GEÇTİN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "💥 KAYAR DUVARA ÇARPTIN (-30 Kütle)! Açıklığın kaydığı yöne geç!");
                    }
                    break;
                }

                case EndlessObstacleType.PincerShutters:
                {
                    float gapHalf = obs.CurrentPincerGapHalfWidth;
                    if (IsSplit)
                    {
                        bool leftOk = Mathf.Abs(leftPieceX) <= gapHalf + 0.15f;
                        bool rightOk = Mathf.Abs(rightPieceX) <= gapHalf + 0.15f;
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
                            TriggerObstacleCrash(obs, "💥 KAPANAN MAKAS DUVARA ÇARPTIN (-30 Kütle)!");
                        }
                        return;
                    }

                    float jellyRad = ComputeVisualDiameter(CurrentMass) * 0.42f;
                    if (Mathf.Abs(CurrentLateralOffset) + jellyRad <= gapHalf + 0.25f)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ AÇILIR MAKAS KAPIDAN GEÇTİN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "💥 MAKAS KAPI KAPALIYKEN ÇARPTIN (-30 Kütle)! Açıldığı anı yakala!");
                    }
                    break;
                }

                case EndlessObstacleType.VerticalCrusherGate:
                {
                    float crusherY = obs.CurrentCrusherBottomY;
                    float effectiveMass = IsSplit ? CurrentMass * 0.5f : CurrentMass;
                    float jellyHeight = ComputeVisualDiameter(effectiveMass);

                    if (crusherY >= jellyHeight - 0.12f || effectiveMass <= 78f)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ PİSTON KAPININ ALTINDAN GEÇTİN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "💥 İNEN PİSTONA ÇARPTIN (-30 Kütle)! Küçül (-) veya piston kalkınca geç!");
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
                            RegisterCleanGatePass(obs, 40, "✓ SALLANAN BALYOZDAN SIYRILDIN! (+40 Puan)");
                        }
                        else if (!leftHit || !rightHit)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, !leftHit ? leftPieceX : rightPieceX,
                                "⚠ BALYOZ BİR PARÇAYA ÇARPTI! Sağlam parçayla devam!");
                        }
                        else
                        {
                            TriggerObstacleCrash(obs, "💥 SALLANAN BALYOZA ÇARPTIN (-30 Kütle)!");
                        }
                        return;
                    }

                    if (Mathf.Abs(CurrentLateralOffset - headX) >= hitRadius)
                    {
                        RegisterCleanGatePass(obs, 40, "✓ SALLANAN BALYOZDAN KAÇTIN! (+40 Puan)");
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "💥 SALLANAN BALYOZA ÇARPTIN (-30 Kütle)! Balyozun ters yönüne kaç!");
                    }
                    break;
                }

                case EndlessObstacleType.LeftOpeningWall:
                {
                    if (IsSplit)
                    {
                        bool leftPassed = leftPieceX <= -0.55f;
                        bool rightPassed = rightPieceX <= -0.55f;
                        if (leftPassed && rightPassed)
                        {
                            RegisterCleanGatePass(obs, 30, "✓ İKİ PARÇA DA SOL AÇIKLIKTAN GEÇTİ! (+30 Puan)");
                        }
                        else if (leftPassed)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, leftPieceX,
                                "⚠ SAĞ PARÇA DUVARA ÇARPTI! Sol açıklıktan geçen parçayla devam (-%50 Kütle)!");
                        }
                        else
                        {
                            TriggerObstacleCrash(obs, "💥 SAĞ TARAF KAPALI (-30 Kütle)! Sola kaç!");
                        }
                        return;
                    }

                    if (CurrentLateralOffset <= -0.55f)
                    {
                        RegisterCleanGatePass(obs, 30, "✓ SOL AÇIKLIKTAN GEÇTİN! (+30 Puan)");
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "💥 SAĞ TARAF KAPALI (-30 Kütle)! Sol açıklığa yönel!");
                    }
                    break;
                }

                case EndlessObstacleType.RightOpeningWall:
                {
                    if (IsSplit)
                    {
                        bool leftPassed = leftPieceX >= 0.55f;
                        bool rightPassed = rightPieceX >= 0.55f;
                        if (leftPassed && rightPassed)
                        {
                            RegisterCleanGatePass(obs, 30, "✓ İKİ PARÇA DA SAĞ AÇIKLIKTAN GEÇTİ! (+30 Puan)");
                        }
                        else if (rightPassed)
                        {
                            SacrificeOneSplitPieceAndContinue(obs, rightPieceX,
                                "⚠ SOL PARÇA DUVARA ÇARPTI! Sağ açıklıktan geçen parçayla devam (-%50 Kütle)!");
                        }
                        else
                        {
                            TriggerObstacleCrash(obs, "💥 SOL TARAF KAPALI (-30 Kütle)! Sağa kaç!");
                        }
                        return;
                    }

                    if (CurrentLateralOffset >= 0.55f)
                    {
                        RegisterCleanGatePass(obs, 30, "✓ SAĞ AÇIKLIKTAN GEÇTİN! (+30 Puan)");
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "💥 SOL TARAF KAPALI (-30 Kütle)! Sağ açıklığa yönel!");
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
            obs.ShatterBreakablePiece();
            ComboCount = 0;
            IsSplit = false;
            CurrentLateralOffset = Mathf.Clamp(survivingLateralX, -maxLateralOffset, maxLateralOffset);
            _currentSplitBlend = 0f;
            CurrentMass *= 0.5f;
            _displayedMass = CurrentMass;
            _wobbleAmplitude = 0.32f;
            _wobblePhase = 0f;
            _bounceCooldown = 0.12f;

            if (CurrentMass < 22f)
            {
                TriggerGameOver("💥 JÖLE ÇOK KÜÇÜLDÜ VE ERİDİ! (Kütle Tükendi)");
                return;
            }
            SetBanner(message, 2.8f);
        }

        private void TriggerObstacleCrash(EndlessGateObstacle obs, string reason)
        {
            ComboCount = 0;
            _wobbleAmplitude = 0.36f;
            _wobblePhase = 0f;
            CurrentMass -= 30f;

            if (primaryPlayer != null)
                SpawnJellySplash(primaryPlayer.transform.position, new Color(1f, 0.35f, 0.35f), 10);

            if (CurrentMass < 22f)
            {
                obs.resolved = true;
                obs.ShatterBreakablePiece();
                TriggerGameOver("💥 JÖLE DUVARDA PARÇALANDI! (Kütle Bitti)");
                return;
            }

            if (!obs.hasBouncedOnce)
            {
                obs.hasBouncedOnce = true;
                _bounceCooldown = 0.38f;
                SetBanner(reason + " (Hizalanıp geç!)", 3.0f);
            }
            else
            {
                obs.resolved = true;
                obs.ShatterBreakablePiece();
                _bounceCooldown = 0.18f;
                SetBanner(reason + " (Duvar kırıldı!)", 2.8f);
            }
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

        private void SpawnNextRoadSlice()
        {
            float z = _spawnedSliceUpToZ;
            _spawnedSliceUpToZ += sliceLength;

            EvaluateRoadCurve(z, out Vector3 center, out Quaternion rot, out Vector3 right);
            BiomePalette biome = GetBiomeAtZ(z);

            var sliceRoot = new GameObject($"RoadSlice_{z:F0}");
            sliceRoot.transform.SetParent(trackRoot, false);
            sliceRoot.transform.SetPositionAndRotation(center, rot);
            _spawnedChunks.Add(sliceRoot);

            int sliceIdx = Mathf.FloorToInt(z / sliceLength);
            bool altStripe = sliceIdx % 2 == 0;
            Color floorCol = altStripe ? biome.TrackLight : biome.TrackDark;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "RoadBed";
            floor.transform.SetParent(sliceRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(roadWidth, 0.5f, sliceLength + 0.25f);
            LevelLoader.ApplyMaterialOrColor(floor, trackMaterial, floorCol);

            // Center dashed glowing line
            if (altStripe)
            {
                var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = "CenterLaneDash";
                DestroyCollider(dash);
                dash.transform.SetParent(sliceRoot.transform, false);
                dash.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                dash.transform.localScale = new Vector3(0.16f, 0.04f, sliceLength * 0.7f);
                LevelLoader.ApplyMaterialOrColor(dash, null, biome.StripeColor);
            }

            float halfW = roadWidth * 0.5f + 0.2f;
            Color curbCol = altStripe ? biome.CurbColor : Color.Lerp(biome.CurbColor, Color.white, 0.65f);

            var leftRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftRail.name = "LeftCurb";
            leftRail.transform.SetParent(sliceRoot.transform, false);
            leftRail.transform.localPosition = new Vector3(-halfW, 0.22f, 0f);
            leftRail.transform.localScale = new Vector3(0.35f, 0.55f, sliceLength + 0.25f);
            LevelLoader.ApplyMaterialOrColor(leftRail, gateMaterial, curbCol);

            var rightRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightRail.name = "RightCurb";
            rightRail.transform.SetParent(sliceRoot.transform, false);
            rightRail.transform.localPosition = new Vector3(halfW, 0.22f, 0f);
            rightRail.transform.localScale = new Vector3(0.35f, 0.55f, sliceLength + 0.25f);
            LevelLoader.ApplyMaterialOrColor(rightRail, gateMaterial, curbCol);

            // Roadside scenery crystals/pillars every 12 meters for rich 3D world sense of speed
            if (sliceIdx % 6 == 0 && z > 8f)
            {
                SpawnRoadsideScenery(sliceRoot.transform, sliceIdx, biome);
            }

            if (z >= _nextObstacleZ)
            {
                SpawnObstacleAndPreparationPickups(_nextObstacleZ, _obstacleCounter);
                _obstacleCounter++;
                _nextObstacleZ += 26f;
            }
        }

        private void SpawnRoadsideScenery(Transform sliceRoot, int sliceIdx, BiomePalette biome)
        {
            float sideDist = roadWidth * 0.5f + 2.4f;
            float h = 2.8f + (sliceIdx % 3) * 1.1f;

            var leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPillar.name = "SceneryPillarL";
            DestroyCollider(leftPillar);
            leftPillar.transform.SetParent(sliceRoot, false);
            leftPillar.transform.localPosition = new Vector3(-sideDist, h * 0.4f - 0.5f, 0f);
            leftPillar.transform.localRotation = Quaternion.Euler(0f, 25f, -8f);
            leftPillar.transform.localScale = new Vector3(0.75f, h, 0.75f);
            LevelLoader.ApplyMaterialOrColor(leftPillar, null, biome.PropColor);

            var rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPillar.name = "SceneryPillarR";
            DestroyCollider(rightPillar);
            rightPillar.transform.SetParent(sliceRoot, false);
            rightPillar.transform.localPosition = new Vector3(+sideDist, h * 0.4f - 0.5f, 0f);
            rightPillar.transform.localRotation = Quaternion.Euler(0f, -25f, 8f);
            rightPillar.transform.localScale = new Vector3(0.75f, h, 0.75f);
            LevelLoader.ApplyMaterialOrColor(rightPillar, null, biome.PropColor);

            // Overhead neon arch every 36 meters
            if (sliceIdx % 18 == 0)
            {
                var topBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                topBeam.name = "SceneryOverheadArch";
                DestroyCollider(topBeam);
                topBeam.transform.SetParent(sliceRoot, false);
                topBeam.transform.localPosition = new Vector3(0f, h - 0.2f, 0f);
                topBeam.transform.localScale = new Vector3(sideDist * 2.1f, 0.35f, 0.35f);
                LevelLoader.ApplyMaterialOrColor(topBeam, null, biome.StripeColor);
            }
        }

        private void SpawnObstacleAndPreparationPickups(float gateZ, int index)
        {
            int pattern = index % 9;
            switch (pattern)
            {
                case 0:
                    // 1. Small Center Hole: Clear Shrink (-) path so player shrinks below 75!
                    SpawnPickupOnRoad(gateZ - 14f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ - 10f, -1.1f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 10f, +1.1f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 6f, 0f, EndlessPickupType.Shrink, 25f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.SmallCenterHole);
                    break;

                case 1:
                    // 2. Moving Sliding Door Wall: Dynamic left-right moving gate!
                    SpawnPickupOnRoad(gateZ - 13f, -1.2f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 13f, +1.2f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 7.5f, 0f, EndlessPickupType.SpeedBoost, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.SlidingDoorWall);
                    break;

                case 2:
                    // 3. Twin Split Tunnels: Full row of Purple Split (÷2) crystals before, Gold Merge (><) after!
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
                    // 4. Pincer Shutters (Açılıp Kapanan Makas Kapı)
                    SpawnPickupOnRoad(gateZ - 12f, -1.2f, EndlessPickupType.Shrink, 20f);
                    SpawnPickupOnRoad(gateZ - 12f, +1.2f, EndlessPickupType.Grow, 25f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.PincerShutters);
                    break;

                case 4:
                    // 5. Heavy Smash Wall: Generous Green Grow (+) pickups so player reaches >=115 mass and smashes it!
                    SpawnPickupOnRoad(gateZ - 15f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ - 11.5f, -1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 11.5f, +1.1f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 6.5f, 0f, EndlessPickupType.Grow, 35f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.HeavySmashWall);
                    break;

                case 5:
                    // 6. Vertical Crusher Gate (İnip Kalkan Giyotin Piston)
                    SpawnPickupOnRoad(gateZ - 11f, -1.2f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 11f, +1.2f, EndlessPickupType.SpeedBoost, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.VerticalCrusherGate);
                    break;

                case 6:
                    // 7. Swinging Pendulum Hammer (Sallanan Balyoz)
                    SpawnPickupOnRoad(gateZ - 11f, -1.4f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 11f, +1.4f, EndlessPickupType.Grow, 25f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.SwingingPendulum);
                    break;

                case 7:
                    // 8. Moving Twin Tunnels (Sağa-Sola Kayan İkiz Tünel)
                    SpawnPickupOnRoad(gateZ - 12f, -1.3f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 12f, 0f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 12f, +1.3f, EndlessPickupType.Split, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.MovingTwinTunnels);
                    SpawnPickupOnRoad(gateZ + 7.5f, -1.1f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 7.5f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 7.5f, +1.1f, EndlessPickupType.Merge, 0f);
                    break;

                case 8:
                    // 9. Alternating Left/Right Slalom with Speed Boost
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
                    col = new Color(0.20f, 0.96f, 0.48f);
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

            Color wallColor = new Color(0.92f, 0.44f, 0.36f);
            Color lintelColor = new Color(0.22f, 0.28f, 0.42f);
            Color neonCyan = new Color(0.20f, 0.88f, 1.00f);

            switch (type)
            {
                case EndlessObstacleType.SmallCenterHole:
                    // Visually narrow center mouse-hole (width 1.0m, height 0.95m) so player clearly sees they must be SMALL!
                    CreateWallBlock(gateRoot.transform, "LeftWall", new Vector3(-1.82f, 1.1f, 0f), new Vector3(2.6f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightWall", new Vector3(+1.82f, 1.1f, 0f), new Vector3(2.6f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "TopHoleFrame", new Vector3(0f, 1.58f, 0f), new Vector3(1.1f, 1.25f, 0.58f), drainMaterial, new Color(0.96f, 0.28f, 0.36f));
                    break;

                case EndlessObstacleType.TwinSplitTunnels:
                    CreateWallBlock(gateRoot.transform, "OuterLeftPost", new Vector3(-2.75f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "CenterSplitPillar", new Vector3(0f, 1.1f, 0f), new Vector3(1.05f, 2.2f, 0.65f), null, new Color(0.74f, 0.32f, 0.98f));
                    CreateWallBlock(gateRoot.transform, "OuterRightPost", new Vector3(+2.75f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "TopTwinArch", new Vector3(0f, 2.0f, 0f), new Vector3(6.2f, 0.45f, 0.58f), null, lintelColor);
                    break;

                case EndlessObstacleType.MovingTwinTunnels:
                    var movingRig = new GameObject("MovingTwinRig").transform;
                    movingRig.SetParent(gateRoot.transform, false);
                    CreateWallBlock(movingRig, "OuterLeftPost", new Vector3(-2.45f, 1.1f, 0f), new Vector3(0.6f, 2.2f, 0.55f), null, neonCyan);
                    CreateWallBlock(movingRig, "CenterSplitPillar", new Vector3(0f, 1.1f, 0f), new Vector3(0.95f, 2.2f, 0.65f), null, new Color(0.78f, 0.35f, 1.00f));
                    CreateWallBlock(movingRig, "OuterRightPost", new Vector3(+2.45f, 1.1f, 0f), new Vector3(0.6f, 2.2f, 0.55f), null, neonCyan);
                    CreateWallBlock(movingRig, "TopTwinArch", new Vector3(0f, 2.0f, 0f), new Vector3(5.5f, 0.45f, 0.58f), null, lintelColor);
                    obs.movingPartA = movingRig;
                    break;

                case EndlessObstacleType.HeavySmashWall:
                    CreateWallBlock(gateRoot.transform, "LeftFrame", new Vector3(-2.3f, 1.1f, 0f), new Vector3(1.6f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightFrame", new Vector3(+2.3f, 1.1f, 0f), new Vector3(1.6f, 2.2f, 0.55f), gateMaterial, wallColor);
                    var smashCenter = CreateWallBlock(gateRoot.transform, "BreakableSmashCenter", new Vector3(0f, 0.95f, 0f), new Vector3(2.8f, 1.9f, 0.42f), pickupMaterial, new Color(0.28f, 0.95f, 0.54f));
                    obs.breakableCenterPiece = smashCenter.transform;
                    break;

                case EndlessObstacleType.SlidingDoorWall:
                    var slideLeft = CreateWallBlock(gateRoot.transform, "SlidingDoorLeft", new Vector3(-1.8f, 1.1f, 0f), new Vector3(2.2f, 2.2f, 0.58f), null, neonCyan);
                    var slideRight = CreateWallBlock(gateRoot.transform, "SlidingDoorRight", new Vector3(+1.8f, 1.1f, 0f), new Vector3(2.2f, 2.2f, 0.58f), null, neonCyan);
                    CreateWallBlock(gateRoot.transform, "SlidingTopRail", new Vector3(0f, 2.3f, 0f), new Vector3(6.4f, 0.35f, 0.65f), null, lintelColor);
                    obs.movingPartA = slideLeft.transform;
                    obs.movingPartB = slideRight.transform;
                    break;

                case EndlessObstacleType.PincerShutters:
                    var pincerL = CreateWallBlock(gateRoot.transform, "PincerLeft", new Vector3(-1.8f, 1.1f, 0f), new Vector3(2.4f, 2.2f, 0.58f), null, new Color(0.98f, 0.58f, 0.20f));
                    var pincerR = CreateWallBlock(gateRoot.transform, "PincerRight", new Vector3(+1.8f, 1.1f, 0f), new Vector3(2.4f, 2.2f, 0.58f), null, new Color(0.98f, 0.58f, 0.20f));
                    CreateWallBlock(gateRoot.transform, "PincerTopRail", new Vector3(0f, 2.3f, 0f), new Vector3(6.4f, 0.35f, 0.65f), null, lintelColor);
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
                    CreateWallBlock(gateRoot.transform, "BlockedRightSide", new Vector3(+1.25f, 1.1f, 0f), new Vector3(3.7f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "LeftArchTop", new Vector3(-1.65f, 1.85f, 0f), new Vector3(2.2f, 0.7f, 0.55f), goalMaterial, lintelColor);
                    break;

                case EndlessObstacleType.RightOpeningWall:
                    CreateWallBlock(gateRoot.transform, "BlockedLeftSide", new Vector3(-1.25f, 1.1f, 0f), new Vector3(3.7f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightArchTop", new Vector3(+1.65f, 1.85f, 0f), new Vector3(2.2f, 0.7f, 0.55f), goalMaterial, lintelColor);
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
            EvaluateRoadCurve(CurrentDistance + 8.5f, out Vector3 lookAheadPt, out _, out _);

            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 camPos = roadCenter - horizForward * 7.2f + Vector3.up * 5.0f + roadRight * (CurrentLateralOffset * 0.22f);
            mainCamera.transform.position = camPos;
            mainCamera.transform.rotation = Quaternion.LookRotation((lookAheadPt + Vector3.up * 0.4f) - camPos, Vector3.up);
        }

        private void UpdateCameraSmooth(float dt)
        {
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out _, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 8.5f, out Vector3 lookAheadPt, out _, out _);

            Vector3 horizForward = new Vector3(lookAheadPt.x - roadCenter.x, 0f, lookAheadPt.z - roadCenter.z).normalized;
            Vector3 desiredPos = roadCenter - horizForward * 7.2f + Vector3.up * 5.0f + roadRight * (CurrentLateralOffset * 0.22f);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPos, Mathf.Clamp01(dt * 8f));

            Quaternion desiredRot = Quaternion.LookRotation((lookAheadPt + Vector3.up * 0.4f) - mainCamera.transform.position, Vector3.up);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, desiredRot, Mathf.Clamp01(dt * 8f));
        }

        private void EnsureGuiResources()
        {
            if (_flatTex == null)
            {
                _flatTex = new Texture2D(1, 1);
                _flatTex.SetPixel(0, 0, Color.white);
                _flatTex.Apply();
            }
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                _badgeStyle.normal.textColor = Color.white;
            }
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                _headerStyle.normal.textColor = Color.white;
            }
            if (_subStyle == null)
            {
                _subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                _subStyle.normal.textColor = new Color(0.82f, 0.90f, 1f);
            }
            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
                _bannerStyle.normal.textColor = Color.white;
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
            EnsureGuiResources();

            // 1. Draw 3D World-Space Floating Badges for upcoming Gates & Pickups
            if (mainCamera != null && !IsGameOver)
            {
                for (int i = 0; i < _activeObstacles.Count; i++)
                {
                    var obs = _activeObstacles[i];
                    if (obs == null || obs.resolved) continue;
                    float dist = obs.trackZ - CurrentDistance;
                    if (dist < -1f || dist > 38f) continue;

                    Vector3 worldTop = obs.transform.position + Vector3.up * 2.85f;
                    Vector3 sp = mainCamera.WorldToScreenPoint(worldTop);
                    if (sp.z > 0.5f)
                    {
                        float guiY = Screen.height - sp.y;
                        string txt = obs.GetGateHeaderText();
                        float bw = Mathf.Max(190f, txt.Length * 8.2f + 24f);
                        Rect br = new Rect(sp.x - bw * 0.5f, guiY - 16f, bw, 28f);
                        DrawRect(new Rect(br.x - 2f, br.y - 2f, br.width + 4f, br.height + 4f), new Color(0.05f, 0.08f, 0.14f, 0.88f));
                        DrawRect(br, obs.GetGateHeaderColor() * new Color(1f, 1f, 1f, 0.92f));
                        _badgeStyle.normal.textColor = new Color(0.06f, 0.08f, 0.12f);
                        GUI.Label(br, txt, _badgeStyle);
                    }
                }

                for (int i = 0; i < _activePickups.Count; i++)
                {
                    var p = _activePickups[i];
                    if (p == null || p.IsConsumed || !p.gameObject.activeInHierarchy) continue;
                    float dist = p.trackZ - CurrentDistance;
                    if (dist < 0f || dist > 22f) continue;

                    Vector3 sp = mainCamera.WorldToScreenPoint(p.transform.position + Vector3.up * 0.95f);
                    if (sp.z > 0.5f)
                    {
                        float guiY = Screen.height - sp.y;
                        string txt = p.GetShortBadgeText();
                        float bw = 88f;
                        Rect br = new Rect(sp.x - bw * 0.5f, guiY - 12f, bw, 22f);
                        DrawRect(br, new Color(0.06f, 0.09f, 0.15f, 0.82f));
                        _badgeStyle.normal.textColor = p.GetBadgeColor();
                        GUI.Label(br, txt, _badgeStyle);
                    }
                }
            }

            // 2. Top-Left Biome & Distance Card
            BiomePalette biome = GetBiomeAtZ(CurrentDistance);
            Rect topLeft = new Rect(18f, 16f, 310f, 76f);
            DrawRect(topLeft, new Color(0.06f, 0.09f, 0.16f, 0.88f));
            DrawRect(new Rect(topLeft.x, topLeft.y, 6f, topLeft.height), biome.StripeColor);
            GUI.Label(new Rect(topLeft.x + 16f, topLeft.y + 8f, 285f, 28f), $"📍 {biome.Name}", _headerStyle);
            string comboTxt = ComboCount > 1 ? $"  |  🔥 x{Mathf.Clamp(1 + ComboCount / 2, 1, 5)} KOMBO" : "";
            GUI.Label(new Rect(topLeft.x + 16f, topLeft.y + 38f, 285f, 22f), $"Mesafe: {CurrentDistance:F0} m{comboTxt}", _subStyle);

            // Progress bar to next biome (every 220m)
            float biomeProgress = (CurrentDistance % 220f) / 220f;
            DrawRect(new Rect(topLeft.x + 16f, topLeft.y + 62f, 276f, 6f), new Color(1f, 1f, 1f, 0.15f));
            DrawRect(new Rect(topLeft.x + 16f, topLeft.y + 62f, 276f * biomeProgress, 6f), biome.StripeColor);

            // 3. Top-Right Score & Controls Card
            Rect topRight = new Rect(Screen.width - 328f, 16f, 310f, 76f);
            DrawRect(topRight, new Color(0.06f, 0.09f, 0.16f, 0.88f));
            DrawRect(new Rect(topRight.x + topRight.width - 6f, topRight.y, 6f, topRight.height), new Color(1f, 0.82f, 0.22f));
            GUI.Label(new Rect(topRight.x + 16f, topRight.y + 8f, 280f, 28f), $"🏆 SKOR: {Score:N0}", _headerStyle);
            GUI.Label(new Rect(topRight.x + 16f, topRight.y + 36f, 280f, 20f), $"En Yüksek: {HighScore:N0}  |  [Boşluk]: Bölün/Birleş", _subStyle);
            GUI.Label(new Rect(topRight.x + 16f, topRight.y + 54f, 280f, 18f), "[A/D veya Fare]: Yönlendir  |  [R]: Yeniden", _subStyle);

            // 4. Bottom-Center Jelly Mass & Size Gauge Bar + Status Banner
            float gaugeW = 480f;
            Rect bottomCard = new Rect((Screen.width - gaugeW) * 0.5f, Screen.height - 96f, gaugeW, 78f);
            DrawRect(bottomCard, new Color(0.06f, 0.09f, 0.16f, 0.90f));

            string sizeCategory = CurrentMass <= 75f
                ? "KÜÇÜK (Dar Delikten Sığar)"
                : (CurrentMass >= 115f ? "DEV (Duvarları Kırar!)" : "NORMAL BOYUT");
            string formTag = IsSplit ? "🟣 2 PARÇA (BÖLÜNMÜŞ ÷2)" : "🔵 TEK PARÇA";

            _badgeStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(bottomCard.x + 12f, bottomCard.y + 6f, gaugeW - 24f, 22f),
                $"KÜTLE: {CurrentMass:F0}  —  {sizeCategory}   |   {formTag}", _badgeStyle);

            // Gauge bar background (0..180 mass scale)
            Rect barBg = new Rect(bottomCard.x + 20f, bottomCard.y + 32f, gaugeW - 40f, 14f);
            DrawRect(barBg, new Color(0.16f, 0.20f, 0.28f, 1f));

            float fillRatio = Mathf.Clamp01(CurrentMass / 180f);
            Color barCol = CurrentMass <= 75f
                ? new Color(0.25f, 0.88f, 1.00f)
                : (CurrentMass >= 115f ? new Color(0.22f, 0.96f, 0.52f) : new Color(0.35f, 0.70f, 1.00f));
            DrawRect(new Rect(barBg.x, barBg.y, barBg.width * fillRatio, barBg.height), barCol);

            // Threshold markers at 75 (Small limit) and 115 (Smash limit)
            float mark75X = barBg.x + barBg.width * (75f / 180f);
            float mark115X = barBg.x + barBg.width * (115f / 180f);
            DrawRect(new Rect(mark75X, barBg.y - 2f, 2f, barBg.height + 4f), new Color(1f, 0.38f, 0.42f));
            DrawRect(new Rect(mark115X, barBg.y - 2f, 2f, barBg.height + 4f), new Color(0.25f, 1f, 0.55f));

            // Status Banner inside bottom card
            _bannerStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
            GUI.Label(new Rect(bottomCard.x + 10f, bottomCard.y + 49f, gaugeW - 20f, 24f), StatusBanner, _bannerStyle);

            // 5. Game Over Modal
            if (IsGameOver)
            {
                float w = 520f;
                float h = 220f;
                Rect boxRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

                DrawRect(new Rect(boxRect.x - 3f, boxRect.y - 3f, boxRect.width + 6f, boxRect.height + 6f), new Color(1f, 0.32f, 0.40f, 0.9f));
                DrawRect(boxRect, new Color(0.06f, 0.09f, 0.16f, 0.96f));

                GUIStyle goTitle = new GUIStyle(_headerStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 28 };
                goTitle.normal.textColor = new Color(1f, 0.38f, 0.44f);
                GUI.Label(new Rect(boxRect.x + 20f, boxRect.y + 18f, w - 40f, 40f), "OYUN BİTTİ!", goTitle);

                _bannerStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(boxRect.x + 25f, boxRect.y + 66f, w - 50f, 65f),
                    $"{StatusBanner}\nMesafe: {CurrentDistance:F0} m   |   Skor: {Score:N0}   |   Rekor: {HighScore:N0}", _bannerStyle);

                if (GUI.Button(new Rect(boxRect.x + (w - 240f) * 0.5f, boxRect.y + 148f, 240f, 48f), "TEKRAR OYNA (Boşluk / R)"))
                {
                    StartNewEndlessRun();
                }
            }
        }
    }
}
