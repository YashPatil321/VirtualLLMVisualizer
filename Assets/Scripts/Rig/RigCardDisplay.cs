using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Rig
{
    /// <summary>
    /// Listens to a TracePlayer and drives the visual state of the cards: which one is
    /// working, how hot it is, how loaded. Holds no playback logic of its own.
    ///
    /// The load bookkeeping lives in CardLoadState, a plain C# class, so it can be tested
    /// in edit mode. This class only turns that state into Transforms.
    ///
    /// Card visuals are plain Transforms for now. Swap in real materials and emission
    /// at V7 without touching this class.
    /// </summary>
    public class RigCardDisplay : MonoBehaviour
    {
        [Header("References")]
        public TracePlayer player;
        public RigLayout layout;

        [Tooltip("Card visuals, index 0 to 7. Leave empty to have them placed from layout at Start.")]
        public Transform[] cards;

        [Tooltip("Fans and LED glow per card, index 0 to 7. Optional.")]
        public CardVisual[] visuals;

        [Tooltip("Place and lift the card Transforms. Turn off when something else moves " +
                 "them, such as AssemblyPlayer flying cards in from the tray.")]
        public bool driveTransforms = true;

        [Header("Placeholder feedback")]
        [Tooltip("How far an active card lifts. Stand in for real emission until V7.")]
        public float activeLift = 0.02f;

        public float lerpSpeed = 6f;

        CardLoadState _state;

        void Awake()
        {
            int count = layout != null ? layout.cardCount : 8;
            _state = new CardLoadState(count);
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
            if (driveTransforms) PlaceCardsFromLayout();
        }

        void PlaceCardsFromLayout()
        {
            if (layout == null || cards == null) return;

            for (int i = 0; i < cards.Length && i < layout.cardCount; i++)
            {
                if (cards[i] == null) continue;
                cards[i].localPosition = layout.GetCardLocalPosition(i);
            }
        }

        void OnHopStarted(Hop hop)
        {
            if (!hop.HasGpuTelemetry) return;

            // gpu_util arrives 0 to 100. Model hops carry an index but no util,
            // so fall back to fully loaded rather than showing an idle card
            // while it is visibly the one doing the work.
            float load = hop.gpu_util > 0f ? hop.gpu_util / 100f : 1f;
            // A replica: every card it is split across works together.
            for (int c = hop.gpu_index; c <= hop.GpuLast; c++) _state.HopStarted(c, load);
        }

        void OnHopEnded(Hop hop)
        {
            if (!hop.HasGpuTelemetry) return;
            for (int c = hop.gpu_index; c <= hop.GpuLast; c++) _state.HopEnded(c);
        }

        void OnTraceStarted(Trace trace) => _state.ResetAll();
        void OnTraceFinished(Trace trace) => _state.ResetAll();

        void Update()
        {
            _state.Step(Time.deltaTime, lerpSpeed);

            if (visuals != null)
            {
                for (int i = 0; i < visuals.Length && i < _state.CardCount; i++)
                    if (visuals[i] != null) visuals[i].SetLoad(_state.Current(i));
            }

            if (!driveTransforms || cards == null || layout == null) return;

            for (int i = 0; i < cards.Length && i < _state.CardCount; i++)
            {
                if (cards[i] == null) continue;

                Vector3 basePos = layout.GetCardLocalPosition(i);
                basePos.y += activeLift * _state.Current(i);
                cards[i].localPosition = basePos;
            }
        }

        public int ActiveCard => _state != null ? _state.ActiveCard : -1;
        public float LoadOf(int index) => _state != null ? _state.Current(index) : 0f;
    }
}
