using System.Collections.Generic;
using UnityEngine;

namespace OCS.VR.Rig
{
    /// <summary>
    /// The rig build order, as data. V3's acceptance criterion is that the rig assembles
    /// itself in the correct real order with nothing hardcoded in a script, so this asset
    /// is the only place the order lives.
    /// </summary>
    [CreateAssetMenu(fileName = "AssemblySequence", menuName = "OCS/Assembly Sequence")]
    public class AssemblySequence : ScriptableObject
    {
        [Tooltip("Played in list order. This is the real order from the Rig 2 build log.")]
        public List<AssemblyStep> steps = new List<AssemblyStep>();

        [Tooltip("Seconds of stillness between one part settling and the next starting.")]
        public float gapSeconds = 0.25f;

        public int StepCount => steps != null ? steps.Count : 0;

        public float TotalDurationSeconds
        {
            get
            {
                if (steps == null || steps.Count == 0) return 0f;

                float total = 0f;
                for (int i = 0; i < steps.Count; i++) total += steps[i].DurationSeconds;
                total += gapSeconds * (steps.Count - 1);
                return total;
            }
        }

        /// <summary>
        /// Checks the sequence the way TraceLoader checks a trace: loudly, in the editor,
        /// rather than halfway through a demo.
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;

            if (steps == null || steps.Count == 0)
            {
                error = "Assembly sequence has no steps.";
                return false;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                AssemblyStep s = steps[i];

                if (s == null)
                {
                    error = $"Step {i} is null.";
                    return false;
                }

                if (string.IsNullOrEmpty(s.stepId))
                {
                    error = $"Step {i} has no stepId. Narration cues key off it.";
                    return false;
                }

                if (s.travelSeconds <= 0f)
                {
                    error = $"Step {i} ({s.stepId}) has travelSeconds {s.travelSeconds}. It would snap into place.";
                    return false;
                }

                if (s.settleSeconds < 0f)
                {
                    error = $"Step {i} ({s.stepId}) has negative settleSeconds.";
                    return false;
                }

                for (int j = i + 1; j < steps.Count; j++)
                {
                    if (steps[j] != null && steps[j].stepId == s.stepId)
                    {
                        error = $"Steps {i} and {j} share stepId '{s.stepId}'. Cues would be ambiguous.";
                        return false;
                    }
                }
            }

            return true;
        }

        public AssemblyStep Find(string stepId)
        {
            if (steps == null || string.IsNullOrEmpty(stepId)) return null;

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].stepId == stepId) return steps[i];
            }
            return null;
        }
    }
}
