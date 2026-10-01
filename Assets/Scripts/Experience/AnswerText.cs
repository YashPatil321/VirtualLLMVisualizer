using System.Collections.Generic;
using System.Text;

namespace OCS.VR.Experience
{
    /// <summary>
    /// The answer panel's text: wrapped to fit, and cut into one frame per word so the
    /// response can type itself out as the tokens arrive without building strings every
    /// frame. Plain C#, tested.
    /// </summary>
    public static class AnswerText
    {
        /// <summary>Word wraps text to lines of at most maxChars, joined with newlines.</summary>
        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string[] words = text.Split(' ');
            var sb = new StringBuilder(text.Length + 8);
            int line = 0;
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length == 0) continue;
                if (line > 0 && line + 1 + w.Length > maxChars)
                {
                    sb.Append('\n');
                    line = 0;
                }
                else if (line > 0)
                {
                    sb.Append(' ');
                    line++;
                }
                sb.Append(w);
                line += w.Length;
            }
            return sb.ToString();
        }

        /// <summary>
        /// frames[k] is the response with its first k words showing, wrapped. When it runs
        /// past maxLines, the oldest lines scroll off the top, as a chat window would.
        /// </summary>
        public static string[] Frames(string response, int maxChars, int maxLines)
        {
            var words = new List<string>();
            if (!string.IsNullOrEmpty(response))
                foreach (string w in response.Split(' '))
                    if (w.Length > 0) words.Add(w);

            var frames = new string[words.Count + 1];
            frames[0] = string.Empty;
            var sb = new StringBuilder();
            for (int k = 1; k <= words.Count; k++)
            {
                if (k > 1) sb.Append(' ');
                sb.Append(words[k - 1]);
                frames[k] = LastLines(Wrap(sb.ToString(), maxChars), maxLines);
            }
            return frames;
        }

        /// <summary>How many words should show at elapsedMs, spread evenly over the hop.</summary>
        public static int WordsAt(float elapsedMs, float startMs, float endMs, int wordCount)
        {
            if (wordCount <= 0 || elapsedMs <= startMs) return 0;
            if (elapsedMs >= endMs || endMs <= startMs) return wordCount;
            int n = (int)(wordCount * (elapsedMs - startMs) / (endMs - startMs)) + 1;
            return n > wordCount ? wordCount : n;
        }

        /// <summary>
        /// The heading over the response: which card wrote it and with what model, as the
        /// trace reports them. "RESPONSE  ·  GPU 3  ·  placeholder-7b, Q4_K_M".
        /// </summary>
        public static string ResponseHeading(OCS.VR.Telemetry.Trace trace)
        {
            var sb = new StringBuilder("RESPONSE");
            if (trace == null || trace.hops == null) return sb.ToString();
            int gpu = -1;
            string model = null, quant = null;
            foreach (var hop in trace.hops)
            {
                if (hop.HasGpuTelemetry && gpu < 0) gpu = hop.gpu_index;
                if (string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(hop.model)) model = hop.model;
                if (string.IsNullOrEmpty(quant) && !string.IsNullOrEmpty(hop.quantization)) quant = hop.quantization;
            }
            if (gpu >= 0) sb.Append("  ·  GPU ").Append(gpu);
            if (!string.IsNullOrEmpty(model))
            {
                sb.Append("  ·  ").Append(model);
                if (!string.IsNullOrEmpty(quant)) sb.Append(", ").Append(quant);
            }
            return sb.ToString();
        }

        static string LastLines(string wrapped, int maxLines)
        {
            if (maxLines <= 0) return wrapped;
            int count = 1;
            for (int i = 0; i < wrapped.Length; i++) if (wrapped[i] == '\n') count++;
            if (count <= maxLines) return wrapped;
            int skip = count - maxLines, idx = 0;
            while (skip > 0) { idx = wrapped.IndexOf('\n', idx) + 1; skip--; }
            return wrapped.Substring(idx);
        }
    }
}
