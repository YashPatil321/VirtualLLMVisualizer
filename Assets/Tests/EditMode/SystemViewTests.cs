using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OCS.VR.Experience;
using OCS.VR.Telemetry;

namespace OCS.VR.Tests
{
    public class SystemRouteStateTests
    {
        [Test]
        public void AdjacentHopsOnOneNodeDoNotBlinkItOff()
        {
            // Same rule as the rig cards. rig-2 carries both the rig hop and the model
            // hop, and they meet at 168ms, so last event wins would flicker the node.
            var s = new SystemRouteState(4);

            s.HopStarted(2);
            s.HopStarted(2);
            s.HopEnded(2);

            Assert.IsTrue(s.IsBusy(2), "still running the model hop");

            s.HopEnded(2);
            Assert.IsFalse(s.IsBusy(2));
        }

        [Test]
        public void MovingToANewNodeRestartsTheEdge()
        {
            var s = new SystemRouteState(4);

            s.HopStarted(0);
            Assert.AreEqual(1f, s.EdgeProgress01, "first node has no edge to travel");

            s.HopStarted(1);
            Assert.AreEqual(0, s.PreviousNode);
            Assert.AreEqual(1, s.CurrentNode);
            Assert.AreEqual(0f, s.EdgeProgress01);
        }

        [Test]
        public void EdgeProgressReachesOneAndStops()
        {
            var s = new SystemRouteState(4);
            s.HopStarted(0);
            s.HopStarted(1);

            for (int f = 0; f < 200; f++) s.Step(1f / 72f, 4f, 8f);

            Assert.AreEqual(1f, s.EdgeProgress01);
        }

        [Test]
        public void GlowRisesOnBusyNodesAndFallsOnIdleOnes()
        {
            var s = new SystemRouteState(4);
            s.HopStarted(1);
            for (int f = 0; f < 72; f++) s.Step(1f / 72f, 4f, 8f);
            Assert.IsTrue(s.Glow(1) > 0.9f);

            s.HopEnded(1);
            for (int f = 0; f < 72; f++) s.Step(1f / 72f, 4f, 8f);
            Assert.IsTrue(s.Glow(1) < 0.1f);
        }

        [Test]
        public void OutOfRangeNodesAreIgnored()
        {
            var s = new SystemRouteState(2);
            s.HopStarted(9);
            s.HopEnded(-1);

            Assert.AreEqual(-1, s.CurrentNode);
            Assert.AreEqual(0f, s.Glow(9));
        }

        [Test]
        public void ResetAllClearsTheRoute()
        {
            var s = new SystemRouteState(3);
            s.HopStarted(0);
            s.HopStarted(1);
            s.ResetAll();

            Assert.AreEqual(-1, s.CurrentNode);
            Assert.AreEqual(-1, s.PreviousNode);
            Assert.IsFalse(s.IsBusy(0));
        }
    }

    public class SystemGraphTests
    {
        static SystemGraph MakeGraph()
        {
            var g = ScriptableObject.CreateInstance<SystemGraph>();
            g.nodes = new List<SystemNode>
            {
                new SystemNode { nodeId = "web-client",    displayName = "Client",    hopType = HopType.Client },
                new SystemNode { nodeId = "ocs-broker",    displayName = "Broker",    hopType = HopType.Broker },
                new SystemNode { nodeId = "gpu-scheduler", displayName = "Scheduler", hopType = HopType.Scheduler },
                new SystemNode { nodeId = "rig-2",         displayName = "Rig 2",     hopType = HopType.Rig }
            };
            return g;
        }

        [Test]
        public void ResolvesByNodeIdFirst()
        {
            var g = MakeGraph();
            var hop = new Hop { hop = "rig", node_id = "rig-2" };

            Assert.AreEqual("Rig 2", g.Resolve(hop).DisplayLabel);
        }

        [Test]
        public void FallsBackToHopTypeForAnUnknownNodeId()
        {
            // A rig we have not placed yet should still land on a rig box rather than
            // vanishing from the system view.
            var g = MakeGraph();
            var hop = new Hop { hop = "rig", node_id = "rig-9" };

            Assert.AreEqual("Rig 2", g.Resolve(hop).DisplayLabel);
        }

        [Test]
        public void AnUnknownHopTypeResolvesToNothing()
        {
            // The contract promises Anvay that a new hop type never breaks the build.
            var g = MakeGraph();
            var hop = new Hop { hop = "quantum_relay", node_id = "x" };

            Assert.IsNull(g.Resolve(hop));
        }

        [Test]
        public void ValidateRejectsDuplicateNodeIds()
        {
            var g = MakeGraph();
            g.nodes[1].nodeId = "web-client";

            string error;
            Assert.IsFalse(g.Validate(out error));
        }

        [Test]
        public void EveryHopInTheSampleTraceResolves()
        {
            // The system view must have somewhere to put every hop the shipped trace
            // contains, or Act 4 has gaps in it.
            var g = MakeGraph();
            string[] nodeIds = { "web-client", "ocs-broker", "gpu-scheduler", "rig-2", "rig-2", "ocs-broker", "web-client" };
            string[] hopTypes = { "client", "broker", "scheduler", "rig", "model", "broker", "client" };

            for (int i = 0; i < nodeIds.Length; i++)
            {
                var hop = new Hop { hop = hopTypes[i], node_id = nodeIds[i] };
                Assert.IsNotNull(g.Resolve(hop), $"no node for {hopTypes[i]} on {nodeIds[i]}");
            }
        }
    }

    public class NarrationTrackTests
    {
        static NarrationTrack MakeTrack()
        {
            var t = ScriptableObject.CreateInstance<NarrationTrack>();
            t.defaultHoldSeconds = 4f;
            t.lines = new List<NarrationLine>
            {
                new NarrationLine { cue = "act.Assembly", text = "Eight cards, one at a time." },
                new NarrationLine { cue = "step.gpu-0",   text = "First card in.", holdSeconds = 2f }
            };
            return t;
        }

        [Test]
        public void FindsALineByCue()
        {
            Assert.AreEqual("Eight cards, one at a time.", MakeTrack().Find("act.Assembly").text);
        }

        [Test]
        public void AnUnauthoredCueReturnsNothingRatherThanThrowing()
        {
            Assert.IsNull(MakeTrack().Find("act.Nonexistent"));
        }

        [Test]
        public void HoldFallsBackToTheTrackDefault()
        {
            var t = MakeTrack();
            Assert.AreEqual(4f, t.HoldFor(t.Find("act.Assembly")));
            Assert.AreEqual(2f, t.HoldFor(t.Find("step.gpu-0")));
        }

        [Test]
        public void CueHelpersMatchTheAuthoredFormat()
        {
            Assert.AreEqual("act.Assembly", NarrationTrack.ActCue(Act.Assembly));
            Assert.AreEqual("step.gpu-0", NarrationTrack.StepCue("gpu-0"));
            Assert.AreEqual("hop.Model", NarrationTrack.HopCue(HopType.Model));
        }

        [Test]
        public void ValidateRejectsDuplicateCues()
        {
            var t = MakeTrack();
            t.lines[1].cue = "act.Assembly";

            string error;
            Assert.IsFalse(t.Validate(out error));
        }
    }
}
