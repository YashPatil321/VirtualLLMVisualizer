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

        // Layout, in world metres. The viewer stands at (0, 1.6, -1.2) facing +z, the bench
        // is 2m ahead, the parts table sits to its left and the frame waits to its right.
        static readonly Vector3 RigPosition = new Vector3(0f, 0.9f, 0.8f);
        const float BenchTop = 0.88f;
        const float TrayGap = 0.04f;
        static readonly Vector3 FrameStandPosition = new Vector3(1.2f, BenchTop, 0.8f);

        // SystemGraph.asset positions nodes at 1.4 to 2.2m and 4.8m wide, which puts them
        // well above the rig and past the edge of view. SystemViewDisplay rewrites marker
        // positions from the asset every frame, so the root transform is the lever: scale
        // the graph down and drop it so it sits just behind and above the cards.
        static readonly Vector3 SystemViewPosition = new Vector3(0f, 0.4f, 1.5f);
        const float SystemViewScale = 0.6f;

        // Subtitle line hovering just above the front edge of the bench, below the cards,
        // so it never covers the rig or the system view. Dark, because the placeholder
        // floor and the horizon behind it are both near white.
        static readonly Vector3 NarrationPosition = new Vector3(0f, 0.98f, 0.45f);
        static readonly Color TextColour = new Color(0.1f, 0.12f, 0.15f);

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
            rigRoot.transform.position = RigPosition;

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
            // Small parts wait on a table left of the bench, laid out in rows so none of them
            // overlap. The frame is too big for the table, so it waits on a stand to the right
            // and slides across onto the bench at the same height.
            Vector2 traySize;
            Vector3[] trayLocal = LayoutTray(sequence, out traySize);

            GameObject tray = new GameObject("Tray");
            tray.transform.position = new Vector3(
                -0.6f - 0.05f - (traySize.x * 0.5f + 0.05f),
                BenchTop,
                RigPosition.z);

            GameObject trayTable = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trayTable.name = "Tray Table";
            trayTable.transform.SetParent(tray.transform, false);
            trayTable.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            trayTable.transform.localScale = new Vector3(traySize.x + 0.1f, 0.06f, traySize.y + 0.1f);

            GameObject frameStand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameStand.name = "Frame Stand";
            frameStand.transform.position = FrameStandPosition + new Vector3(0f, -0.03f, 0f);
            frameStand.transform.localScale = new Vector3(1.05f, 0.06f, 0.6f);

            GameObject parts = new GameObject("Parts");
            parts.transform.position = Vector3.zero;

            var bindings = new List<AssemblyPartBinding>(sequence.StepCount);
            for (int i = 0; i < sequence.StepCount; i++)
            {
                AssemblyStep step = sequence.steps[i];

                GameObject traySlot = new GameObject("Tray " + step.stepId);
                traySlot.transform.SetParent(tray.transform, false);
                if (TrayRowFor(step.kind) >= 0)
                    traySlot.transform.localPosition = trayLocal[i];
                else
                    traySlot.transform.position = FrameStandPosition + new Vector3(0f, ScaleFor(step).y * 0.5f, 0f);

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
            systemView.transform.position = SystemViewPosition;
            systemView.transform.localScale = Vector3.one * SystemViewScale;

            // Child sizes are divided by the root scale so markers, labels and the pulse stay
            // their real size; only the spacing between nodes shrinks.
            Vector3 markerScale = new Vector3(0.28f, 0.16f, 0.06f) / SystemViewScale;

            var markers = new Transform[graph.NodeCount];
            for (int i = 0; i < graph.NodeCount; i++)
            {
                SystemNode node = graph.nodes[i];

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "Node " + node.nodeId;
                marker.transform.SetParent(systemView.transform, false);
                marker.transform.localPosition = node.position;
                marker.transform.localScale = markerScale;
                markers[i] = marker.transform;

                GameObject label = new GameObject("Label " + node.nodeId);
                label.transform.SetParent(marker.transform, false);
                label.transform.localPosition = new Vector3(0f, 1.4f, -0.6f);
                // Undo the marker's non-uniform scale so the text is not squashed.
                label.transform.localScale = new Vector3(1f / markerScale.x, 1f / markerScale.y, 1f / markerScale.z)
                                             * (0.02f / SystemViewScale);
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = node.DisplayLabel;
                text.anchor = TextAnchor.MiddleCenter;
                text.fontSize = 90;
                text.characterSize = 0.5f;
                text.color = TextColour;
            }

            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = "Pulse";
            pulse.transform.SetParent(systemView.transform, false);
            pulse.transform.localScale = Vector3.one * (0.09f / SystemViewScale);

            SystemViewDisplay sysView = systemView.AddComponent<SystemViewDisplay>();
            sysView.player = player;
            sysView.graph = graph;
            sysView.nodeMarkers = markers;
            sysView.pulse = pulse.transform;

            // ---------- narration ----------
            GameObject narrationGo = new GameObject("Narration");
            narrationGo.transform.position = NarrationPosition;
            narrationGo.transform.localScale = Vector3.one * 0.02f;

            TextMesh narrationText = narrationGo.AddComponent<TextMesh>();
            narrationText.text = string.Empty;
            narrationText.anchor = TextAnchor.MiddleCenter;
            narrationText.fontSize = 90;
            narrationText.characterSize = 0.5f;
            narrationText.color = TextColour;

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

            // Reload what was just saved. This project enters Play mode without reloading
            // the scene, and pressing Play on the freshly built in-memory scene came up
            // with some data asset fields null (seen on sequence, layout and graph), which
            // leaves the arc stuck in Assembly. The scene loaded from disk plays correctly.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

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

        /// <summary>
        /// Which tray row a part waits in, front (nearest the viewer) to back, so tall GPUs
        /// never hide the flat parts behind them. -1 means it does not go on the tray.
        /// </summary>
        static int TrayRowFor(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.Riser:
                case PartKind.Cable:
                case PartKind.Fan:         return 0;
                case PartKind.Motherboard:
                case PartKind.Cpu:
                case PartKind.Ram:
                case PartKind.Psu:         return 1;
                case PartKind.Gpu:         return 2;
                default:                   return -1;
            }
        }

        /// <summary>
        /// Tray slot for every step, relative to the centre of the tray surface: each row
        /// centred on x, parts in step order with a gap between them, resting on the surface.
        /// Steps that do not go on the tray get Vector3.zero. Size is the footprint used.
        /// </summary>
        static Vector3[] LayoutTray(AssemblySequence sequence, out Vector2 size)
        {
            const int rows = 3;
            var width = new float[rows];
            var depth = new float[rows];
            for (int i = 0; i < sequence.StepCount; i++)
            {
                int r = TrayRowFor(sequence.steps[i].kind);
                if (r < 0) continue;
                Vector3 s = ScaleFor(sequence.steps[i]);
                if (width[r] > 0f) width[r] += TrayGap;
                width[r] += s.x;
                depth[r] = Mathf.Max(depth[r], s.z);
            }

            size = Vector2.zero;
            for (int r = 0; r < rows; r++)
            {
                if (depth[r] <= 0f) continue;
                if (size.y > 0f) size.y += TrayGap;
                size.x = Mathf.Max(size.x, width[r]);
                size.y += depth[r];
            }

            var rowZ = new float[rows];
            var cursor = new float[rows];
            float z = -size.y * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                cursor[r] = -width[r] * 0.5f;
                if (depth[r] <= 0f) continue;
                rowZ[r] = z + depth[r] * 0.5f;
                z += depth[r] + TrayGap;
            }

            var slots = new Vector3[sequence.StepCount];
            for (int i = 0; i < sequence.StepCount; i++)
            {
                int r = TrayRowFor(sequence.steps[i].kind);
                if (r < 0) continue;
                Vector3 s = ScaleFor(sequence.steps[i]);
                slots[i] = new Vector3(cursor[r] + s.x * 0.5f, s.y * 0.5f, rowZ[r]);
                cursor[r] += s.x + TrayGap;
            }
            return slots;
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
