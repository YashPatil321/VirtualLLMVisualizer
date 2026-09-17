using System;
using System.Collections.Generic;

namespace OCS.VR.Rig
{
    /// <summary>
    /// Which assembly step is running and how far through it is. Plain C#, no
    /// MonoBehaviour and no Transforms, so the build order can be tested in edit mode
    /// exactly the way trace playback can.
    ///
    /// No allocation in Advance.
    /// </summary>
    public class AssemblyTimeline
    {
        readonly List<AssemblyStep> _steps;
        readonly float _gap;

        public event Action<AssemblyStep, int> StepStarted;
        public event Action<AssemblyStep, int> StepEnded;
        public event Action Finished;

        float _elapsed;
        int _index = -1;
        bool _inStep;
        bool _finished;

        public float Elapsed => _elapsed;
        public int CurrentIndex => _index;
        public bool IsFinished => _finished;
        public int StepCount => _steps.Count;
        public float TotalSeconds { get; }

        /// <summary>0 while travelling, 1 once seated. Stays 1 through the settle.</summary>
        public float TravelProgress01 { get; private set; }

        public AssemblyTimeline(AssemblySequence sequence)
        {
            _steps = sequence != null && sequence.steps != null
                ? sequence.steps
                : new List<AssemblyStep>();
            _gap = sequence != null ? sequence.gapSeconds : 0f;
            TotalSeconds = sequence != null ? sequence.TotalDurationSeconds : 0f;
        }

        public AssemblyStep CurrentStep =>
            _index >= 0 && _index < _steps.Count ? _steps[_index] : null;

        public void Reset()
        {
            _elapsed = 0f;
            _index = -1;
            _inStep = false;
            _finished = false;
            TravelProgress01 = 0f;
        }

        public void Advance(float deltaTime)
        {
            if (_finished || _steps.Count == 0)
            {
                if (!_finished && _steps.Count == 0) FinishNow();
                return;
            }

            _elapsed += deltaTime;

            // Walk forward. A long frame can cross more than one step, so this loops
            // rather than assuming one step per frame.
            while (!_finished)
            {
                int next = _index + 1;

                if (!_inStep)
                {
                    if (next >= _steps.Count) { FinishNow(); return; }

                    float startAt = StartTimeOf(next);
                    if (_elapsed < startAt)
                    {
                        // Inside the gap between steps. The part that just seated must
                        // stay seated: reporting 0 here would snap it back to the tray.
                        TravelProgress01 = _index >= 0 ? 1f : 0f;
                        return;
                    }

                    _index = next;
                    _inStep = true;
                    StepStarted?.Invoke(_steps[_index], _index);
                    continue;
                }

                AssemblyStep s = _steps[_index];
                float stepStart = StartTimeOf(_index);
                float stepEnd = stepStart + s.DurationSeconds;

                if (_elapsed >= stepEnd)
                {
                    TravelProgress01 = 1f;
                    _inStep = false;
                    StepEnded?.Invoke(s, _index);
                    if (_index + 1 >= _steps.Count) { FinishNow(); return; }
                    continue;
                }

                float travelled = _elapsed - stepStart;
                TravelProgress01 = s.travelSeconds <= 0f
                    ? 1f
                    : Clamp01(travelled / s.travelSeconds);
                return;
            }
        }

        public float StartTimeOf(int index)
        {
            float t = 0f;
            for (int i = 0; i < index && i < _steps.Count; i++)
            {
                t += _steps[i].DurationSeconds + _gap;
            }
            return t;
        }

        void FinishNow()
        {
            _finished = true;
            TravelProgress01 = 1f;
            Finished?.Invoke();
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
