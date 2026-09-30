using System;
using System.Reflection;
using NUnit.Framework;

public static class RunTests
{
    public static int Main(string[] args)
    {
        int pass = 0, fail = 0;
        foreach (var t in new[]{ typeof(OCS.VR.Tests.TraceLoaderTests), typeof(OCS.VR.Tests.CardLoadStateTests),
                                typeof(OCS.VR.Tests.AssemblyTimelineTests), typeof(OCS.VR.Tests.SystemRouteStateTests),
                                typeof(OCS.VR.Tests.SystemGraphTests), typeof(OCS.VR.Tests.NarrationTrackTests),
                                typeof(OCS.VR.Tests.CardVisualMathTests),
                                typeof(OCS.VR.Tests.BeamGlowStateTests), typeof(OCS.VR.Tests.TelemetryTextTests),
                                typeof(OCS.VR.Tests.TimelineLayoutTests), typeof(OCS.VR.Tests.StageMotionTests) })
        {
        Console.WriteLine("-- " + t.Name);
        var inst = Activator.CreateInstance(t);
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.GetCustomAttribute<TestAttribute>() == null) continue;
            try { m.Invoke(inst, null); Console.WriteLine("  PASS  " + m.Name); pass++; }
            catch (TargetInvocationException ex)
            { Console.WriteLine("  FAIL  " + m.Name + " :: " + ex.InnerException.Message); fail++; }
        }
        }
        Console.WriteLine($"\n{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
