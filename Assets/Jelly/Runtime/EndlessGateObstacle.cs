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
        public float animSpeed = 2.1f;
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
            float t = Time.time * animSpeed + animPhaseOffset;

            switch (obstacleType)
            {
                case EndlessObstacleType.SlidingDoorWall:
                {
                    // The gap slides between X = -1.45 and X = +1.45
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
            }
        }

        public string GetGateHeaderText()
        {
            switch (obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole: return $"DAR GEÇİT (Kütle ≤ {maxAllowedMassForSmallHole:F0})";
                case EndlessObstacleType.TwinSplitTunnels: return "İKİZ TÜNEL (÷2 Bölün)";
                case EndlessObstacleType.HeavySmashWall: return $"CAM DUVAR (Kütle ≥ {minRequiredMassForSmash:F0} Kırar)";
                case EndlessObstacleType.LeftOpeningWall: return "◀ SOL KORİDOR";
                case EndlessObstacleType.RightOpeningWall: return "SAĞ KORİDOR ▶";
                case EndlessObstacleType.SlidingDoorWall: return "◀▶ KAYAR KAPI";
                case EndlessObstacleType.PincerShutters: return "▶◀ MAKAS KAPI";
                case EndlessObstacleType.VerticalCrusherGate: return "▲▼ PİSTON KAPI";
                case EndlessObstacleType.SwingingPendulum: return "⚠ DEV SARKAÇ";
                case EndlessObstacleType.MovingTwinTunnels: return "◀÷2▶ HAREKETLİ İKİZ TÜNEL";
                default: return "ENGEL";
            }
        }

        public Color GetGateHeaderColor()
        {
            switch (obstacleType)
            {
                case EndlessObstacleType.SmallCenterHole: return new Color(1.00f, 0.35f, 0.42f);
                case EndlessObstacleType.TwinSplitTunnels:
                case EndlessObstacleType.MovingTwinTunnels: return new Color(0.76f, 0.38f, 1.00f);
                case EndlessObstacleType.HeavySmashWall: return new Color(0.18f, 0.92f, 0.50f);
                case EndlessObstacleType.SlidingDoorWall:
                case EndlessObstacleType.PincerShutters:
                case EndlessObstacleType.VerticalCrusherGate: return new Color(0.15f, 0.80f, 0.98f);
                case EndlessObstacleType.SwingingPendulum: return new Color(1.00f, 0.60f, 0.15f);
                default: return new Color(1.00f, 0.80f, 0.22f);
            }
        }

        /// <summary>
        /// Only shatters the breakable center glass/crystal panel of HeavySmashWall.
        /// Solid walls NEVER disappear or crumble when the player fails!
        /// </summary>
        public void ShatterBreakablePiece()
        {
            if (breakableCenterPiece != null && breakableCenterPiece.gameObject.activeSelf)
            {
                Vector3 burstCenter = breakableCenterPiece.position;
                breakableCenterPiece.gameObject.SetActive(false);
                SpawnDebrisBurst(burstCenter, new Color(0.28f, 0.96f, 0.62f), 14);
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
