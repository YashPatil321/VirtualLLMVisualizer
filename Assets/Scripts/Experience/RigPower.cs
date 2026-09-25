using System.Collections;
using UnityEngine;
using OCS.VR.Rig;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Powers the cards on at Act 3 and off again when the arc restarts. Cards come up
    /// one after another rather than all at once, so power on reads as a moment instead
    /// of a switch being flipped, which is V4's acceptance line.
    /// </summary>
    public class RigPower : MonoBehaviour
    {
        public ExperienceSequencer sequencer;
        public CardVisual[] cards;

        [Tooltip("Seconds between each card powering on.")]
        public float stagger = 0.18f;

        WaitForSeconds _wait;
        Coroutine _run;

        void Awake()
        {
            _wait = new WaitForSeconds(stagger);
        }

        void OnEnable()
        {
            if (sequencer != null) sequencer.ActStarted += OnActStarted;
        }

        void OnDisable()
        {
            if (sequencer != null) sequencer.ActStarted -= OnActStarted;
        }

        void Start()
        {
            SetAll(false);
        }

        void OnActStarted(Act act)
        {
            switch (act)
            {
                case Act.EmptyBench:
                case Act.Assembly:
                    if (_run != null) StopCoroutine(_run);
                    _run = null;
                    SetAll(false);
                    break;

                case Act.PowerOn:
                    if (_run != null) StopCoroutine(_run);
                    _run = StartCoroutine(PowerUp());
                    break;
            }
        }

        IEnumerator PowerUp()
        {
            if (cards == null) yield break;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null) cards[i].SetPowered(true);
                yield return _wait;
            }
            _run = null;
        }

        void SetAll(bool on)
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null) cards[i].SetPowered(on);
        }
    }
}
