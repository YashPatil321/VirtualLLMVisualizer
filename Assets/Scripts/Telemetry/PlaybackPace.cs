using System.Collections.Generic;

namespace OCS.VR.Telemetry
{
    /// <summary>
    /// How fast the replay runs, in trace milliseconds per real second. A real request
    /// crosses the whole network in a tenth of a second and then spends seconds on the
    /// GPUs, so played straight the routing is invisible. Each running hop gets at least
    /// a minimum time on screen; long hops, the GPU work, still play at timeScale. The
    /// trace's own numbers never change: the boards and timeline show real durations.
    /// Plain C#, tested.
    /// </summary>
    public static class PlaybackPace
    {
        public static float MsPerSecond(IList<Hop> active, float timeScale, float minHopSeconds)
        {
            float rate = timeScale * 1000f;
            if (active == null || minHopSeconds <= 0f) return rate;
            for (int i = 0; i < active.Count; i++)
            {
                float d = active[i].DurationMs;
                if (d <= 0f) continue;
                float slowest = d / minHopSeconds;     // this hop lasts minHopSeconds at this rate
                if (slowest < rate) rate = slowest;
            }
            return rate;
        }
    }
}
