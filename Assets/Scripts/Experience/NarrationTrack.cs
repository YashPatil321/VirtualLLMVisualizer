using System;
using System.Collections.Generic;
using UnityEngine;

namespace OCS.VR.Experience
{
    /// <summary>
    /// One spoken or written line, keyed by a cue string.
    ///
    /// Cue format, so lines can be authored before the visuals exist:
    ///   act.EmptyBench, act.Assembly, act.PowerOn, act.Request, act.Answer, act.Complete
    ///   step.&lt;stepId&gt;   one per assembly step
    ///   hop.&lt;hopType&gt;   client, broker, scheduler, mini, rig, model
    /// </summary>
    [Serializable]
    public class NarrationLine
    {
        public string cue;

        [TextArea(1, 3)]
        [Tooltip("Keep it to one line. The viewer is looking around, not reading.")]
        public string text;

        [Tooltip("Seconds the line stays up. 0 uses the track's default.")]
        public float holdSeconds;

        [Tooltip("Optional. Placeholder audio is fine through V6.")]
        public AudioClip clip;
    }

    /// <summary>
    /// Narration as data. docs/experience-design.md says narration gets cut or rewritten
    /// after testing on three outside people at V6, so it has to be editable without a
    /// recompile and without touching the sequencer.
    /// </summary>
    [CreateAssetMenu(fileName = "NarrationTrack", menuName = "OCS/Narration Track")]
    public class NarrationTrack : ScriptableObject
    {
        public List<NarrationLine> lines = new List<NarrationLine>();

        [Tooltip("Used when a line sets holdSeconds to 0.")]
        public float defaultHoldSeconds = 4f;

        public int LineCount => lines != null ? lines.Count : 0;

        public NarrationLine Find(string cue)
        {
            if (lines == null || string.IsNullOrEmpty(cue)) return null;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] != null && lines[i].cue == cue) return lines[i];
            }
            return null;
        }

        public float HoldFor(NarrationLine line)
        {
            if (line == null) return 0f;
            return line.holdSeconds > 0f ? line.holdSeconds : defaultHoldSeconds;
        }

        public bool Validate(out string error)
        {
            error = null;
            if (lines == null) return true;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null || string.IsNullOrEmpty(lines[i].cue))
                {
                    error = $"Narration line {i} has no cue.";
                    return false;
                }

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (lines[j] != null && lines[j].cue == lines[i].cue)
                    {
                        error = $"Narration lines {i} and {j} share cue '{lines[i].cue}'.";
                        return false;
                    }
                }
            }
            return true;
        }

        public static string ActCue(Act act) => "act." + act;
        public static string StepCue(string stepId) => "step." + stepId;
        public static string HopCue(Telemetry.HopType hopType) => "hop." + hopType;
    }
}
