using NUnit.Framework;
using UnityEngine;
using OCS.VR.Experience;

namespace OCS.VR.Tests
{
    public class FlightPathTests
    {
        [Test]
        public void StartsAtTheTrayAndEndsInTheSocket()
        {
            var from = new Vector3(-1f, 0.9f, 0.8f);
            var to = new Vector3(0.2f, 1.1f, 0.7f);
            Assert.IsTrue(FlightPath.Position(from, to, 0f, 0.4f) == from);
            Assert.IsTrue(FlightPath.Position(from, to, 1f, 0.4f) == to);
        }

        [Test]
        public void ItLiftsOverTheBenchOnTheWay()
        {
            var from = new Vector3(-1f, 0.9f, 0f);
            var to = new Vector3(0f, 0.9f, 0f);
            Vector3 mid = FlightPath.Position(from, to, 0.5f, 0.4f);
            Assert.AreEqual(0.9f + 0.3f, mid.y, 1e-4f);   // three quarters of the arc height
            Assert.AreEqual(-0.5f, mid.x, 1e-4f);
        }

        [Test]
        public void AFrontEntryArrivesLevel()
        {
            var from = new Vector3(-1.2f, 0.95f, 0.8f);
            var to = new Vector3(0.27f, 0.96f, 0.72f);
            var lift = new Vector3(0f, 0.5f, 0f);
            var front = new Vector3(0f, 0f, -0.5f);
            Assert.IsTrue(FlightPath.Via(from, to, 1f, lift, front) == to);
            // Just before arriving it is level with the socket and in front of it.
            Vector3 late = FlightPath.Via(from, to, 0.97f, lift, front);
            Assert.AreEqual(to.y, late.y, 0.01f);
            Assert.Less(late.z, to.z);
        }

        [Test]
        public void TheLandingDipIsSmall()
        {
            // At 0.5 the ease runs about 0.8 % past the end: a centimetre on a 40 cm arc.
            float peak = 0f;
            for (float t = 0f; t <= 1f; t += 0.001f) peak = Mathf.Max(peak, FlightPath.EaseOutBack(t, 0.5f));
            Assert.Less(peak, 1.01f);
            Assert.Greater(peak, 1.005f);
        }

        [Test]
        public void LongerFlightsArcHigher()
        {
            Assert.AreEqual(0.15f, FlightPath.ArcFor(0.1f));
            Assert.Greater(FlightPath.ArcFor(2f), FlightPath.ArcFor(1f));
        }

        [Test]
        public void TheEaseOvershootsThenLandsExactly()
        {
            bool over = false;
            for (float t = 0.5f; t < 1f; t += 0.01f) if (FlightPath.EaseOutBack(t, 0.8f) > 1f) over = true;
            Assert.IsTrue(over, "should run a little past the socket before settling");
            Assert.AreEqual(1f, FlightPath.EaseOutBack(1f, 0.8f));
            Assert.AreEqual(0f, FlightPath.EaseOutBack(0f, 0.8f));
        }

        [Test]
        public void TheSpinIsGoneOnArrival()
        {
            Assert.AreEqual(180f, FlightPath.SpinAt(0f, 180f));
            Assert.AreEqual(0f, FlightPath.SpinAt(1f, 180f));
            Assert.Less(FlightPath.SpinAt(0.7f, 180f), 10f);
        }
    }

    public class WorldPulseTests
    {
        [Test]
        public void TheHallIdlesBeforePowerOn()
        {
            float r, s, wake;
            WorldPulse.Evaluate(Act.Assembly, 30f, out r, out s, out wake);
            Assert.AreEqual(0f, s);
            Assert.AreEqual(0f, wake);
        }

        [Test]
        public void TheWaveWaitsForTheCardsThenRunsOut()
        {
            float r, s, wake;
            WorldPulse.Evaluate(Act.PowerOn, WorldPulse.WaveDelay * 0.5f, out r, out s, out wake);
            Assert.AreEqual(0f, r);
            WorldPulse.Evaluate(Act.PowerOn, WorldPulse.WaveDelay + 1f, out r, out s, out wake);
            Assert.AreEqual(WorldPulse.WaveSpeed, r, 1e-4f);
            Assert.Greater(s, 0f);
            Assert.AreEqual(r, wake, 1e-4f);
        }

