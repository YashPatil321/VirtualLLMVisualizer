using UnityEngine;
using OCS.VR.Rig;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Light beams between the system view's nodes, along the path the request travels.
    /// Each beam flashes as the request passes along it and fades after. One more beam
    /// runs from the rig's node down to the physical card doing the work, and glows with
    /// that card's load, which ties the diagram to the machine in front of the viewer.
    ///
    /// The edges come from the trace at build time; the rules live in BeamGlowState.
    /// No allocation in Update.
    /// </summary>
    public class SystemBeams : MonoBehaviour
    {
        [Header("References")]
        public TracePlayer player;
        public SystemGraph graph;
        public SystemViewDisplay view;
        public RigCardDisplay rig;

        [Header("Beams")]
        [Tooltip("Node indices at each beam's ends. Filled by the scene builder from the trace.")]
        public int[] edgeFrom;
        public int[] edgeTo;
        public LineRenderer[] lines;

        [Tooltip("From the rig's node down to the working card.")]
        public LineRenderer cardBeam;

        [Header("Look")]
        public Color idleColor = new Color(0.35f, 0.55f, 0.9f, 0.25f);
        public Color litColor = new Color(0.6f, 0.85f, 1f, 1f);
        public Color cardBeamColor = new Color(1f, 0.6f, 0.2f, 0.9f);
        public float idleWidth = 0.006f;
        public float litWidth = 0.03f;
        public float fadePerSecond = 1.2f;

        [Tooltip("Card beam width at full load, metres. Its shape along the length " +
                 "comes from the line's width curve.")]
        public float cardBeamWidth = 0.03f;

        [Tooltip("How far below the rig's node the card beam starts, so it leaves the " +
                 "bottom of the panel rather than its middle.")]
        public float cardBeamDrop = 0.05f;

        [Tooltip("Optional. A glow over the working card, as bright as the card is busy.")]
        public Renderer cardHalo;
        [ColorUsage(true, true)] public Color haloColor = new Color(2.0f, 0.9f, 0.25f, 0.9f);
        public float haloSize = 0.5f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock _block;

        BeamGlowState _state;
        int _cardIndex = -1;
        int _cardNode = -1;

        void Awake()
        {
            _state = new BeamGlowState(edgeFrom, edgeTo);
        }

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
            _state.Reset();
            _cardIndex = -1;
            _cardNode = -1;
        }

        void OnHopStarted(Hop hop)
        {
            if (graph == null) return;
            SystemNode node = graph.Resolve(hop);
            int index = node == null ? -1 : graph.IndexOf(node);
            _state.NodeEntered(index);

            if (hop.HasGpuTelemetry && index >= 0)
            {
                _cardIndex = hop.gpu_index;
                _cardNode = index;
            }
        }

        void Update()
        {
            if (_state == null) return;
            _state.Step(Time.deltaTime, fadePerSecond);

            if (lines != null && view != null && view.nodeMarkers != null)
            {
                for (int e = 0; e < lines.Length && e < _state.EdgeCount; e++)
                {
                    LineRenderer line = lines[e];
                    if (line == null) continue;
                    Transform a = Marker(_state.From(e));
                    Transform b = Marker(_state.To(e));
                    if (a == null || b == null) continue;

                    float g = _state.Glow(e);
                    line.SetPosition(0, a.position);
                    line.SetPosition(1, b.position);
                    line.widthMultiplier = Mathf.Lerp(idleWidth, litWidth, g);
                    Color c = Color.Lerp(idleColor, litColor, g);
                    line.startColor = c;
                    line.endColor = c;
                }
            }

            UpdateCardBeam();
        }

        void UpdateCardBeam()
        {
            if (cardBeam == null) return;

            float load = rig != null && _cardIndex >= 0 ? rig.LoadOf(_cardIndex) : 0f;
            Transform from = Marker(_cardNode);
            Transform to = rig != null && rig.cards != null && _cardIndex >= 0 && _cardIndex < rig.cards.Length
                ? rig.cards[_cardIndex] : null;

            bool on = load > 0.02f && from != null && to != null;
            if (cardBeam.enabled != on) cardBeam.enabled = on;
            UpdateHalo(on, to, load);
            if (!on) return;

            cardBeam.SetPosition(0, from.position + Vector3.down * cardBeamDrop);
            cardBeam.SetPosition(1, to.position + Vector3.up * 0.07f);
            cardBeam.widthMultiplier = Mathf.Lerp(0f, cardBeamWidth, load);
            Color c = cardBeamColor;
            c.a *= load;
            cardBeam.startColor = c;
            cardBeam.endColor = c;
        }

        void UpdateHalo(bool on, Transform card, float load)
        {
            if (cardHalo == null) return;
            if (cardHalo.enabled != on) cardHalo.enabled = on;
            if (!on) return;
            // A slow throb on top of the load, so it reads as working rather than lit.
            float throb = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f);
            cardHalo.transform.position = card.position + Vector3.up * 0.06f;
            cardHalo.transform.localScale = Vector3.one * (haloSize * (0.6f + 0.4f * load) * throb);
            if (_block == null) _block = new MaterialPropertyBlock();
            Color c = haloColor;
            c.a *= load;
            cardHalo.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            cardHalo.SetPropertyBlock(_block);
        }

        Transform Marker(int node)
        {
            if (view == null || view.nodeMarkers == null || node < 0 || node >= view.nodeMarkers.Length) return null;
            return view.nodeMarkers[node];
        }
    }
}
