namespace OCS.VR.Experience
{
    /// <summary>
    /// The room's reaction to the rig, as numbers the shaders read. Plain C#, tested.
    ///
    /// At power on a wave leaves the rig and runs out across the floor, and every rack it
    /// passes wakes up: WaveRadius is where the wave is, WaveStrength how bright it still
    /// is, WakeRadius how far out the racks are on. Before power on the hall idles in
    /// standby; after it everything is awake.
    /// </summary>
    public static class WorldPulse
    {
        /// <summary>Metres per second the wave travels. The hall is about 30 m long.</summary>
        public const float WaveSpeed = 11f;

        /// <summary>Seconds into power on before the wave leaves, so the cards light first.</summary>
        public const float WaveDelay = 1.4f;

        /// <summary>Metres over which the wave fades out.</summary>
        public const float WaveFade = 28f;

        /// <summary>Further than anything in the hall: fully awake.</summary>
        public const float AwakeRadius = 60f;

        public static void Evaluate(Act act, float secondsIntoAct,
                                    out float waveRadius, out float waveStrength, out float wakeRadius)
        {
            if (act < Act.PowerOn)
            {
                waveRadius = 0f;
                waveStrength = 0f;
                wakeRadius = 0f;
                return;
            }
            if (act > Act.PowerOn)
            {
                waveRadius = AwakeRadius;
                waveStrength = 0f;
                wakeRadius = AwakeRadius;
                return;
            }

            float r = (secondsIntoAct - WaveDelay) * WaveSpeed;
            if (r <= 0f)
            {
                waveRadius = 0f;
                waveStrength = 0f;
                wakeRadius = 0f;
                return;
            }
            waveRadius = r;
            float fade = 1f - r / WaveFade;
            waveStrength = fade > 0f ? fade : 0f;
            // The racks light just behind the wave front, as if the wave switched them on.
            wakeRadius = r;
        }
    }
}
