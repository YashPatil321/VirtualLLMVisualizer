namespace OCS.VR.Experience
{
    /// <summary>
    /// Lays out the "where the time goes" bar: one segment per hop, width proportional to
    /// how long the hop took, so the viewer sees at a glance that nearly all of a request
    /// is the GPU generating. Tiny hops get a minimum width so they stay visible.
    /// Plain C#, tested.
    /// </summary>
    public static class TimelineLayout
    {
        /// <summary>
        /// Segment widths that add up to totalWidth, each at least minWidth, the rest
        /// shared in proportion to duration.
        /// </summary>
        public static float[] Widths(float[] durations, float totalWidth, float minWidth)
        {
            int n = durations != null ? durations.Length : 0;
            var widths = new float[n];
            if (n == 0) return widths;
            if (minWidth * n >= totalWidth)
            {
                for (int i = 0; i < n; i++) widths[i] = totalWidth / n;
                return widths;
            }

            // Repeatedly pin anything that would fall under the minimum, and share what's
            // left among the rest. Converges in at most n passes.
            var pinned = new bool[n];
            for (int pass = 0; pass < n; pass++)
            {
                float free = totalWidth, sum = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (pinned[i]) free -= minWidth;
                    else sum += durations[i] > 0f ? durations[i] : 0f;
                }

                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (pinned[i]) { widths[i] = minWidth; continue; }
                    float d = durations[i] > 0f ? durations[i] : 0f;
                    widths[i] = sum > 0f ? free * d / sum : free / CountFree(pinned);
                    if (widths[i] < minWidth) { pinned[i] = true; changed = true; }
                }
                if (!changed) break;
            }
            return widths;
        }

        /// <summary>Left edge of each segment, starting at 0.</summary>
        public static float[] Offsets(float[] widths)
        {
            var offsets = new float[widths.Length];
            float x = 0f;
            for (int i = 0; i < widths.Length; i++) { offsets[i] = x; x += widths[i]; }
            return offsets;
        }

        static int CountFree(bool[] pinned)
        {
            int n = 0;
            foreach (bool p in pinned) if (!p) n++;
            return n == 0 ? 1 : n;
        }
    }
}