        [Test]
        public void TheWaveFadesAsItGoes()
        {
            float r1, s1, r2, s2, w;
            WorldPulse.Evaluate(Act.PowerOn, WorldPulse.WaveDelay + 0.5f, out r1, out s1, out w);
            WorldPulse.Evaluate(Act.PowerOn, WorldPulse.WaveDelay + 2f, out r2, out s2, out w);
            Assert.Greater(s1, s2);
        }

        [Test]
        public void EverythingIsAwakeAfterPowerOn()
        {
            float r, s, wake;
            WorldPulse.Evaluate(Act.Request, 0f, out r, out s, out wake);
            Assert.AreEqual(WorldPulse.AwakeRadius, wake);
            Assert.AreEqual(0f, s);
        }
    }

    public class AnswerTextTests
    {
        [Test]
        public void WrapsOnWordBoundaries()
        {
            Assert.AreEqual("one two\nthree", AnswerText.Wrap("one two three", 8));
        }

        [Test]
        public void OneFramePerWord()
        {
            string[] f = AnswerText.Frames("a b c", 40, 4);
            Assert.AreEqual(4, f.Length);
            Assert.AreEqual("", f[0]);
            Assert.AreEqual("a b", f[2]);
            Assert.AreEqual("a b c", f[3]);
        }

        [Test]
        public void OldLinesScrollOffTheTop()
        {
            string[] f = AnswerText.Frames("aaaa bbbb cccc dddd", 4, 2);
            Assert.AreEqual("cccc\ndddd", f[4]);
        }

        [Test]
        public void WordsArriveEvenlyAcrossTheHop()
        {
            Assert.AreEqual(0, AnswerText.WordsAt(50f, 100f, 200f, 10));
            Assert.AreEqual(6, AnswerText.WordsAt(150f, 100f, 200f, 10));
            Assert.AreEqual(10, AnswerText.WordsAt(250f, 100f, 200f, 10));
        }
    }

    public class JourneyTextTests
    {
        static System.Collections.Generic.List<OCS.VR.Telemetry.Hop> Hops()
        {
            return new System.Collections.Generic.List<OCS.VR.Telemetry.Hop>
            {
                new OCS.VR.Telemetry.Hop { hop = "client", t_start_ms = 0, t_end_ms = 10, label = "Sent" },
                new OCS.VR.Telemetry.Hop { hop = "model", t_start_ms = 10, t_end_ms = 1010, label = "Generating", gpu_index = 3 },
            };
        }

        [Test]
        public void OneStatePerHopPlusBeforeAndAfter()
        {
            string[] f = JourneyText.Frames(Hops(), new[] { "Client", "Rig 2" });
            Assert.AreEqual(4, f.Length);
            Assert.IsTrue(f[0].Contains("1  Client  ·  Sent  ·  10 ms"), f[0]);
        }

        [Test]
        public void TheRunningHopIsLitAndTheGpuInAmber()
        {
            string[] f = JourneyText.Frames(Hops(), new[] { "Client", "Rig 2" });
            Assert.IsTrue(f[1].StartsWith("<color=" + JourneyText.Active + ">1"), f[1]);
            Assert.IsTrue(f[2].Contains("<color=" + JourneyText.Gpu + ">2"), f[2]);
            Assert.IsTrue(f[2].StartsWith("<color=" + JourneyText.Done + ">1"), f[2]);
        }

        [Test]
        public void TheLastStateGivesTheTotalAndTheGpuShare()
        {
            string[] f = JourneyText.Frames(Hops(), new[] { "Client", "Rig 2" });
            Assert.IsTrue(f[3].Contains("Total 1.01 s"), f[3]);
            Assert.IsTrue(f[3].Contains("99.0%"), f[3]);
        }

        [Test]
        public void TheResponseHeadingNamesTheCardAndModel()
        {
            var trace = new OCS.VR.Telemetry.Trace();
            trace.hops.Add(new OCS.VR.Telemetry.Hop { hop = "rig", gpu_index = 3, model = "m-7b", quantization = "Q4_K_M" });
            Assert.AreEqual("RESPONSE  ·  GPU 3  ·  m-7b, Q4_K_M", AnswerText.ResponseHeading(trace));
        }
    }
}
