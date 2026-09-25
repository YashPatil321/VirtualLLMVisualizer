namespace OCS.VR.Rig
{
    /// <summary>
    /// How a card should look for a given power state and load: fan speed and LED glow.
    /// Plain C# so the rules are covered by edit mode tests, the same split as
    /// CardLoadState. CardVisual only applies the numbers to Transforms and materials.
    /// </summary>
    public static class CardVisualMath
    {
        /// <summary>Fan speed in revolutions per second. Off means stopped.</summary>
        public static float FanRps(bool powered, float load01, float idleRps, float maxRps)
        {
            if (!powered) return 0f;
            return idleRps + (maxRps - idleRps) * Clamp01(load01);
        }

        /// <summary>
        /// LED brightness, 0 to 1. Off is dark, on and idle is a faint glow so the viewer
        /// can see the rig is alive, and full load is fully lit.
        /// </summary>
        public static float Glow(bool powered, float load01, float idleGlow)
        {
            if (!powered) return 0f;
            float idle = Clamp01(idleGlow);
            return idle + (1f - idle) * Clamp01(load01);
        }

        /// <summary>
        /// Moves current toward target by at most maxDelta. Used so fans spin up and
        /// down instead of jumping, which reads as a machine rather than a state change.
        /// </summary>
        public static float Approach(float current, float target, float maxDelta)
        {
            if (maxDelta <= 0f) return current;
            if (current < target) return current + maxDelta >= target ? target : current + maxDelta;
            if (current > target) return current - maxDelta <= target ? target : current - maxDelta;
            return current;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
