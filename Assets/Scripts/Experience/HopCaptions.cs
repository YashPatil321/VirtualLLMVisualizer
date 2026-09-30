using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// A caption under each system node saying what just happened there and how long it
    /// took: "Session resolved, request queued · 26 ms". Captions stay up once set, so by
    /// the end of Act 4 every stop on the path explains itself.
    ///
    /// Captions are built when a hop starts, never per frame.
    /// </summary>
    public class HopCaptions : MonoBehaviour
    {
        public TracePlayer player;
        public SystemGraph graph;

        [Tooltip("One per node, in the graph's node order.")]
        public TextMesh[] captions;

        void OnEnable()
        {
            if (player == null) return;
            player.HopStarted += OnHopStarted;
            player.TraceStarted += OnTraceStarted;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.HopStarted -= OnHopStarted;
            player.TraceStarted -= OnTraceStarted;
        }

        void OnTraceStarted(Trace trace)
        {
            if (captions == null) return;
            for (int i = 0; i < captions.Length; i++)
                if (captions[i] != null) captions[i].text = string.Empty;
        }

        void OnHopStarted(Hop hop)
        {
            if (graph == null || captions == null) return;
            SystemNode node = graph.Resolve(hop);
            int i = node == null ? -1 : graph.IndexOf(node);
            if (i < 0 || i >= captions.Length || captions[i] == null) return;
            captions[i].text = TelemetryText.HopCaption(hop);
        }
    }
}
