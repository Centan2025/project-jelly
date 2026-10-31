using UnityEngine;

namespace Jelly
{
    public enum EndlessObstacleType
    {
        SmallCenterHole,
        TwinSplitTunnels,
        HeavySmashWall,
        LeftOpeningWall,
        RightOpeningWall
    }

    public class EndlessGateObstacle : MonoBehaviour
    {
        public EndlessObstacleType obstacleType;
        public float trackZ;
        public float maxAllowedMassForSmallHole = 78f;
        public float minRequiredMassForSmash = 125f;
        public Transform breakableCenterPiece;
        public bool resolved;

        public void ShatterBreakablePiece()
        {
            if (breakableCenterPiece == null) return;
            breakableCenterPiece.gameObject.SetActive(false);
        }
    }
}
