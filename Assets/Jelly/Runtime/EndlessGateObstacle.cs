using UnityEngine;

namespace Jelly
{
    public enum EndlessObstacleType
    {
        SmallCenterHole,       // 1. Dar Geçit (Ø 0.72 - 0.85m, Sadece Küçük Jöle ≤ 75)
        TwinSplitTunnels,      // 4. İkiz Tünel (Sol/sağ ~2.5m, Ortası kapalı, Bölünerek geçilir)
        HeavySmashWall,        // 3. Kırılabilir Cam Duvar (Sadece Dev Jöle ≥ 115, Parçalanabilir)
        LeftOpeningWall,
        RightOpeningWall,
        SlidingDoorWall,       // Hareketli Kayar Kapı
        PincerShutters,        // Açılıp Kapanan Makas Duvarlar
        VerticalCrusherGate,   // 2. Piston Kapı (Açıklık Yük: 0.62 - 0.85m, Sadece Küçük Jöle / Zamanlama)
        SwingingPendulum,      // Sarkaç (Genişlik ~1.2m, Zamanlamayla veya bölünerek geçilir)
        MovingTwinTunnels,     // Hareketli İkiz Tünel (Açılıp kapanan çift tünel, Bölünerek geçilir)
        SpinnerBar,            // Dönen Engel (Çap ~4.6m, Dönen kollar, zamanlama ile geçilir)
        MultiHoleDecisionWall, // 5. Çoklu Delik Duvarı (Boyut Seçimi / Bölünme Seçimi / Risk–Ödül Seçimi)
        FlexibleSqueezeGate,   // Faz 4: Esnek Geçit (Jöle daralarak uzun açıklıktan geçer ≤ 88)
        MassScaleGate,         // Faz 4: Kütle Terazisi (Kapıyı açmak için belirli kütle gerekir ≥ 105)
        SlidingHoleWall,       // Faz 4: Hareketli Delik Duvarı (Doğru zamanlama ister)
        MicroShatterGate       // Faz 6: Parçalanma Kapısı (Çok sayıda küçük parçaya dönüştürür)
    }

    public class EndlessGateObstacle : MonoBehaviour
    {
        public static float GlobalObstacleTimeScale = 1.0f;

        public EndlessObstacleType obstacleType;
        public MultiHoleWallConfig multiHoleConfig;
        public float trackZ;
        public float maxAllowedMassForSmallHole = 75f;
        public float minRequiredMassForSmash = 115f;
        public Transform breakableCenterPiece;
        public Transform movingPartA;
        public Transform movingPartB;
        public Transform movingPartC;
        public float animSpeed = 2.1f;
        public float animPhaseOffset;
        public bool resolved;
        public bool hasBouncedOnce;

        // Live dynamic state for collision evaluation
        public float CurrentOpeningCenterX { get; private set; }
        public float CurrentPincerGapHalfWidth { get; private set; } = 1.2f;
        public float CurrentCrusherBottomY { get; private set; } = 1.5f;
        public float CurrentHazardCenterX { get; private set; }
        public float CurrentSpinnerAngleDeg { get; private set; }

        private float _accumTime;

        public bool IsBreakableCrystalSurface => obstacleType == EndlessObstacleType.HeavySmashWall;

        private void Start()
        {
            if (Mathf.Approximately(animPhaseOffset, 0f))
                animPhaseOffset = trackZ * 0.19f;
            _accumTime = animPhaseOffset;

            if (obstacleType == EndlessObstacleType.MultiHoleDecisionWall && multiHoleConfig == null)
            {
                multiHoleConfig = MultiHoleWallConfig.CreateSizeChoicePreset();
            }
        }

