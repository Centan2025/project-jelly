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
                GameObject clone = Instantiate(template.gameObject, template.transform.parent);
                clone.name = template.gameObject.name + "_Fragment";
                frag = clone.GetComponent<JellyFragment>();
                if (frag == null) frag = clone.AddComponent<JellyFragment>();
            }

            frag.gameObject.SetActive(true);
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
