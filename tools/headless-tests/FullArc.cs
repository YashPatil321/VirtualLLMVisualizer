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
        var facts    = AssetLoader.Load<InfoBoard>(Path.Combine(dataDir, "RigFacts.asset"));
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

        Console.WriteLine($"  RigFacts         \"{facts.title}\", {(facts.lines != null ? facts.lines.Length : 0)} lines");
        if (facts.lines == null || facts.lines.Length == 0) { Console.WriteLine("  >>> RigFacts has no lines."); problems++; }

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

        // Fans and LEDs, wired the way ExperienceSceneBuilder wires them.
        var visuals = new CardVisual[8];
        var fanT = new Transform[8];
        var ledR = new Renderer[8];
        for (int i = 0; i < 8; i++)
        {
            fanT[i] = new Transform();
            ledR[i] = new Renderer();
            visuals[i] = new CardVisual { fans = new[] { fanT[i] }, leds = new[] { ledR[i] } };
        }
        rigDisplay.visuals = visuals;
        var power = new RigPower { sequencer = seq, cards = visuals };

        // Beams, captions, the card readout and the timeline, wired as the builder does.
        Trace parsedTrace; string perr;
        TraceLoader.TryParse(json, out parsedTrace, out perr);
        var drawable = TraceLoader.DrawableHops(parsedTrace);
        var route = new List<int>();
        foreach (var h in drawable) { var n = graph.Resolve(h); route.Add(n == null ? -1 : graph.IndexOf(n)); }
        var ef = new List<int>(); var et = new List<int>();
        BeamGlowState.EdgesFromRoute(route, ef, et);
        var beamLines = new LineRenderer[ef.Count];
        for (int i = 0; i < beamLines.Length; i++) beamLines[i] = new LineRenderer();
        var cardBeam = new LineRenderer { enabled = false };
        var beams = new SystemBeams { player = player, graph = graph, view = sysView, rig = rigDisplay,
                                      edgeFrom = ef.ToArray(), edgeTo = et.ToArray(), lines = beamLines, cardBeam = cardBeam };
        var captions = new TextMesh[graph.NodeCount];
        for (int i = 0; i < captions.Length; i++) captions[i] = new TextMesh();
        var hopCaptions = new HopCaptions { player = player, graph = graph, captions = captions };
        var readoutText = new TextMesh();
        var readout = new CardTelemetryLabel { player = player, rig = rigDisplay, text = readoutText };
        var durations = new float[drawable.Count];
        for (int i = 0; i < durations.Length; i++) durations[i] = drawable[i].DurationMs;
        float[] segW = TimelineLayout.Widths(durations, 2.4f, 0.05f);
        var segs = new Renderer[drawable.Count];
        for (int i = 0; i < segs.Length; i++) segs[i] = new Renderer();
        var timeline = new RequestTimeline { player = player, segments = segs, segmentLeft = TimelineLayout.Offsets(segW),
                                             segmentWidth = segW, playhead = new Transform() };
        int modelSeg = drawable.FindIndex(h => h.Type == HopType.Model);
        // The stage cues, as the builder sets them.
        var trayT = new Transform(); var standT = new Transform(); var viewT = new Transform(); var timelineT = new Transform();
        var stage = new StageDirector { sequencer = seq, cues = new[]
        {
            new StageCue { target = trayT, act = Act.PowerOn, move = StageMove.Sink, delay = 0.3f, duration = 2.5f, depth = 1f },
            new StageCue { target = standT, act = Act.PowerOn, move = StageMove.Sink, delay = 0.6f, duration = 2.5f, depth = 1f },
            new StageCue { target = viewT, act = Act.PowerOn, move = StageMove.Appear, delay = 1.8f, duration = 1.4f },
            new StageCue { target = timelineT, act = Act.Request, move = StageMove.Appear, delay = 0f, duration = 0.6f },
        } };
        bool viewShownDuringAssembly = false, timelineShownBeforeRequest = false, trayShownAtRequest = false;
        float viewScaleAtRequest = -1f, timelineScaleLate = 0f;
        // The hall reacting, bursts, the token stream and the answer panel, as the builder wires them.
        var pulseDriver = new WorldPulseDriver { sequencer = seq, player = player, centre = new Transform() };
        var ringRs = new Renderer[4];
        for (int i = 0; i < ringRs.Length; i++) ringRs[i] = new Renderer();
        var bursts = new ImpactBursts { assembly = assembly, sequencer = seq, player = player, rig = rigDisplay,
                                        view = sysView, graph = graph, rings = ringRs, sparks = new ParticleSystem() };
        var tokensPs = new ParticleSystem();
        var tokenStream = new TokenStream { player = player, rig = rigDisplay, tokens = tokensPs, target = new Transform() };
        var promptText = new TextMesh(); var responseText = new TextMesh();
        var answerPanel = new AnswerPanel { player = player, prompt = promptText, response = responseText, responseHeading = new TextMesh() };
        int wakeId = Shader.PropertyToID("_OCSWake"), waveId = Shader.PropertyToID("_OCSWaveStrength");
        float wakeAtRequest = -1f, peakWave = 0f, peakRipple = 0f, peakTokenRate = 0f, tokenRateAfter = -1f;
        string answerMid = null;
        var journeyText = new TextMesh();
        var journey = new JourneyBoard { player = player, graph = graph, text = journeyText };
        string journeyDuringModel = null;
        var extras = new object[] { beams, hopCaptions, readout, timeline, stage, pulseDriver, bursts, tokenStream, answerPanel, journey };
        int emissionId = Shader.PropertyToID("_EmissionColor");
        System.Func<int, float> glowOf = i =>
        {
            Color c;
            if (ledR[i].LastBlock == null || !ledR[i].LastBlock.Colors.TryGetValue(emissionId, out c)) return 0f;
            // Idle and hot colours share their green value, so green tracks glow alone.
            return c.g / visuals[i].idleColor.g;
        };

        foreach (var c in new object[] { rigDisplay, sysView, assembly, narrator, seq, power }) Call(c, "Awake");
        foreach (var c in extras) Call(c, "Awake");
        foreach (var c in extras) Call(c, "OnEnable");
        foreach (var v in visuals) Call(v, "Awake");
        foreach (var c in new object[] { rigDisplay, sysView, narrator, seq, power }) Call(c, "OnEnable");

        var acts = new List<(float t, Act a)>();
        seq.ActStarted += a => acts.Add((MonoBehaviour.Now, a));
        var narrated = new List<(float t, string text)>();
        narrator.LineChanged += l => narrated.Add((MonoBehaviour.Now, l.text));
        var steps = new List<(float t, string id)>();
        assembly.StepStarted += (s, i) => steps.Add((MonoBehaviour.Now, s.stepId));

        foreach (var c in new object[] { player, assembly, rigDisplay, sysView, narrator, seq, power }) Call(c, "Start");
        foreach (var c in extras) Call(c, "Start");
        var beamPeak = new float[beamLines.Length];
        float cardBeamPeak = 0f, playheadMax = 0f, modelSegPeakRed = 0f;
        bool sawStats = false, sawTokensDone = false; string midReadout = null;
        float maxGlowBeforePower = 0f, fanDegreesBeforePower = 0f;
        float peakGlowCard3 = 0f, peakGlowOthersDuringRequest = 0f;
        float minIdleGlowAfterPower = 1f;
        float peakHeatCard3 = 0f, peakHeatOthers = 0f;
        System.Func<int, float> heatOf = i =>
        {
            Color c;
            if (ledR[i].LastBlock == null || !ledR[i].LastBlock.Colors.TryGetValue(emissionId, out c) || c.g <= 0f) return 0f;
            // Red over green rises from idle (0.2/0.9) to hot (2.4/0.9) as the card heats.
            float idle = visuals[i].idleColor.r / visuals[i].idleColor.g, hot = visuals[i].hotColor.r / visuals[i].hotColor.g;
            return (c.r / c.g - idle) / (hot - idle);
        };

        // ---- run it at 72 fps ----
        float dt = 1f / 72f;
        float peakCard3 = 0f;
        int maxBusyNodes = 0;
        for (int f = 0; f < 72 * 600; f++)
        {
            MonoBehaviour.Now += dt; Time.time = MonoBehaviour.Now; Time.deltaTime = dt;
            Call(player, "Update"); Call(assembly, "Update");
            Call(rigDisplay, "Update"); Call(sysView, "Update"); Call(narrator, "Update");
            foreach (var v in visuals) Call(v, "Update");
            Call(beams, "Update"); Call(timeline, "Update"); Call(readout, "LateUpdate"); Call(stage, "Update");
            Call(pulseDriver, "Update"); Call(bursts, "Update"); Call(answerPanel, "Update");
            MonoBehaviour.PumpCoroutines();

            for (int b = 0; b < beamLines.Length; b++) if (beamLines[b].widthMultiplier > beamPeak[b]) beamPeak[b] = beamLines[b].widthMultiplier;
            if (cardBeam.enabled && cardBeam.widthMultiplier > cardBeamPeak) cardBeamPeak = cardBeam.widthMultiplier;
            if (timeline.playhead.localPosition.x > playheadMax) playheadMax = timeline.playhead.localPosition.x;
            Color sc;
            if (segs[modelSeg].LastBlock != null && segs[modelSeg].LastBlock.Colors.TryGetValue(Shader.PropertyToID("_EmissionColor"), out sc) && sc.r > modelSegPeakRed) modelSegPeakRed = sc.r;
            string rt = readoutText.text ?? "";
            if (rt.Contains("94% load")) sawStats = true;
            if (rt.Contains("186 tokens")) sawTokensDone = true;
            if (midReadout == null && rt.Contains("Generating") && !rt.Contains(" 0 tokens") && !rt.Contains("186 tokens")) midReadout = rt;

            Act now = seq.CurrentAct;
            float g;
            if (now == Act.PowerOn && Shader.GlobalFloats.TryGetValue(waveId, out g) && g > peakWave) peakWave = g;
            if (now == Act.Request && wakeAtRequest < 0f) wakeAtRequest = Shader.GlobalFloats.TryGetValue(wakeId, out g) ? g : 0f;
            if (pulseDriver.Ripple > peakRipple) peakRipple = pulseDriver.Ripple;
            if (tokenStream.Rate > peakTokenRate) peakTokenRate = tokenStream.Rate;
            if (now == Act.Answer && tokenRateAfter < 0f) tokenRateAfter = tokenStream.Rate;
            string shown = answerPanel.Shown ?? "";
            if (answerMid == null && shown.Length > 40) answerMid = shown;
            if (journeyDuringModel == null && tokenStream.Rate > 0f) journeyDuringModel = journey.Shown;
            if ((now == Act.EmptyBench || now == Act.Assembly) && viewT.gameObject.activeSelf) viewShownDuringAssembly = true;
            if (now < Act.Request && timelineT.gameObject.activeSelf) timelineShownBeforeRequest = true;
            if (now == Act.Request && viewScaleAtRequest < 0f)
            {
                viewScaleAtRequest = viewT.gameObject.activeSelf ? viewT.localScale.x : 0f;
                trayShownAtRequest = trayT.gameObject.activeSelf || standT.gameObject.activeSelf;
            }
            if (now == Act.Answer && timelineT.gameObject.activeSelf) timelineScaleLate = timelineT.localScale.x;
            if (now == Act.EmptyBench || now == Act.Assembly)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (glowOf(i) > maxGlowBeforePower) maxGlowBeforePower = glowOf(i);
                    if (fanT[i].RotatedDegrees > fanDegreesBeforePower) fanDegreesBeforePower = fanT[i].RotatedDegrees;
                }
            }
            if (now == Act.Request)
            {
                if (glowOf(3) > peakGlowCard3) peakGlowCard3 = glowOf(3);
                if (heatOf(3) > peakHeatCard3) peakHeatCard3 = heatOf(3);
                for (int i = 0; i < 8; i++) if (i != 3 && heatOf(i) > peakHeatOthers) peakHeatOthers = heatOf(i);
                for (int i = 0; i < 8; i++)
                {
                    if (i == 3) continue;
                    if (glowOf(i) > peakGlowOthersDuringRequest) peakGlowOthersDuringRequest = glowOf(i);
                    if (glowOf(i) < minIdleGlowAfterPower) minIdleGlowAfterPower = glowOf(i);
                }
            }

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

        Console.WriteLine($"  glow before power on  : {maxGlowBeforePower:F2}   fan degrees before power on: {fanDegreesBeforePower:F0}");
        Console.WriteLine($"  during the request    : card 3 peaks at {peakGlowCard3:F2}, other cards at most {peakGlowOthersDuringRequest:F2} (idle glow {minIdleGlowAfterPower:F2})");
        Console.WriteLine($"  colour heat           : card 3 reaches {peakHeatCard3:F2} (amber is 1), other cards at most {peakHeatOthers:F2}");
        if (peakHeatCard3 < 0.95f) { Console.WriteLine("  >>> BUG: the working card doesn't shift to its hot colour."); problems++; }
        if (peakHeatOthers > 0.05f) { Console.WriteLine("  >>> BUG: an idle card shifts colour."); problems++; }
        int lit = 0; foreach (float w in beamPeak) if (w > (beams.idleWidth + beams.litWidth) / 2f) lit++;
        Console.WriteLine($"  beams                 : {lit} of {beamLines.Length} lit as the request passed; card beam peak width {cardBeamPeak:F3} m");
        Console.WriteLine($"  timeline              : generation segment peaked at red {modelSegPeakRed:F1}, playhead reached {playheadMax:F2} of 2.40 m");
        Console.WriteLine($"  scheduler caption     : \"{captions[graph.IndexOf(graph.Find("gpu-scheduler"))].text.Replace("\n", " / ")}\"");
        Console.WriteLine($"  card readout mid-run  : \"{(midReadout ?? "(none)").Replace("\n", " / ")}\"");
        if (lit != beamLines.Length || beamLines.Length != 4) { Console.WriteLine("  >>> BUG: not every beam on the route lit, or the route isn't 4 beams."); problems++; }
        string finalAnswer = answerPanel.Shown ?? "";
        Console.WriteLine($"  hall                  : wave peaked at {peakWave:F2}, racks awake to {wakeAtRequest:F0} m by the request, " +
                          $"ripples peaked at {peakRipple:F2}");
        Console.WriteLine($"  bursts                : {bursts.Played} (23 parts, 6 node pings, power on, the card, the answer)");
        Console.WriteLine($"  tokens                : {peakTokenRate:F1} particles/s while generating, {tokenRateAfter:F1} after");
        Console.WriteLine($"  prompt                : \"{(promptText.text ?? "").Replace("\n", " / ")}\"");
        Console.WriteLine($"  answer mid-stream     : \"{(answerMid ?? "(none)").Replace("\n", " / ")}\"");
        Console.WriteLine($"  answer at the end     : \"{finalAnswer.Replace("\n", " / ")}\"");
        string journeyEnd = journey.Shown ?? "";
        Console.WriteLine($"  journey board, end    : \"{journeyEnd.Substring(Math.Max(0, journeyEnd.LastIndexOf('\n') + 1))}\"");
        Console.WriteLine($"  answer heading        : \"{(answerPanel.responseHeading != null ? answerPanel.responseHeading.text : "(none)")}\"");
        if (journeyDuringModel == null || !journeyDuringModel.Contains("<color=" + JourneyText.Gpu + ">5  Rig 2  ·  Generating"))
        { Console.WriteLine("  >>> BUG: the journey board didn't light the GPU step while it ran.\n      " + journeyDuringModel); problems++; }
        if (!journeyEnd.Contains("Total 3.18 s") || !journeyEnd.Contains("96.6%")) { Console.WriteLine("  >>> BUG: the journey board didn't end on the total."); problems++; }
        if (peakWave < 0.5f || wakeAtRequest < 30f) { Console.WriteLine("  >>> BUG: the power on wave didn't run or didn't wake the hall."); problems++; }
        if (peakRipple < 0.9f) { Console.WriteLine("  >>> BUG: the floor never rippled while generating."); problems++; }
        if (bursts.Played < 32) { Console.WriteLine("  >>> BUG: missing bursts."); problems++; }
        if (peakTokenRate <= 0f || tokenRateAfter != 0f) { Console.WriteLine("  >>> BUG: tokens didn't stream, or kept streaming after."); problems++; }
        if (string.IsNullOrEmpty(promptText.text) || answerMid == null || !finalAnswer.EndsWith("tenth of a second.")) { Console.WriteLine("  >>> BUG: the answer panel didn't show the prompt or type out the whole answer."); problems++; }
        Console.WriteLine($"  stage                 : system view at scale {viewScaleAtRequest:F2} when the request starts, " +
                          $"timeline at {timelineScaleLate:F2} by the answer, tray and stand {(trayShownAtRequest ? "still there" : "gone")}");
        if (viewShownDuringAssembly || timelineShownBeforeRequest) { Console.WriteLine("  >>> BUG: the system view or timeline showed before its act."); problems++; }
        if (viewScaleAtRequest < 0.99f || timelineScaleLate < 0.99f) { Console.WriteLine("  >>> BUG: the system view or timeline wasn't fully in when needed."); problems++; }
        if (trayShownAtRequest) { Console.WriteLine("  >>> BUG: the empty tray or frame stand was still up at the request."); problems++; }
        if (cardBeamPeak < beams.cardBeamWidth * 0.5f) { Console.WriteLine("  >>> BUG: the beam down to the working card never showed."); problems++; }
        if (modelSegPeakRed < 2f) { Console.WriteLine("  >>> BUG: the generation segment never lit in the GPU colour."); problems++; }
        if (playheadMax < 2.3f) { Console.WriteLine("  >>> BUG: the timeline playhead didn't reach the end."); problems++; }
        if (!sawStats || !sawTokensDone || midReadout == null) { Console.WriteLine("  >>> BUG: the card readout missed its stats or its token count."); problems++; }
        if (!captions[graph.IndexOf(graph.Find("gpu-scheduler"))].text.Contains("Least busy rig selected")) { Console.WriteLine("  >>> BUG: the scheduler's caption is wrong."); problems++; }
        float spun = 0f; for (int i = 0; i < 8; i++) spun += fanT[i].RotatedDegrees;
        Console.WriteLine($"  fans turned           : {spun / 8f / 360f:F0} revolutions per card on average");
        if (maxGlowBeforePower > 0.001f || fanDegreesBeforePower > 0.001f) { Console.WriteLine("  >>> BUG: cards glow or spin before power on."); problems++; }
        if (peakGlowCard3 < 0.95f) { Console.WriteLine("  >>> BUG: card 3 does not reach full glow while generating."); problems++; }
        if (peakGlowOthersDuringRequest > 0.2f) { Console.WriteLine("  >>> BUG: another card glows as if it were working."); problems++; }
        if (minIdleGlowAfterPower < 0.05f) { Console.WriteLine("  >>> BUG: powered cards are dark when idle."); problems++; }
        if (spun <= 0f) { Console.WriteLine("  >>> BUG: fans never turned."); problems++; }

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
