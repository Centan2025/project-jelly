using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    public class JellyFragmentRegistry : MonoBehaviour
    {
        public static JellyFragmentRegistry Instance { get; private set; }
        private readonly List<JellyFragment> _activeFragments = new List<JellyFragment>();

        public IReadOnlyList<JellyFragment> ActiveFragments => _activeFragments;
        public int ActiveCount => _activeFragments.Count;
        public float LostMassTotal { get; private set; }
        public event Action<JellyFragment, float> OnFragmentLost;

        private void Awake()
        {
            Instance = this;
        }

        public void Register(JellyFragment fragment)
        {
            if (fragment == null) return;
            fragment.EnsureComponents();
            fragment.IsActiveFragment = true;
            if (!_activeFragments.Contains(fragment))
                _activeFragments.Add(fragment);
        }

        public void Unregister(JellyFragment fragment)
        {
            if (fragment == null) return;
            fragment.IsActiveFragment = false;
            _activeFragments.Remove(fragment);
        }

        public void ReportFragmentLost(JellyFragment fragment)
        {
            if (fragment == null || !_activeFragments.Contains(fragment)) return;
            float lostMass = fragment.MassController != null ? fragment.MassController.CurrentMass : 0f;
            LostMassTotal += lostMass;
            Unregister(fragment);
            OnFragmentLost?.Invoke(fragment, lostMass);
        }

        public float GetTotalActiveMass()
        {
            float sum = 0f;
            for (int i = 0; i < _activeFragments.Count; i++)
            {
                var f = _activeFragments[i];
                if (f != null && f.MassController != null)
                    sum += f.MassController.CurrentMass;
            }
            return sum;
        }

        public Vector3 GetCentroid()
        {
            if (_activeFragments.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int valid = 0;
            for (int i = 0; i < _activeFragments.Count; i++)
            {
                if (_activeFragments[i] != null)
                {
                    sum += _activeFragments[i].transform.position;
                    valid++;
                }
            }
            return valid > 0 ? sum / valid : Vector3.zero;
        }

        public float GetMaxSpreadDistance()
        {
            if (_activeFragments.Count < 2) return 0f;
            Vector3 c = GetCentroid();
            float maxD = 0f;
            for (int i = 0; i < _activeFragments.Count; i++)
            {
                if (_activeFragments[i] != null)
                    maxD = Mathf.Max(maxD, Vector3.Distance(c, _activeFragments[i].transform.position));
            }
            return maxD * 2f;
        }

        public void ResetRegistry()
        {
            _activeFragments.Clear();
            LostMassTotal = 0f;
        }
    }
}
