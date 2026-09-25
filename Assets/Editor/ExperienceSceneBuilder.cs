using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OCS.VR.Experience;
using OCS.VR.Rig;
using OCS.VR.Telemetry;

namespace OCS.VR.EditorTools
{
    /// <summary>
    /// Builds the whole experience scene from the data assets, in one menu click.
    ///
    /// Everything here is placeholder geometry: primitives standing in for the rig and
    /// the system view, exactly what docs/experience-design.md calls for through V4.
    /// Real art replaces the meshes at V7 and none of this wiring changes, because the
    /// components bind to data assets rather than to specific models.
    ///
    /// Re-running it rebuilds the scene from scratch, so it stays disposable while the
    /// layout is still being argued about.
    /// </summary>
    public static class ExperienceSceneBuilder
    {
        const string DataDir = "Assets/Data";
        const string ScenePath = "Assets/Scenes/Experience.unity";

        [MenuItem("OCS/Build Experience Scene")]
        public static void BuildScene()
        {
            RigLayout layout = Load<RigLayout>("RigLayout.asset");
            AssemblySequence sequence = Load<AssemblySequence>("AssemblySequence.asset");
            SystemGraph graph = Load<SystemGraph>("SystemGraph.asset");
            NarrationTrack track = Load<NarrationTrack>("NarrationTrack.asset");
            TextAsset trace = AssetDatabase.LoadAssetAtPath<TextAsset>(DataDir + "/sample-trace.json");

            if (layout == null || sequence == null || graph == null || track == null || trace == null)
            {
                Debug.LogError("[SceneBuilder] Missing one or more assets in " + DataDir +
                               ". Expected RigLayout, AssemblySequence, SystemGraph, NarrationTrack and sample-trace.json.");
                return;
            }

            string error;
            if (!sequence.Validate(out error)) { Debug.LogError("[SceneBuilder] " + error); return; }
            if (!graph.Validate(out error)) { Debug.LogError("[SceneBuilder] " + error); return; }
            if (!track.Validate(out error)) { Debug.LogError("[SceneBuilder] " + error); return; }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---------- floor and camera ----------
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            Camera cam = Camera.main;
            if (cam != null)
            {
                // Roughly standing eye height, looking at the bench.
                cam.transform.position = new Vector3(0f, 1.6f, -1.2f);
                cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
                cam.name = "Main Camera (flat preview, XR rig replaces this)";
            }

            // ---------- rig ----------
            GameObject rigRoot = new GameObject("RigRoot");
            rigRoot.transform.position = new Vector3(0f, 0.9f, 0.8f);

            GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Bench";
            bench.transform.SetParent(rigRoot.transform, false);
            bench.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            bench.transform.localScale = new Vector3(1.2f, 0.06f, 0.6f);

            var cardTransforms = new Transform[layout.cardCount];
            for (int i = 0; i < layout.cardCount; i++)
            {
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
                card.name = "Card " + i;
                card.transform.SetParent(rigRoot.transform, false);
                card.transform.localPosition = layout.GetCardLocalPosition(i);
                // A 1070 is roughly 27cm x 11cm x 4cm.
                card.transform.localScale = new Vector3(0.04f, 0.11f, 0.27f);
                cardTransforms[i] = card.transform;
            }

            // ---------- playback ----------
            GameObject experience = new GameObject("Experience");

            TracePlayer player = experience.AddComponent<TracePlayer>();
            player.traceAsset = trace;
            player.playOnStart = false;      // the sequencer owns when it plays
            player.timeScale = 0.5f;
            player.tailHold = 1.5f;

            RigCardDisplay cardDisplay = rigRoot.AddComponent<RigCardDisplay>();
            cardDisplay.player = player;
            cardDisplay.layout = layout;
            cardDisplay.cards = cardTransforms;

            // ---------- assembly ----------
            GameObject tray = new GameObject("Tray");
            tray.transform.position = new Vector3(-1.3f, 0.9f, 0.8f);

            GameObject parts = new GameObject("Parts");
            parts.transform.position = Vector3.zero;

            var bindings = new List<AssemblyPartBinding>(sequence.StepCount);
            for (int i = 0; i < sequence.StepCount; i++)
            {
                AssemblyStep step = sequence.steps[i];

                GameObject traySlot = new GameObject("Tray " + step.stepId);
                traySlot.transform.SetParent(tray.transform, false);
                traySlot.transform.localPosition = new Vector3(0f, 0.02f * i, 0f);

                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = step.DisplayName;
                part.transform.SetParent(parts.transform, false);
                part.transform.position = traySlot.transform.position;
                part.transform.localScale = ScaleFor(step);

                GameObject socket = null;
                if (!step.TargetsCard)
                {
                    // Card steps get their socket from RigLayout instead, so spacing stays
                    // a single number in one asset.
                    socket = new GameObject("Socket " + step.stepId);
                    socket.transform.SetParent(rigRoot.transform, false);
                    socket.transform.localPosition = SocketFor(step);
                }

                bindings.Add(new AssemblyPartBinding
                {
                    stepId = step.stepId,
                    part = part.transform,
                    tray = traySlot.transform,
                    socket = socket != null ? socket.transform : null
                });
            }

            AssemblyPlayer assembly = experience.AddComponent<AssemblyPlayer>();
            assembly.sequence = sequence;
            assembly.layout = layout;
            assembly.rigRoot = rigRoot.transform;
            assembly.bindings = bindings.ToArray();
            assembly.playOnStart = false;

            // ---------- system view ----------
            GameObject systemView = new GameObject("SystemView");
            systemView.transform.position = new Vector3(0f, 0f, 1.5f);

            var markers = new Transform[graph.NodeCount];
            for (int i = 0; i < graph.NodeCount; i++)
            {
                SystemNode node = graph.nodes[i];

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "Node " + node.nodeId;
                marker.transform.SetParent(systemView.transform, false);
                marker.transform.localPosition = node.position;
                marker.transform.localScale = new Vector3(0.28f, 0.16f, 0.06f);
                markers[i] = marker.transform;

                GameObject label = new GameObject("Label " + node.nodeId);
                label.transform.SetParent(marker.transform, false);
                label.transform.localPosition = new Vector3(0f, 1.4f, -0.6f);
                // Undo the marker's non-uniform scale so the text is not squashed.
                label.transform.localScale = new Vector3(1f / 0.28f, 1f / 0.16f, 1f / 0.06f) * 0.02f;
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = node.DisplayLabel;
                text.anchor = TextAnchor.MiddleCenter;
                text.fontSize = 90;
                text.characterSize = 0.5f;
            }

            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = "Pulse";
            pulse.transform.SetParent(systemView.transform, false);
            pulse.transform.localScale = Vector3.one * 0.09f;

            SystemViewDisplay sysView = systemView.AddComponent<SystemViewDisplay>();
            sysView.player = player;
            sysView.graph = graph;
            sysView.nodeMarkers = markers;
            sysView.pulse = pulse.transform;

            // ---------- narration ----------
            GameObject narrationGo = new GameObject("Narration");
            narrationGo.transform.position = new Vector3(0f, 1.15f, 0.7f);
            narrationGo.transform.localScale = Vector3.one * 0.02f;

            TextMesh narrationText = narrationGo.AddComponent<TextMesh>();
            narrationText.text = string.Empty;
            narrationText.anchor = TextAnchor.MiddleCenter;
            narrationText.fontSize = 90;
            narrationText.characterSize = 0.5f;

            NarrationDirector director = experience.AddComponent<NarrationDirector>();
            director.track = track;
            director.tracePlayer = player;
            director.assemblyPlayer = assembly;
            director.narrateHops = true;

            NarrationLabel narrationLabel = narrationGo.AddComponent<NarrationLabel>();
            narrationLabel.director = director;
            narrationLabel.target = narrationText;

            // ---------- the arc ----------
            ExperienceSequencer sequencer = experience.AddComponent<ExperienceSequencer>();
            sequencer.tracePlayer = player;
            sequencer.assemblyPlayer = assembly;
            sequencer.playOnStart = true;
            sequencer.loop = false;
            // Short while you are watching it on a monitor. Retune for the real thing.
            sequencer.emptyBenchDuration = 4f;
            sequencer.powerOnDuration = 4f;
            sequencer.answerDuration = 6f;

            director.sequencer = sequencer;

            // ---------- save ----------
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] Built {ScenePath}: {layout.cardCount} cards, " +
                      $"{sequence.StepCount} assembly parts, {graph.NodeCount} system nodes. " +
                      $"Press Play to watch the arc on a flat screen.");
        }

        static T Load<T>(string file) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(DataDir + "/" + file);
        }

        /// <summary>Rough real world sizes so the blockout reads at the right scale.</summary>
        static Vector3 ScaleFor(AssemblyStep step)
        {
            switch (step.kind)
            {
                case PartKind.Chassis:     return new Vector3(1.0f, 0.04f, 0.5f);
                case PartKind.Motherboard: return new Vector3(0.30f, 0.02f, 0.24f);
                case PartKind.Cpu:         return new Vector3(0.05f, 0.01f, 0.05f);
                case PartKind.Ram:         return new Vector3(0.13f, 0.03f, 0.01f);
                case PartKind.Psu:         return new Vector3(0.15f, 0.09f, 0.14f);
                case PartKind.Riser:       return new Vector3(0.03f, 0.02f, 0.06f);
                case PartKind.Gpu:         return new Vector3(0.04f, 0.11f, 0.27f);
                case PartKind.Cable:       return new Vector3(0.02f, 0.02f, 0.30f);
                case PartKind.Fan:         return new Vector3(0.12f, 0.12f, 0.03f);
                default:                   return Vector3.one * 0.1f;
            }
        }

        static Vector3 SocketFor(AssemblyStep step)
        {
            switch (step.kind)
            {
                case PartKind.Chassis:     return new Vector3(0f, -0.02f, 0f);
                case PartKind.Motherboard: return new Vector3(0f, 0.02f, 0f);
                case PartKind.Cpu:         return new Vector3(0f, 0.04f, 0.05f);
                case PartKind.Ram:         return new Vector3(0.10f, 0.05f, 0f);
                case PartKind.Psu:         return new Vector3(-0.45f, 0.06f, 0f);
                case PartKind.Cable:       return new Vector3(0f, 0.06f, -0.20f);
                case PartKind.Fan:         return new Vector3(0.45f, 0.10f, 0f);
                default:                   return Vector3.zero;
            }
        }
    }
}
