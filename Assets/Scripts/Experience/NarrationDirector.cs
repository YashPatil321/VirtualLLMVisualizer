using System;
using UnityEngine;
using OCS.VR.Rig;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Turns what is happening into the line that should be showing. Subscribes to the
    /// sequencer, the assembly player and the trace player, resolves a cue against a
    /// NarrationTrack, and raises LineChanged. It does not draw anything: a world space
    /// text component or an AudioSource listens to it.
    ///
    /// Cue strings are built once in Awake, not per event, so nothing here allocates
    /// during playback.
    /// </summary>
    public class NarrationDirector : MonoBehaviour
    {
        [Header("References")]
        public NarrationTrack track;
        public ExperienceSequencer sequencer;
        public AssemblyPlayer assemblyPlayer;
        public TracePlayer tracePlayer;

        [Header("Behaviour")]
        [Tooltip("Hop lines are the chattiest. Turn off if the request act feels noisy.")]
        public bool narrateHops = true;

        [Tooltip("Optional. Plays NarrationLine.clip when a line has one.")]
        public AudioSource audioSource;

        public event Action<NarrationLine> LineChanged;
        public event Action LineCleared;

        string[] _actCues;
        string[] _hopCues;

        NarrationLine _current;
        float _clearAt;

        public NarrationLine CurrentLine => _current;
        public string CurrentText => _current != null ? _current.text : string.Empty;

        void Awake()
        {
            // Precompute every cue string so events never allocate.
            Array actValues = Enum.GetValues(typeof(Act));
            _actCues = new string[actValues.Length];
            for (int i = 0; i < actValues.Length; i++)
                _actCues[i] = NarrationTrack.ActCue((Act)actValues.GetValue(i));

            Array hopValues = Enum.GetValues(typeof(HopType));
            _hopCues = new string[hopValues.Length];
            for (int i = 0; i < hopValues.Length; i++)
                _hopCues[i] = NarrationTrack.HopCue((HopType)hopValues.GetValue(i));
        }

        void OnEnable()
        {
            if (sequencer != null) sequencer.ActStarted += OnActStarted;
            if (assemblyPlayer != null) assemblyPlayer.StepStarted += OnStepStarted;
            if (tracePlayer != null) tracePlayer.HopStarted += OnHopStarted;
        }

        void OnDisable()
        {
            if (sequencer != null) sequencer.ActStarted -= OnActStarted;
            if (assemblyPlayer != null) assemblyPlayer.StepStarted -= OnStepStarted;
            if (tracePlayer != null) tracePlayer.HopStarted -= OnHopStarted;
        }

        void Start()
        {
            if (track == null)
            {
                Debug.LogWarning("[NarrationDirector] No NarrationTrack assigned. The experience will run silent.");
                return;
            }

            string error;
            if (!track.Validate(out error)) Debug.LogError("[NarrationDirector] " + error);
        }

        void OnActStarted(Act act)
        {
            int i = (int)act;
            Show(i >= 0 && i < _actCues.Length ? _actCues[i] : null);
        }

        void OnStepStarted(AssemblyStep step, int index)
        {
            if (step == null) return;

            // A step can carry its own line so the build order and its narration stay in
            // one asset. The track is the fallback.
            if (!string.IsNullOrEmpty(step.narration))
            {
                ShowLiteral(step.narration);
                return;
            }

            Show(NarrationTrack.StepCue(step.stepId));
        }

        void OnHopStarted(Hop hop)
        {
            if (!narrateHops || hop == null) return;

            int i = (int)hop.Type;
            Show(i >= 0 && i < _hopCues.Length ? _hopCues[i] : null);
        }

        void Show(string cue)
        {
            if (track == null || string.IsNullOrEmpty(cue)) return;

            NarrationLine line = track.Find(cue);
            if (line == null) return;      // no line authored for this cue yet, stay quiet

            _current = line;
            _clearAt = Time.time + track.HoldFor(line);

            if (audioSource != null && line.clip != null) audioSource.PlayOneShot(line.clip);

            LineChanged?.Invoke(line);
        }

        readonly NarrationLine _adHoc = new NarrationLine();

        void ShowLiteral(string text)
        {
            // Reuses one instance so a step's inline narration does not allocate.
            _adHoc.cue = string.Empty;
            _adHoc.text = text;
            _adHoc.holdSeconds = 0f;

            _current = _adHoc;
            _clearAt = Time.time + (track != null ? track.defaultHoldSeconds : 4f);

            LineChanged?.Invoke(_adHoc);
        }

        void Update()
        {
            if (_current == null) return;

            if (Time.time >= _clearAt)
            {
                _current = null;
                LineCleared?.Invoke();
            }
        }
    }
}
