using System;
using System.Collections.Generic;
using UnityEngine;
using OCS.VR.Telemetry;

namespace OCS.VR.Experience
{
    /// <summary>
    /// One box in the system view: client, broker, scheduler, a rig.
    /// </summary>
    [Serializable]
    public class SystemNode
    {
        [Tooltip("Matches Hop.node_id from the trace, e.g. rig-2, ocs-broker.")]
        public string nodeId;

        [Tooltip("Shown on the world space label.")]
        public string displayName;

        [Tooltip("One line under the name saying what this part of the system does.")]
        public string role;

        [Tooltip("Used to place a hop whose node_id is not in this graph.")]
        public HopType hopType = HopType.Unknown;

        [Tooltip("Where the node sits, relative to the system view root.")]
        public Vector3 position;

        public string DisplayLabel => string.IsNullOrEmpty(displayName) ? nodeId : displayName;
    }

    /// <summary>
    /// The system view topology, as data. V5 needs nodes for client, broker,
    /// scheduler and both rigs; putting them in an asset means Yash can move the layout
    /// around without touching code, which is the same reason the rig layout is an asset.
    /// </summary>
    [CreateAssetMenu(fileName = "SystemGraph", menuName = "OCS/System Graph")]
    public class SystemGraph : ScriptableObject
    {
        public List<SystemNode> nodes = new List<SystemNode>();

        public int NodeCount => nodes != null ? nodes.Count : 0;

        /// <summary>
        /// Exact node_id first, then hop type. A hop that matches neither returns null and
        /// the caller skips it, which is what docs/metrics-contract.md promises Anvay:
        /// a new node or hop type never breaks a shipped headset build.
        /// </summary>
        public SystemNode Resolve(Hop hop)
        {
            if (hop == null || nodes == null) return null;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].nodeId == hop.node_id) return nodes[i];
            }

            HopType t = hop.Type;
            if (t == HopType.Unknown) return null;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].hopType == t) return nodes[i];
            }

            return null;
        }

        public SystemNode Find(string nodeId)
        {
            if (nodes == null || string.IsNullOrEmpty(nodeId)) return null;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].nodeId == nodeId) return nodes[i];
            }
            return null;
        }

        public int IndexOf(SystemNode node)
        {
            if (nodes == null || node == null) return -1;
            for (int i = 0; i < nodes.Count; i++) if (ReferenceEquals(nodes[i], node)) return i;
            return -1;
        }

        public bool Validate(out string error)
        {
            error = null;

            if (nodes == null || nodes.Count == 0)
            {
                error = "System graph has no nodes.";
                return false;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null || string.IsNullOrEmpty(nodes[i].nodeId))
                {
                    error = $"Node {i} has no nodeId.";
                    return false;
                }

                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (nodes[j] != null && nodes[j].nodeId == nodes[i].nodeId)
                    {
                        error = $"Nodes {i} and {j} share nodeId '{nodes[i].nodeId}'.";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
