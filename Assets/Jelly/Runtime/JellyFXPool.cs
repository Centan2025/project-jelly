using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public class JellyFXPool : MonoBehaviour
    {
        public static JellyFXPool Instance { get; private set; }
        private readonly Queue<ParticleSystem> _pool = new Queue<ParticleSystem>();

        private void Awake() => Instance = this;

        public ParticleSystem SpawnBurst(Vector3 pos, Color color, int count = 12)
        {
            ParticleSystem ps = null;
            while (_pool.Count > 0 && ps == null)
                ps = _pool.Dequeue();

            if (ps == null)
            {
                var go = new GameObject("PooledJellyFX");
                go.transform.SetParent(transform, false);
                ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.duration = 0.45f;
                main.startLifetime = 0.4f;
                main.startSpeed = 3.5f;
                main.startSize = 0.16f;
                main.maxParticles = 32;
            }

            ps.transform.position = pos;
            var m = ps.main;
            m.startColor = color;
            ps.gameObject.SetActive(true);
            ps.Emit(count);
            return ps;
        }

        public void ReturnToPool(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            if (!_pool.Contains(ps))
                _pool.Enqueue(ps);
        }
    }
}
