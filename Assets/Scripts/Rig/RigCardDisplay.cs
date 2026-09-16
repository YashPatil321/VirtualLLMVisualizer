using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Rig
{
    /// <summary>
    /// Listens to a TracePlayer and drives the visual state of the cards: which one is
    /// working, how hot it is, how loaded. Holds no playback logic of its own.
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

        [Header("Placeholder feedback")]
        [Tooltip("How far an active card lifts. Stand in for real emission until V7.")]
        public float activeLift = 0.02f;

        public float lerpSpeed = 6f;

        float[] _targetLoad;
        float[] _currentLoad;
        int _activeCard = -1;

        void Awake()
        {
            int count = layout != null ? layout.cardCount : 8;
            _targetLoad = new float[count];
            _currentLoad = new float[count];
        }

        void OnEnable()
        {
            if (player == null) return;
            player.HopStarted += OnHopStarted;
            player.HopEnded += OnHopEnded;
            player.TraceFinished += OnTraceFinished;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.HopStarted -= OnHopStarted;
            player.HopEnded -= OnHopEnded;
            player.TraceFinished -= OnTraceFinished;
        }

        void Start()
        {
            PlaceCardsFromLayout();
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
            if (layout != null && !layout.IsValidIndex(hop.gpu_index)) return;

            _activeCard = hop.gpu_index;

            // gpu_util arrives 0 to 100. Model hops carry an index but no util,
            // so fall back to fully loaded rather than showing an idle card
            // while it is visibly the one doing the work.
            float load = hop.gpu_util > 0f ? hop.gpu_util / 100f : 1f;
            _targetLoad[hop.gpu_index] = Mathf.Clamp01(load);
        }

        void OnHopEnded(Hop hop)
        {
            if (!hop.HasGpuTelemetry) return;
            if (layout != null && !layout.IsValidIndex(hop.gpu_index)) return;

            _targetLoad[hop.gpu_index] = 0f;
            if (_activeCard == hop.gpu_index) _activeCard = -1;
        }

        void OnTraceFinished(Trace trace)
        {
            for (int i = 0; i < _targetLoad.Length; i++) _targetLoad[i] = 0f;
            _activeCard = -1;
        }

        void Update()
        {
            if (cards == null) return;

            float t = Time.deltaTime * lerpSpeed;

            for (int i = 0; i < cards.Length && i < _currentLoad.Length; i++)
            {
                _currentLoad[i] = Mathf.Lerp(_currentLoad[i], _targetLoad[i], t);

                if (cards[i] == null || layout == null) continue;

                Vector3 basePos = layout.GetCardLocalPosition(i);
                basePos.y += activeLift * _currentLoad[i];
                cards[i].localPosition = basePos;
            }
        }

        public int ActiveCard => _activeCard;
        public float LoadOf(int index)
        {
            if (_currentLoad == null || index < 0 || index >= _currentLoad.Length) return 0f;
            return _currentLoad[index];
        }
    }
}
