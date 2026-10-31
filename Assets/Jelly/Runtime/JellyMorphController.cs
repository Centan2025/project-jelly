using UnityEngine;

namespace Jelly
{
    public class JellyMorphController : MonoBehaviour
    {
        [Range(0.4f, 1f)] public float lateralCompression = 1f;
        [Range(0.4f, 1f)] public float verticalCompression = 1f;
        public float preSplitStretch;

        public Vector3 GetEffectiveHalfExtents(float radius)
        {
            float rx = radius * lateralCompression;
            float ry = radius * verticalCompression;
            float rz = radius / Mathf.Max(0.35f, Mathf.Sqrt(lateralCompression * verticalCompression));
            return new Vector3(rx, ry, rz);
        }

        public void SetSqueeze(float lateralRatio, float verticalRatio, JellyMassConfig config)
        {
            float minLat = config != null ? config.maxLateralCompressionRatio : 0.62f;
            float minVert = config != null ? config.maxVerticalCompressionRatio : 0.65f;
            lateralCompression = Mathf.Clamp(lateralRatio, minLat, 1f);
            verticalCompression = Mathf.Clamp(verticalRatio, minVert, 1f);
        }

        public void Relax(float dt, float speed = 6f)
        {
            lateralCompression = Mathf.MoveTowards(lateralCompression, 1f, dt * speed);
            verticalCompression = Mathf.MoveTowards(verticalCompression, 1f, dt * speed);
            preSplitStretch = Mathf.MoveTowards(preSplitStretch, 0f, dt * speed);
        }
    }
}
