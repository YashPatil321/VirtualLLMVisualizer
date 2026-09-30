using System.Collections.Generic;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Which light beams in the system view are lit. A beam is an edge between two nodes
    /// that the request actually travels, worked out once from the trace. When the request
    /// moves from one node to the next, that edge flashes and then fades.
    ///
    /// Plain C#, no Transforms, so the rules are covered by edit mode tests. SystemBeams
    /// turns the numbers into LineRenderers. No allocation after construction.
    /// </summary>
    public class BeamGlowState
    {
        readonly int[] _from;
        readonly int[] _to;
        readonly float[] _glow;

        public int EdgeCount => _glow.Length;
        public int LastNode { get; private set; }

        public BeamGlowState(int[] from, int[] to)
        {
            int n = from != null && to != null ? System.Math.Min(from.Length, to.Length) : 0;
            _from = new int[n];
            _to = new int[n];
            _glow = new float[n];
            for (int i = 0; i < n; i++) { _from[i] = from[i]; _to[i] = to[i]; }
            LastNode = -1;
        }

        public int From(int edge) => _from[edge];
        public int To(int edge) => _to[edge];
        public float Glow(int edge) => edge >= 0 && edge < _glow.Length ? _glow[edge] : 0f;

        /// <summary>
        /// The request has arrived at a node. Lights the edge it came along, if there is
        /// one, and returns that edge's index, or -1.
        /// </summary>
        public int NodeEntered(int node)
        {
            if (node < 0) return -1;

            int lit = -1;
            if (LastNode >= 0 && LastNode != node)
            {
                lit = Find(LastNode, node);
                if (lit >= 0) _glow[lit] = 1f;
            }
            LastNode = node;
            return lit;
        }

        public void Step(float deltaTime, float fadePerSecond)
        {
            float drop = deltaTime * fadePerSecond;
            for (int i = 0; i < _glow.Length; i++)
            {
                _glow[i] -= drop;
                if (_glow[i] < 0f) _glow[i] = 0f;
            }
        }

        public void Reset()
        {
            for (int i = 0; i < _glow.Length; i++) _glow[i] = 0f;
            LastNode = -1;
        }

        int Find(int a, int b)
        {
            for (int i = 0; i < _from.Length; i++)
            {
                if ((_from[i] == a && _to[i] == b) || (_from[i] == b && _to[i] == a)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Every distinct pair of consecutive nodes in a route, in the order first travelled.
        /// Direction doesn't matter, so the way out and the way back share a beam where they
        /// use the same two nodes. Nodes that can't be placed (-1) break the chain.
        /// </summary>
        public static void EdgesFromRoute(IList<int> route, List<int> from, List<int> to)
        {
            from.Clear();
            to.Clear();
            int prev = -1;
            for (int i = 0; i < route.Count; i++)
            {
                int node = route[i];
                if (node < 0) { prev = -1; continue; }
                if (prev >= 0 && prev != node)
                {
                    bool seen = false;
                    for (int e = 0; e < from.Count; e++)
                    {
                        if ((from[e] == prev && to[e] == node) || (from[e] == node && to[e] == prev)) { seen = true; break; }
                    }
                    if (!seen) { from.Add(prev); to.Add(node); }
                }
                prev = node;
            }
        }
    }
}
