using System.Collections.Generic;
using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Shows JourneyText on a board, lighting each hop as the request reaches it. The text
    /// for every state is built when the trace starts; hops only swap which one shows.
    /// </summary>
    public class JourneyBoard : MonoBehaviour
    {
        public TracePlayer player;
        public SystemGraph graph;
        public TextMesh text;

        string[] _frames;
        int _started;

        void OnEnable()
        {
            if (player == null) return;
            player.TraceStarted += OnTraceStarted;
            player.HopStarted += OnHopStarted;
            player.TraceFinished += OnTraceFinished;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.TraceStarted -= OnTraceStarted;
            player.HopStarted -= OnHopStarted;
            player.TraceFinished -= OnTraceFinished;
        }

        void OnTraceStarted(Trace trace)
        {
            List<Hop> hops = TraceLoader.DrawableHops(trace);
            var names = new List<string>(hops.Count);
            for (int i = 0; i < hops.Count; i++)
            {
                SystemNode node = graph != null ? graph.Resolve(hops[i]) : null;
                names.Add(node != null ? node.DisplayLabel : hops[i].hop);
            }
            _frames = JourneyText.Frames(hops, names);
            _started = 0;
            Show(0);
        }

        void OnHopStarted(Hop hop)
        {
            // Hops start in play order, the order the lines were built in.
            _started++;
            Show(_started);
        }

        void OnTraceFinished(Trace trace)
        {
            if (_frames != null) Show(_frames.Length - 1);
        }

        void Show(int frame)
        {
            if (_frames == null || text == null || frame < 0 || frame >= _frames.Length) return;
            text.text = _frames[frame];
        }

        /// <summary>For the headless run: what the board says.</summary>
        public string Shown => text != null ? text.text : null;
    }
}