        private void Update()
        {
            _accumTime += Time.deltaTime * animSpeed * GlobalObstacleTimeScale;
            float t = _accumTime;

            switch (obstacleType)
            {
                case EndlessObstacleType.SlidingDoorWall:
                {
                    CurrentOpeningCenterX = Mathf.Sin(t) * 1.45f;
                    float halfGap = 0.98f;
                    if (movingPartA != null)
                    {
                        float leftInnerEdge = CurrentOpeningCenterX - halfGap;
                        float leftWidth = Mathf.Max(0.35f, leftInnerEdge - (-3.2f));
                        movingPartA.localPosition = new Vector3(-3.2f + leftWidth * 0.5f, 1.15f, 0f);
                        movingPartA.localScale = new Vector3(leftWidth, 2.3f, 0.55f);
                    }
                    if (movingPartB != null)
                    {
                        float rightInnerEdge = CurrentOpeningCenterX + halfGap;
                        float rightWidth = Mathf.Max(0.35f, 3.2f - rightInnerEdge);
                        movingPartB.localPosition = new Vector3(3.2f - rightWidth * 0.5f, 1.15f, 0f);
                        movingPartB.localScale = new Vector3(rightWidth, 2.3f, 0.55f);
                    }
                    break;
                }

                case EndlessObstacleType.PincerShutters:
                {
                    float wave = (Mathf.Sin(t * 1.1f) + 1f) * 0.5f;
                    CurrentPincerGapHalfWidth = Mathf.Lerp(0.25f, 1.68f, wave);
                    if (movingPartA != null)
                    {
                        float w = 2.4f;
                        movingPartA.localPosition = new Vector3(-CurrentPincerGapHalfWidth - w * 0.5f, 1.15f, 0f);
                    }
                    if (movingPartB != null)
                    {
                        float w = 2.4f;
                        movingPartB.localPosition = new Vector3(+CurrentPincerGapHalfWidth + w * 0.5f, 1.15f, 0f);
                    }
                    break;
                }

                case EndlessObstacleType.VerticalCrusherGate:
                {
                    float wave = (Mathf.Sin(t * 1.2f) + 1f) * 0.5f;
                    CurrentCrusherBottomY = Mathf.Lerp(0.62f, 1.90f, wave);
                    if (movingPartA != null)
                    {
                        float blockHeight = 1.6f;
                        movingPartA.localPosition = new Vector3(0f, CurrentCrusherBottomY + blockHeight * 0.5f, 0f);
                    }
                    break;
                }

                case EndlessObstacleType.SwingingPendulum:
                {
                    float angleDeg = Mathf.Sin(t * 1.3f) * 42f;
                    if (movingPartA != null)
                    {
                        movingPartA.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                    }
                    CurrentHazardCenterX = -Mathf.Sin(angleDeg * Mathf.Deg2Rad) * 3.0f;
                    break;
                }

                case EndlessObstacleType.MovingTwinTunnels:
                {
                    CurrentOpeningCenterX = Mathf.Sin(t * 0.85f) * 0.80f;
                    if (movingPartA != null)
                    {
                        movingPartA.localPosition = new Vector3(CurrentOpeningCenterX, 0f, 0f);
                    }
                    break;
                }

                case EndlessObstacleType.SpinnerBar:
                {
                    CurrentSpinnerAngleDeg = (t * 82f) % 360f;
                    if (movingPartA != null)
                    {
                        movingPartA.localRotation = Quaternion.Euler(0f, CurrentSpinnerAngleDeg, 0f);
                    }
                    break;
                }

                case EndlessObstacleType.SlidingHoleWall:
                {
                    CurrentOpeningCenterX = Mathf.Sin(t * 1.15f) * 1.65f;
                    if (movingPartA != null)
                    {
                        movingPartA.localPosition = new Vector3(CurrentOpeningCenterX, 0.72f, 0f);
                    }
                    break;
                }
            }
        }

        public void SetCrusherBottomYForTesting(float bottomY)
        {
            CurrentCrusherBottomY = bottomY;
        }

        public void SetOpeningCenterXForTesting(float centerX)
        {
            CurrentOpeningCenterX = centerX;
        }

        public Phase2ObstacleEvaluation EvaluateCoreObstacle(float totalMass, float centerX, bool isSplit)
        {
            switch (obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole:
                    return Phase2CoreObstacleEvaluator.EvaluateSmallCenterHole(totalMass, centerX, isSplit, maxAllowedMassForSmallHole);
                case EndlessObstacleType.VerticalCrusherGate:
                    return Phase2CoreObstacleEvaluator.EvaluateVerticalCrusherGate(totalMass, centerX, isSplit, CurrentCrusherBottomY, maxAllowedMassForSmallHole);
                case EndlessObstacleType.HeavySmashWall:
                    return Phase2CoreObstacleEvaluator.EvaluateHeavySmashWall(totalMass, centerX, isSplit, minRequiredMassForSmash);
                case EndlessObstacleType.TwinSplitTunnels:
                    return Phase2CoreObstacleEvaluator.EvaluateTwinSplitTunnels(totalMass, centerX, isSplit);
                case EndlessObstacleType.MultiHoleDecisionWall:
                    return Phase2CoreObstacleEvaluator.EvaluateMultiHoleWall(
                        multiHoleConfig ?? MultiHoleWallConfig.CreateSizeChoicePreset(),
                        totalMass, centerX, isSplit);
                case EndlessObstacleType.FlexibleSqueezeGate:
                    return Phase2CoreObstacleEvaluator.EvaluateFlexibleSqueezeGate(totalMass, centerX, isSplit);
                case EndlessObstacleType.MassScaleGate:
                    return Phase2CoreObstacleEvaluator.EvaluateMassScaleGate(totalMass, centerX, isSplit);
                case EndlessObstacleType.SlidingHoleWall:
                    return Phase2CoreObstacleEvaluator.EvaluateSlidingHoleWall(totalMass, centerX, isSplit, CurrentOpeningCenterX);
                case EndlessObstacleType.MicroShatterGate:
                    return Phase2CoreObstacleEvaluator.EvaluateMicroShatterGate(totalMass, centerX, isSplit);
                default:
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                        CanPass = false,
                        ShatteredCrystal = false,
                        FeedbackBanner = "ENGEL"
                    };
            }
        }

