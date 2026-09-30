using System;
using UnityEngine;

namespace OCS.VR.Experience
{
    public enum StageMove
    {
        /// <summary>Grows from nothing to its built size.</summary>
        Appear,
        /// <summary>Drops into the floor and switches off.</summary>
        Sink
    }

    [Serializable]
    public class StageCue
    {
        public Transform target;
        public Act act = Act.PowerOn;
        public StageMove move = StageMove.Appear;
        [Tooltip("Seconds into the act before it starts.")]
        public float delay;
        public float duration = 1.2f;
        [Tooltip("Sink only: metres it drops.")]
        public float depth = 1f;
    }

    /// <summary>
    /// Keeps the stage clean by showing things only while they matter. The empty tray
    /// and frame stand sink away once the rig is built, the system view grows in as the
    /// rig powers on, and the timeline appears with the request.
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        public ExperienceSequencer sequencer;
        public StageCue[] cues;

        Vector3[] _scale;
        Vector3[] _position;
        Act _act = Act.None;
        float _actStart;

        void Awake()
        {
            int n = cues != null ? cues.Length : 0;
            _scale = new Vector3[n];
            _position = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Transform t = cues[i].target;
                if (t == null) continue;
                _scale[i] = t.localScale;
                _position[i] = t.localPosition;
            }
        }

        void OnEnable()
        {
            if (sequencer != null) sequencer.ActStarted += OnActStarted;
        }

        void OnDisable()
        {
            if (sequencer != null) sequencer.ActStarted -= OnActStarted;
        }

        void Start() => Apply();

        void OnActStarted(Act act)
        {
            _act = act;
            _actStart = Time.time;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            if (cues == null) return;
            float into = Time.time - _actStart;
            for (int i = 0; i < cues.Length; i++)
            {
                StageCue cue = cues[i];
                if (cue.target == null) continue;
                float p = StageMotion.Progress(_act, into, cue.act, cue.delay, cue.duration);

                if (cue.move == StageMove.Appear)
                {
                    // Never exactly zero: a zero scale makes a degenerate matrix.
                    cue.target.localScale = _scale[i] * Mathf.Max(0.0001f, p);
                    SetShown(cue.target, p > 0f);
                }
                else
                {
                    cue.target.localPosition = _position[i] + Vector3.down * (cue.depth * p);
                    SetShown(cue.target, p < 1f);
                }
            }
        }

        static void SetShown(Transform t, bool on)
        {
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }
    }
}
