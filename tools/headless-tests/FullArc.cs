using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using OCS.VR.Telemetry;
using OCS.VR.Rig;
using OCS.VR.Experience;

public static class FullArc
{
    static void Call(object o, string m)
    {
        var mi = o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null) mi.Invoke(o, null);
    }

    public static int Main(string[] args)
    {
        string dataDir = args[0];
        int problems = 0;

        // ---- load the real .asset files into the real types ----
        var layout   = AssetLoader.Load<RigLayout>(Path.Combine(dataDir, "RigLayout.asset"));
        var sequence = AssetLoader.Load<AssemblySequence>(Path.Combine(dataDir, "AssemblySequence.asset"));
        var graph    = AssetLoader.Load<SystemGraph>(Path.Combine(dataDir, "SystemGraph.asset"));
        var track    = AssetLoader.Load<NarrationTrack>(Path.Combine(dataDir, "NarrationTrack.asset"));
        string json  = File.ReadAllText(Path.Combine(dataDir, "sample-trace.json"));

        Console.WriteLine("=== assets bound to their C# types ===");
        Console.WriteLine($"  RigLayout        {layout.cardCount} cards, spacing {layout.cardSpacing}, temp {layout.tempMinC}-{layout.tempMaxC}C");
        Console.WriteLine($"  AssemblySequence {sequence.StepCount} steps, {sequence.TotalDurationSeconds:F1}s");
        Console.WriteLine($"  SystemGraph      {graph.NodeCount} nodes");
        Console.WriteLine($"  NarrationTrack   {track.LineCount} lines, default hold {track.defaultHoldSeconds}s");

        if (AssetLoader.Unknown.Count > 0)
        {
            Console.WriteLine("  >>> FIELD NAME MISMATCHES (these would silently default in Unity):");
            foreach (var u in AssetLoader.Unknown) Console.WriteLine("      " + u);
            problems++;
        }
        else Console.WriteLine("  every key in every asset matched a real field.");

        string err;
        if (!sequence.Validate(out err)) { Console.WriteLine("  >>> sequence invalid: " + err); problems++; }
        if (!graph.Validate(out err))    { Console.WriteLine("  >>> graph invalid: " + err); problems++; }
        if (!track.Validate(out err))    { Console.WriteLine("  >>> track invalid: " + err); problems++; }

        // ---- build the whole experience ----
        MonoBehaviour.ResetScheduler();
        Time.time = 0f;

        var player = new TracePlayer { traceAsset = new TextAsset(json), playOnStart = true, timeScale = 0.5f };

        var cards = new Transform[8];
        for (int i = 0; i < 8; i++) cards[i] = new Transform();
        var rigDisplay = new RigCardDisplay { player = player, layout = layout, cards = cards, activeLift = 0.02f, lerpSpeed = 6f };

        var markers = new Transform[graph.NodeCount];
        for (int i = 0; i < markers.Length; i++) markers[i] = new Transform();
        var sysView = new SystemViewDisplay { player = player, graph = graph, nodeMarkers = markers, pulse = new Transform() };

        var bindings = new List<AssemblyPartBinding>();
        var parts = new Dictionary<string, Transform>();
        foreach (var s in sequence.steps)
        {
            var part = new Transform();
            parts[s.stepId] = part;
            bindings.Add(new AssemblyPartBinding { stepId = s.stepId, part = part, tray = new Transform(), socket = new Transform() });
        }
        var assembly = new AssemblyPlayer { sequence = sequence, layout = layout, bindings = bindings.ToArray(), playOnStart = true };

        var seq = new ExperienceSequencer
        {
            tracePlayer = player, assemblyPlayer = assembly,
            emptyBenchDuration = 20f, powerOnDuration = 15f, answerDuration = 20f,
            playOnStart = true, loop = false
        };
        var narrator = new NarrationDirector { track = track, sequencer = seq, assemblyPlayer = assembly, tracePlayer = player, narrateHops = true };

        foreach (var c in new object[] { rigDisplay, sysView, assembly, narrator, seq }) Call(c, "Awake");
        foreach (var c in new object[] { rigDisplay, sysView, narrator, seq }) Call(c, "OnEnable");

        var acts = new List<(float t, Act a)>();
        seq.ActStarted += a => acts.Add((MonoBehaviour.Now, a));
        var narrated = new List<(float t, string text)>();
        narrator.LineChanged += l => narrated.Add((MonoBehaviour.Now, l.text));
        var steps = new List<(float t, string id)>();
        assembly.StepStarted += (s, i) => steps.Add((MonoBehaviour.Now, s.stepId));

        foreach (var c in new object[] { player, assembly, rigDisplay, sysView, narrator, seq }) Call(c, "Start");

        // ---- run it at 72 fps ----
        float dt = 1f / 72f;
        float peakCard3 = 0f;
        int maxBusyNodes = 0;
        for (int f = 0; f < 72 * 600; f++)
        {
            MonoBehaviour.Now += dt; Time.time = MonoBehaviour.Now; Time.deltaTime = dt;
            Call(player, "Update"); Call(assembly, "Update");
            Call(rigDisplay, "Update"); Call(sysView, "Update"); Call(narrator, "Update");
            MonoBehaviour.PumpCoroutines();

            if (rigDisplay.LoadOf(3) > peakCard3) peakCard3 = rigDisplay.LoadOf(3);
            int busy = 0; for (int n = 0; n < graph.NodeCount; n++) if (sysView.GlowOf(n) > 0.5f) busy++;
            if (busy > maxBusyNodes) maxBusyNodes = busy;
            if (seq.CurrentAct == Act.Complete) break;
        }

        Console.WriteLine("\n=== the five act arc, end to end ===");
        foreach (var e in acts) Console.WriteLine($"  t={e.t,7:F1}s  {e.a}");

        float total = acts.Count > 0 ? MonoBehaviour.Now : 0f;
        Console.WriteLine($"\n  assembly steps played : {steps.Count} of {sequence.StepCount}");
        Console.WriteLine($"  narration lines shown : {narrated.Count}");
        Console.WriteLine($"  peak load on card 3   : {peakCard3:F2}");
        Console.WriteLine($"  total runtime         : {total:F0}s (budget {ExperienceSequencer.MaxRuntimeSeconds:F0}s)");

        if (steps.Count != sequence.StepCount) { Console.WriteLine("  >>> BUG: not every assembly step played."); problems++; }
        if (peakCard3 < 0.9f) { Console.WriteLine("  >>> BUG: card 3 never lit properly."); problems++; }
        if (seq.CurrentAct != Act.Complete) { Console.WriteLine("  >>> BUG: the arc never reached Complete."); problems++; }
        if (total > ExperienceSequencer.MaxRuntimeSeconds) { Console.WriteLine("  >>> OVER BUDGET: longer than five minutes."); problems++; }
        if (narrated.Count == 0) { Console.WriteLine("  >>> BUG: no narration fired."); problems++; }

        Console.WriteLine("\n  first lines the viewer hears:");
        for (int i = 0; i < Math.Min(6, narrated.Count); i++)
            Console.WriteLine($"    t={narrated[i].t,6:F1}s  \"{narrated[i].text}\"");

        Console.WriteLine($"\n{problems} problem(s).");
        return problems;
    }
}
