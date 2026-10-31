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
        public float baseForwardSpeed = 10.5f;
        public float maxForwardSpeed = 19f;
        public float speedRampPerHundredMeters = 1.4f;
        public float lateralSteerSpeed = 9.5f;
        public float maxLateralOffset = 2.45f;
        public float roadWidth = 6.4f;
        public float sliceLength = 2.0f;
        public float lookAheadSpawnDistance = 96f;
        public float cleanupBehindDistance = 18f;

        [Header("Live State")]
        public float CurrentDistance;
        public float CurrentLateralOffset;
        public float CurrentMass = 100f;
        public bool IsSplit;
        public int Score;
        public bool IsGameOver;
        public string StatusBanner = "Yeşil (+): Büyü | Kırmızı (-): Küçül | Mor (÷2): Bölün | Altın (><): Birleş";
        public float StatusBannerTimer = 6f;

        private GameObject _secondarySplitVisual;
        private float _spawnedSliceUpToZ;
        private float _nextObstacleZ = 24f;
        private int _obstacleCounter;
        private Vector2 _dragPrevPos;
        private bool _dragging;
        private float _bounceCooldown;

        private readonly List<GameObject> _spawnedChunks = new List<GameObject>();
        private readonly List<EndlessGateObstacle> _activeObstacles = new List<EndlessGateObstacle>();

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            StartNewEndlessRun();
        }

        public void StartNewEndlessRun()
        {
            ClearTrack();
            CurrentDistance = 0f;
            CurrentLateralOffset = 0f;
            CurrentMass = 90f;
            IsSplit = false;
            Score = 0;
            IsGameOver = false;
            _spawnedSliceUpToZ = -6f;
            _nextObstacleZ = 22f;
            _obstacleCounter = 0;
            _bounceCooldown = 0f;
            SetBanner("Yol boyunca A/D veya Fare ile yönlendir! Engellere uygun nesneleri yakala!", 4.5f);

            EnsurePlayerObjects();
            SyncJellyVisualsAndPosition(0f);

            while (_spawnedSliceUpToZ < lookAheadSpawnDistance)
            {
                SpawnNextRoadSlice();
            }
            UpdateCameraImmediate();
        }

        public void BuildInitialPreviewInEditor(JellyController existingPlayer)
        {
            if (existingPlayer != null) primaryPlayer = existingPlayer;
            if (mainCamera == null) mainCamera = Camera.main;
            StartNewEndlessRun();
        }

        private void EnsurePlayerObjects()
        {
            EnsureTrackRoot();
            if (primaryPlayer == null)
                primaryPlayer = FindAnyObjectByType<JellyController>();

            if (primaryPlayer != null)
            {
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
                    LevelLoader.ApplyMaterialOrColor(_secondarySplitVisual, jellyMaterial, new Color(0.28f, 0.80f, 0.98f, 0.9f));
                }
            }
            _secondarySplitVisual.SetActive(IsSplit);
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
            for (int i = trackRoot.childCount - 1; i >= 0; i--)
            {
                var ch = trackRoot.GetChild(i);
                if (primaryPlayer != null && ch == primaryPlayer.transform) continue;
                if (Application.isPlaying) Destroy(ch.gameObject);
                else DestroyImmediate(ch.gameObject);
            }
            _secondarySplitVisual = null;
        }

        public static void EvaluateRoadCurve(float z, out Vector3 pos, out Quaternion rot, out Vector3 right)
        {
            float blend = Mathf.Clamp01((z - 4f) / 14f);
            float x = blend * (Mathf.Sin(z * 0.055f) * 6.2f + Mathf.Sin(z * 0.115f) * 2.4f);
            float y = blend * (Mathf.Sin(z * 0.065f) * 2.1f + Mathf.Cos(z * 0.13f) * 0.85f - 0.85f);

            float dz = 0.25f;
            float z2 = z + dz;
            float blend2 = Mathf.Clamp01((z2 - 4f) / 14f);
            float x2 = blend2 * (Mathf.Sin(z2 * 0.055f) * 6.2f + Mathf.Sin(z2 * 0.115f) * 2.4f);
            float y2 = blend2 * (Mathf.Sin(z2 * 0.065f) * 2.1f + Mathf.Cos(z2 * 0.13f) * 0.85f - 0.85f);

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

            // 1. Sample horizontal steering (A/D, Arrow keys, Mouse/Touch drag)
            float steerInput = 0f;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerInput -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerInput += 1f;
            }

            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                Vector2 p = ts.primaryTouch.position.ReadValue();
                if (!_dragging) { _dragging = true; _dragPrevPos = p; }
                steerInput += (p.x - _dragPrevPos.x) * 0.12f;
                _dragPrevPos = p;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                if (!_dragging) { _dragging = true; _dragPrevPos = p; }
                steerInput += (p.x - _dragPrevPos.x) * 0.12f;
                _dragPrevPos = p;
            }
            else
            {
                _dragging = false;
            }

            float maxOffset = IsSplit ? maxLateralOffset - 0.85f : maxLateralOffset;
            CurrentLateralOffset = Mathf.Clamp(CurrentLateralOffset + steerInput * lateralSteerSpeed * dt, -maxOffset, maxOffset);

            // 2. Advance along the winding road
            float speed = Mathf.Min(maxForwardSpeed, baseForwardSpeed + (CurrentDistance * 0.01f) * speedRampPerHundredMeters);
            if (_bounceCooldown > 0f)
                speed = -5.5f;

            CurrentDistance = Mathf.Max(0f, CurrentDistance + speed * dt);

            // 3. Position & animate Jelly (single or split twin) on the 3D spline road
            SyncJellyVisualsAndPosition(dt * speed);

            // 4. Evaluate cutout obstacles as player crosses their Z plane
            CheckObstacles();

            // 5. Generate new curved road slices & obstacles ahead, cleanup behind
            while (_spawnedSliceUpToZ < CurrentDistance + lookAheadSpawnDistance)
            {
                SpawnNextRoadSlice();
            }
            CleanupBehindSlices();

            // 6. Smoothly follow the winding road with the camera
            UpdateCameraSmooth(dt);
        }

        private void SyncJellyVisualsAndPosition(float forwardDelta)
        {
            EnsurePlayerObjects();
            if (primaryPlayer == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out Quaternion roadRot, out Vector3 roadRight);
            Vector3 roadUp = roadRot * Vector3.up;

            float singleMass = IsSplit ? CurrentMass * 0.5f : CurrentMass;
            float radius = JellyMassModel.ComputeRadius(singleMass, 100f, 0.52f);
            float diameter = radius * 2f;

            if (primaryPlayer.massController != null)
                primaryPlayer.massController.SetMassImmediate(singleMass);

            if (!IsSplit)
            {
                Vector3 worldPos = roadCenter + roadRight * CurrentLateralOffset + roadUp * (radius + 0.02f);
                primaryPlayer.transform.position = worldPos;
                primaryPlayer.transform.localScale = Vector3.one * diameter;
                primaryPlayer.transform.Rotate(roadRight, forwardDelta * 110f, Space.World);
                if (_secondarySplitVisual != null && _secondarySplitVisual.activeSelf)
                    _secondarySplitVisual.SetActive(false);
            }
            else
            {
                float spread = 1.32f;
                Vector3 leftPos = roadCenter + roadRight * (CurrentLateralOffset - spread) + roadUp * (radius + 0.02f);
                Vector3 rightPos = roadCenter + roadRight * (CurrentLateralOffset + spread) + roadUp * (radius + 0.02f);

                primaryPlayer.transform.position = leftPos;
                primaryPlayer.transform.localScale = Vector3.one * diameter;
                primaryPlayer.transform.Rotate(roadRight, forwardDelta * 120f, Space.World);

                if (_secondarySplitVisual != null)
                {
                    if (!_secondarySplitVisual.activeSelf) _secondarySplitVisual.SetActive(true);
                    _secondarySplitVisual.transform.position = rightPos;
                    _secondarySplitVisual.transform.localScale = Vector3.one * diameter;
                    _secondarySplitVisual.transform.rotation = primaryPlayer.transform.rotation;
                }
            }
        }

        public void ApplyPickup(EndlessPickupType type, float amount)
        {
            switch (type)
            {
                case EndlessPickupType.Grow:
                    CurrentMass = Mathf.Clamp(CurrentMass + amount, 25f, 220f);
                    Score += 10;
                    SetBanner($"🟢 BÜYÜDÜN! (+{amount:F0} Kütle -> {CurrentMass:F0})", 2.0f);
                    break;

                case EndlessPickupType.Shrink:
                    CurrentMass = Mathf.Clamp(CurrentMass - amount, 30f, 220f);
                    Score += 10;
                    SetBanner($"🔴 KÜÇÜLDÜN! (-{amount:F0} Kütle -> {CurrentMass:F0})", 2.0f);
                    break;

                case EndlessPickupType.Split:
                    if (!IsSplit)
                    {
                        IsSplit = true;
                        Score += 15;
                        SetBanner("🟣 İKİYE BÖLÜNDÜN! (÷2) Orta direkli tünelden geçebilirsin!", 2.5f);
                    }
                    break;

                case EndlessPickupType.Merge:
                    if (IsSplit)
                    {
                        IsSplit = false;
                        Score += 15;
                        SetBanner("🟡 TEKRAR BİRLEŞTİN! (><) Tek büyük jöle oldun!", 2.5f);
                    }
                    break;
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
            switch (obs.obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole:
                {
                    bool alignedCenter = !IsSplit && Mathf.Abs(CurrentLateralOffset) <= 0.72f;
                    bool smallEnough = CurrentMass <= obs.maxAllowedMassForSmallHole;
                    if (alignedCenter && smallEnough)
                    {
                        obs.resolved = true;
                        Score += 30;
                        SetBanner("✓ DAR DELİKTEN GEÇTİN! (+30 Puan)", 2.2f);
                    }
                    else
                    {
                        string reason = IsSplit
                            ? "✖ DAR DELİK TEK VE KÜÇÜK JÖLE İSTER! Altın (><) ve Kırmızı (-) yakala!"
                            : (!alignedCenter
                                ? "✖ DUVARA ÇARPTIN! Ortadaki deliğe hizalan!"
                                : $"✖ ÇOK BÜYÜKSÜN ({CurrentMass:F0} > {obs.maxAllowedMassForSmallHole:F0})! Kırmızı (-) yakalayıp küçül!");
                        TriggerObstacleCrash(obs, reason);
                    }
                    break;
                }

                case EndlessObstacleType.TwinSplitTunnels:
                {
                    bool alignedStraddle = IsSplit && Mathf.Abs(CurrentLateralOffset) <= 0.85f;
                    if (alignedStraddle)
                    {
                        obs.resolved = true;
                        Score += 35;
                        SetBanner("✓ İKİLİ TÜNELDEN BÖLÜNEREK GEÇTİN! (+35 Puan)", 2.2f);
                    }
                    else
                    {
                        string reason = !IsSplit
                            ? "✖ ORTADA DİREK VAR! Önce Mor (÷2) kristali yakalayıp ikiye bölünmelisin!"
                            : "✖ DİREĞE ÇARPTIN! İki parçayı yola ortala!";
                        TriggerObstacleCrash(obs, reason);
                    }
                    break;
                }

                case EndlessObstacleType.HeavySmashWall:
                {
                    bool alignedCenter = Mathf.Abs(CurrentLateralOffset) <= 1.25f;
                    bool heavyEnough = !IsSplit && CurrentMass >= obs.minRequiredMassForSmash;
                    if (alignedCenter && heavyEnough)
                    {
                        obs.resolved = true;
                        obs.ShatterBreakablePiece();
                        Score += 40;
                        SetBanner("💥 DEV JÖLEYLE DUVARI PARÇALADIN! (+40 Puan)", 2.4f);
                    }
                    else
                    {
                        string reason = IsSplit
                            ? "✖ KIRILABİLİR DUVAR İÇİN TEK VE BÜYÜK JÖLE GEREKİR!"
                            : $"✖ KÜTLEN YETERSİZ ({CurrentMass:F0} < {obs.minRequiredMassForSmash:F0})! Yeşil (+) yakalayıp büyü!";
                        TriggerObstacleCrash(obs, reason);
                    }
                    break;
                }

                case EndlessObstacleType.LeftOpeningWall:
                {
                    if (!IsSplit && CurrentLateralOffset <= -0.85f)
                    {
                        obs.resolved = true;
                        Score += 25;
                        SetBanner("✓ SOL AÇIKLIKTAN GEÇTİN! (+25 Puan)", 2.0f);
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "✖ SAĞ TARAF KAPALI! Sol açıklıktan geç!");
                    }
                    break;
                }

                case EndlessObstacleType.RightOpeningWall:
                {
                    if (!IsSplit && CurrentLateralOffset >= 0.85f)
                    {
                        obs.resolved = true;
                        Score += 25;
                        SetBanner("✓ SAĞ AÇIKLIKTAN GEÇTİN! (+25 Puan)", 2.0f);
                    }
                    else
                    {
                        TriggerObstacleCrash(obs, "✖ SOL TARAF KAPALI! Sağ açıklıktan geç!");
                    }
                    break;
                }
            }
        }

        private void TriggerObstacleCrash(EndlessGateObstacle obs, string reason)
        {
            _bounceCooldown = 0.55f;
            CurrentMass -= 35f;
            SetBanner(reason, 3.2f);
            if (CurrentMass < 25f)
            {
                CurrentMass = 0f;
                IsGameOver = true;
                SetBanner($"💥 OYUN BİTTİ! Skor: {Score} | Mesafe: {CurrentDistance:F0}m  [R veya Tıkla: Yeniden Başla]", 999f);
            }
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
            Vector3 up = rot * Vector3.up;

            var sliceRoot = new GameObject($"RoadSlice_{z:F0}");
            sliceRoot.transform.SetParent(trackRoot, false);
            sliceRoot.transform.SetPositionAndRotation(center, rot);
            _spawnedChunks.Add(sliceRoot);

            bool altStripe = Mathf.FloorToInt(z / sliceLength) % 2 == 0;
            Color floorCol = altStripe ? new Color(0.91f, 0.94f, 0.98f) : new Color(0.84f, 0.89f, 0.96f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "RoadBed";
            floor.transform.SetParent(sliceRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(roadWidth, 0.5f, sliceLength + 0.25f);
            LevelLoader.ApplyMaterialOrColor(floor, trackMaterial, floorCol);

            float halfW = roadWidth * 0.5f + 0.2f;
            var leftRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftRail.name = "LeftCurb";
            leftRail.transform.SetParent(sliceRoot.transform, false);
            leftRail.transform.localPosition = new Vector3(-halfW, 0.22f, 0f);
            leftRail.transform.localScale = new Vector3(0.35f, 0.55f, sliceLength + 0.25f);
            LevelLoader.ApplyMaterialOrColor(leftRail, gateMaterial, new Color(0.95f, 0.56f, 0.44f));

            var rightRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightRail.name = "RightCurb";
            rightRail.transform.SetParent(sliceRoot.transform, false);
            rightRail.transform.localPosition = new Vector3(halfW, 0.22f, 0f);
            rightRail.transform.localScale = new Vector3(0.35f, 0.55f, sliceLength + 0.25f);
            LevelLoader.ApplyMaterialOrColor(rightRail, gateMaterial, new Color(0.95f, 0.56f, 0.44f));

            if (z >= _nextObstacleZ)
            {
                SpawnObstacleAndPreparationPickups(_nextObstacleZ, _obstacleCounter);
                _obstacleCounter++;
                _nextObstacleZ += 26f;
            }
        }

        private void SpawnObstacleAndPreparationPickups(float gateZ, int index)
        {
            int pattern = index % 5;
            switch (pattern)
            {
                case 0:
                    // 1. Small Center Hole: needs Shrink (-) pickup before the gate!
                    SpawnPickupOnRoad(gateZ - 13f, -1.35f, EndlessPickupType.Shrink, 30f);
                    SpawnPickupOnRoad(gateZ - 13f, +1.35f, EndlessPickupType.Grow, 25f);
                    SpawnPickupOnRoad(gateZ - 7.5f, 0f, EndlessPickupType.Shrink, 25f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.SmallCenterHole);
                    break;

                case 1:
                    // 2. Twin Split Tunnels: needs Purple Split (÷2) before the gate, and Gold Merge (><) after!
                    SpawnPickupOnRoad(gateZ - 12f, -1.1f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 12f, +1.1f, EndlessPickupType.Split, 0f);
                    SpawnPickupOnRoad(gateZ - 7f, 0f, EndlessPickupType.Split, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.TwinSplitTunnels);
                    SpawnPickupOnRoad(gateZ + 6.5f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 10.5f, -1.1f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ + 10.5f, +1.1f, EndlessPickupType.Merge, 0f);
                    break;

                case 2:
                    // 3. Heavy Smash Wall: needs Green Grow (+) pickups to reach 125+ mass!
                    SpawnPickupOnRoad(gateZ - 14f, 0f, EndlessPickupType.Merge, 0f);
                    SpawnPickupOnRoad(gateZ - 11f, -1.2f, EndlessPickupType.Grow, 35f);
                    SpawnPickupOnRoad(gateZ - 11f, +1.2f, EndlessPickupType.Shrink, 25f);
                    SpawnPickupOnRoad(gateZ - 6.5f, 0f, EndlessPickupType.Grow, 35f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.HeavySmashWall);
                    break;

                case 3:
                    // 4. Left Opening Wall on a curve
                    SpawnPickupOnRoad(gateZ - 11f, -1.4f, EndlessPickupType.Grow, 20f);
                    SpawnPickupOnRoad(gateZ - 7f, -1.4f, EndlessPickupType.Merge, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.LeftOpeningWall);
                    break;

                case 4:
                    // 5. Right Opening Wall on a curve
                    SpawnPickupOnRoad(gateZ - 11f, +1.4f, EndlessPickupType.Shrink, 20f);
                    SpawnPickupOnRoad(gateZ - 7f, +1.4f, EndlessPickupType.Merge, 0f);
                    BuildCutoutGate(gateZ, EndlessObstacleType.RightOpeningWall);
                    break;
            }
        }

        private void SpawnPickupOnRoad(float z, float lateralOffset, EndlessPickupType type, float amount)
        {
            EvaluateRoadCurve(z, out Vector3 center, out Quaternion rot, out Vector3 right);
            Vector3 up = rot * Vector3.up;
            Vector3 pos = center + right * lateralOffset + up * 0.68f;

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
                    col = new Color(0.25f, 0.95f, 0.48f);
                    // Plus sign crossbar
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
                    // Minus sign bar
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
                    go.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                    go.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                    mat = null;
                    col = new Color(0.72f, 0.30f, 0.98f);
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
            _spawnedChunks.Add(go);
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

            _spawnedChunks.Add(gateRoot);
            _activeObstacles.Add(obs);

            Color wallColor = new Color(0.95f, 0.48f, 0.38f);
            Color lintelColor = new Color(0.28f, 0.34f, 0.46f);

            switch (type)
            {
                case EndlessObstacleType.SmallCenterHole:
                    // Left solid wall (-3.1 to -0.58), Right solid wall (+0.58 to +3.1), Top lintel over hole
                    CreateWallBlock(gateRoot.transform, "LeftWall", new Vector3(-1.85f, 1.1f, 0f), new Vector3(2.5f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightWall", new Vector3(+1.85f, 1.1f, 0f), new Vector3(2.5f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "TopHoleFrame", new Vector3(0f, 1.6f, 0f), new Vector3(1.25f, 1.2f, 0.55f), drainMaterial, new Color(0.95f, 0.30f, 0.38f));
                    break;

                case EndlessObstacleType.TwinSplitTunnels:
                    // Center Divider Pillar (blocks unsplit jelly!) + Left/Right outer pillars + Top arch
                    CreateWallBlock(gateRoot.transform, "OuterLeftPost", new Vector3(-2.75f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "CenterSplitPillar", new Vector3(0f, 1.1f, 0f), new Vector3(1.15f, 2.2f, 0.65f), null, new Color(0.72f, 0.30f, 0.98f));
                    CreateWallBlock(gateRoot.transform, "OuterRightPost", new Vector3(+2.75f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "TopTwinArch", new Vector3(0f, 1.95f, 0f), new Vector3(6.2f, 0.5f, 0.55f), null, lintelColor);
                    break;

                case EndlessObstacleType.HeavySmashWall:
                    // Side walls + Breakable Emerald/Gold Smash Center
                    CreateWallBlock(gateRoot.transform, "LeftFrame", new Vector3(-2.3f, 1.1f, 0f), new Vector3(1.6f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "RightFrame", new Vector3(+2.3f, 1.1f, 0f), new Vector3(1.6f, 2.2f, 0.55f), gateMaterial, wallColor);
                    var smashCenter = CreateWallBlock(gateRoot.transform, "BreakableSmashCenter", new Vector3(0f, 0.95f, 0f), new Vector3(2.8f, 1.9f, 0.45f), pickupMaterial, new Color(0.35f, 0.92f, 0.52f));
                    obs.breakableCenterPiece = smashCenter.transform;
                    break;

                case EndlessObstacleType.LeftOpeningWall:
                    // Right & Center blocked (-0.5 to +3.1), Left side (-2.6 to -0.6) open!
                    CreateWallBlock(gateRoot.transform, "BlockedRightSide", new Vector3(+1.25f, 1.1f, 0f), new Vector3(3.7f, 2.2f, 0.55f), gateMaterial, wallColor);
                    CreateWallBlock(gateRoot.transform, "LeftArchTop", new Vector3(-1.65f, 1.85f, 0f), new Vector3(2.2f, 0.7f, 0.55f), goalMaterial, lintelColor);
                    break;

                case EndlessObstacleType.RightOpeningWall:
                    // Left & Center blocked (-3.1 to +0.5), Right side (+0.6 to +2.6) open!
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
        }

        private void UpdateCameraImmediate()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out Quaternion roadRot, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 7.5f, out Vector3 lookAheadPt, out _, out _);

            Vector3 camOffset = roadRot * new Vector3(0f, 5.2f, -7.2f) + roadRight * (CurrentLateralOffset * 0.45f);
            mainCamera.transform.position = roadCenter + camOffset;
            mainCamera.transform.rotation = Quaternion.LookRotation((lookAheadPt + Vector3.up * 0.5f) - mainCamera.transform.position, Vector3.up);
        }

        private void UpdateCameraSmooth(float dt)
        {
            if (mainCamera == null) return;

            EvaluateRoadCurve(CurrentDistance, out Vector3 roadCenter, out Quaternion roadRot, out Vector3 roadRight);
            EvaluateRoadCurve(CurrentDistance + 8.0f, out Vector3 lookAheadPt, out _, out _);

            Vector3 desiredPos = roadCenter + roadRot * new Vector3(0f, 5.2f, -7.2f) + roadRight * (CurrentLateralOffset * 0.45f);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPos, Mathf.Clamp01(dt * 10f));

            Quaternion desiredRot = Quaternion.LookRotation((lookAheadPt + Vector3.up * 0.5f) - mainCamera.transform.position, Vector3.up);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, desiredRot, Mathf.Clamp01(dt * 10f));
        }
    }
}
