using System;
using UnityEngine;
using OCS.VR.Rig;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Binds one assembly step to the Transforms it moves. Sockets may be left empty for
    /// card steps: the position then comes from RigLayout, so card spacing stays a single
    /// number in one asset rather than eight hand placed transforms.
    /// </summary>
    [Serializable]
    public class AssemblyPartBinding
    {
        public string stepId;
        public Transform part;

        [Tooltip("Where the part waits before its step. Usually a slot on the tray.")]
        public Transform tray;

        [Tooltip("Where it ends up. Leave empty on card steps to use RigLayout instead.")]
        public Transform socket;
    }

    /// <summary>
    /// Plays an AssemblySequence and moves parts from tray to socket. All ordering and
    /// timing lives in AssemblyTimeline, which is plain C#; this class only turns that
    /// into Transforms, the same split TracePlayer and RigCardDisplay use.
    ///
    /// No allocation in Update.
    /// </summary>
    public class AssemblyPlayer : MonoBehaviour
    {
        [Header("Data")]
        public AssemblySequence sequence;
        public RigLayout layout;

        [Header("Bindings")]
        [Tooltip("One per step. Steps with no binding still take their time, they just do not move anything.")]
        public AssemblyPartBinding[] bindings;

        [Tooltip("Root the card sockets are relative to. Defaults to this transform.")]
        public Transform rigRoot;

        [Header("Playback")]
        public bool playOnStart = false;

        [Tooltip("Eases the travel so parts do not move at a constant crawl.")]
        public bool smoothTravel = true;

        public event Action Started;
        public event Action<AssemblyStep, int> StepStarted;
        public event Action<AssemblyStep, int> StepEnded;
        public event Action Finished;

        AssemblyTimeline _timeline;
        bool _playing;

        public bool IsPlaying => _playing;
        public bool IsFinished => _timeline != null && _timeline.IsFinished;
        public AssemblyStep CurrentStep => _timeline?.CurrentStep;
        public float TotalSeconds => _timeline != null ? _timeline.TotalSeconds : 0f;

        void Awake()
        {
            if (rigRoot == null) rigRoot = transform;
            Build();
        }

        void Start()
        {
            ParkAllParts();
            if (playOnStart) Play();
        }

        bool Build()
        {
            if (sequence == null)
            {
                Debug.LogError("[AssemblyPlayer] No AssemblySequence assigned.");
                return false;
            }

            string error;
            if (!sequence.Validate(out error))
            {
                Debug.LogError("[AssemblyPlayer] " + error);
                return false;
            }

            _timeline = new AssemblyTimeline(sequence);
            _timeline.StepStarted += OnStepStarted;
            _timeline.StepEnded += OnStepEnded;
            _timeline.Finished += OnFinished;

            Debug.Log($"[AssemblyPlayer] {sequence.StepCount} steps, {sequence.TotalDurationSeconds:F1}s.");
            return true;
        }

        public void Play()
        {
            if (_timeline == null && !Build()) return;

            _timeline.Reset();
            ParkAllParts();
            _playing = true;
            Started?.Invoke();
        }

        public void Stop() => _playing = false;

        /// <summary>Everything seated. Used when the viewer arrives after assembly.</summary>
        public void SnapToAssembled()
        {
            if (bindings == null) return;

            for (int i = 0; i < bindings.Length; i++)
            {
                AssemblyPartBinding b = bindings[i];
                if (b == null || b.part == null) continue;
                b.part.position = SocketPositionFor(b);
                b.part.gameObject.SetActive(true);
            }
            _playing = false;
        }

        void ParkAllParts()
        {
            if (bindings == null) return;

            for (int i = 0; i < bindings.Length; i++)
            {
                AssemblyPartBinding b = bindings[i];
                if (b == null || b.part == null) continue;
                if (b.tray != null) b.part.position = b.tray.position;
            }
        }

        void Update()
        {
            if (!_playing || _timeline == null) return;

            _timeline.Advance(Time.deltaTime);

            AssemblyStep step = _timeline.CurrentStep;
            if (step == null) return;

            AssemblyPartBinding binding = FindBinding(step.stepId);
            if (binding == null || binding.part == null) return;

            float t = _timeline.TravelProgress01;
            if (smoothTravel) t = t * t * (3f - 2f * t);   // smoothstep, no allocation

            Vector3 from = binding.tray != null ? binding.tray.position : binding.part.position;
            binding.part.position = Vector3.Lerp(from, SocketPositionFor(binding), t);
        }

        Vector3 SocketPositionFor(AssemblyPartBinding binding)
        {
            if (binding.socket != null) return binding.socket.position;

            AssemblyStep step = sequence != null ? sequence.Find(binding.stepId) : null;
            if (step != null && step.TargetsCard && layout != null && layout.IsValidIndex(step.cardIndex))
            {
                Vector3 local = layout.GetCardLocalPosition(step.cardIndex);
                return rigRoot != null ? rigRoot.TransformPoint(local) : local;
            }

            return binding.part != null ? binding.part.position : Vector3.zero;
        }

        AssemblyPartBinding FindBinding(string stepId)
        {
            if (bindings == null || string.IsNullOrEmpty(stepId)) return null;

            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != null && bindings[i].stepId == stepId) return bindings[i];
            }
            return null;
        }

        void OnStepStarted(AssemblyStep s, int i) => StepStarted?.Invoke(s, i);
        void OnStepEnded(AssemblyStep s, int i) => StepEnded?.Invoke(s, i);

        void OnFinished()
        {
            _playing = false;
            Finished?.Invoke();
        }
    }
}
