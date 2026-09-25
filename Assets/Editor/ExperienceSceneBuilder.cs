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
    /// Uses the models in Assets/Art/Models when they exist (generate them with
    /// tools/blender/generate_rig_parts.py) and falls back to placeholders built from
    /// primitives when they don't. Both are real size and name their parts the same way,
    /// so every component works the same with either.
    ///
    /// Re-running it rebuilds the scene from scratch. The scene is disposable; this
    /// script and the data assets are the source of truth.
    /// </summary>
    public static class ExperienceSceneBuilder
    {
        const string DataDir = "Assets/Data";
        const string ModelsDir = "Assets/Art/Models";
        const string MaterialsDir = "Assets/Art/Materials";
        const string ScenePath = "Assets/Scenes/Experience.unity";

        // Layout, in world metres, tuned by looking at the scene through the main camera.
        // The viewer stands at (0, 1.6, -1.2) facing +z, the bench is 2m ahead, the parts
        // table sits to its left and the frame waits on a stand to its right.
        static readonly Vector3 RigPosition = new Vector3(0f, 0.9f, 0.8f);
        const float BenchTopWorld = 0.88f;
        const float TrayGap = 0.04f;
        static readonly Vector3 FrameStandPosition = new Vector3(1.2f, BenchTopWorld, 0.8f);

        // The same bench surface in RigRoot's space, which is where sockets are placed.
        // RigLayout's card positions are relative to RigRoot too.
        const float BenchTop = BenchTopWorld - 0.9f;

        // SystemViewDisplay rewrites node positions from SystemGraph.asset every frame, so
        // moving nodes in the asset or the scene doesn't stick. The root transform is the
        // lever: scale the graph down and drop it so it sits just behind and above the cards.
        static readonly Vector3 SystemViewPosition = new Vector3(0f, 0.4f, 1.5f);
        const float SystemViewScale = 0.6f;

        // Subtitle line just above the front edge of the bench, below the cards, so it never
        // covers the rig or the system view. Dark, because the floor and horizon behind it
        // are near white.
        static readonly Vector3 NarrationPosition = new Vector3(0f, 0.98f, 0.45f);
        static readonly Color TextColour = new Color(0.1f, 0.12f, 0.15f);

        // Real sizes, metres, in Unity axes: x across, y up, z along. These match the
        // Blender models exactly, so placeholders and models are interchangeable.
        static readonly Vector3 CardSize = new Vector3(0.040f, 0.111f, 0.267f);
        static readonly Vector3 FrameSize = new Vector3(0.720f, 0.300f, 0.400f);
        static readonly Vector3 PsuSize = new Vector3(0.150f, 0.086f, 0.160f);
        static readonly Vector3 BoardSize = new Vector3(0.305f, 0.0016f, 0.244f);
        static readonly Vector3 RiserSize = new Vector3(0.040f, 0.0016f, 0.100f);

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

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

            CreateMaterials();
            int modelsUsed = 0;

            // ---------- room ----------
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            Paint(floor, "Floor");

            Camera cam = Camera.main;
            if (cam != null)
            {
                // Standing eye height, where the layout was judged from.
                cam.transform.position = new Vector3(0f, 1.6f, -1.2f);
                cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
                cam.name = "Main Camera (flat preview, XR rig replaces this)";
            }

            // The template's light casts realtime shadows. CLAUDE.md: none until profiled.
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null) sun.shadows = LightShadows.None;

            // ---------- bench and rig root ----------
            GameObject rigRoot = new GameObject("RigRoot");
            rigRoot.transform.position = RigPosition;

            GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Bench";
            bench.transform.SetParent(rigRoot.transform, false);
            bench.transform.localScale = new Vector3(1.2f, 0.06f, 0.6f);
            bench.transform.localPosition = new Vector3(0f, BenchTop - 0.03f, 0f);
            Paint(bench, "Bench");

            GameObject benchBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            benchBase.name = "Bench Base";
            benchBase.transform.position = new Vector3(RigPosition.x, (BenchTopWorld - 0.06f) / 2f, RigPosition.z);
            benchBase.transform.localScale = new Vector3(1.1f, BenchTopWorld - 0.06f, 0.5f);
            Paint(benchBase, "BenchDark");

            // ---------- parts, laid out on a tray table ----------
            var partObjects = new Dictionary<string, GameObject>();
            var cardVisuals = new CardVisual[layout.cardCount];
            var cardTransforms = new Transform[layout.cardCount];

            foreach (AssemblyStep step in sequence.steps)
            {
                bool fromModel;
                GameObject part = CreatePart(step, out fromModel);
                if (fromModel) modelsUsed++;
                part.name = step.DisplayName;
                partObjects[step.stepId] = part;

                if (step.kind == PartKind.Gpu && layout.IsValidIndex(step.cardIndex))
                {
                    cardTransforms[step.cardIndex] = part.transform;
                    cardVisuals[step.cardIndex] = AddCardVisual(part);
                }
            }

            GameObject parts = new GameObject("Parts");
            Transform trayOrigin = LayOutTray(sequence, partObjects, parts.transform);

            // ---------- sockets ----------
            var bindings = new List<AssemblyPartBinding>(sequence.StepCount);
            foreach (AssemblyStep step in sequence.steps)
            {
                GameObject part = partObjects[step.stepId];

                GameObject traySlot = new GameObject("Tray " + step.stepId);
                traySlot.transform.SetParent(trayOrigin, true);
                traySlot.transform.position = part.transform.position;

                Transform socket = null;
                if (step.kind != PartKind.Gpu)
                {
                    // Cards take their socket from RigLayout inside AssemblyPlayer, so card
                    // spacing stays one number in one asset. Everything else gets one here.
                    GameObject s = new GameObject("Socket " + step.stepId);
                    s.transform.SetParent(rigRoot.transform, false);
                    s.transform.localPosition = SocketFor(step, layout);
                    socket = s.transform;
                }

                bindings.Add(new AssemblyPartBinding
                {
                    stepId = step.stepId,
                    part = part.transform,
                    tray = traySlot.transform,
                    socket = socket
                });
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
            cardDisplay.visuals = cardVisuals;
            // AssemblyPlayer moves the cards. Two things writing positions would fight.
            cardDisplay.driveTransforms = false;
            cardDisplay.activeLift = 0f;

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
                RemoveCollider(marker);
                Paint(marker, "Node");
                markers[i] = marker.transform;

                GameObject label = new GameObject("Label " + node.nodeId);
                label.transform.SetParent(marker.transform, false);
                label.transform.localPosition = new Vector3(0f, 1.4f, -0.6f);
                // Undo the marker's non-uniform scale so the text isn't squashed.
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
            RemoveCollider(pulse);
            Paint(pulse, "Pulse");

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

            RigPower power = experience.AddComponent<RigPower>();
            power.sequencer = sequencer;
            power.cards = cardVisuals;

            // ---------- save ----------
            EnsureFolder("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();

            // Reload what was just saved. This project enters Play mode without reloading
            // the scene, and pressing Play on the freshly built in-memory scene came up
            // with some data asset fields null (seen on sequence, layout and graph), which
            // leaves the arc stuck in Assembly. The scene loaded from disk plays correctly.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            string art = modelsUsed > 0
                ? modelsUsed + " parts from " + ModelsDir
                : "placeholder parts (run tools/blender/generate_rig_parts.py for models)";
            Debug.Log($"[SceneBuilder] Built {ScenePath}: {layout.cardCount} cards, " +
                      $"{sequence.StepCount} assembly steps, {graph.NodeCount} system nodes, {art}. " +
                      "Press Play to watch the arc on a flat screen.");
        }

        // ------------------------------------------------------------------ parts

        static GameObject CreatePart(AssemblyStep step, out bool fromModel)
        {
            string model = ModelFor(step.kind);
            if (model != null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/" + model + ".fbx");
                if (prefab != null)
                {
                    fromModel = true;
                    return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                }
            }

            fromModel = false;
            switch (step.kind)
            {
                case PartKind.Gpu:         return PlaceholderCard();
                case PartKind.Chassis:     return PlaceholderFrame();
                case PartKind.Psu:         return Block("PSU", PsuSize, "PSUBody");
                case PartKind.Motherboard: return Block("Motherboard", BoardSize, "PCB");
                case PartKind.Riser:       return Block("Riser", RiserSize, "PCB");
                case PartKind.Cpu:         return Block("CPU", new Vector3(0.05f, 0.006f, 0.05f), "Steel");
                case PartKind.Ram:         return Block("RAM", new Vector3(0.006f, 0.03f, 0.133f), "Shroud");
                case PartKind.Cable:       return Block("Cable", new Vector3(0.02f, 0.02f, 0.30f), "BlackPlastic");
                case PartKind.Fan:         return Block("Fans", new Vector3(0.03f, 0.12f, 0.12f), "BlackPlastic");
                default:                   return Block("Part", Vector3.one * 0.1f, "Shroud");
            }
        }

        static string ModelFor(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.Gpu:         return "gpu_card";
                case PartKind.Chassis:     return "frame";
                case PartKind.Psu:         return "psu";
                case PartKind.Motherboard: return "motherboard";
                case PartKind.Riser:       return "riser";
                default:                   return null;
            }
        }

        static Vector3 SizeFor(AssemblyStep step)
        {
            switch (step.kind)
            {
                case PartKind.Gpu:         return CardSize;
                case PartKind.Chassis:     return FrameSize;
                case PartKind.Psu:         return PsuSize;
                case PartKind.Motherboard: return BoardSize;
                case PartKind.Riser:       return RiserSize;
                case PartKind.Cpu:         return new Vector3(0.05f, 0.006f, 0.05f);
                case PartKind.Ram:         return new Vector3(0.006f, 0.03f, 0.133f);
                case PartKind.Cable:       return new Vector3(0.02f, 0.02f, 0.30f);
                case PartKind.Fan:         return new Vector3(0.03f, 0.12f, 0.12f);
                default:                   return Vector3.one * 0.1f;
            }
        }

        /// <summary>Where each part ends up, relative to RigRoot. Parts sit on things.</summary>
        static Vector3 SocketFor(AssemblyStep step, RigLayout layout)
        {
            float cardBottom = layout.firstCardOffset.y - CardSize.y / 2f;
            switch (step.kind)
            {
                case PartKind.Chassis:     return new Vector3(0f, BenchTop + FrameSize.y / 2f, 0f);
                case PartKind.Motherboard: return new Vector3(0f, BenchTop + 0.021f, 0.02f);
                case PartKind.Cpu:         return new Vector3(0.02f, BenchTop + 0.027f, 0.07f);
                case PartKind.Ram:         return new Vector3(0.10f, BenchTop + 0.037f, 0.05f);
                case PartKind.Psu:         return new Vector3(-0.47f, BenchTop + PsuSize.y / 2f, 0.05f);
                case PartKind.Cable:       return new Vector3(-0.20f, cardBottom + CardSize.y + 0.02f, -0.12f);
                case PartKind.Fan:         return new Vector3(0.38f, BenchTop + 0.14f, 0f);
                case PartKind.Riser:
                    // Directly under its card, so the card visibly plugs into it.
                    Vector3 card = layout.IsValidIndex(step.cardIndex)
                        ? layout.GetCardLocalPosition(step.cardIndex)
                        : Vector3.zero;
                    return new Vector3(card.x, cardBottom - 0.012f, card.z);
                default:                   return Vector3.zero;
            }
        }

        /// <summary>
        /// Which tray row a part waits in, front (nearest the viewer) to back, so tall cards
        /// never hide the flat parts behind them. -1 means it doesn't go on the tray.
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
        /// Small parts wait on a table left of the bench, in rows so none overlap. The frame
        /// is too big for the table, so it waits on a stand to the right and slides across
        /// onto the bench at the same height. Returns the tray transform.
        /// </summary>
        static Transform LayOutTray(AssemblySequence sequence, Dictionary<string, GameObject> partObjects, Transform partsParent)
        {
            const int rows = 3;
            var width = new float[rows];
            var depth = new float[rows];
            foreach (AssemblyStep step in sequence.steps)
            {
                int r = TrayRowFor(step.kind);
                if (r < 0) continue;
                Vector3 size = SizeFor(step);
                if (width[r] > 0f) width[r] += TrayGap;
                width[r] += size.x;
                depth[r] = Mathf.Max(depth[r], size.z);
            }

            float trayWidth = 0f, trayDepth = 0f;
            for (int r = 0; r < rows; r++)
            {
                if (depth[r] <= 0f) continue;
                if (trayDepth > 0f) trayDepth += TrayGap;
                trayWidth = Mathf.Max(trayWidth, width[r]);
                trayDepth += depth[r];
            }

            var rowZ = new float[rows];
            var cursor = new float[rows];
            float z = -trayDepth * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                cursor[r] = -width[r] * 0.5f;
                if (depth[r] <= 0f) continue;
                rowZ[r] = z + depth[r] * 0.5f;
                z += depth[r] + TrayGap;
            }

            GameObject tray = new GameObject("Tray");
            tray.transform.position = new Vector3(
                -0.6f - 0.05f - (trayWidth * 0.5f + 0.05f),
                BenchTopWorld,
                RigPosition.z);

            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Tray Table";
            table.transform.SetParent(tray.transform, false);
            table.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            table.transform.localScale = new Vector3(trayWidth + 0.1f, 0.06f, trayDepth + 0.1f);
            Paint(table, "Bench");
            Pedestal(tray.transform.position, trayWidth, trayDepth);

            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Frame Stand";
            stand.transform.position = FrameStandPosition + new Vector3(0f, -0.03f, 0f);
            stand.transform.localScale = new Vector3(1.05f, 0.06f, 0.6f);
            Paint(stand, "Bench");
            Pedestal(FrameStandPosition, 0.95f, 0.5f);

            foreach (AssemblyStep step in sequence.steps)
            {
                GameObject part = partObjects[step.stepId];
                part.transform.SetParent(partsParent, true);
                Vector3 size = SizeFor(step);
                int r = TrayRowFor(step.kind);
                if (r >= 0)
                {
                    part.transform.position = tray.transform.TransformPoint(
                        new Vector3(cursor[r] + size.x * 0.5f, size.y * 0.5f, rowZ[r]));
                    cursor[r] += size.x + TrayGap;
                }
                else
                {
                    part.transform.position = FrameStandPosition + new Vector3(0f, size.y * 0.5f, 0f);
                }
            }
            return tray.transform;
        }

        /// <summary>A dark block from the floor up to a surface, so tables don't float.</summary>
        static void Pedestal(Vector3 topCentre, float width, float depth)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Pedestal";
            float height = topCentre.y - 0.06f;
            block.transform.position = new Vector3(topCentre.x, height / 2f, topCentre.z);
            block.transform.localScale = new Vector3(width, height, depth);
            Paint(block, "BenchDark");
        }

        // ----------------------------------------------------------- placeholders

        /// <summary>Two fans, a shroud, a backplate and an LED strip, named like the model.</summary>
        static GameObject PlaceholderCard()
        {
            GameObject root = new GameObject("GPU (placeholder)");
            Child(root, "Body", PrimitiveType.Cube, new Vector3(-0.002f, 0f, 0f),
                  new Vector3(0.030f, CardSize.y - 0.004f, CardSize.z), "Shroud");
            Child(root, "Backplate", PrimitiveType.Cube, new Vector3(0.0175f, 0f, 0f),
                  new Vector3(0.002f, CardSize.y - 0.012f, CardSize.z - 0.01f), "ShroudAccent");
            Child(root, "LED", PrimitiveType.Cube, new Vector3(-0.012f, CardSize.y / 2f - 0.002f, 0.02f),
                  new Vector3(0.004f, 0.003f, CardSize.z * 0.55f), "LED");

            for (int f = 0; f < 2; f++)
            {
                GameObject fan = new GameObject("Fan" + f);
                fan.transform.SetParent(root.transform, false);
                fan.transform.localPosition = new Vector3(-0.0185f, 0f, f == 0 ? -0.063f : 0.063f);

                GameObject disc = Child(fan, "Disc", PrimitiveType.Cylinder, Vector3.zero,
                                        new Vector3(0.082f, 0.002f, 0.082f), "FanBlack");
                disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // cylinder axis to X

                // Blades, so the spin is visible. A plain disc spinning looks still.
                for (int b = 0; b < 3; b++)
                {
                    GameObject blade = Child(fan, "Blade", PrimitiveType.Cube, new Vector3(-0.003f, 0f, 0f),
                                             new Vector3(0.002f, 0.074f, 0.012f), "ShroudAccent");
                    blade.transform.localRotation = Quaternion.Euler(60f * b, 0f, 0f);
                }
            }
            return root;
        }

        /// <summary>An open air frame: four posts, rails between them, and a card bar.</summary>
        static GameObject PlaceholderFrame()
        {
            GameObject root = new GameObject("Frame (placeholder)");
            float t = 0.02f;
            float w = FrameSize.x, h = FrameSize.y, d = FrameSize.z;
            float yb = -h / 2f + t / 2f, yt = h / 2f - t / 2f;
            float[] xs = { -w / 2f + t / 2f, w / 2f - t / 2f };
            float[] zs = { -d / 2f + t / 2f, d / 2f - t / 2f };

            foreach (float x in xs)
                foreach (float z in zs)
                    Child(root, "Post", PrimitiveType.Cube, new Vector3(x, 0f, z), new Vector3(t, h, t), "Aluminium");
            foreach (float z in zs)
            {
                Child(root, "Rail", PrimitiveType.Cube, new Vector3(0f, yb, z), new Vector3(w - 2f * t, t, t), "Aluminium");
                Child(root, "Rail", PrimitiveType.Cube, new Vector3(0f, yt, z), new Vector3(w - 2f * t, t, t), "Aluminium");
            }
            foreach (float x in xs)
                Child(root, "Rail", PrimitiveType.Cube, new Vector3(x, yb, 0f), new Vector3(t, t, d - 2f * t), "Aluminium");
            return root;
        }

        static GameObject Block(string name, Vector3 size, string material)
        {
            GameObject root = new GameObject(name + " (placeholder)");
            Child(root, "Body", PrimitiveType.Cube, Vector3.zero, size, material);
            return root;
        }

        static GameObject Child(GameObject parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, string material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            RemoveCollider(go);
            Paint(go, material);
            return go;
        }

        static CardVisual AddCardVisual(GameObject card)
        {
            CardVisual visual = card.AddComponent<CardVisual>();

            // The models' LED comes in with a plain imported material. Swap in the shared
            // one with emission switched on, or the glow has nothing to drive.
            Transform led = card.transform.Find("LED");
            if (led != null)
            {
                Renderer r = led.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = Materials["LED"];
            }
            return visual;
        }

        // -------------------------------------------------------------- materials

        static void CreateMaterials()
        {
            Materials.Clear();
            EnsureFolder(MaterialsDir);
            Mat("Floor", new Color(0.30f, 0.31f, 0.33f), 0f, 0.2f);
            Mat("Bench", new Color(0.42f, 0.30f, 0.20f), 0f, 0.35f);
            Mat("BenchDark", new Color(0.18f, 0.18f, 0.19f), 0f, 0.3f);
            Mat("Shroud", new Color(0.08f, 0.08f, 0.09f), 0.2f, 0.55f);
            Mat("ShroudAccent", new Color(0.55f, 0.56f, 0.58f), 0.9f, 0.7f);
            Mat("FanBlack", new Color(0.03f, 0.03f, 0.03f), 0f, 0.4f);
            Mat("PCB", new Color(0.03f, 0.12f, 0.06f), 0f, 0.45f);
            Mat("Steel", new Color(0.62f, 0.63f, 0.65f), 1f, 0.65f);
            Mat("Aluminium", new Color(0.78f, 0.79f, 0.80f), 1f, 0.6f);
            Mat("BlackPlastic", new Color(0.05f, 0.05f, 0.05f), 0f, 0.3f);
            Mat("PSUBody", new Color(0.10f, 0.10f, 0.11f), 0.6f, 0.6f);
            Mat("Node", new Color(0.20f, 0.30f, 0.45f), 0f, 0.5f);
            Mat("LED", new Color(0.9f, 0.9f, 0.9f), 0f, 0.7f, emissive: true);
            Mat("Pulse", new Color(0.9f, 0.9f, 1.0f), 0f, 0.7f, emissive: true, emission: new Color(1.2f, 1.4f, 2.0f));
        }

        /// <summary>
        /// Reuses the asset if it exists, so rebuilding the scene doesn't churn material
        /// GUIDs or undo tweaks someone made in the inspector.
        /// </summary>
        static void Mat(string name, Color color, float metallic, float smoothness,
                        bool emissive = false, Color? emission = null)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null)
                {
                    Debug.LogError("[SceneBuilder] URP Lit shader not found. Is this a URP project?");
                    return;
                }
                mat = new Material(lit);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
                if (emissive)
                {
                    // Emission must be switched on in the material itself. A property block
                    // can change its colour at runtime but can't turn the feature on.
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    mat.SetColor("_EmissionColor", emission ?? Color.black);
                }
                AssetDatabase.CreateAsset(mat, path);
            }
            Materials[name] = mat;
        }

        static void Paint(GameObject go, string material)
        {
            Renderer r = go.GetComponent<Renderer>();
            Material mat;
            if (r != null && Materials.TryGetValue(material, out mat)) r.sharedMaterial = mat;
        }

        // ---------------------------------------------------------------- helpers

        static void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static T Load<T>(string file) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(DataDir + "/" + file);
        }
    }
}
