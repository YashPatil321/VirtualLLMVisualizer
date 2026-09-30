using UnityEngine;
using OCS.VR.Rig;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// While the model generates, glowing tokens stream off the working card and fly into
    /// the answer panel, at the rate the card is really producing them (slowed by the
    /// replay's time scale, like everything else).
    /// </summary>
    public class TokenStream : MonoBehaviour
    {
        public TracePlayer player;
        public RigCardDisplay rig;
        public ParticleSystem tokens;

        [Tooltip("Where the tokens fly to. The answer panel.")]
        public Transform target;

        [Tooltip("Particles per generated token. More than one reads as a stream.")]
        public float particlesPerToken = 1.5f;

        [Tooltip("Seconds a token takes to reach the panel.")]
        public float flightSeconds = 0.9f;

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

        void Start() => SetRate(0f);

        void OnTraceStarted(Trace trace)
        {
            SetRate(0f);
            if (tokens != null) tokens.Clear();
        }

        void OnHopStarted(Hop hop)
        {
            if (hop.Type != HopType.Model || !hop.HasGpuTelemetry || tokens == null || target == null) return;
            if (rig == null || rig.cards == null || hop.gpu_index < 0 || hop.gpu_index >= rig.cards.Length) return;
            Transform card = rig.cards[hop.gpu_index];
            if (card == null) return;

            Vector3 from = card.position + Vector3.up * 0.07f;
            Vector3 dir = target.position - from;
            tokens.transform.position = from;
            tokens.transform.rotation = Quaternion.LookRotation(dir);

            var main = tokens.main;
            main.startLifetime = flightSeconds;
            main.startSpeed = dir.magnitude / flightSeconds;

            float scale = player != null ? player.timeScale : 1f;
            SetRate(hop.tokens_per_sec * scale * particlesPerToken);
            if (!tokens.isPlaying) tokens.Play();
        }

        void OnHopEnded(Hop hop)
        {
            if (hop.Type == HopType.Model) SetRate(0f);
        }

        void SetRate(float perSecond)
        {
            if (tokens == null) return;
            var emission = tokens.emission;
            emission.rateOverTime = perSecond;
            Rate = perSecond;
        }

        /// <summary>For the headless run: the emission rate last set.</summary>
        public float Rate { get; private set; }
    }
}
