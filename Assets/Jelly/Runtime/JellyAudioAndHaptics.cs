using System.Collections.Generic;
using UnityEngine;

namespace Jelly
{
    /// <summary>
    /// Procedural Web/Mobile-ready Audio Synthesizer & Haptics Engine.
    /// Zero external asset dependencies; responds live to SoundEnabled & VibrationEnabled settings.
    /// </summary>
    public class JellyAudioAndHaptics : MonoBehaviour
    {
        private AudioSource _sfxSource;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            BuildProceduralClips();
        }

        private void BuildProceduralClips()
        {
            _clips["pickup"] = CreateToneClip("sfx_pickup", 540f, 880f, 0.11f, 0.35f, false);
            _clips["shrink"] = CreateToneClip("sfx_shrink", 620f, 340f, 0.12f, 0.32f, false);
            _clips["split"] = CreateToneClip("sfx_split", 320f, 680f, 0.15f, 0.38f, false);
            _clips["squeeze"] = CreateToneClip("sfx_squeeze", 260f, 760f, 0.16f, 0.40f, false);
            _clips["smash"] = CreateNoiseBurstClip("sfx_smash", 0.24f, 0.45f, 420f);
            _clips["thud"] = CreateToneClip("sfx_thud", 165f, 62f, 0.18f, 0.48f, false);
            _clips["win"] = CreateFanfareClip("sfx_win");
        }

        public void PlayPickup(bool soundOn, bool hapticOn, int comboCount, bool isShrink = false)
        {
            if (soundOn && _sfxSource != null)
            {
                string key = isShrink ? "shrink" : "pickup";
                if (_clips.TryGetValue(key, out AudioClip clip))
                {
                    _sfxSource.pitch = Mathf.Clamp(1f + comboCount * 0.055f, 0.95f, 1.55f);
                    _sfxSource.PlayOneShot(clip, 0.72f);
                }
            }
            TriggerLightHaptic(hapticOn);
        }

        public void PlaySplitMerge(bool soundOn, bool hapticOn)
        {
            if (soundOn && _sfxSource != null && _clips.TryGetValue("split", out AudioClip clip))
            {
                _sfxSource.pitch = Random.Range(0.96f, 1.08f);
                _sfxSource.PlayOneShot(clip, 0.78f);
            }
            TriggerLightHaptic(hapticOn);
        }

        public void PlayGateSqueeze(bool soundOn, bool hapticOn, int comboCount)
        {
            if (soundOn && _sfxSource != null && _clips.TryGetValue("squeeze", out AudioClip clip))
            {
                _sfxSource.pitch = Mathf.Clamp(1f + comboCount * 0.04f, 1f, 1.4f);
                _sfxSource.PlayOneShot(clip, 0.80f);
            }
            TriggerLightHaptic(hapticOn);
        }

        public void PlayCrystalSmash(bool soundOn, bool hapticOn)
        {
            if (soundOn && _sfxSource != null && _clips.TryGetValue("smash", out AudioClip clip))
            {
                _sfxSource.pitch = Random.Range(0.95f, 1.12f);
                _sfxSource.PlayOneShot(clip, 0.92f);
            }
            TriggerHeavyHaptic(hapticOn);
        }

        public void PlayWallThud(bool soundOn, bool hapticOn)
        {
            if (soundOn && _sfxSource != null && _clips.TryGetValue("thud", out AudioClip clip))
            {
                _sfxSource.pitch = 1f;
                _sfxSource.PlayOneShot(clip, 0.88f);
            }
            TriggerHeavyHaptic(hapticOn);
        }

        public void PlayLevelWin(bool soundOn, bool hapticOn)
        {
            if (soundOn && _sfxSource != null && _clips.TryGetValue("win", out AudioClip clip))
            {
                _sfxSource.pitch = 1f;
                _sfxSource.PlayOneShot(clip, 0.85f);
            }
            TriggerLightHaptic(hapticOn);
        }

        private static void TriggerLightHaptic(bool enabled)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (enabled) Handheld.Vibrate();
#endif
        }

        private static void TriggerHeavyHaptic(bool enabled)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (enabled) Handheld.Vibrate();
#endif
        }

        private static AudioClip CreateToneClip(string name, float startHz, float endHz, float dur, float amp, bool square)
        {
            const int sr = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(sr * dur));
            var data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = Mathf.Lerp(startHz, endHz, t);
                phase += (2f * Mathf.PI * freq) / sr;
                float env = Mathf.Sin(t * Mathf.PI);
                float sample = square ? Mathf.Sign(Mathf.Sin(phase)) : Mathf.Sin(phase);
                data[i] = sample * env * amp;
            }
            var clip = AudioClip.Create(name, len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNoiseBurstClip(string name, float dur, float amp, float toneHz)
        {
            const int sr = 22050;
            int len = Mathf.Max(1, Mathf.RoundToInt(sr * dur));
            var data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float env = Mathf.Pow(1f - t, 1.8f);
                phase += (2f * Mathf.PI * (toneHz + t * 900f)) / sr;
                float noise = (Mathf.PerlinNoise(i * 0.35f, 0.5f) * 2f - 1f) * 0.7f + Mathf.Sin(phase) * 0.3f;
                data[i] = noise * env * amp;
            }
            var clip = AudioClip.Create(name, len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateFanfareClip(string name)
        {
            const int sr = 22050;
            float dur = 0.52f;
            int len = Mathf.RoundToInt(sr * dur);
            var data = new float[len];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5 E5 G5 C6
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                int noteIdx = Mathf.Clamp(Mathf.FloorToInt(t * notes.Length), 0, notes.Length - 1);
                float localT = (t * notes.Length) - noteIdx;
                float env = Mathf.Sin(localT * Mathf.PI) * (1f - t * 0.25f);
                phase += (2f * Mathf.PI * notes[noteIdx]) / sr;
                data[i] = Mathf.Sin(phase) * env * 0.42f;
            }
            var clip = AudioClip.Create(name, len, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
