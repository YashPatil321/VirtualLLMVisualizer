using System;
using System.Collections;
using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    public enum Act
    {
        None = 0,
        EmptyBench,
        Assembly,
        PowerOn,
        Request,
        Answer,
        Complete
    }

    /// <summary>
    /// Drives the five act arc. Acts advance on timers and on the trace finishing,
    /// not on input, because this is an experience and nobody has to do anything.
    ///
    /// Durations are fields so pacing can be retuned in the inspector without a
    /// recompile. Retiming is the thing that will get adjusted most.
    /// </summary>
    public class ExperienceSequencer : MonoBehaviour
    {
        [Header("References")]
        public TracePlayer tracePlayer;

        [Header("Act durations, seconds")]
        public float emptyBenchDuration = 20f;
        public float assemblyDuration = 90f;
        public float powerOnDuration = 15f;
        public float answerDuration = 20f;

        [Header("Behaviour")]
        public bool playOnStart = true;
        public bool loop = false;

        public event Action<Act> ActStarted;
        public event Action<Act> ActEnded;

        Act _current = Act.None;
        Coroutine _run;
        bool _traceDone;

        public Act CurrentAct => _current;

        /// <summary>Total runtime excluding the request act, which is trace length.</summary>
        public float FixedDurationSeconds =>
            emptyBenchDuration + assemblyDuration + powerOnDuration + answerDuration;

        void Awake()
        {
            // This sequencer owns when the trace plays. Leaving playOnStart on would
            // start it during Act 1, so TraceFinished lands long before Act 4 and the
            // request act waits on a flag that is already set. Awake, not Start:
            // component Start order is undefined and TracePlayer.Start would win.
            if (tracePlayer != null) tracePlayer.playOnStart = false;
        }

        void OnEnable()
        {
            if (tracePlayer != null) tracePlayer.TraceFinished += OnTraceFinished;
        }

        void OnDisable()
        {
            if (tracePlayer != null) tracePlayer.TraceFinished -= OnTraceFinished;
        }

        void Start()
        {
            WarnIfOverBudget();
            if (playOnStart) Begin();
        }

        void WarnIfOverBudget()
        {
            float traceSeconds = 0f;
            if (tracePlayer != null && tracePlayer.Load() && tracePlayer.CurrentTrace != null)
            {
                traceSeconds = tracePlayer.CurrentTrace.TotalDurationSeconds / Mathf.Max(0.01f, tracePlayer.timeScale);
            }

            float total = FixedDurationSeconds + traceSeconds;
            if (total > 300f)
            {
                Debug.LogWarning($"[Sequencer] Full runtime is {total:F0}s. The design target is under 300s. Tighten act durations.");
            }
        }

        public void Begin()
        {
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(RunArc());
        }

        public void Abort()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
            if (tracePlayer != null) tracePlayer.Stop();
            SetAct(Act.None);
        }

        IEnumerator RunArc()
        {
            do
            {
                _traceDone = false;

                SetAct(Act.EmptyBench);
                yield return new WaitForSeconds(emptyBenchDuration);

                SetAct(Act.Assembly);
                yield return new WaitForSeconds(assemblyDuration);

                SetAct(Act.PowerOn);
                yield return new WaitForSeconds(powerOnDuration);

                SetAct(Act.Request);
                if (tracePlayer != null)
                {
                    // Cleared here, not at the top of the arc, so nothing that happened
                    // during the earlier acts can satisfy the wait below.
                    _traceDone = false;
                    tracePlayer.Play();
                    // Wait on the trace rather than a timer, so retiming the trace
                    // never desyncs the arc from it.
                    while (!_traceDone) yield return null;
                }
                else
                {
                    Debug.LogWarning("[Sequencer] No TracePlayer assigned. Skipping the request act.");
                }

                SetAct(Act.Answer);
                yield return new WaitForSeconds(answerDuration);

                SetAct(Act.Complete);

                if (loop) yield return new WaitForSeconds(3f);
            }
            while (loop);

            _run = null;
        }

        void OnTraceFinished(Trace trace)
        {
            _traceDone = true;
        }

        void SetAct(Act next)
        {
            if (_current == next) return;

            if (_current != Act.None) ActEnded?.Invoke(_current);
            _current = next;
            if (_current != Act.None)
            {
                Debug.Log($"[Sequencer] Act: {_current}");
                ActStarted?.Invoke(_current);
            }
        }
    }
}
