using UnityEngine;
using OCS.VR.Rig;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// A flash where something happens: a ring of light rushing out along the floor and a
    /// spray of sparks. Every part that seats gets one, the rig gets a big one as it
    /// powers on, the working card gets one when the job lands on it, and the client gets
    /// one when the answer arrives. Rings come from a small pool; sparks from one particle
    /// system. No allocation.
    /// </summary>
    public class ImpactBursts : MonoBehaviour
    {
        [Header("What to listen to")]
        public AssemblyPlayer assembly;
        public ExperienceSequencer sequencer;
        public TracePlayer player;
        public RigCardDisplay rig;
        public SystemViewDisplay view;
        public SystemGraph graph;

        [Header("Pieces")]
        [Tooltip("Flat quads with the ring glow material, reused in turn.")]
        public Renderer[] rings;
        public ParticleSystem sparks;

        [Header("Look")]
        [ColorUsage(true, true)] public Color seatColor = new Color(0.4f, 1.2f, 1.8f, 1f);
        [ColorUsage(true, true)] public Color powerColor = new Color(0.5f, 1.5f, 2.2f, 1f);
        [ColorUsage(true, true)] public Color cardColor = new Color(2.0f, 0.9f, 0.25f, 1f);
        public float ringSeconds = 0.8f;

        static readonly int ColorId = Shader.PropertyToID("_Color");

        float[] _start;
        float[] _size;
        Color[] _color;
        int _next;
        MaterialPropertyBlock _block;

        void Awake()
        {
            int n = rings != null ? rings.Length : 0;
            _start = new float[n];
            _size = new float[n];
            _color = new Color[n];
            _block = new MaterialPropertyBlock();
            for (int i = 0; i < n; i++)
            {
                _start[i] = -100f;
                if (rings[i] != null) rings[i].gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            if (assembly != null) assembly.StepEnded += OnStepEnded;
            if (sequencer != null) sequencer.ActStarted += OnActStarted;
            if (player != null)
            {
                player.HopStarted += OnHopStarted;
                player.TraceFinished += OnTraceFinished;
            }
        }

        void OnDisable()
        {
            if (assembly != null) assembly.StepEnded -= OnStepEnded;
            if (sequencer != null) sequencer.ActStarted -= OnActStarted;
            if (player != null)
            {
                player.HopStarted -= OnHopStarted;
                player.TraceFinished -= OnTraceFinished;
            }
        }

        void OnStepEnded(AssemblyStep step, int index)
        {
            Transform part = PartFor(step);
            if (part == null) return;
            bool big = step.kind == PartKind.Chassis;
            Play(part.position, big ? 1.4f : 0.45f, seatColor, big ? 60 : 18);
        }

        void OnActStarted(Act act)
        {
            if (act == Act.PowerOn && rig != null) Play(rig.transform.position, 3.2f, powerColor, 120);
        }

        void OnHopStarted(Hop hop)
        {
            if (hop.Type != HopType.Model || !hop.HasGpuTelemetry || rig == null || rig.cards == null) return;
            if (hop.gpu_index < 0 || hop.gpu_index >= rig.cards.Length || rig.cards[hop.gpu_index] == null) return;
            Play(rig.cards[hop.gpu_index].position + Vector3.up * 0.06f, 0.6f, cardColor, 50);
        }

        void OnTraceFinished(Trace trace)
        {
            if (view == null || view.nodeMarkers == null || graph == null) return;
            for (int i = 0; i < graph.NodeCount && i < view.nodeMarkers.Length; i++)
            {
                if (graph.nodes[i].hopType != HopType.Client || view.nodeMarkers[i] == null) continue;
                Play(view.nodeMarkers[i].position, 1.6f, powerColor, 80);
                return;
            }
        }

        Transform PartFor(AssemblyStep step)
        {
            if (step == null || assembly == null || assembly.bindings == null) return null;
            for (int i = 0; i < assembly.bindings.Length; i++)
            {
                AssemblyPartBinding b = assembly.bindings[i];
                if (b != null && b.stepId == step.stepId) return b.part;
            }
            return null;
        }

        /// <summary>A ring size metres across at position, and count sparks.</summary>
        public void Play(Vector3 position, float size, Color color, int count)
        {
            if (rings != null && rings.Length > 0)
            {
                int i = _next;
                _next = (_next + 1) % rings.Length;
                Renderer ring = rings[i];
                if (ring != null)
                {
                    ring.transform.position = position;
                    ring.gameObject.SetActive(true);
                    _start[i] = Time.time;
                    _size[i] = size;
                    _color[i] = color;
                }
            }

            if (sparks != null && count > 0)
            {
                var emit = new ParticleSystem.EmitParams();    // a struct: no allocation
                emit.position = position;
                emit.applyShapeToPosition = true;
                emit.startColor = color;
                sparks.Emit(emit, count);
            }
            Played++;
        }

        void Update()
        {
            if (rings == null) return;
            for (int i = 0; i < rings.Length; i++)
            {
                Renderer ring = rings[i];
                if (ring == null || !ring.gameObject.activeSelf) continue;
                float t = (Time.time - _start[i]) / ringSeconds;
                if (t >= 1f)
                {
                    ring.gameObject.SetActive(false);
                    continue;
                }
                float grow = 1f - (1f - t) * (1f - t) * (1f - t);        // ease out
                ring.transform.localScale = Vector3.one * (_size[i] * (0.15f + 0.85f * grow));
                Color c = _color[i];
                c.a = (1f - t) * (1f - t);
                ring.GetPropertyBlock(_block);
                _block.SetColor(ColorId, c);
                ring.SetPropertyBlock(_block);
            }
        }

        /// <summary>For the headless run: how many bursts have gone off.</summary>
        public int Played { get; private set; }
    }
}
