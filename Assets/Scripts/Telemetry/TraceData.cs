using System;
using System.Collections.Generic;

namespace OCS.VR.Telemetry
{
    public enum HopType
    {
        Unknown = 0,
        Client,
        Broker,
        Scheduler,
        // No Mini runs in the current system; requests go from the scheduler straight to
        // a rig. Kept because SystemGraph.asset stores hop types as numbers, and removing
        // this would shift Rig and Model to the wrong values.
        Mini,
        Rig,
        Model
    }

    public enum HopStatus
    {
        Ok = 0,
        Queued,
        Failed
    }

    /// <summary>
    /// One hop of a request. Field names are snake_case on purpose: UnityEngine.JsonUtility
    /// maps JSON keys to field names literally and has no rename attribute. The clean C#
    /// surface is the properties below. Do not rename the fields without also changing
    /// docs/metrics-contract.md and every recorded trace.
    /// </summary>
    [Serializable]
    public class Hop
    {
        public string hop;
        public string node_id;
        public float t_start_ms;
        public float t_end_ms;
        public string status;
        public string label;

        // Rig hops only. gpu_index defaults to -1 so an absent field is distinguishable
        // from card 0. JsonUtility leaves absent fields at their initialized value.
        // Worth confirming on the first real parse, because a silent 0 here would
        // light up the wrong card.
        public int gpu_index = -1;
        public float gpu_util;
        public float vram_used_mb;
        public float temp_c;
        public string model;
        public string quantization;

        // Model hops only.
        public int tokens_out;
        public float tokens_per_sec;

        public HopType Type => ParseType(hop);
        public HopStatus Status => ParseStatus(status);
        public float DurationMs => t_end_ms - t_start_ms;
        public bool HasGpuTelemetry => gpu_index >= 0;
        public string DisplayLabel => string.IsNullOrEmpty(label) ? node_id : label;

        public static HopType ParseType(string value)
        {
            if (string.IsNullOrEmpty(value)) return HopType.Unknown;

            switch (value.ToLowerInvariant())
            {
                case "client": return HopType.Client;
                case "broker": return HopType.Broker;
                case "scheduler": return HopType.Scheduler;
                case "mini": return HopType.Mini;
                case "rig": return HopType.Rig;
                case "model": return HopType.Model;
                // Unknown hop types are skipped by the player, never thrown on.
                // The broker can add hop types without breaking a shipped headset build.
                default: return HopType.Unknown;
            }
        }

        public static HopStatus ParseStatus(string value)
        {
            if (string.IsNullOrEmpty(value)) return HopStatus.Ok;

            switch (value.ToLowerInvariant())
            {
                case "queued": return HopStatus.Queued;
                case "failed": return HopStatus.Failed;
                default: return HopStatus.Ok;
            }
        }
    }

    [Serializable]
    public class Trace
    {
        public string trace_id;
        public string prompt_preview;
        /// <summary>Optional. The start of the answer, shown typing out in VR.</summary>
        public string response_preview;
        public string recorded_at;
        public List<Hop> hops = new List<Hop>();

        public float TotalDurationMs
        {
            get
            {
                float end = 0f;
                for (int i = 0; i < hops.Count; i++)
                {
                    if (hops[i].t_end_ms > end) end = hops[i].t_end_ms;
                }
                return end;
            }
        }

        public float TotalDurationSeconds => TotalDurationMs / 1000f;
    }
}
