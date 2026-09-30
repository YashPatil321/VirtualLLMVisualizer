using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Draws the system view: a node marker per box in the SystemGraph and a pulse that
    /// travels the edge as each hop fires. Subscribes to TracePlayer and holds no playback
    /// logic; the routing state is SystemRouteState, which is plain C#.
    ///
    /// No allocation in Update.
    /// </summary>
    public class SystemViewDisplay : MonoBehaviour
    {
        [Header("References")]
        public TracePlayer player;
        public SystemGraph graph;

        [Tooltip("One marker per node, in the same order as the graph's node list. " +
                 "Leave empty to have them placed from the graph at Start.")]
        public Transform[] nodeMarkers;

        [Tooltip("Travels along the edge as the request moves. Optional.")]
        public Transform pulse;

        [Tooltip("Added to the pulse's position. A little behind the panels, so it tucks " +
                 "in behind a node when it arrives instead of covering its name.")]
        public Vector3 pulseOffset;

        [Header("Feel")]
        [Tooltip("Edges per second the pulse travels.")]
        public float travelSpeed = 4f;

        public float glowSpeed = 8f;

        [Tooltip("How far a busy node lifts. Stand in for emission until V7.")]
        public float busyLift = 0.05f;

        [Tooltip("Optional. One per node, lit while the request is there. Needs an " +
                 "emissive material.")]
        public Renderer[] nodeAccents;
        [ColorUsage(false, true)] public Color accentIdle = new Color(0.04f, 0.12f, 0.18f);
        [ColorUsage(false, true)] public Color accentBusy = new Color(0.4f, 1.6f, 2.4f);

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock _block;

        SystemRouteState _state;

        public int CurrentNode => _state != null ? _state.CurrentNode : -1;
        public float EdgeProgress01 => _state != null ? _state.EdgeProgress01 : 0f;
        public float GlowOf(int node) => _state != null ? _state.Glow(node) : 0f;

        void Awake()
        {
            _state = new SystemRouteState(graph != null ? graph.NodeCount : 0);
        }

        void OnEnable()
        {
            if (player == null) return;
            player.HopStarted += OnHopStarted;
            player.HopEnded += OnHopEnded;
            player.TraceStarted += OnTraceStarted;
            player.TraceFinished += OnTraceFinished;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.HopStarted -= OnHopStarted;
            player.HopEnded -= OnHopEnded;
            player.TraceStarted -= OnTraceStarted;
            player.TraceFinished -= OnTraceFinished;
        }

        void Start()
        {
            if (graph == null) return;

            string error;
            if (!graph.Validate(out error)) Debug.LogError("[SystemViewDisplay] " + error);

            PlaceMarkersFromGraph();
        }

        void PlaceMarkersFromGraph()
        {
            if (nodeMarkers == null || graph == null) return;

            for (int i = 0; i < nodeMarkers.Length && i < graph.NodeCount; i++)
            {
                if (nodeMarkers[i] == null) continue;
                nodeMarkers[i].localPosition = graph.nodes[i].position;
            }
        }

        void OnHopStarted(Hop hop)
        {
            int index = IndexFor(hop);
            if (index < 0)
            {
                // Contract: a node the graph does not know about is skipped, not an error.
                return;
            }
            _state.HopStarted(index);
        }

        void OnHopEnded(Hop hop)
        {
            int index = IndexFor(hop);
            if (index < 0) return;
            _state.HopEnded(index);
        }

        int IndexFor(Hop hop)
        {
            if (graph == null) return -1;
            SystemNode node = graph.Resolve(hop);
            return node == null ? -1 : graph.IndexOf(node);
        }

        void OnTraceStarted(Trace trace) => _state.ResetAll();
        void OnTraceFinished(Trace trace) => _state.ResetAll();

        void Update()
        {
            if (_state == null || graph == null) return;

            _state.Step(Time.deltaTime, travelSpeed, glowSpeed);

            if (nodeMarkers != null)
            {
                for (int i = 0; i < nodeMarkers.Length && i < graph.NodeCount; i++)
                {
                    if (nodeMarkers[i] == null) continue;
                    Vector3 p = graph.nodes[i].position;
                    p.y += busyLift * _state.Glow(i);
                    nodeMarkers[i].localPosition = p;
                }
            }

            if (nodeAccents != null)
            {
                if (_block == null) _block = new MaterialPropertyBlock();
                for (int i = 0; i < nodeAccents.Length && i < graph.NodeCount; i++)
                {
                    if (nodeAccents[i] == null) continue;
                    nodeAccents[i].GetPropertyBlock(_block);
                    _block.SetColor(EmissionColorId, Color.Lerp(accentIdle, accentBusy, _state.Glow(i)));
                    nodeAccents[i].SetPropertyBlock(_block);
                }
            }

            if (pulse != null) UpdatePulse();
        }

        void UpdatePulse()
        {
            int cur = _state.CurrentNode;
            if (cur < 0 || cur >= graph.NodeCount)
            {
                pulse.gameObject.SetActive(false);
                return;
            }

            if (!pulse.gameObject.activeSelf) pulse.gameObject.SetActive(true);

            Vector3 to = graph.nodes[cur].position;
            int prev = _state.PreviousNode;
            Vector3 from = prev >= 0 && prev < graph.NodeCount ? graph.nodes[prev].position : to;

            pulse.localPosition = Vector3.Lerp(from, to, _state.EdgeProgress01) + pulseOffset;
        }
    }
}
