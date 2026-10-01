using System.Collections.Generic;
using UnityEngine;

namespace OCS.VR.Telemetry
{
    /// <summary>
    /// Parses and validates trace JSON. Loading is separate from playback on purpose:
    /// a bad trace should fail loudly here, in the editor, not halfway through a demo
    /// with a headset on someone's face.
    /// </summary>
    public static class TraceLoader
    {
        public static bool TryParse(string json, out Trace trace, out string error)
        {
            trace = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Trace JSON was empty.";
                return false;
            }

            try
            {
                trace = JsonUtility.FromJson<Trace>(json);
            }
            catch (System.Exception e)
            {
                error = "Trace JSON failed to parse: " + e.Message;
                return false;
            }

            if (trace == null)
            {
                error = "Trace JSON parsed to null.";
                return false;
            }

            if (trace.hops == null || trace.hops.Count == 0)
            {
                error = "Trace has no hops.";
                return false;
            }

            // Sort by start time rather than trusting array order, so out of order
            // writes from the broker replay correctly.
            trace.hops.Sort(CompareByStart);

            if (!Validate(trace, out error)) return false;

            return true;
        }

        public static bool TryLoad(TextAsset asset, out Trace trace, out string error)
        {
            if (asset == null)
            {
                trace = null;
                error = "No trace asset assigned.";
                return false;
            }

            return TryParse(asset.text, out trace, out error);
        }

        static int CompareByStart(Hop a, Hop b)
        {
            return a.t_start_ms.CompareTo(b.t_start_ms);
        }

        static bool Validate(Trace trace, out string error)
        {
            error = null;

            for (int i = 0; i < trace.hops.Count; i++)
            {
                Hop h = trace.hops[i];

                if (h.t_end_ms < h.t_start_ms)
                {
                    error = $"Hop {i} ({h.hop}) ends before it starts.";
                    return false;
                }

                if (h.Type == HopType.Rig && !h.HasGpuTelemetry)
                {
                    // Not fatal. The rig hop still draws, the card just does not light.
                    Debug.LogWarning($"[TraceLoader] Rig hop {i} on {h.node_id} has no gpu_index. No card will light for it.");
                }

                if (h.gpu_index >= 0 && h.GpuLast > 7)
                {
                    error = $"Hop {i} runs on GPUs {h.gpu_index} to {h.GpuLast}. The rig has 8 cards, 0 to 7.";
                    return false;
                }
            }

            if (trace.TotalDurationMs <= 0f)
            {
                error = "Trace has zero duration.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Hops that the experience knows how to draw, in play order.
        /// Unknown types are dropped here rather than checked during playback.
        /// </summary>
        public static List<Hop> DrawableHops(Trace trace)
        {
            List<Hop> result = new List<Hop>(trace.hops.Count);

            for (int i = 0; i < trace.hops.Count; i++)
            {
                if (trace.hops[i].Type != HopType.Unknown)
                {
                    result.Add(trace.hops[i]);
                }
                else
                {
                    Debug.Log($"[TraceLoader] Skipping unknown hop type '{trace.hops[i].hop}'.");
                }
            }

            return result;
        }
    }
}
