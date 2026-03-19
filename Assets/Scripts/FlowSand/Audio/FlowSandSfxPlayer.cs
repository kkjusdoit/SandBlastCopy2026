using System.Collections.Generic;
using UnityEngine;

namespace FlowSand.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class FlowSandSfxPlayer : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new();
        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.18f;

            clips["move"] = CreateTone("move", 330f, 0.04f, WaveType.Square);
            clips["rotate"] = CreateTone("rotate", 520f, 0.05f, WaveType.Square);
            clips["lock"] = CreateTone("lock", 180f, 0.08f, WaveType.Sine);
            clips["clear"] = CreateTone("clear", 740f, 0.14f, WaveType.Sine);
            clips["start"] = CreateSweep("start", 260f, 520f, 0.18f);
            clips["gameover"] = CreateSweep("gameover", 320f, 120f, 0.28f);
        }

        public void PlayMove() => Play("move");
        public void PlayRotate() => Play("rotate");
        public void PlayLock() => Play("lock");
        public void PlayClear() => Play("clear");
        public void PlayStart() => Play("start");
        public void PlayGameOver() => Play("gameover");

        private void Play(string key)
        {
            if (clips.TryGetValue(key, out AudioClip clip))
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private AudioClip CreateTone(string name, float frequency, float duration, WaveType waveType)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - (i / (float)samples));
                float phase = time * frequency * Mathf.PI * 2f;
                float sample = waveType == WaveType.Square ? Mathf.Sign(Mathf.Sin(phase)) : Mathf.Sin(phase);
                data[i] = sample * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateSweep(string name, float startFrequency, float endFrequency, float duration)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += (frequency * Mathf.PI * 2f) / sampleRate;
                float envelope = Mathf.SmoothStep(1f, 0f, t);
                data[i] = Mathf.Sin(phase) * envelope * 0.3f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private enum WaveType
        {
            Sine,
            Square,
        }
    }
}
