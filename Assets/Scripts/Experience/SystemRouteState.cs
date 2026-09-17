using System.Collections.Generic;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Where the request currently is in the system view, and how far along the edge it
    /// has travelled between the previous node and this one. Plain C#, no Transforms, so
    /// the whole route can be tested in edit mode.
    ///
    /// Node lighting uses the same rule as the rig cards: a node stays lit while any hop
    /// is still running on it, so two adjacent hops on one node never blink it off.
    ///
    /// No allocation after construction.
    /// </summary>
    public class SystemRouteState
    {
        readonly int _nodeCount;
        readonly int[] _activeHops;
        readonly float[] _glow;

        public int NodeCount => _nodeCount;
        public int CurrentNode { get; private set; }
        public int PreviousNode { get; private set; }

        /// <summary>0 at the previous node, 1 at the current one.</summary>
        public float EdgeProgress01 { get; private set; }

        public SystemRouteState(int nodeCount)
        {
            _nodeCount = nodeCount < 0 ? 0 : nodeCount;
            _activeHops = new int[_nodeCount];
            _glow = new float[_nodeCount];
            CurrentNode = -1;
            PreviousNode = -1;
        }

        public bool IsValidIndex(int node) => node >= 0 && node < _nodeCount;

        public void HopStarted(int node)
        {
            if (!IsValidIndex(node)) return;

            if (node != CurrentNode)
            {
                PreviousNode = CurrentNode;
                CurrentNode = node;
                EdgeProgress01 = PreviousNode >= 0 ? 0f : 1f;
            }

            _activeHops[node]++;
        }

        public void HopEnded(int node)
        {
            if (!IsValidIndex(node)) return;

            if (_activeHops[node] > 0) _activeHops[node]--;
        }

        public bool IsBusy(int node) => IsValidIndex(node) && _activeHops[node] > 0;

        /// <summary>Eased 0 to 1 glow per node, for emission or label emphasis.</summary>
        public float Glow(int node) => IsValidIndex(node) ? _glow[node] : 0f;

        public void ResetAll()
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                _activeHops[i] = 0;
                _glow[i] = 0f;
            }
            CurrentNode = -1;
            PreviousNode = -1;
            EdgeProgress01 = 0f;
        }

        /// <summary>Call once per frame. travelSpeed is edges per second.</summary>
        public void Step(float deltaTime, float travelSpeed, float glowSpeed)
        {
            if (EdgeProgress01 < 1f)
            {
                EdgeProgress01 += deltaTime * travelSpeed;
                if (EdgeProgress01 > 1f) EdgeProgress01 = 1f;
            }

            float t = deltaTime * glowSpeed;
            if (t > 1f) t = 1f;

            for (int i = 0; i < _nodeCount; i++)
            {
                float target = _activeHops[i] > 0 ? 1f : 0f;
                _glow[i] += (target - _glow[i]) * t;
            }
        }
    }
}
