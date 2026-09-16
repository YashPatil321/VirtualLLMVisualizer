using System;
using System.Collections.Generic;
using UnityEngine;

namespace OCS.VR.Telemetry
{
    /// <summary>
    /// Plays a trace against wall clock time and raises events as hops start and end.
    /// Nothing visual lives here. Visuals subscribe. That split means the whole request
    /// path can be tested in a flat scene on a desktop before any headset is involved.
    ///
    /// No allocation in Update. This runs on a mobile chip and a GC spike reads as a
    /// stutter through a headset.
    /// </summary>
    public class TracePlayer : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Replay source. data/sample-trace.json until the broker emits real ones.")]
        public TextAsset traceAsset;

        [Header("Playback")]
        public bool playOnStart = true;

        [Tooltip("1 is real time. Real requests are often too fast to read, so the " +
                 "experience usually runs slower than life.")]
        [Range(0.05f, 4f)]
        public float timeScale = 0.5f;

        [Tooltip("Seconds to hold on the final hop before finishing.")]
        public float tailHold = 1.5f;

        public event Action<Hop> HopStarted;
        public event Action<Hop> HopEnded;
        public event Action<Trace> TraceStarted;
        public event Action<Trace> TraceFinished;

        Trace _trace;
        List<Hop> _hops;
        readonly List<Hop> _active = new List<Hop>(8);

        float _elapsedMs;
        int _nextIndex;
        bool _playing;

        public bool IsPlaying => _playing;
        public Trace CurrentTrace => _trace;
        public float ElapsedMs => _elapsedMs;

        public float NormalizedTime
        {
            get
            {
                if (_trace == null || _trace.TotalDurationMs <= 0f) return 0f;
                return Mathf.Clamp01(_elapsedMs / _trace.TotalDurationMs);
            }
        }

        void Start()
        {
            if (playOnStart) LoadAndPlay();
        }

        public void LoadAndPlay()
        {
            if (!Load()) return;
            Play();
        }

        public bool Load()
        {
            string error;

            if (!TraceLoader.TryLoad(traceAsset, out _trace, out error))
            {
                Debug.LogError("[TracePlayer] " + error);
                return false;
            }

            _hops = TraceLoader.DrawableHops(_trace);
            Debug.Log($"[TracePlayer] Loaded {_trace.trace_id}: {_hops.Count} hops, {_trace.TotalDurationSeconds:F2}s.");
            return true;
        }

        public void Play()
        {
            if (_trace == null && !Load()) return;

            Reset();
            _playing = true;
            TraceStarted?.Invoke(_trace);
        }

        public void Stop()
        {
            _playing = false;
            EndAllActive();
        }

        public void Reset()
        {
            _elapsedMs = 0f;
            _nextIndex = 0;
            EndAllActive();
        }

        void Update()
        {
            if (!_playing) return;

            _elapsedMs += Time.deltaTime * 1000f * timeScale;

            // Start any hop whose window has opened.
            while (_nextIndex < _hops.Count && _hops[_nextIndex].t_start_ms <= _elapsedMs)
            {
                Hop h = _hops[_nextIndex];
                _active.Add(h);
                HopStarted?.Invoke(h);
                _nextIndex++;
            }

            // End any active hop whose window has closed. Iterating backwards so
            // removal does not shift indices we have not visited yet.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].t_end_ms <= _elapsedMs)
                {
                    Hop h = _active[i];
                    _active.RemoveAt(i);
                    HopEnded?.Invoke(h);
                }
            }

            bool done = _nextIndex >= _hops.Count && _active.Count == 0;
            if (done && _elapsedMs >= _trace.TotalDurationMs + tailHold * 1000f)
            {
                _playing = false;
                TraceFinished?.Invoke(_trace);
            }
        }

        void EndAllActive()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Hop h = _active[i];
                _active.RemoveAt(i);
                HopEnded?.Invoke(h);
            }
        }
    }
}
