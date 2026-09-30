using System.Collections.Generic;
using NUnit.Framework;
using OCS.VR.Experience;
using OCS.VR.Telemetry;

namespace OCS.VR.Tests
{
    public class BeamGlowStateTests
    {
        [Test]
        public void TheSampleRouteGivesFourBeams()
        {
            // client 0, broker 1, scheduler 2, rig-2 3. Out: 0 1 2 3, model stays on 3,
            // back: 1 0. The way back skips the scheduler, so rig to broker is its own beam.
            var route = new List<int> { 0, 1, 2, 3, 3, 1, 0 };
            var from = new List<int>();
            var to = new List<int>();
            BeamGlowState.EdgesFromRoute(route, from, to);

            Assert.AreEqual(4, from.Count);
            Assert.AreEqual(3, from[3]);
            Assert.AreEqual(1, to[3]);
        }

        [Test]
        public void MovingBetweenNodesLightsTheBeamBetweenThem()
        {
            var s = new BeamGlowState(new[] { 0, 1 }, new[] { 1, 2 });
            Assert.AreEqual(-1, s.NodeEntered(0), "the first node has no beam behind it");
            Assert.AreEqual(0, s.NodeEntered(1));
            Assert.AreEqual(1f, s.Glow(0));
            Assert.AreEqual(0f, s.Glow(1));
        }

        [Test]
        public void TheWayBackLightsTheSameBeam()
        {
            var s = new BeamGlowState(new[] { 0 }, new[] { 1 });
            s.NodeEntered(1);
            Assert.AreEqual(0, s.NodeEntered(0), "direction shouldn't matter");
        }

        [Test]
        public void StayingOnANodeLightsNothing()
        {
            // The rig hop and the model hop are both on rig-2.
            var s = new BeamGlowState(new[] { 0 }, new[] { 1 });
            s.NodeEntered(1);
            s.Step(10f, 1f);
            Assert.AreEqual(-1, s.NodeEntered(1));
            Assert.AreEqual(0f, s.Glow(0));
        }

        [Test]
        public void GlowFadesAndStopsAtZero()
        {
            var s = new BeamGlowState(new[] { 0 }, new[] { 1 });
            s.NodeEntered(0);
            s.NodeEntered(1);
            s.Step(0.25f, 2f);
            Assert.AreEqual(0.5f, s.Glow(0));
            s.Step(10f, 2f);
            Assert.AreEqual(0f, s.Glow(0));
        }

        [Test]
        public void UnplacedNodesBreakTheChain()
        {
            var from = new List<int>();
            var to = new List<int>();
            BeamGlowState.EdgesFromRoute(new List<int> { 0, -1, 2 }, from, to);
            Assert.AreEqual(0, from.Count, "no beam should jump over a node that isn't drawn");
        }
    }

    public class TelemetryTextTests
    {
        static Hop RigHop() => new Hop
        {
            hop = "rig", node_id = "rig-2", gpu_index = 3, gpu_util = 94f, temp_c = 71f, vram_used_mb = 7100f,
            t_start_ms = 55, t_end_ms = 82, label = "Job accepted"
        };

        [Test]
        public void CardHeaderNamesTheGpuAndRig()
        {
            Assert.AreEqual("GPU 3  ·  rig-2", TelemetryText.CardHeader(RigHop()));
        }

        [Test]
        public void CardStatsShowLoadTemperatureAndMemory()
        {
            Assert.AreEqual("94% load  ·  71°C  ·  7.1 GB VRAM", TelemetryText.CardStats(RigHop()));
        }

        [Test]
        public void CardStatsLeaveOutWhatTheHopDoesNotCarry()
        {
            var modelHop = new Hop { hop = "model", gpu_index = 3, tokens_out = 186 };
            Assert.AreEqual("", TelemetryText.CardStats(modelHop));
        }

        [Test]
        public void GeneratingLineCountsTokens()
        {
            Assert.AreEqual("Generating  ·  42 tokens  ·  60.5 tok/s", TelemetryText.Generating(42, 60.5f));
            Assert.AreEqual("Generating  ·  1 token", TelemetryText.Generating(1, 0f));
        }

