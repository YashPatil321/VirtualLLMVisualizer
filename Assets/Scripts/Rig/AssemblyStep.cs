using System;

namespace OCS.VR.Rig
{
    public enum PartKind
    {
        Chassis = 0,
        Motherboard,
        Cpu,
        Ram,
        Psu,
        Riser,
        Gpu,
        Cable,
        Fan
    }

    /// <summary>
    /// One move in the assembly: a part travelling from the tray to its socket.
    /// Plain serializable data so the whole build order lives in an AssemblySequence
    /// asset and can be retimed without a recompile, per CLAUDE.md.
    ///
    /// Order and timings come from the Rig 2 build log. If the real order changes,
    /// change the asset, not this class.
    /// </summary>
    [Serializable]
    public class AssemblyStep
    {
        public string stepId;

        [UnityEngine.Tooltip("Shown in the world space label as the part flies in.")]
        public string partName;

        public PartKind kind;

        [UnityEngine.Tooltip("Which card slot, for Gpu and Riser steps. -1 for everything else.")]
        public int cardIndex = -1;

        [UnityEngine.Tooltip("Seconds the part takes to travel from tray to socket.")]
        public float travelSeconds = 1.5f;

        [UnityEngine.Tooltip("Seconds it sits settled before the next step begins.")]
        public float settleSeconds = 0.4f;

        [UnityEngine.Tooltip("One line, what it is and why it is there. Blank for no narration.")]
        public string narration;

        public float DurationSeconds => travelSeconds + settleSeconds;
        public bool TargetsCard => cardIndex >= 0;
        public string DisplayName => string.IsNullOrEmpty(partName) ? stepId : partName;
    }
}
