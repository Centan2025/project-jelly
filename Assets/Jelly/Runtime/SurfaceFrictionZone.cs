using UnityEngine;

namespace Jelly
{
    public class SurfaceFrictionZone : MonoBehaviour
    {
        [Range(0.05f, 3f)] public float frictionMultiplier = 0.2f;
    }
}
