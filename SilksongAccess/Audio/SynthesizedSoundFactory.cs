using UnityEngine;
using System.Reflection;

namespace SilksongAccess.Audio
{
    /// <summary>
    /// Generates synthesized audio for spatial enemy pings.
    /// Based on TerrariaAccess implementation - creates static noise with envelope.
    /// </summary>
    public static class SynthesizedSoundFactory
    {
        private const int SAMPLE_RATE = 44100;

        // Cached MethodInfo for SetData to avoid ReadOnlySpan overload issues
        private static MethodInfo _setDataMethod;

        /// <summary>
        /// Envelope structure for attack/release phases.
        /// </summary>
        public struct ToneEnvelope
        {
            public float attack;   // Attack time in seconds
            public float release;  // Release time in seconds
            public bool useHannWindow; // Apply Hann windowing for smoothing

            public ToneEnvelope(float attack, float release, bool useHannWindow = true)
            {
                this.attack = attack;
                this.release = release;
                this.useHannWindow = useHannWindow;
            }
        }

        // Predefined envelopes matching TerrariaAccess
        public static readonly ToneEnvelope WorldCue = new ToneEnvelope(0.18f, 0.4f, true);
        public static readonly ToneEnvelope ShortBeep = new ToneEnvelope(0.02f, 0.08f, true); // Quick beep for pings

        /// <summary>
        /// Creates a short beep tone for enemy ping detection with smooth fade-out.
        /// Uses a quick attack and exponential decay for a pleasant, non-jarring sound.
        /// </summary>
        /// <param name="frequency">Frequency of the beep in Hz</param>
        /// <param name="duration">Total duration of the beep in seconds (default 0.1)</param>
        public static AudioClip CreatePingBeep(float frequency = 800f, float duration = 0.1f)
        {
            int sampleCount = Mathf.CeilToInt(duration * SAMPLE_RATE);

            string clipName = $"EnemyPingBeep_{frequency}_{duration}";
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SAMPLE_RATE, false);
            float[] samples = new float[sampleCount];

            // Envelope timing
            float attackTime = 0.005f; // 5ms quick attack
            float peakHoldTime = duration * 0.15f; // Hold at peak briefly
            float decayStart = attackTime + peakHoldTime;
            float decayDuration = duration - decayStart;
            
            // Exponential decay rate (higher = faster decay)
            float decayRate = 4.0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / SAMPLE_RATE;

                // Simple sine wave for clean tone
                float sample = Mathf.Sin(Mathf.PI * 2f * frequency * time);

                // Calculate smooth envelope
                float envelope;
                
                if (time < attackTime)
                {
                    // Quick smooth attack using sine curve (0 to 1)
                    float attackProgress = time / attackTime;
                    envelope = Mathf.Sin(attackProgress * Mathf.PI * 0.5f);
                }
                else if (time < decayStart)
                {
                    // Hold at peak
                    envelope = 1.0f;
                }
                else
                {
                    // Exponential decay with smooth ending
                    float decayProgress = (time - decayStart) / decayDuration;
                    // Exponential decay: e^(-rate * progress)
                    envelope = Mathf.Exp(-decayRate * decayProgress);
                    // Apply additional smoothing at the very end to avoid clicks
                    if (decayProgress > 0.8f)
                    {
                        float endSmooth = (1.0f - decayProgress) / 0.2f;
                        envelope *= endSmooth;
                    }
                }

                samples[i] = sample * envelope * 0.5f; // Moderate volume
            }

            SetAudioClipData(clip, samples, 0);
            return clip;
        }

        /// <summary>
        /// Creates a static ping sound for enemy detection.
        /// This generates noise with an envelope for a "radar ping" effect.
        /// </summary>
        public static AudioClip CreateStaticPing()
        {
            float duration = WorldCue.attack + WorldCue.release;
            int sampleCount = Mathf.CeilToInt(duration * SAMPLE_RATE);

            AudioClip clip = AudioClip.Create("EnemyStaticPing", sampleCount, 1, SAMPLE_RATE, false);
            float[] samples = new float[sampleCount];

            // Generate noise with envelope
            System.Random random = new System.Random();
            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / SAMPLE_RATE;

                // Generate random noise (-1 to 1)
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                // Apply envelope
                float envelope = CalculateEnvelope(time, duration, WorldCue);

                samples[i] = noise * envelope * 0.5f; // Scale down to prevent clipping
            }

            SetAudioClipData(clip, samples, 0);
            return clip;
        }

        /// <summary>
        /// Creates an additive tone with harmonics for alternative ping sound.
        /// Fundamental frequency of 360Hz like TerrariaAccess.
        /// </summary>
        public static AudioClip CreateAdditiveTone(float fundamentalFrequency = 360f, float[] partials = null)
        {
            if (partials == null)
            {
                // Default: fundamental + 2 harmonics with decreasing amplitude
                partials = new float[] { 1.0f, 0.5f, 0.25f };
            }

            float duration = WorldCue.attack + WorldCue.release;
            int sampleCount = Mathf.CeilToInt(duration * SAMPLE_RATE);

            AudioClip clip = AudioClip.Create("EnemyTonePing", sampleCount, 1, SAMPLE_RATE, false);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / SAMPLE_RATE;
                float sample = 0f;

                // Add harmonics
                for (int p = 0; p < partials.Length; p++)
                {
                    float frequency = fundamentalFrequency * (p + 1);
                    float amplitude = partials[p];
                    sample += Mathf.Sin(Mathf.PI * 2f * frequency * time) * amplitude;
                }

                // Normalize
                sample /= partials.Length;

                // Apply envelope
                float envelope = CalculateEnvelope(time, duration, WorldCue);

                samples[i] = sample * envelope * 0.3f; // Scale for mixing
            }

            SetAudioClipData(clip, samples, 0);
            return clip;
        }

        /// <summary>
        /// Sets audio clip data using reflection to avoid ReadOnlySpan overload issues in .NET Framework 4.8
        /// </summary>
        private static void SetAudioClipData(AudioClip clip, float[] samples, int offsetSamples)
        {
            if (_setDataMethod == null)
            {
                _setDataMethod = typeof(AudioClip).GetMethod("SetData", new System.Type[] { typeof(float[]), typeof(int) });
            }
            _setDataMethod.Invoke(clip, new object[] { samples, offsetSamples });
        }

        /// <summary>
        /// Calculate envelope value at given time.
        /// </summary>
        private static float CalculateEnvelope(float time, float duration, ToneEnvelope envelope)
        {
            float attackEnd = envelope.attack;
            float releaseStart = duration - envelope.release;

            float value;

            if (time <= attackEnd)
            {
                // Attack phase - linear fade in
                value = time / envelope.attack;
            }
            else if (time >= releaseStart)
            {
                // Release phase - linear fade out
                float releaseTime = time - releaseStart;
                value = 1f - (releaseTime / envelope.release);
            }
            else
            {
                // Sustain phase
                value = 1f;
            }

            // Apply Hann window if enabled
            if (envelope.useHannWindow)
            {
                float normalized = time / duration;
                float hann = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * 2f * normalized);
                value *= hann;
            }

            return Mathf.Clamp01(value);
        }
    }
}
