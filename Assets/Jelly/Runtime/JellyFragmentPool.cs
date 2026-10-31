using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public class JellyFragmentPool : MonoBehaviour
    {
        private readonly Queue<JellyFragment> _pool = new Queue<JellyFragment>();
        public int PrewarmCount = 2;

        public JellyFragment GetOrCreate(JellyController template)
        {
            JellyFragment frag = null;
            while (_pool.Count > 0 && frag == null)
                frag = _pool.Dequeue();

            if (frag == null)
            {
                frag = CreateCleanFragmentInstance(template);
            }

            frag.gameObject.SetActive(true);
            frag.EnsureComponents();
            return frag;
        }

        private static JellyFragment CreateCleanFragmentInstance(JellyController template)
        {
            Transform parent = template != null ? template.transform.parent : null;
            var go = new GameObject(template != null ? template.gameObject.name + "_Fragment" : "JellyPlayer_Fragment");
            if (parent != null) go.transform.SetParent(parent, false);

            go.AddComponent<Rigidbody>();
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.5f;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Visual";
            if (Application.isPlaying) Destroy(vis.GetComponent<Collider>());
            else DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(go.transform, false);

            if (template != null)
            {
                var templateRenderer = template.GetComponentInChildren<Renderer>();
                var visRenderer = vis.GetComponent<Renderer>();
                if (templateRenderer != null && visRenderer != null)
                    visRenderer.sharedMaterial = templateRenderer.sharedMaterial;
            }

            go.AddComponent<JellyPhysicsBody>();
            go.AddComponent<JellyGroundDetector>();
            var input = go.AddComponent<JellyInputAdapter>();
            var motor = go.AddComponent<JellyMotor>();
            go.AddComponent<JellyMorphController>();
            var deform = go.AddComponent<JellyDeformationController>();
            deform.visualRoot = vis.transform;
            var massCtrl = go.AddComponent<JellyMassController>();
            var controller = go.AddComponent<JellyController>();
            if (template != null)
            {
                controller.movementConfig = template.movementConfig;
                input.config = template.movementConfig;
                motor.config = template.movementConfig;
                if (template.massController != null)
                    massCtrl.config = template.massController.config;
            }

            var frag = go.AddComponent<JellyFragment>();
            frag.EnsureComponents();
            return frag;
        }

        public void Release(JellyFragment fragment)
        {
            if (fragment == null) return;
            fragment.IsActiveFragment = false;
            fragment.gameObject.SetActive(false);
            if (!_pool.Contains(fragment))
                _pool.Enqueue(fragment);
        }
    }
}
