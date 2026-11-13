using UnityEngine;

namespace Jelly
{
    /// <summary>
    /// Real-time 3D per-vertex Jelly wave, rolling ripple, lateral inertia shear, and spring-damper squash-and-stretch component.
    /// Placed in its own file matching class name so Unity serializes the MonoBehaviour without missing-script warnings.
    /// </summary>
    [ExecuteAlways]
    public class JellyRealtimeDeformer : MonoBehaviour
    {
        private Mesh _baseMesh;
        private Mesh _workingMesh;
        private Vector3[] _baseVertices;
        private Vector3[] _deformedVertices;
        private Vector2[] _baseUVs;
        private Vector2[] _animatedUVs;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private Vector3 _prevWorldPos;
        private bool _hasPrevPos;
        private float _rollPhase;
        private float _idleTime;
        private float _lateralShear;
        private float _lateralShearVel;
        private float _forwardTilt;
        private float _forwardTiltVel;
        private float _squashBounce;
        private float _squashBounceVel;
        private float _prevScaleY;
        private float _squeezeTimer;
        private float _squeezeDuration = 0.25f;
        private float _squeezeIntensity;
        private float _wallImpactTimer;

        public void InitializeWithBaseMesh(Mesh baseMesh)
        {
            if (baseMesh == null || !baseMesh.isReadable) return;
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshFilter == null) return;

            if (_baseMesh == baseMesh && _workingMesh != null && _meshFilter.sharedMesh == _workingMesh)
                return;

            _baseMesh = baseMesh;
            _baseVertices = baseMesh.vertices;
            _deformedVertices = new Vector3[_baseVertices.Length];
            _baseUVs = baseMesh.uv;
            _animatedUVs = new Vector2[_baseUVs.Length];
            System.Array.Copy(_baseVertices, _deformedVertices, _baseVertices.Length);
            System.Array.Copy(_baseUVs, _animatedUVs, _baseUVs.Length);

            _workingMesh = Object.Instantiate(baseMesh);
            _workingMesh.name = "JellyDynamicDeformMesh";
            _workingMesh.MarkDynamic();
            _meshFilter.sharedMesh = _workingMesh;

            SetJellyTint(new Color(0.14f, 0.80f, 1.00f, 0.96f));
        }

        public void SetJellyTint(Color tint)
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                return;