        public string GetGateHeaderText()
        {
            switch (obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole: return $"DAR GEÇİT (Sadece Küçük Jöle ≤ {maxAllowedMassForSmallHole:F0})";
                case EndlessObstacleType.TwinSplitTunnels: return "İKİZ TÜNEL (÷2 Bölünerek Geçilir)";
                case EndlessObstacleType.HeavySmashWall: return $"KIRILABİLİR CAM DUVAR (Dev Jöle ≥ {minRequiredMassForSmash:F0})";
                case EndlessObstacleType.MultiHoleDecisionWall:
                    return multiHoleConfig != null && !string.IsNullOrEmpty(multiHoleConfig.HeaderTitle)
                        ? multiHoleConfig.HeaderTitle
                        : "ÇOKLU DELİK DUVARI (Boyut / Bölünme / Risk Seçimi)";
                case EndlessObstacleType.LeftOpeningWall: return "◀ SOL KORİDOR";
                case EndlessObstacleType.RightOpeningWall: return "SAĞ KORİDOR ▶";
                case EndlessObstacleType.SlidingDoorWall: return "◀▶ KAYAR KAPI";
                case EndlessObstacleType.PincerShutters: return "▶◀ MAKAS KAPI";
                case EndlessObstacleType.VerticalCrusherGate: return "⬇ PİSTON KAPI (Küçük Jöle / Zamanlama)";
                case EndlessObstacleType.SwingingPendulum: return "⚠ SARKAÇ (Zamanlama veya ÷2 Bölün)";
                case EndlessObstacleType.MovingTwinTunnels: return "◀÷2▶ HAREKETLİ İKİZ TÜNEL";
                case EndlessObstacleType.SpinnerBar: return "↻ DÖNEN ENGEL (Zamanlama ile Geç)";
                case EndlessObstacleType.FlexibleSqueezeGate: return "⟿ ESNEK GEÇİT (Daralarak Süzül ≤ 88)";
                case EndlessObstacleType.MassScaleGate: return "⚖ KÜTLE TERAZİSİ (Kapıyı Açmak İçin ≥ 105)";
                case EndlessObstacleType.SlidingHoleWall: return "⇄ HAREKETLİ DELİK DUVARI (Zamanlamayı Tuttur)";
                case EndlessObstacleType.MicroShatterGate: return "✨ PARÇALANMA KAPISI (Çoklu Mikro-Bölünme)";
                default: return "ENGEL";
            }
        }

        public Color GetGateHeaderColor()
        {
            switch (obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole: return new Color(0.62f, 0.36f, 0.96f);
                case EndlessObstacleType.TwinSplitTunnels:
                case EndlessObstacleType.MovingTwinTunnels: return new Color(0.84f, 0.36f, 1.00f);
                case EndlessObstacleType.HeavySmashWall: return new Color(0.28f, 0.88f, 1.00f);
                case EndlessObstacleType.MultiHoleDecisionWall: return new Color(1.00f, 0.82f, 0.24f);
                case EndlessObstacleType.VerticalCrusherGate: return new Color(1.00f, 0.78f, 0.18f);
                case EndlessObstacleType.SlidingDoorWall:
                case EndlessObstacleType.PincerShutters: return new Color(0.20f, 0.85f, 0.98f);
                case EndlessObstacleType.SwingingPendulum:
                case EndlessObstacleType.SpinnerBar: return new Color(0.96f, 0.45f, 0.92f);
                default: return new Color(1.00f, 0.80f, 0.22f);
            }
        }

        /// <summary>
        /// Only shatters the breakable center glass/crystal panel of HeavySmashWall.
        /// Solid walls NEVER disappear or crumble when the player fails!
        /// </summary>
        public void ShatterBreakablePiece()
        {
            if (!IsBreakableCrystalSurface) return;
            if (breakableCenterPiece != null && breakableCenterPiece.gameObject.activeSelf)
            {
                Vector3 burstCenter = breakableCenterPiece.position;
                breakableCenterPiece.gameObject.SetActive(false);
                SpawnDebrisBurst(burstCenter, new Color(0.48f, 0.92f, 1.00f), 16);
            }
        }

        private static void SpawnDebrisBurst(Vector3 worldPos, Color color, int count)
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < count; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "GateDebrisShard";
                var col = shard.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);

                shard.transform.position = worldPos + Random.insideUnitSphere * 0.6f;
                shard.transform.localScale = Vector3.one * Random.Range(0.18f, 0.38f);
                shard.transform.rotation = Random.rotation;
                LevelLoader.ApplyMaterialOrColor(shard, null, color);

                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.linearVelocity = new Vector3(
                    Random.Range(-4.5f, 4.5f),
                    Random.Range(3.0f, 6.5f),
                    Random.Range(4.0f, 10.5f));
                rb.angularVelocity = Random.insideUnitSphere * 12f;

                Object.Destroy(shard, 1.35f);
            }
        }
    }
}
