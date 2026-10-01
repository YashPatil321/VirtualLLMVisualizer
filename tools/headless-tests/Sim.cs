using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using OCS.VR.Telemetry;
using OCS.VR.Rig;
using OCS.VR.Experience;

public static class Sim
{
    static void Call(object o, string m)
    {
        var mi = o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null) mi.Invoke(o, null);
    }

    public static int Main(string[] args)
    {
        string json = File.ReadAllText(args[0]);
        int problems = 0;

        // ---------- SIM 1: card lighting through the request ----------
        Console.WriteLine("=== SIM 1: which card is lit, frame by frame (72 fps) ===");
        var player = new TracePlayer { traceAsset = new TextAsset(json), playOnStart = true, timeScale = 0.5f };
        var layout = new RigLayout { cardCount = 8, cardSpacing = 0.075f,
                                     firstCardOffset = new Vector3(-0.2625f, 0.2155f, -0.045f), cardAxis = Vector3.right,
                                     tempMinC = 35f, tempMaxC = 85f };
        var cards = new Transform[8];
        for (int i = 0; i < 8; i++) cards[i] = new Transform();
        var disp = new RigCardDisplay { player = player, layout = layout, cards = cards, activeLift = 0.02f, lerpSpeed = 6f };

        Call(disp, "Awake"); Call(disp, "OnEnable"); Call(disp, "Start"); Call(player, "Start");

        Time.deltaTime = 1f / 72f;
        var samples = new List<(float ms, float load)>();
        for (int f = 0; f < 72 * 120; f++)
        {
            Call(player, "Update");
            Call(disp, "Update");
            samples.Add((player.ElapsedMs, disp.LoadOf(0)));   // GPU 0, first card of the replica
            if (!player.IsPlaying && f > 10) break;
        }

        float peakDuringGen = 0f, peakOverall = 0f;
        foreach (var s in samples)
        {
            if (s.load > peakOverall) peakOverall = s.load;
            if (s.ms >= 2300f && s.ms <= 9900f && s.load > peakDuringGen) peakDuringGen = s.load;
        }
        Console.WriteLine($"  peak load on GPU 0, whole trace      : {peakOverall:F3}");
        Console.WriteLine($"  peak load on GPU 0, DURING generation: {peakDuringGen:F3}   (2131-9965 ms, generating)");
        for (float probe = 0; probe <= 10400; probe += 800)
        {
            float best = 0f; foreach (var s in samples) if (Math.Abs(s.ms - probe) < 20f && s.load > best) best = s.load;
            Console.WriteLine($"    t={probe,5:F0}ms  GPU 0 load={best:F3}");
        }
        if (peakDuringGen < 0.5f)
        { Console.WriteLine("  >>> BUG: GPU 0 is DARK during the generation it is supposed to be lit for."); problems++; }

        // ---------- SIM 2: sequencer act arc ----------
        Console.WriteLine("\n=== SIM 2: five-act arc ===");
        MonoBehaviour.ResetScheduler();
        var p2 = new TracePlayer { traceAsset = new TextAsset(json), playOnStart = true, timeScale = 0.5f };
        var seq = new ExperienceSequencer { tracePlayer = p2, emptyBenchDuration = 20f, assemblyDuration = 90f,
                                            powerOnDuration = 15f, answerDuration = 20f, playOnStart = true, loop = false };
        var actLog = new List<(float t, Act a)>();
        seq.ActStarted += a => actLog.Add((MonoBehaviour.Now, a));

        Call(seq, "OnEnable"); Call(p2, "Start"); Call(seq, "Start");

        float dt = 1f / 72f;
        for (int f = 0; f < (int)(72 * 400); f++)
        {
            MonoBehaviour.Now += dt;
            Time.deltaTime = dt;
            Call(p2, "Update");
            MonoBehaviour.PumpCoroutines();
            if (seq.CurrentAct == Act.Complete) break;
        }
        foreach (var e in actLog) Console.WriteLine($"  t={e.t,7:F1}s  {e.a}");
        float reqStart = -1, ansStart = -1;
        foreach (var e in actLog) { if (e.a == Act.Request) reqStart = e.t; if (e.a == Act.Answer) ansStart = e.t; }
        float reqLen = (ansStart >= 0 && reqStart >= 0) ? ansStart - reqStart : -1;
        Console.WriteLine($"  Request act lasted {reqLen:F2}s (trace is ~6.4s at timeScale 0.5 + 1.5s tail)");
        if (reqLen >= 0 && reqLen < 1.0f)
        { Console.WriteLine("  >>> BUG: the Request act collapsed - the trace was already finished before Act 4 began."); problems++; }

        Console.WriteLine($"\n{problems} runtime problem(s) found.");
        return problems;
    }
}