            Material mat = BlenderKitModelSpawner.GetOrCreateGlossyJellyMaterial(tint);
            if (_meshRenderer.sharedMaterial != mat)
            {
                _meshRenderer.sharedMaterial = mat;
            }
        }

        public void TriggerJellyImpulse(float strength = 0.28f)
        {
            _squashBounceVel += strength * 8.5f;
        }

        public void TriggerGateSqueeze(float intensity = 0.36f, float duration = 0.28f)
        {
            _squeezeIntensity = Mathf.Clamp(intensity, 0.1f, 0.55f);
            _squeezeDuration = Mathf.Max(0.12f, duration);
            _squeezeTimer = _squeezeDuration;
            _squashBounceVel += 3.8f;
        }

        public void TriggerWallImpact()
        {
            _wallImpactTimer = 0.26f;
            _squashBounceVel -= 7.5f;
        }

        private void Update()
        {
            if (_workingMesh == null || _baseVertices == null || _baseVertices.Length == 0)
            {
                Mesh fallback = BlenderKitModelSpawner.GetOrCreateUprightJellyDomeBaseMesh();
                if (fallback != null)
                    InitializeWithBaseMesh(fallback);
                if (_workingMesh == null || _baseVertices == null)
                    return;
            }

            float dt = Application.isPlaying ? Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f) : 0.016f;
            _idleTime += dt;

            Vector3 currentPos = transform.position;
            if (!_hasPrevPos)
            {
                _prevWorldPos = currentPos;
                _prevScaleY = transform.lossyScale.y;
                _hasPrevPos = true;
            }

            Vector3 worldVel = (currentPos - _prevWorldPos) / Mathf.Max(0.001f, dt);
            _prevWorldPos = currentPos;

            float curScaleY = transform.lossyScale.y;
            if (Mathf.Abs(curScaleY - _prevScaleY) > 0.015f)
            {
                _squashBounceVel += Mathf.Clamp((curScaleY - _prevScaleY) * 18f, -3.5f, 3.5f);
                _prevScaleY = curScaleY;
            }

            Vector3 localVel = transform.InverseTransformDirection(worldVel);
            float forwardSpeed = Mathf.Clamp(localVel.z, -16f, 24f);
            float lateralSpeed = Mathf.Clamp(localVel.x, -14f, 14f);
            float verticalSpeed = Mathf.Clamp(localVel.y, -15f, 15f);

            float effectiveRollSpeed = Mathf.Abs(forwardSpeed) > 0.2f ? forwardSpeed * 1.65f : 3.6f;
            _rollPhase += effectiveRollSpeed * dt;

            float targetShear = Mathf.Clamp(-lateralSpeed * 0.036f, -0.28f, 0.28f);
            float shearForce = (targetShear - _lateralShear) * 145f - _lateralShearVel * 10.5f;
            _lateralShearVel += shearForce * dt;
            _lateralShear += _lateralShearVel * dt;

            float targetForwardTilt = Mathf.Clamp(forwardSpeed * 0.014f, -0.16f, 0.20f);
            float tiltForce = (targetForwardTilt - _forwardTilt) * 120f - _forwardTiltVel * 9.5f;
            _forwardTiltVel += tiltForce * dt;
            _forwardTilt += _forwardTiltVel * dt;

            float targetSquash = Mathf.Clamp(-verticalSpeed * 0.020f, -0.24f, 0.24f);
            float squashForce = (targetSquash - _squashBounce) * 185f - _squashBounceVel * 7.2f;
            _squashBounceVel += squashForce * dt;
            _squashBounce += _squashBounceVel * dt;

            float t = _idleTime;
            float idleBreathing = Mathf.Sin(t * 5.4f) * 0.044f + Mathf.Cos(t * 8.9f) * 0.022f;

            float squeezeCurve = 0f;
            if (_squeezeTimer > 0f)
            {
                _squeezeTimer = Mathf.Max(0f, _squeezeTimer - dt);
                float p = 1f - (_squeezeTimer / _squeezeDuration);
                squeezeCurve = Mathf.Sin(p * Mathf.PI) * _squeezeIntensity;
            }

            float wallSquashCurve = 0f;
            if (_wallImpactTimer > 0f)
            {
                _wallImpactTimer = Mathf.Max(0f, _wallImpactTimer - dt);
                float p = 1f - (_wallImpactTimer / 0.26f);
                wallSquashCurve = Mathf.Sin(p * Mathf.PI) * 0.38f;
            }

            float totalSquashY = Mathf.Clamp(_squashBounce + idleBreathing + squeezeCurve * 0.45f - wallSquashCurve * 0.55f, -0.40f, 0.42f);
            float volumePreserveXZ = 1f - totalSquashY * 0.54f;
            float squeezeScaleX = volumePreserveXZ * (1f - squeezeCurve) * (1f + wallSquashCurve * 0.55f);
            float squeezeScaleZ = volumePreserveXZ * (1f + squeezeCurve * 0.65f) * (1f - wallSquashCurve * 0.65f);

            const float baseBottomY = -0.33f;
            const float baseHeightY = 0.66f;

            for (int i = 0; i < _baseVertices.Length; i++)
            {
                Vector3 bv = _baseVertices[i];
                float h01 = Mathf.Clamp01((bv.y - baseBottomY) / baseHeightY);
                float skirtFactor = (1f - h01) * (1f - h01);
                float crownFactor = h01 * h01;

                float rollWave = Mathf.Sin(bv.z * 6.8f - _rollPhase * 2.4f) * 0.042f;
                float crossRipple = Mathf.Cos(bv.x * 6.2f + t * 6.8f) * Mathf.Sin(bv.z * 6.2f - _rollPhase * 2.0f) * 0.026f;
                float skirtWaveRadial = Mathf.Sin(Mathf.Atan2(bv.z, bv.x) * 4f + _rollPhase * 1.9f) * 0.032f * skirtFactor;

                float waveOffset = rollWave * (0.4f + 0.6f * h01) + skirtWaveRadial;
                float vx = bv.x * (squeezeScaleX + waveOffset);
                float vz = bv.z * (squeezeScaleZ + waveOffset);
                float vy = (bv.y - baseBottomY) * (1f + totalSquashY) + baseBottomY;

                vx += (_lateralShear + Mathf.Sin(t * 7.2f + h01 * 3.8f) * 0.028f) * crownFactor;
                vz += (_forwardTilt + Mathf.Cos(t * 5.8f + h01 * 3.4f) * 0.022f) * crownFactor;
                vy += crossRipple * (0.25f + 0.75f * h01);

                if (h01 < 0.12f)
                {
                    vy = Mathf.Lerp(baseBottomY, vy, h01 / 0.12f);
                }

                _deformedVertices[i] = new Vector3(vx, vy, vz);

                Vector2 buv = _baseUVs[i];
                _animatedUVs[i] = new Vector2(
                    Mathf.Clamp01(buv.x + _lateralShear * 0.18f + Mathf.Sin(t * 3.4f + bv.y * 2f) * 0.018f),
                    Mathf.Clamp01(buv.y + Mathf.Sin(_rollPhase * 0.85f + bv.x * 2f) * 0.020f));
            }

            _workingMesh.vertices = _deformedVertices;
            _workingMesh.uv = _animatedUVs;
            _workingMesh.RecalculateNormals();
        }
    }
}
