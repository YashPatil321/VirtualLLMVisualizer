using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// The prompt, and the answer typing itself out word by word as the card generates it.
    /// Every frame of text is built when the trace starts, so Update only picks one.
    /// </summary>
    public class AnswerPanel : MonoBehaviour
    {
        public TracePlayer player;
        public TextMesh prompt;
        public TextMesh response;

        public int charsPerLine = 38;
        public int maxLines = 6;

        string[] _frames;
        Hop _model;
        int _shown = -1;

        void OnEnable()
        {
            if (player != null) player.TraceStarted += OnTraceStarted;
        }

        void OnDisable()
        {
            if (player != null) player.TraceStarted -= OnTraceStarted;
        }

        void OnTraceStarted(Trace trace)
        {
            _model = null;
            if (trace.hops != null)
                for (int i = 0; i < trace.hops.Count; i++)
                    if (trace.hops[i].Type == HopType.Model) { _model = trace.hops[i]; break; }

            if (prompt != null) prompt.text = AnswerText.Wrap(trace.prompt_preview, charsPerLine);
            _frames = AnswerText.Frames(trace.response_preview, charsPerLine, maxLines);
            _shown = -1;
            if (response != null) response.text = string.Empty;
        }

        void Update()
        {
            if (_model == null || _frames == null || response == null || player == null) return;
            int n = AnswerText.WordsAt(player.ElapsedMs, _model.t_start_ms, _model.t_end_ms, _frames.Length - 1);
            if (n == _shown) return;
            _shown = n;
            response.text = _frames[n];
        }

        /// <summary>For the headless run: the response as it stands.</summary>
        public string Shown => response != null ? response.text : null;
    }
}
