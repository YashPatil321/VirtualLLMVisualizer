using UnityEngine;
using OCS.VR.Rig;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Makes the hall react to the rig. Works out the power on wave from WorldPulse and
    /// whether the rig is generating, and hands both to the floor and rack shaders as
    /// global values, once a frame. The shaders do the rest, so a hall of racks costs no
    /// script time at all.
    /// </summary>
    public class WorldPulseDriver : MonoBehaviour
    {
        public ExperienceSequencer sequencer;
        public TracePlayer player;

        [Tooltip("Where waves start. The rig.")]
        public Transform centre;

        [Tooltip("How quickly the generation ripples fade in and out, per second.")]
        public float rippleRate = 1.5f;

        static readonly int CentreId = Shader.PropertyToID("_OCSCentre");
        static readonly int WaveRadiusId = Shader.PropertyToID("_OCSWaveRadius");
        static readonly int WaveStrengthId = Shader.PropertyToID("_OCSWaveStrength");
        static readonly int WakeId = Shader.PropertyToID("_OCSWake");
        static readonly int RippleId = Shader.PropertyToID("_OCSRipple");

        Act _act = Act.None;
        float _actStart;
        int _modelHops;       // prefill and generating can overlap by a frame
        float _ripple;

        void OnEnable()
        {
            if (sequencer != null) sequencer.ActStarted += OnActStarted;
            if (player != null)
            {
                player.HopStarted += OnHopStarted;
                player.HopEnded += OnHopEnded;
                player.TraceStarted += OnTrace;
                player.TraceFinished += OnTrace;
            }
        }

        void OnDisable()
        {
            if (sequencer != null) sequencer.ActStarted -= OnActStarted;
            if (player != null)
            {
                player.HopStarted -= OnHopStarted;
                player.HopEnded -= OnHopEnded;
                player.TraceStarted -= OnTrace;
                player.TraceFinished -= OnTrace;
            }
        }

        void OnActStarted(Act act)
        {
            _act = act;
            _actStart = Time.time;
        }

        void OnHopStarted(Hop hop)
        {
            if (hop.Type == HopType.Model) _modelHops++;
        }

        void OnHopEnded(Hop hop)
        {
            if (hop.Type == HopType.Model && _modelHops > 0) _modelHops--;
        }

        void OnTrace(Trace trace) => _modelHops = 0;

        void Start() => Apply(0f);

        void Update() => Apply(Time.deltaTime);

        void Apply(float dt)
        {
            float radius, strength, wake;
            WorldPulse.Evaluate(_act, Time.time - _actStart, out radius, out strength, out wake);
            _ripple = CardVisualMath.Approach(_ripple, _modelHops > 0 ? 1f : 0f, rippleRate * dt);

            Vector3 c = centre != null ? centre.position : Vector3.zero;
            Shader.SetGlobalVector(CentreId, new Vector4(c.x, c.y, c.z, 0f));
            Shader.SetGlobalFloat(WaveRadiusId, radius);
            Shader.SetGlobalFloat(WaveStrengthId, strength);
            Shader.SetGlobalFloat(WakeId, wake);
            Shader.SetGlobalFloat(RippleId, _ripple);
        }

        /// <summary>For the headless run: what the shaders were last told.</summary>
        public float Ripple => _ripple;
    }
}