        [Test]
        public void DurationSwitchesToSecondsAboveOneSecond()
        {
            Assert.AreEqual("26 ms", TelemetryText.Duration(26f));
            Assert.AreEqual("3.07 s", TelemetryText.Duration(3072f));
        }

        [Test]
        public void HopCaptionIsLabelThenDuration()
        {
            Assert.AreEqual("Job accepted\n27 ms", TelemetryText.HopCaption(RigHop()));
        }

        [Test]
        public void TokensClimbAcrossTheModelHop()
        {
            Assert.AreEqual(0, TelemetryText.TokensAt(50f, 82f, 3154f, 186));
            Assert.AreEqual(93, TelemetryText.TokensAt(1618f, 82f, 3154f, 186));
            Assert.AreEqual(186, TelemetryText.TokensAt(4000f, 82f, 3154f, 186));
        }
    }

    public class TimelineLayoutTests
    {
        [Test]
        public void WidthsFillTheBarAndRespectTheMinimum()
        {
            // The sample trace: six short hops and one 3 second generation.
            float[] durations = { 12, 26, 17, 27, 3072, 19, 7 };
            float[] w = TimelineLayout.Widths(durations, 2.4f, 0.06f);

            float sum = 0f;
            foreach (float x in w) { sum += x; Assert.IsTrue(x >= 0.06f - 1e-4f); }
            Assert.AreEqual(2.4f, sum);
            Assert.IsTrue(w[4] > 2.0f, "generation should dominate the bar");
        }

        [Test]
        public void OffsetsAreRunningTotals()
        {
            float[] o = TimelineLayout.Offsets(new[] { 0.5f, 1f, 0.25f });
            Assert.AreEqual(0f, o[0]);
            Assert.AreEqual(0.5f, o[1]);
            Assert.AreEqual(1.5f, o[2]);
        }

        [Test]
        public void TooManySegmentsShareTheBarEvenly()
        {
            float[] w = TimelineLayout.Widths(new float[] { 1, 1000, 1 }, 0.1f, 0.05f);
            Assert.AreEqual(0.1f / 3f, w[1]);
        }
    }

    public class StageMotionTests
    {
        [Test]
        public void NothingMovesBeforeItsAct()
        {
            Assert.AreEqual(0f, StageMotion.Progress(Act.Assembly, 50f, Act.PowerOn, 0f, 1f));
            Assert.AreEqual(0f, StageMotion.Progress(Act.None, 0f, Act.PowerOn, 0f, 1f));
        }

        [Test]
        public void EverythingHasMovedAfterItsAct()
        {
            // Even if its act was skipped or cut short, a later act finds it done.
            Assert.AreEqual(1f, StageMotion.Progress(Act.Answer, 0f, Act.PowerOn, 5f, 1f));
        }

        [Test]
        public void DuringItsActItWaitsForTheDelayThenEasesIn()
        {
            Assert.AreEqual(0f, StageMotion.Progress(Act.PowerOn, 0.9f, Act.PowerOn, 1f, 2f));
            Assert.AreEqual(0.5f, StageMotion.Progress(Act.PowerOn, 2f, Act.PowerOn, 1f, 2f), 1e-5f);
            Assert.AreEqual(1f, StageMotion.Progress(Act.PowerOn, 3.5f, Act.PowerOn, 1f, 2f));
        }

        [Test]
        public void EasingStartsAndEndsGently()
        {
            Assert.Less(StageMotion.Ease(0.1f), 0.1f);
            Assert.Greater(StageMotion.Ease(0.9f), 0.9f);
        }

        [Test]
        public void ZeroDurationSnaps()
        {
            Assert.AreEqual(0f, StageMotion.Progress(Act.Request, 0.4f, Act.Request, 0.5f, 0f));
            Assert.AreEqual(1f, StageMotion.Progress(Act.Request, 0.5f, Act.Request, 0.5f, 0f));
        }
    }
}
