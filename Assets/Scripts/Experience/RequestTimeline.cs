using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// The "where the time goes" bar: one segment per hop, sized by how long it took, lit
    /// as the request reaches it, with a playhead tracking progress. Built from the trace by
    /// the scene builder, so the proportions are the real ones: routing is a sliver, and
    /// nearly the whole bar is the GPU generating.
    ///
    /// Segments are lit through a property block, so they share one material and nothing
    /// allocates per frame.
    /// </summary>
    public class RequestTimeline : MonoBehaviour
    {
        public TracePlayer player;

        [Tooltip("One per drawable hop, in play order.")]
        public Renderer[] segments;

        [Tooltip("Local x of each segment's left edge, and its width.")]
        public float[] segmentLeft;
        public float[] segmentWidth;

        public Transform playhead;

        [ColorUsage(false, true)] public Color pendingColor = new Color(0.05f, 0.08f, 0.12f);
        [ColorUsage(false, true)] public Color activeColor = new Color(0.5f, 1.2f, 2.0f);
        [ColorUsage(false, true)] public Color doneColor = new Color(0.12f, 0.3f, 0.55f);
        [ColorUsage(false, true)] public Color gpuColor = new Color(2.2f, 0.9f, 0.25f);

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _block;
        int _started;
        Hop _current;
        int _currentIndex = -1;

        void Awake() => _block = new MaterialPropertyBlock();

        void OnEnable()
        {
            if (player == null) return;
            player.HopStarted += OnHopStarted;
            player.HopEnded += OnHopEnded;
            player.TraceStarted += OnTraceStarted;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.HopStarted -= OnHopStarted;
            player.HopEnded -= OnHopEnded;
            player.TraceStarted -= OnTraceStarted;
        }

        // The stage director switches this on as the request act begins, so Start can come
        // after the trace has already started. Don't wipe hops that have begun.
        void Start()
        {
            if (_started == 0) ResetAll();
        }

        void OnTraceStarted(Trace trace) => ResetAll();

        void ResetAll()
        {
            _started = 0;
            _current = null;
            _currentIndex = -1;
            if (segments != null)
                for (int i = 0; i < segments.Length; i++) Paint(i, pendingColor);
            if (playhead != null && segmentLeft != null && segmentLeft.Length > 0)
                SetPlayhead(segmentLeft[0]);
        }

        void OnHopStarted(Hop hop)
        {
            // Hops start in play order, which is the order the segments were built in.
            int i = _started++;
            if (segments == null || i >= segments.Length) return;
            _current = hop;
            _currentIndex = i;
            Paint(i, hop.HasGpuTelemetry ? gpuColor : activeColor);
        }

        void OnHopEnded(Hop hop)
        {
            if (hop != _current || _currentIndex < 0) return;
            Paint(_currentIndex, doneColor);
        }

        void Update()
        {
            if (_current == null || playhead == null || segmentLeft == null || _currentIndex >= segmentLeft.Length) return;
            float t = _current.DurationMs > 0f ? (player.ElapsedMs - _current.t_start_ms) / _current.DurationMs : 1f;
            t = Mathf.Clamp01(t);
            SetPlayhead(segmentLeft[_currentIndex] + segmentWidth[_currentIndex] * t);
        }

        void SetPlayhead(float x)
        {
            Vector3 p = playhead.localPosition;
            p.x = x;
            playhead.localPosition = p;
        }

        void Paint(int i, Color c)
        {
            if (segments == null || i < 0 || i >= segments.Length || segments[i] == null) return;
            segments[i].GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, c);
            segments[i].SetPropertyBlock(_block);
        }
    }
}
