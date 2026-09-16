using NUnit.Framework;
using OCS.VR.Telemetry;

namespace OCS.VR.Tests
{
    /// <summary>
    /// Needs the Unity Test Framework package and an assembly definition referencing
    /// the OCS.VR scripts. If the Tests folder has no asmdef yet, create one and
    /// reference nunit.framework plus the runtime assembly.
    /// </summary>
    public class TraceLoaderTests
    {
        const string MinimalTrace = @"{
            ""trace_id"": ""t1"",
            ""hops"": [
                { ""hop"": ""client"", ""node_id"": ""web"", ""t_start_ms"": 0, ""t_end_ms"": 10, ""status"": ""ok"" },
                { ""hop"": ""rig"", ""node_id"": ""rig-2"", ""t_start_ms"": 10, ""t_end_ms"": 50, ""status"": ""ok"", ""gpu_index"": 3, ""gpu_util"": 94, ""temp_c"": 71 }
            ]
        }";

        [Test]
        public void ParsesValidTrace()
        {
            Trace trace;
            string error;

            Assert.IsTrue(TraceLoader.TryParse(MinimalTrace, out trace, out error), error);
            Assert.AreEqual("t1", trace.trace_id);
            Assert.AreEqual(2, trace.hops.Count);
            Assert.AreEqual(50f, trace.TotalDurationMs);
        }

        [Test]
        public void AbsentGpuIndexIsNotCardZero()
        {
            // The whole reason gpu_index defaults to -1. If this fails, the client hop
            // will light card 0 during the experience.
            Trace trace;
            string error;

            TraceLoader.TryParse(MinimalTrace, out trace, out error);
            Hop clientHop = trace.hops[0];

            Assert.IsFalse(clientHop.HasGpuTelemetry);
            Assert.AreEqual(-1, clientHop.gpu_index);
        }

        [Test]
        public void ReadsGpuTelemetryOnRigHops()
        {
            Trace trace;
            string error;

            TraceLoader.TryParse(MinimalTrace, out trace, out error);
            Hop rigHop = trace.hops[1];

            Assert.IsTrue(rigHop.HasGpuTelemetry);
            Assert.AreEqual(3, rigHop.gpu_index);
            Assert.AreEqual(HopType.Rig, rigHop.Type);
        }

        [Test]
        public void SortsOutOfOrderHops()
        {
            const string outOfOrder = @"{
                ""trace_id"": ""t2"",
                ""hops"": [
                    { ""hop"": ""broker"", ""node_id"": ""b"", ""t_start_ms"": 100, ""t_end_ms"": 120, ""status"": ""ok"" },
                    { ""hop"": ""client"", ""node_id"": ""web"", ""t_start_ms"": 0, ""t_end_ms"": 10, ""status"": ""ok"" }
                ]
            }";

            Trace trace;
            string error;

            Assert.IsTrue(TraceLoader.TryParse(outOfOrder, out trace, out error), error);
            Assert.AreEqual(HopType.Client, trace.hops[0].Type);
        }

        [Test]
        public void UnknownHopTypeIsDroppedNotThrown()
        {
            const string unknown = @"{
                ""trace_id"": ""t3"",
                ""hops"": [
                    { ""hop"": ""client"", ""node_id"": ""web"", ""t_start_ms"": 0, ""t_end_ms"": 10, ""status"": ""ok"" },
                    { ""hop"": ""quantum_relay"", ""node_id"": ""x"", ""t_start_ms"": 10, ""t_end_ms"": 20, ""status"": ""ok"" }
                ]
            }";

            Trace trace;
            string error;

            Assert.IsTrue(TraceLoader.TryParse(unknown, out trace, out error), error);
            Assert.AreEqual(1, TraceLoader.DrawableHops(trace).Count);
        }

        [Test]
        public void RejectsHopEndingBeforeItStarts()
        {
            const string backwards = @"{
                ""trace_id"": ""t4"",
                ""hops"": [
                    { ""hop"": ""client"", ""node_id"": ""web"", ""t_start_ms"": 50, ""t_end_ms"": 10, ""status"": ""ok"" }
                ]
            }";

            Trace trace;
            string error;

            Assert.IsFalse(TraceLoader.TryParse(backwards, out trace, out error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void RejectsEmptyTrace()
        {
            Trace trace;
            string error;

            Assert.IsFalse(TraceLoader.TryParse("{ \"trace_id\": \"t5\", \"hops\": [] }", out trace, out error));
        }
    }
}
