using UnityEngine;
using OCS.VR.Rig;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// A readout floating over whichever card is working: which GPU, its load, temperature
    /// and memory, then a token counter climbing while the model generates. This is where
    /// the viewer sees that the answer is being written on that card, right now.
    ///
    /// Every string the counter will need is built once when the model hop starts, so the
    /// counter ticks in Update without allocating.
    /// </summary>
    public class CardTelemetryLabel : MonoBehaviour
    {
        public TracePlayer player;
        public RigCardDisplay rig;
        public TextMesh text;

        [Tooltip("Where the readout sits, metres from the card's origin in world axes.")]
        public Vector3 offset = new Vector3(0f, 0.2f, 0f);

        int _card = -1;       // first card of the replica the readout describes
        int _cardLast = -1;
        string _header = string.Empty;
        string _stats = string.Empty;
        Hop _generating;
        string[] _frames;
        int _shown = -1;

        void OnEnable()
        {
            if (player == null) return;
            player.HopStarted += OnHopStarted;
            player.TraceStarted += OnTraceStarted;
            player.TraceFinished += OnTraceFinished;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.HopStarted -= OnHopStarted;
            player.TraceStarted -= OnTraceStarted;
            player.TraceFinished -= OnTraceFinished;
        }

        void Start() => Clear();

        void OnTraceStarted(Trace trace) => Clear();
        void OnTraceFinished(Trace trace) => Clear();

        void Clear()
        {
            _card = -1;
            _cardLast = -1;
            _generating = null;
            _frames = null;
            _shown = -1;
            if (text != null) text.text = string.Empty;
        }

        void OnHopStarted(Hop hop)
        {
            if (!hop.HasGpuTelemetry) return;

            if (hop.gpu_index != _card) _stats = string.Empty;
            _card = hop.gpu_index;
            _cardLast = hop.GpuLast;
            _header = TelemetryText.CardHeader(hop);
            string split = TelemetryText.SplitLine(hop);
            if (split.Length > 0) _header = _header + "\n" + split;
            string stats = TelemetryText.CardStats(hop);
            if (stats.Length > 0) _stats = stats;

            if (hop.Type == HopType.Model && hop.tokens_out > 0)
            {
                // One string per token count, built now so Update never allocates.
                string top = _stats.Length > 0 ? _header + "\n" + _stats + "\n" : _header + "\n";
                _frames = new string[hop.tokens_out + 1];
                for (int n = 0; n <= hop.tokens_out; n++)
                    _frames[n] = top + TelemetryText.Generating(n, hop.tokens_per_sec);
                _generating = hop;
                _shown = -1;
            }
            else if (text != null)
            {
                text.text = _stats.Length > 0 ? _header + "\n" + _stats : _header;
            }
        }

        void LateUpdate()
        {
            if (_card < 0 || rig == null || rig.cards == null || _cardLast >= rig.cards.Length
                || rig.cards[_card] == null || rig.cards[_cardLast] == null) return;

            // Over the middle of the replica, offset to the side of the beam.
            transform.position = (rig.cards[_card].position + rig.cards[_cardLast].position) * 0.5f + offset;

            if (_generating == null || _frames == null || text == null) return;
            int n = TelemetryText.TokensAt(player.ElapsedMs, _generating.t_start_ms, _generating.t_end_ms, _generating.tokens_out);
            if (n == _shown) return;
            _shown = n;
            text.text = _frames[n];
        }
    }
}
