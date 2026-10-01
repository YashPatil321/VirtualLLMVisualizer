using System.Globalization;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// The words and numbers shown over the working card and under each system node.
    /// Plain C# so the formatting is tested. Every method here builds a string, so call
    /// them when a hop starts, never every frame.
    /// </summary>
    public static class TelemetryText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>"GPU 3 · rig-2"</summary>
        public static string CardHeader(Hop hop)
        {
            if (hop.GpuCount > 1)
                return "GPUs " + hop.gpu_index.ToString(Inv) + "–" + hop.GpuLast.ToString(Inv) + "  ·  " + hop.node_id;
            return "GPU " + hop.gpu_index.ToString(Inv) + "  ·  " + hop.node_id;
        }

        /// <summary>"94% load · 71°C · 7.1 GB". Fields the hop doesn't carry are left out.</summary>
        public static string CardStats(Hop hop)
        {
            string s = "";
            if (hop.gpu_util > 0f) s = Join(s, Mathf0(hop.gpu_util) + "% load");
            if (hop.temp_c > 0f) s = Join(s, Mathf0(hop.temp_c) + "°C");
            if (hop.vram_used_mb > 0f)
                s = Join(s, (hop.vram_used_mb / 1000f).ToString("0.0", Inv) + " GB VRAM" + (hop.GpuCount > 1 ? " each" : ""));
            return s;
        }

        /// <summary>
        /// For a model split across cards: "Split 4 ways: every token crosses all 4 cards".
        /// Empty for one card.
        /// </summary>
        public static string SplitLine(Hop hop)
        {
            if (hop == null || hop.GpuCount <= 1) return string.Empty;
            string n = hop.GpuCount.ToString(Inv);
            return "Split " + n + " ways: every token crosses all " + n + " cards";
        }

        /// <summary>"Generating · 42 tokens · 60.5 tok/s"</summary>
        public static string Generating(int tokens, float tokensPerSec)
        {
            string s = "Generating  ·  " + tokens.ToString(Inv) + (tokens == 1 ? " token" : " tokens");
            if (tokensPerSec > 0f) s += "  ·  " + tokensPerSec.ToString("0.#", Inv) + " tok/s";
            return s;
        }

        /// <summary>"Session resolved, request queued\n26 ms"</summary>
        public static string HopCaption(Hop hop)
        {
            return hop.DisplayLabel + "\n" + Duration(hop.DurationMs);
        }

        /// <summary>"26 ms" under a second, "3.07 s" above it.</summary>
        public static string Duration(float ms)
        {
            if (ms < 1000f) return Mathf0(ms) + " ms";
            return (ms / 1000f).ToString("0.00", Inv) + " s";
        }

        /// <summary>
        /// How many tokens have been generated at a moment in the model hop, assuming a
        /// steady rate. Clamped to 0 and the total.
        /// </summary>
        public static int TokensAt(float elapsedMs, float startMs, float endMs, int total)
        {
            if (total <= 0 || endMs <= startMs) return total < 0 ? 0 : total;
            float t = (elapsedMs - startMs) / (endMs - startMs);
            if (t <= 0f) return 0;
            if (t >= 1f) return total;
            int n = (int)(t * total);
            return n > total ? total : n;
        }

        static string Mathf0(float v) => System.Math.Round(v).ToString("0", Inv);
        static string Join(string a, string b) => a.Length == 0 ? b : a + "  ·  " + b;
    }
}
