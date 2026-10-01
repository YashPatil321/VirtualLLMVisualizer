using System.Collections.Generic;
using System.Text;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// The "how a request flows" board: one line per hop with where it ran, what it did
    /// and how long it took. The hop that is running is lit, the ones done are plain and
    /// the ones to come are dim. Every state is built once, when the trace starts, so the
    /// board only swaps strings as the request moves. Plain C#, tested.
    /// </summary>
    public static class JourneyText
    {
        public const string Done = "#C9D4E0";
        public const string Pending = "#56606B";
        public const string Active = "#5BD1FF";
        public const string Gpu = "#FFA040";

        /// <summary>
        /// frames[0]: nothing started. frames[k + 1]: hop k running. frames[n + 1]: all done,
        /// with the total and the GPU's share underneath.
        /// </summary>
        public static string[] Frames(IList<Hop> hops, IList<string> names)
        {
            int n = hops != null ? hops.Count : 0;
            var frames = new string[n + 2];
            float total = 0f, gpu = 0f;
            for (int i = 0; i < n; i++)
            {
                total += hops[i].DurationMs;
                if (hops[i].Type == HopType.Model) gpu += hops[i].DurationMs;
            }

            for (int current = -1; current <= n; current++)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    string colour = i < current || current == n ? Done
                                  : i == current ? (hops[i].Type == HopType.Model ? Gpu : Active)
                                  : Pending;
                    string name = names != null && i < names.Count && !string.IsNullOrEmpty(names[i]) ? names[i] : hops[i].hop;
                    if (i > 0) sb.Append('\n');
                    sb.Append("<color=").Append(colour).Append('>')
                      .Append(i + 1).Append("  ").Append(name).Append("  ·  ")
                      .Append(hops[i].DisplayLabel).Append("  ·  ").Append(TelemetryText.Duration(hops[i].DurationMs))
                      .Append("</color>");
                }
                if (current == n)
                {
                    sb.Append("\n\n<color=").Append(Active).Append(">Total ").Append(TelemetryText.Duration(total))
                      .Append("  ·  the GPU took ").Append(Percent(gpu, total)).Append("</color>");
                }
                frames[current + 1] = sb.ToString();
            }
            return frames;
        }

        /// <summary>"96.6%": share of the whole, one decimal.</summary>
        public static string Percent(float part, float whole)
        {
            if (whole <= 0f) return "0%";
            return (part / whole * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }
    }
}
