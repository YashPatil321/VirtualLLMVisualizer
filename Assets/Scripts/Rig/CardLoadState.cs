namespace OCS.VR.Rig
{
    /// <summary>
    /// Per card load bookkeeping for the rig, as plain C# with no MonoBehaviour and no
    /// Transforms. Pulled out of RigCardDisplay so the Act 5 lighting behaviour can be
    /// covered by edit mode tests without a scene, the same way trace playback is.
    ///
    /// The rule that matters: a card stays lit while any hop is still running on it.
    /// A rig hop and the model hop after it overlap on one card, and the rig hop's end
    /// lands in the same frame the model hop starts, so last event wins would blank the
    /// card exactly when generation begins.
    ///
    /// No allocation after construction. This is stepped every frame on a mobile chip.
    /// </summary>
    public class CardLoadState
    {
        readonly float[] _target;
        readonly float[] _current;
        readonly int[] _activeHops;

        public int CardCount => _target.Length;

        /// <summary>Most recently started card, or -1 when nothing is running.</summary>
        public int ActiveCard { get; private set; }

        public CardLoadState(int cardCount)
        {
            if (cardCount < 0) cardCount = 0;
            _target = new float[cardCount];
            _current = new float[cardCount];
            _activeHops = new int[cardCount];
            ActiveCard = -1;
        }

        public bool IsValidIndex(int card) => card >= 0 && card < _target.Length;

        /// <param name="load01">0 to 1. Callers convert gpu_util from percent.</param>
        public void HopStarted(int card, float load01)
        {
            if (!IsValidIndex(card)) return;

            _activeHops[card]++;
            _target[card] = Clamp01(load01);
            ActiveCard = card;
        }

        public void HopEnded(int card)
        {
            if (!IsValidIndex(card)) return;

            if (_activeHops[card] > 0) _activeHops[card]--;

            // Still busy with an overlapping hop, so leave it lit.
            if (_activeHops[card] > 0) return;

            _target[card] = 0f;
            if (ActiveCard == card) ActiveCard = -1;
        }

        /// <summary>Back to idle. Used when a trace starts or finishes.</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _target.Length; i++)
            {
                _target[i] = 0f;
                _activeHops[i] = 0;
            }
            ActiveCard = -1;
        }

        /// <summary>Eases current toward target. Call once per frame.</summary>
        public void Step(float deltaTime, float lerpSpeed)
        {
            float t = Clamp01(deltaTime * lerpSpeed);
            for (int i = 0; i < _current.Length; i++)
            {
                _current[i] += (_target[i] - _current[i]) * t;
            }
        }

        public float Current(int card) => IsValidIndex(card) ? _current[card] : 0f;
        public float Target(int card) => IsValidIndex(card) ? _target[card] : 0f;
        public int ActiveHopCount(int card) => IsValidIndex(card) ? _activeHops[card] : 0;

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
