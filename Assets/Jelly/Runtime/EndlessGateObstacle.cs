using UnityEngine;

namespace Jelly
{
    public enum EndlessObstacleType
    {
        SmallCenterHole,
        TwinSplitTunnels,
        HeavySmashWall,
        LeftOpeningWall,
        RightOpeningWall,
        SlidingDoorWall,      // Hareketli Kayar Kapı (sağa-sola kayan açıklık)
        PincerShutters,       // Açılıp Kapanan Makas Duvarlar
        VerticalCrusherGate,  // İnip Kalkan Giyotin / Piston Kapı
        SwingingPendulum,     // Sallanan Dev Balyoz Sarkaç
        MovingTwinTunnels     // Sağa-sola kayan İkiz Bölünme Tüneli
    }

    public class EndlessGateObstacle : MonoBehaviour
    {
        public EndlessObstacleType obstacleType;
        public float trackZ;
        public float maxAllowedMassForSmallHole = 75f;
        public float minRequiredMassForSmash = 115f;
        public Transform breakableCenterPiece;
        public Transform movingPartA;
        public Transform movingPartB;
        public Transform movingPartC;
        public float animSpeed = 2.2f;
        public float animPhaseOffset;
        public bool resolved;
        public bool hasBouncedOnce;

        // Live dynamic state for collision evaluation
        public float CurrentOpeningCenterX { get; private set; }
        public float CurrentPincerGapHalfWidth { get; private set; } = 1.2f;
        public float CurrentCrusherBottomY { get; private set; } = 1.5f;
        public float CurrentHazardCenterX { get; private set; }

        private void Start()
        {
            if (Mathf.Approximately(animPhaseOffset, 0f))
                animPhaseOffset = trackZ * 0.19f;
        }

        private void Update()
        {
            if (resolved) return;
            float t = Time.time * animSpeed + animPhaseOffset;

            switch (obstacleType)
            {
                case EndlessObstacleType.SlidingDoorWall:
                {
                    // The gap slides between X = -1.55 and X = +1.55
                    CurrentOpeningCenterX = Mathf.Sin(t) * 1.55f;
                    // movingPartA is Left sliding door, movingPartB is Right sliding door
                    // Gap width is 1.90m (halfGap = 0.95m)
                    float halfGap = 0.95f;
                    if (movingPartA != null)
                    {
                        float leftInnerEdge = CurrentOpeningCenterX - halfGap;
                        float leftWidth = Mathf.Max(0.4f, leftInnerEdge - (-3.2f));
                        movingPartA.localPosition = new Vector3(-3.2f + leftWidth * 0.5f, 1.1f, 0f);
                        movingPartA.localScale = new Vector3(leftWidth, 2.2f, 0.58f);
                    }
                    if (movingPartB != null)
                    {
                        float rightInnerEdge = CurrentOpeningCenterX + halfGap;
                        float rightWidth = Mathf.Max(0.4f, 3.2f - rightInnerEdge);
                        movingPartB.localPosition = new Vector3(3.2f - rightWidth * 0.5f, 1.1f, 0f);
                        movingPartB.localScale = new Vector3(rightWidth, 2.2f, 0.58f);
                    }
                    break;
                }

                case EndlessObstacleType.PincerShutters:
                {
                    // Two heavy blast doors open and close in the middle
                    // Gap half-width oscillates between 0.25m (almost closed -> split or wait!) and 1.65m (wide open)
                    float wave = (Mathf.Sin(t * 1.15f) + 1f) * 0.5f; // 0..1
                    CurrentPincerGapHalfWidth = Mathf.Lerp(0.22f, 1.65f, wave);
                    if (movingPartA != null)
                    {
                        float w = 2.4f;
                        movingPartA.localPosition = new Vector3(-CurrentPincerGapHalfWidth - w * 0.5f, 1.1f, 0f);
                    }
                    if (movingPartB != null)
                    {
                        float w = 2.4f;
                        movingPartB.localPosition = new Vector3(+CurrentPincerGapHalfWidth + w * 0.5f, 1.1f, 0f);
                    }
                    break;
                }

                case EndlessObstacleType.VerticalCrusherGate:
                {
                    // Heavy piston moves up and down between bottomY = 0.65m (low, only small jelly <75 fits) and 1.85m (high, any jelly fits)
                    float wave = (Mathf.Sin(t * 1.25f) + 1f) * 0.5f;
                    CurrentCrusherBottomY = Mathf.Lerp(0.62f, 1.85f, wave);
                    if (movingPartA != null)
                    {
                        float blockHeight = 1.6f;
                        movingPartA.localPosition = new Vector3(0f, CurrentCrusherBottomY + blockHeight * 0.5f, 0f);
                    }
                    break;
                }

                case EndlessObstacleType.SwingingPendulum:
                {
                    // Overhead pendulum swings between -42 deg and +42 deg around pivot at (0, 3.8, 0)
                    float angleDeg = Mathf.Sin(t * 1.35f) * 42f;
                    if (movingPartA != null)
                    {
                        movingPartA.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                    }
                    // Arm length is 3.0m downward from Y=3.8m -> head X = -Sin(angle) * 3.0m
                    CurrentHazardCenterX = -Mathf.Sin(angleDeg * Mathf.Deg2Rad) * 3.0f;
                    break;
                }

                case EndlessObstacleType.MovingTwinTunnels:
                {
                    // Entire twin tunnel assembly shifts left/right between -0.85m and +0.85m
                    CurrentOpeningCenterX = Mathf.Sin(t * 0.9f) * 0.85f;
                    if (movingPartA != null)
                    {
                        movingPartA.localPosition = new Vector3(CurrentOpeningCenterX, 0f, 0f);
                    }
                    break;
                }
            }
        }

        public string GetGateHeaderText()
        {
            switch (obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole: return $"DAR DELİK (Kütle ≤ {maxAllowedMassForSmallHole:F0})";
                case EndlessObstacleType.TwinSplitTunnels: return "İKİZ TÜNEL (÷2 Bölün veya Yan Tünel)";
                case EndlessObstacleType.HeavySmashWall: return $"KIRILABİLİR DUVAR (Kütle ≥ {minRequiredMassForSmash:F0})";
                case EndlessObstacleType.LeftOpeningWall: return "◀ SOL AÇIKLIK";
                case EndlessObstacleType.RightOpeningWall: return "SAĞ AÇIKLIK ▶";
                case EndlessObstacleType.SlidingDoorWall: return "◀▶ HAREKETLİ KAYAR KAPI";
                case EndlessObstacleType.PincerShutters: return "▶◀ AÇILIR-KAPANIR MAKAS KAPI";
                case EndlessObstacleType.VerticalCrusherGate: return "▲▼ PİSTON KAPI (Kalkınca Geç / Küçül)";
                case EndlessObstacleType.SwingingPendulum: return "⚠ SALLANAN BALYOZ (Kaçın!)";
                case EndlessObstacleType.MovingTwinTunnels: return "◀÷2▶ HAREKETLİ İKİZ TÜNEL";
                default: return "ENGEL";
            }
        }

        public Color GetGateHeaderColor()
        {
            switch (obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole: return new Color(1.00f, 0.38f, 0.44f);
                case EndlessObstacleType.TwinSplitTunnels:
                case EndlessObstacleType.MovingTwinTunnels: return new Color(0.80f, 0.45f, 1.00f);
                case EndlessObstacleType.HeavySmashWall: return new Color(0.25f, 0.96f, 0.52f);
                case EndlessObstacleType.SlidingDoorWall:
                case EndlessObstacleType.PincerShutters:
                case EndlessObstacleType.VerticalCrusherGate: return new Color(0.25f, 0.85f, 1.00f);
                case EndlessObstacleType.SwingingPendulum: return new Color(1.00f, 0.65f, 0.18f);
                default: return new Color(1.00f, 0.85f, 0.30f);
            }
        }

        public void ShatterBreakablePiece()
        {
            Vector3 burstCenter = transform.position + Vector3.up * 1.0f;
            Color shardCol = GetGateHeaderColor();

            if (breakableCenterPiece != null)
            {
                burstCenter = breakableCenterPiece.position;
                breakableCenterPiece.gameObject.SetActive(false);
                SpawnDebrisBurst(burstCenter, shardCol, 10);
                return;
            }

            SpawnDebrisBurst(burstCenter, shardCol, 8);
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
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
