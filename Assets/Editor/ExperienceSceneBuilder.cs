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
        const string EnvironmentPrefabPath = "Assets/Prefabs/Environment.prefab";

        // Layout, in world metres. The bench, tray and frame stand were tuned by looking at
        // the scene through the main camera. The viewer now starts 1.15 m from the bench
        // rather than 2 m, so the rig fills the view; the rig itself stays real size, which
        // is the point of seeing it in VR.
        static readonly Vector3 RigPosition = new Vector3(0f, 0.9f, 0.8f);
        const float BenchTopWorld = 0.88f;
        const float TrayGap = 0.04f;
        static readonly Vector3 FrameStandPosition = new Vector3(1.2f, BenchTopWorld, 0.8f);

        // The same bench surface in RigRoot's space, which is where sockets are placed.
        // RigLayout's card positions are relative to RigRoot too.
        const float BenchTop = BenchTopWorld - 0.9f;

        // Where the viewer's eyes start. Panels and the dashboard are turned to face it.
        static readonly Vector3 ViewerEye = new Vector3(0f, 1.6f, -0.35f);

        // SystemViewDisplay rewrites node positions from SystemGraph.asset every frame, so
        // the root transform is the lever. It sits at the middle of the arch the nodes
        // make over the rig, which is also the point the view grows out of at power on.
        // SystemGraph.asset positions are relative to it; Rig 2's node is straight above
        // the real rig, so the beam from it drops onto the working card.
        static readonly Vector3 SystemViewPosition = new Vector3(0f, 2.4f, 3.6f);

        // The system view is built big, out over the hall: panels, text, the pulse and
        // the beams are all this many times their desk size, and read from four metres.
        const float ViewScale = 2.2f;

        // Node panels, metres.
        // Name on top, caption under it, both inside the panel, so nothing hangs below it
        // for Rig 2's beam to cut through.
        static readonly Vector3 PanelSize = new Vector3(0.40f, 0.13f, 0.008f);

        // A strip floating over the front edge of the bench, tilted to face the viewer:
        // the narration line on top, the "where the time goes" bar under it. Everything
        // the viewer reads sits in one place, below the rig, and never covers it.
        static readonly Vector3 DashboardPosition = new Vector3(0f, 0.95f, 0.40f);
        const float TimelineWidth = 0.9f;

        // The readout sits just left of the beam, between the card and Rig 2's panel, so
        // the beam itself connects the numbers to the card they describe. Left, because
        // the answer panel and its stream of tokens are on the right.
        static readonly Vector3 ReadoutOffset = new Vector3(-0.1f, 0.24f, 0f);

        // The answer panel: right of the rig, below eye level, facing the viewer.
        static readonly Vector3 AnswerPosition = new Vector3(1.2f, 1.42f, 1.55f);
        static readonly Vector2 AnswerSize = new Vector2(0.95f, 0.52f);

        // One palette. Cyan is the software and the network; amber is the GPU working.
        // Everything else is near black or grey, so the two colours carry the story.
        static readonly Color TextColour = new Color(0.92f, 0.94f, 0.97f);
        static readonly Color CaptionColour = new Color(0.55f, 0.62f, 0.70f);
        static readonly Color ReadoutColour = new Color(1f, 0.86f, 0.68f);
        static readonly Color Cyan = new Color(0.35f, 0.82f, 1f);
        static readonly Color Amber = new Color(1f, 0.62f, 0.22f);

        // Line heights for text, metres. Sized for the distance each is read from, so all
        // of it is roughly the same size to the eye.
        const float NodeNameHeight = 0.045f;
        const float NodeCaptionHeight = 0.022f;
        const float PartLabelHeight = 0.024f;
        const float ReadoutHeight = 0.024f;
        const float NarrationHeight = 0.028f;
        const float DashboardCaptionHeight = 0.018f;

        // Real sizes, metres, in Unity axes: x across, y up, z along. These match the
        // Blender models exactly, so placeholders and models are interchangeable.
        // Modelled on Rig 2: EVGA GTX 1070 SC cards, a two level 800 mm frame, and two Antec
        // HCP 1300 supplies lying flat at the ends of the lower level.
        static readonly Vector3 CardSize = new Vector3(0.040f, 0.111f, 0.267f);
        static readonly Vector3 FrameSize = new Vector3(0.800f, 0.300f, 0.400f);
        static readonly Vector3 PsuSize = new Vector3(0.200f, 0.086f, 0.150f);
        static readonly Vector3 CoolerSize = new Vector3(0.090f, 0.045f, 0.090f);
        static readonly Vector3 RamSize = new Vector3(0.007f, 0.031f, 0.133f);

        // The PCIe cable harness: its size, and where its centre sits from the middle of
        // the card row. Both printed by generate_rig_parts.py when it builds the harness.
        static readonly Vector3 HarnessSize = new Vector3(0.559f, 0.126f, 0.185f);
        static readonly Vector3 HarnessFromCardRow = new Vector3(0.006f, 0.0753f, 0.1834f);

        // How high the frame's two levels are above the bench. The lower one carries the
        // motherboard and supplies on its rails; the cards stand on risers on the upper one.
        const float LowerLevel = 0.020f;
        const float CardTier = 0.18f;
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
            // Your own room, if you've saved one with OCS > Save Environment As Prefab.
            // Otherwise a default dim server room. Either way, rebuilding keeps the look.
            bool customRoom = PlaceEnvironment();

            Camera cam = Camera.main;
            if (cam != null)
            {
                // Standing eye height, 1.15 m from the bench, with a wider view so the tray
                // and frame stand stay in frame. The XR rig replaces this camera later.
                cam.transform.position = new Vector3(0f, 1.6f, -0.35f);
                cam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
                cam.fieldOfView = 70f;
                cam.name = "Main Camera (flat preview, XR rig replaces this)";
            }

            // The template's light casts realtime shadows. CLAUDE.md: none until profiled.
            // Dimmed and cooled so the room reads as indoors and the LEDs stand out.
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.shadows = LightShadows.None;
                if (!customRoom)
                {
                    sun.intensity = 1.0f;
                    sun.color = new Color(0.85f, 0.9f, 1f);
                    sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                }
            }

            // ---------- bench and rig root ----------
            GameObject rigRoot = new GameObject("RigRoot");
            rigRoot.transform.position = RigPosition;

            GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Bench";
            bench.transform.SetParent(rigRoot.transform, false);
            bench.transform.localScale = new Vector3(1.2f, 0.06f, 0.6f);
            bench.transform.localPosition = new Vector3(0f, BenchTop - 0.03f, 0f);
            Paint(bench, "Plinth");

            GameObject benchBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            benchBase.name = "Bench Base";
            benchBase.transform.position = new Vector3(RigPosition.x, (BenchTopWorld - 0.06f) / 2f, RigPosition.z);
            benchBase.transform.localScale = new Vector3(1.1f, BenchTopWorld - 0.06f, 0.5f);
            Paint(benchBase, "PlinthBase");

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
            Transform frameStand;
            Transform trayOrigin = LayOutTray(sequence, partObjects, parts.transform, out frameStand);
            Transform trayStand = trayOrigin;

            // ---------- sockets ----------
            var bindings = new List<AssemblyPartBinding>(sequence.StepCount);
            var seen = new Dictionary<PartKind, int>();
            foreach (AssemblyStep step in sequence.steps)
            {
                int nth;
                seen.TryGetValue(step.kind, out nth);
                seen[step.kind] = nth + 1;
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
                    s.transform.localPosition = SocketFor(step, nth, layout);
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

            // Each node is a dark panel turned to face the viewer: its name, a caption under
            // it saying what happened there, and a light along its bottom edge that comes
            // up while the request is there. The empty parent is what SystemViewDisplay
            // moves.
            var markers = new Transform[graph.NodeCount];
            var accents = new Renderer[graph.NodeCount];
            var captions = new TextMesh[graph.NodeCount];
            for (int i = 0; i < graph.NodeCount; i++)
            {
                SystemNode node = graph.nodes[i];

                GameObject marker = new GameObject("Node " + node.nodeId);
                marker.transform.SetParent(systemView.transform, false);
                marker.transform.localPosition = node.position;
                marker.transform.rotation = FacingViewer(marker.transform.position);
                markers[i] = marker.transform;

                Vector3 panel = PanelSize * ViewScale;
                Child(marker, "Panel", PrimitiveType.Cube, Vector3.zero, panel, "NodePanel");
                GameObject accent = Child(marker, "Accent", PrimitiveType.Cube,
                                          new Vector3(0f, -panel.y / 2f + 0.006f, -panel.z / 2f - 0.002f),
                                          new Vector3(panel.x - 0.04f, 0.009f, 0.003f), "NodeAccent");
                accents[i] = accent.GetComponent<Renderer>();
                // The accent's glow: a soft line of light spilling past the panel's edge.
                GlowQuad("Accent Glow", marker.transform, new Vector3(0f, -panel.y / 2f + 0.006f, -panel.z / 2f - 0.004f),
                         new Vector3(panel.x * 1.1f, 0.09f, 1f), "GlowBeamSoft");

                float front = -panel.z / 2f - 0.003f;
                Text("Name", marker.transform, new Vector3(0f, 0.03f * ViewScale, front), NodeNameHeight * ViewScale,
                     TextAnchor.MiddleCenter, TextColour, node.DisplayLabel);
                captions[i] = Text("Caption", marker.transform, new Vector3(0f, -0.004f * ViewScale, front),
                                   NodeCaptionHeight * ViewScale, TextAnchor.UpperCenter, CaptionColour);
            }

            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = "Pulse";
            pulse.transform.SetParent(systemView.transform, false);
            pulse.transform.localScale = Vector3.one * (0.04f * ViewScale);
            RemoveCollider(pulse);
            Paint(pulse, "Pulse");
            // A halo round the pulse, so it reads as a ball of light rather than a bead.
            GameObject halo = GlowQuad("Halo", pulse.transform, Vector3.zero, Vector3.one * (0.7f / (0.04f * ViewScale)),
                                       "GlowBlobCyan");
            halo.AddComponent<FaceCamera>().tilt = true;

            // A short fading trail, so the request reads as something travelling rather than
            // a dot teleporting between panels. Width is in world metres.
            TrailRenderer trail = pulse.AddComponent<TrailRenderer>();
            trail.time = 0.55f;
            trail.widthMultiplier = 0.06f;
            trail.minVertexDistance = 0.01f;
            trail.startColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f);
            trail.endColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            if (Materials.ContainsKey("GlowBeam")) trail.sharedMaterial = Materials["GlowBeam"];

            SystemViewDisplay sysView = systemView.AddComponent<SystemViewDisplay>();
            sysView.player = player;
            sysView.graph = graph;
            sysView.nodeMarkers = markers;
            sysView.pulse = pulse.transform;
            sysView.busyLift = 0f;              // the accent light says busy; nothing moves
            sysView.nodeAccents = accents;
            sysView.pulseOffset = new Vector3(0f, 0f, 0.03f);

            HopCaptions hopCaptions = systemView.AddComponent<HopCaptions>();
            hopCaptions.player = player;
            hopCaptions.graph = graph;
            hopCaptions.captions = captions;

            // ---------- how the software works ----------
            BuildBeams(trace, graph, systemView, sysView, cardDisplay, player);
            BuildCardReadout(cardDisplay, player);
            GameObject dashboard = BuildDashboard(trace, player, out GameObject timeline);
            BuildPartLabels(sequence, partObjects, assembly);
            GameObject answer = BuildAnswerPanel(player);
            BuildTokenStream(player, cardDisplay, answer.transform.Find("Response"));

            // ---------- narration ----------
            TextMesh narrationText = Text("Narration", dashboard.transform, new Vector3(0f, 0.035f, 0f), NarrationHeight,
                                          TextAnchor.MiddleCenter, TextColour);

            NarrationDirector director = experience.AddComponent<NarrationDirector>();
            director.track = track;
            director.tracePlayer = player;
            director.assemblyPlayer = assembly;
            director.narrateHops = true;

            NarrationLabel narrationLabel = narrationText.gameObject.AddComponent<NarrationLabel>();
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

            // Only what matters now is on stage. Once the rig is built the empty tray and
            // frame stand sink into the floor; as it powers on the system view grows out
            // of the space above it; the timeline arrives with the request.
            StageDirector stage = experience.AddComponent<StageDirector>();
            stage.sequencer = sequencer;
            stage.cues = new[]
            {
                new StageCue { target = trayStand, act = Act.PowerOn, move = StageMove.Sink, delay = 0.3f, duration = 2.5f, depth = 1f },
                new StageCue { target = frameStand, act = Act.PowerOn, move = StageMove.Sink, delay = 0.6f, duration = 2.5f, depth = 1f },
                new StageCue { target = systemView.transform, act = Act.PowerOn, move = StageMove.Appear, delay = 1.8f, duration = 1.4f },
                new StageCue { target = timeline.transform, act = Act.Request, move = StageMove.Appear, delay = 0f, duration = 0.6f },
                new StageCue { target = answer.transform, act = Act.Request, move = StageMove.Appear, delay = 0f, duration = 0.8f },
            };

            // ---------- the hall reacting ----------
            WorldPulseDriver pulseDriver = experience.AddComponent<WorldPulseDriver>();
            pulseDriver.sequencer = sequencer;
            pulseDriver.player = player;
            pulseDriver.centre = rigRoot.transform;

            BuildBursts(experience, assembly, sequencer, player, cardDisplay, sysView, graph);

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

            string room = customRoom ? "your Environment prefab" : "the default room";
            string art = modelsUsed > 0
                ? modelsUsed + " parts from " + ModelsDir
                : "placeholder parts (run tools/blender/generate_rig_parts.py for models)";
            Debug.Log($"[SceneBuilder] Built {ScenePath}: {layout.cardCount} cards, " +
                      $"{sequence.StepCount} assembly steps, {graph.NodeCount} system nodes, {art}, {room}. " +
                      "Press Play to watch the arc on a flat screen.");
        }

        // ------------------------------------------------------ software visuals

        const int FontSize = 100;

        /// <summary>
        /// A world space text label, sized by line height in metres. TextMesh brings its own
        /// MeshRenderer. A large font size keeps the glyphs sharp; the scale brings it down.
        /// </summary>
        static TextMesh Text(string name, Transform parent, Vector3 position, float lineHeight,
                             TextAnchor anchor, Color colour, string content = "")
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            // A line is fontSize x characterSize / 10 units tall at scale 1.
            go.transform.localScale = Vector3.one * (lineHeight / (FontSize * 0.5f * 0.1f));
            TextMesh t = go.AddComponent<TextMesh>();
            t.text = content;
            t.anchor = anchor;
            t.alignment = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left
                : anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight
                    ? TextAlignment.Right
                    : TextAlignment.Center;
            t.fontSize = FontSize;
            t.characterSize = 0.5f;
            t.color = colour;
            return t;
        }

        /// <summary>A rotation whose forward points away from the viewer, so text on it reads.</summary>
        static Quaternion FacingViewer(Vector3 position)
        {
            return Quaternion.LookRotation(position - ViewerEye, Vector3.up);
        }

        /// <summary>A two point line. Lines need their own GameObject: one renderer each.</summary>
        static LineRenderer Line(string name, Transform parent, float width, Color colour)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.startColor = colour;
            line.endColor = colour;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (Materials.ContainsKey("GlowBeam")) line.sharedMaterial = Materials["GlowBeam"];
            return line;
        }

        const string MeshesDir = "Assets/Art/Meshes";
        static Mesh _glowQuadMesh;

        /// <summary>
        /// A unit quad facing -Z with white vertex colours. The glow shader multiplies by
        /// vertex colour, and Unity's own quad has none, so glow quads use this one.
        /// </summary>
        static Mesh GlowQuadMesh()
        {
            if (_glowQuadMesh != null) return _glowQuadMesh;
            string path = MeshesDir + "/GlowQuad.asset";
            _glowQuadMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (_glowQuadMesh != null) return _glowQuadMesh;

            EnsureFolder(MeshesDir);
            var mesh = new Mesh { name = "GlowQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            _glowQuadMesh = mesh;
            return mesh;
        }

        /// <summary>A glow quad: soft additive light in the shape its material says.</summary>
        static GameObject GlowQuad(string name, Transform parent, Vector3 localPosition, Vector3 localScale, string material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = GlowQuadMesh();
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Material mat;
            if (Materials.TryGetValue(material, out mat)) r.sharedMaterial = mat;
            return go;
        }

        /// <summary>A flat ring of light, lying on the floor or hanging in the air.</summary>
        static LineRenderer LightRing(string name, Transform parent, Vector3 centre, float radius, float width, Color colour)
        {
            const int points = 96;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat
            LineRenderer ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = points;
            ring.widthMultiplier = width;
            ring.alignment = LineAlignment.TransformZ;
            for (int i = 0; i < points; i++)
            {
                float a = i * Mathf.PI * 2f / points;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            ring.startColor = ring.endColor = colour;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            if (Materials.ContainsKey("GlowBeam")) ring.sharedMaterial = Materials["GlowBeam"];
            return ring;
        }

        /// <summary>A particle system set up the way every one here wants it.</summary>
        static ParticleSystem Particles(string name, Transform parent, string material, int max,
                                        float lifetime, MinMaxCurve speed, MinMaxCurve size, Color colour)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = colour;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchical;
            var emission = ps.emission;
            emission.rateOverTime = 0f;

            // Fade in fast, fade out slowly.
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = fade;

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Material mat;
            if (Materials.TryGetValue(material, out mat)) r.sharedMaterial = mat;
            return ps;
        }

        /// <summary>
        /// A beam for every pair of nodes the request travels between, worked out from the
        /// trace now, plus the beam from the rig's node down to the working card. The
        /// beams live under the system view so they grow in with it.
        /// </summary>
        static void BuildBeams(TextAsset trace, SystemGraph graph, GameObject systemView, SystemViewDisplay view,
                               RigCardDisplay rig, TracePlayer player)
        {
            var from = new List<int>();
            var to = new List<int>();
            Trace parsed;
            string error;
            if (TraceLoader.TryParse(trace.text, out parsed, out error))
            {
                var route = new List<int>();
                foreach (Hop hop in TraceLoader.DrawableHops(parsed))
                {
                    SystemNode node = graph.Resolve(hop);
                    route.Add(node == null ? -1 : graph.IndexOf(node));
                }
                BeamGlowState.EdgesFromRoute(route, from, to);
            }

            // The rigs the scheduler could have picked and didn't get a faint link too, so
            // "least busy rig selected" has something to have been chosen over. Nothing
            // ever travels it, so it never lights.
            int scheduler = -1;
            for (int i = 0; i < graph.NodeCount; i++)
                if (graph.nodes[i].hopType == HopType.Scheduler) scheduler = i;
            if (scheduler >= 0)
            {
                for (int i = 0; i < graph.NodeCount; i++)
                {
                    if (graph.nodes[i].hopType != HopType.Unknown || !graph.nodes[i].nodeId.StartsWith("rig")) continue;
                    from.Add(scheduler);
                    to.Add(i);
                }
            }

            var idle = new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f);
            var lines = new LineRenderer[from.Count];
            for (int e = 0; e < from.Count; e++)
                lines[e] = Line("Beam " + graph.nodes[from[e]].nodeId + " - " + graph.nodes[to[e]].nodeId,
                                systemView.transform, 0.008f, idle);

            // Narrow where it leaves the panel, full width where it lands on the card, so it
            // reads as light falling onto the card rather than a bar joining two things.
            LineRenderer cardBeam = Line("Card Beam", systemView.transform, 0f, new Color(Amber.r, Amber.g, Amber.b, 0f));
            cardBeam.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(1f, 1f));
            cardBeam.enabled = false;

            SystemBeams beams = systemView.AddComponent<SystemBeams>();
            beams.player = player;
            beams.graph = graph;
            beams.view = view;
            beams.rig = rig;
            beams.edgeFrom = from.ToArray();
            beams.edgeTo = to.ToArray();
            beams.lines = lines;
            beams.cardBeam = cardBeam;
            beams.idleColor = idle;
            beams.litColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f);
            beams.idleWidth = 0.008f;
            beams.litWidth = 0.05f;
            beams.cardBeamColor = new Color(Amber.r, Amber.g, Amber.b, 0.85f);
            beams.cardBeamWidth = 0.12f;
            beams.cardBeamDrop = PanelSize.y * ViewScale / 2f;

            // The glow on the working card, as bright as the card is busy.
            GameObject halo = GlowQuad("Card Halo", null, Vector3.zero, Vector3.one, "GlowBlob");
            halo.AddComponent<FaceCamera>().tilt = true;
            beams.cardHalo = halo.GetComponent<Renderer>();
            beams.haloSize = 0.7f;
        }

        /// <summary>The readout beside whichever card is working.</summary>
        static void BuildCardReadout(RigCardDisplay rig, TracePlayer player)
        {
            TextMesh text = Text("Card Readout", null, Vector3.zero, ReadoutHeight, TextAnchor.LowerRight, ReadoutColour);
            text.gameObject.AddComponent<FaceCamera>();
            CardTelemetryLabel label = text.gameObject.AddComponent<CardTelemetryLabel>();
            label.player = player;
            label.rig = rig;
            label.text = text;
            label.offset = ReadoutOffset;
        }

        /// <summary>
        /// The prompt, and the answer typing itself out as the card generates it: a dark
        /// panel right of the rig with a cyan and an amber edge, the colours of the two
        /// halves of the story.
        /// </summary>
        static GameObject BuildAnswerPanel(TracePlayer player)
        {
            GameObject root = new GameObject("Answer Panel");
            root.transform.position = AnswerPosition;
            root.transform.rotation = FacingViewer(AnswerPosition);

            float w = AnswerSize.x, h = AnswerSize.y;
            Child(root, "Panel", PrimitiveType.Cube, Vector3.zero, new Vector3(w, h, 0.01f), "NodePanel");
            float front = -0.008f;
            Child(root, "Top Edge", PrimitiveType.Cube, new Vector3(0f, h / 2f - 0.003f, front), new Vector3(w, 0.006f, 0.003f), "NodeAccentLit");
            Child(root, "Bottom Edge", PrimitiveType.Cube, new Vector3(0f, -h / 2f + 0.003f, front), new Vector3(w, 0.006f, 0.003f), "EdgeAmber");
            GlowQuad("Top Glow", root.transform, new Vector3(0f, h / 2f, front - 0.002f), new Vector3(w * 1.1f, 0.12f, 1f), "GlowBeamSoft");

            float left = -w / 2f + 0.05f;
            Text("Prompt Label", root.transform, new Vector3(left, h / 2f - 0.04f, front), 0.022f, TextAnchor.UpperLeft,
                 new Color(Cyan.r, Cyan.g, Cyan.b), "PROMPT");
            TextMesh prompt = Text("Prompt", root.transform, new Vector3(left, h / 2f - 0.07f, front), 0.03f,
                                   TextAnchor.UpperLeft, TextColour);
            Text("Response Label", root.transform, new Vector3(left, h / 2f - 0.17f, front), 0.022f, TextAnchor.UpperLeft,
                 Amber, "RESPONSE  ·  RIG 2, GPU 3");
            TextMesh response = Text("Response", root.transform, new Vector3(left, h / 2f - 0.2f, front), 0.03f,
                                     TextAnchor.UpperLeft, ReadoutColour);

            AnswerPanel panel = root.AddComponent<AnswerPanel>();
            panel.player = player;
            panel.prompt = prompt;
            panel.response = response;
            panel.charsPerLine = 40;
            panel.maxLines = 6;
            return root;
        }

        /// <summary>Glowing tokens streaming off the working card into the answer panel.</summary>
        static void BuildTokenStream(TracePlayer player, RigCardDisplay rig, Transform target)
        {
            ParticleSystem tokens = Particles("Token Stream", null, "GlowBlob", 400, 0.9f,
                                              1.5f, new MinMaxCurve(0.025f, 0.045f), new Color(1f, 0.62f, 0.2f, 1f));
            var shape = tokens.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.02f;
            var size = tokens.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.3f)));

            TokenStream stream = tokens.gameObject.AddComponent<TokenStream>();
            stream.player = player;
            stream.rig = rig;
            stream.tokens = tokens;
            stream.target = target;
        }

        /// <summary>Rings and sparks where things happen: parts seating, power, the answer.</summary>
        static void BuildBursts(GameObject experience, AssemblyPlayer assembly, ExperienceSequencer sequencer,
                                TracePlayer player, RigCardDisplay rig, SystemViewDisplay view, SystemGraph graph)
        {
            GameObject root = new GameObject("Impact Bursts");
            var rings = new Renderer[8];
            for (int i = 0; i < rings.Length; i++)
            {
                GameObject ring = GlowQuad("Ring " + i, root.transform, Vector3.zero, Vector3.one, "GlowRing");
                ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);          // flat, rushing outward
                rings[i] = ring.GetComponent<Renderer>();
            }

            ParticleSystem sparks = Particles("Sparks", root.transform, "GlowBlob", 600, 0.6f,
                                              new MinMaxCurve(0.5f, 1.8f), new MinMaxCurve(0.012f, 0.024f), Color.white);
            var main = sparks.main;
            main.gravityModifier = 0.35f;
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            ImpactBursts bursts = experience.AddComponent<ImpactBursts>();
            bursts.assembly = assembly;
            bursts.sequencer = sequencer;
            bursts.player = player;
            bursts.rig = rig;
            bursts.view = view;
            bursts.graph = graph;
            bursts.rings = rings;
            bursts.sparks = sparks;
        }

        /// <summary>
        /// The strip over the front of the bench: the narration line goes on top (added by
        /// the caller), the "where the time goes" bar under it. The bar is one segment per
        /// hop, sized from the trace now so the proportions are the real ones: a sliver of
        /// cyan for everything the software does, and a long amber run for the GPU.
        /// </summary>
        static GameObject BuildDashboard(TextAsset trace, TracePlayer player, out GameObject timelineRoot)
        {
            GameObject dashboard = new GameObject("Dashboard");
            dashboard.transform.position = DashboardPosition;
            dashboard.transform.rotation = FacingViewer(DashboardPosition);

            timelineRoot = new GameObject("Request Timeline");
            timelineRoot.transform.SetParent(dashboard.transform, false);
            timelineRoot.transform.localPosition = new Vector3(0f, -0.012f, 0f);

            Trace parsed;
            string error;
            if (!TraceLoader.TryParse(trace.text, out parsed, out error)) return dashboard;
            List<Hop> hops = TraceLoader.DrawableHops(parsed);

            var durations = new float[hops.Count];
            float total = 0f, generating = 0f;
            for (int i = 0; i < hops.Count; i++)
            {
                durations[i] = hops[i].DurationMs;
                total += durations[i];
                if (hops[i].Type == HopType.Model) generating += durations[i];
            }

            float[] widths = TimelineLayout.Widths(durations, TimelineWidth, 0.012f);
            float[] lefts = TimelineLayout.Offsets(widths);
            float x0 = -TimelineWidth / 2f;

            var segments = new Renderer[hops.Count];
            const float gap = 0.002f;
            for (int i = 0; i < hops.Count; i++)
            {
                GameObject seg = Child(timelineRoot, "Segment " + i + " " + hops[i].hop, PrimitiveType.Cube,
                                       new Vector3(x0 + lefts[i] + widths[i] / 2f, 0f, 0f),
                                       new Vector3(Mathf.Max(0.002f, widths[i] - gap), 0.008f, 0.004f), "TimelineSegment");
                segments[i] = seg.GetComponent<Renderer>();
                lefts[i] += x0;
            }

            GameObject head = Child(timelineRoot, "Playhead", PrimitiveType.Cube, Vector3.zero,
                                    new Vector3(0.003f, 0.024f, 0.006f), "Pulse");

            Text("Everything Else", timelineRoot.transform, new Vector3(x0, -0.012f, 0f), DashboardCaptionHeight,
                 TextAnchor.UpperLeft, new Color(Cyan.r, Cyan.g, Cyan.b) * 0.85f,
                 "Routing, scheduling, network  " + TelemetryText.Duration(total - generating));
            Text("Generating", timelineRoot.transform, new Vector3(-x0, -0.012f, 0f), DashboardCaptionHeight,
                 TextAnchor.UpperRight, Amber, "GPU generating  " + TelemetryText.Duration(generating));

            RequestTimeline timeline = timelineRoot.AddComponent<RequestTimeline>();
            timeline.player = player;
            timeline.segments = segments;
            timeline.segmentLeft = lefts;
            timeline.segmentWidth = widths;
            timeline.playhead = head.transform;
            timeline.pendingColor = new Color(0.04f, 0.05f, 0.06f);
            // No bloom or tonemapping on Quest, so emission clips at 1 per channel. These are
            // picked to land on cyan and amber after clipping, not on white and yellow.
            timeline.activeColor = new Color(0.45f, 1.2f, 1.6f);
            timeline.doneColor = new Color(0.15f, 0.45f, 0.65f);
            timeline.gpuColor = new Color(1.3f, 0.62f, 0.15f);
            return dashboard;
        }

        /// <summary>
        /// A label over every part the sequence asks for, shown as the part seats and gone
        /// again a few seconds later, so the finished rig is clean. Cards alternate heights,
        /// since at 9 cm apart their labels would otherwise touch.
        /// </summary>
        static void BuildPartLabels(AssemblySequence sequence, Dictionary<string, GameObject> partObjects,
                                    AssemblyPlayer assembly)
        {
            GameObject root = new GameObject("Part Labels");
            foreach (AssemblyStep step in sequence.steps)
            {
                if (!step.labelled) continue;
                GameObject part;
                if (!partObjects.TryGetValue(step.stepId, out part)) continue;

                Vector3 size = SizeFor(step);
                Vector3 offset = step.labelOffset;
                if (offset == Vector3.zero)
                {
                    offset = new Vector3(0f, size.y / 2f + 0.07f, 0f);
                    if (step.kind == PartKind.Gpu && step.cardIndex % 2 == 1) offset.y += 0.04f;
                }

                TextMesh text = Text("Label " + step.stepId, root.transform, Vector3.zero, PartLabelHeight,
                                     TextAnchor.LowerCenter, TextColour, step.LabelText);
                text.gameObject.AddComponent<FaceCamera>();
                LineRenderer leader = Line("Leader", text.transform, 0.001f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f));

                PartLabel label = text.gameObject.AddComponent<PartLabel>();
                label.assembly = assembly;
                label.stepId = step.stepId;
                label.target = part.transform;
                label.offset = offset;
                label.hideAfter = step.labelSeconds;
                label.anchorHeight = size.y / 2f;
                label.textRenderer = text.GetComponent<Renderer>();
                label.leader = leader;
            }
        }

        // ------------------------------------------------------------------- room

        /// <summary>
        /// Drops in Assets/Prefabs/Environment.prefab if it exists, otherwise builds the
        /// default room. Returns true when the prefab was used.
        /// </summary>
        static bool PlaceEnvironment()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath);
            if (prefab != null)
            {
                var env = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                RoomAtmosphere atmosphere = env.GetComponent<RoomAtmosphere>();
                if (atmosphere != null) atmosphere.Apply();
                return true;
            }

            BuildDefaultRoom();
            return false;
        }

        // The hall, in world metres. The stage is its focus: racks line both sides, a sign
        // on each end wall, the ceiling well above everything.
        const float HallHalfWidth = 10.5f, HallHeight = 7f, HallNear = -7f, HallFar = 26f;
        const float RackRowX = 4.6f, OuterRowX = 7.3f;
        const float RackPitch = 0.62f;

        /// <summary>
        /// The data hall around the rig. The rig stands on a lit dais in the middle aisle;
        /// rows of server racks run the length of the hall on both sides, their lights
        /// asleep until the rig powers on and the wave reaches them; a glowing grid floor,
        /// pillars, cable trays, light strips overhead, dust in the air, fog that swallows
        /// the far end, and a sign on each end wall.
        ///
        /// All of it is emissive surfaces and shaders: no lights, no shadows. It is marked
        /// static, so the racks batch into a few draw calls. Everything sits under one
        /// "Environment" object so it can be saved as a prefab and edited by hand.
        /// </summary>
        static void BuildDefaultRoom()
        {
            GameObject env = new GameObject("Environment");
            RoomAtmosphere atmosphere = env.AddComponent<RoomAtmosphere>();
            atmosphere.ambient = new Color(0.2f, 0.21f, 0.25f);
            atmosphere.background = new Color(0.016f, 0.022f, 0.034f);
            atmosphere.fog = true;
            atmosphere.fogColor = atmosphere.background;
            atmosphere.fogStart = 6f;
            atmosphere.fogEnd = 30f;
            atmosphere.Apply();

            float zMid = (HallNear + HallFar) / 2f, depth = HallFar - HallNear;
            var rng = new System.Random(2);            // the same hall on every build

            // Floor: the grid, and the dark dais the rig and viewer stand on.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0f, zMid);
            floor.transform.localScale = new Vector3(HallHalfWidth * 2f / 10f, 1f, depth / 10f);
            Paint(floor, "GridFloor");                // keeps its collider, for teleporting later
            floor.isStatic = true;

            Vector3 stage = new Vector3(RigPosition.x, 0f, 0.5f);
            GameObject dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "Dais";
            dais.transform.SetParent(env.transform, false);
            dais.transform.localPosition = stage + new Vector3(0f, 0.004f, 0f);
            dais.transform.localScale = new Vector3(4.6f, 0.004f, 4.6f);
            RemoveCollider(dais);
            Paint(dais, "Dais");
            dais.isStatic = true;
            LightRing("Dais Edge", env.transform, stage + new Vector3(0f, 0.012f, 0f), 2.3f, 0.025f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f));
            LightRing("Dais Outer", env.transform, stage + new Vector3(0f, 0.012f, 0f), 2.55f, 0.01f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
            LightRing("Bench Ring", env.transform, new Vector3(RigPosition.x, 0.012f, RigPosition.z), 0.95f, 0.01f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
            // A ring of light hanging over the rig, like a lamp over an operating table.
            LightRing("Halo", env.transform, new Vector3(RigPosition.x, 3.3f, RigPosition.z), 1.4f, 0.035f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f));
            LightRing("Halo Inner", env.transform, new Vector3(RigPosition.x, 3.3f, RigPosition.z), 1.25f, 0.012f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f));

            // Walls and ceiling, near black, mostly lost in the fog.
            RoomSlab(env, "Ceiling", new Vector3(0f, HallHeight + 0.05f, zMid), new Vector3(HallHalfWidth * 2f, 0.1f, depth), "RoomWall");
            RoomSlab(env, "Wall Far", new Vector3(0f, HallHeight / 2f, HallFar), new Vector3(HallHalfWidth * 2f, HallHeight, 0.1f), "RoomWall");
            RoomSlab(env, "Wall Near", new Vector3(0f, HallHeight / 2f, HallNear), new Vector3(HallHalfWidth * 2f, HallHeight, 0.1f), "RoomWall");
            RoomSlab(env, "Wall Left", new Vector3(-HallHalfWidth, HallHeight / 2f, zMid), new Vector3(0.1f, HallHeight, depth), "RoomWall");
            RoomSlab(env, "Wall Right", new Vector3(HallHalfWidth, HallHeight / 2f, zMid), new Vector3(0.1f, HallHeight, depth), "RoomWall");

            // Rack rows. The inner rows face the aisle; the outer ones show through the
            // gaps. Every eighth rack is left out, a cross aisle, so the rows read as
            // rows of cabinets and not as walls.
            foreach (float side in new[] { -1f, 1f })
            {
                RackRow(env, rng, side * RackRowX, -side, HallNear + 2f, HallFar - 4f, true);
                RackRow(env, rng, side * OuterRowX, -side, HallNear + 2f, HallFar - 4f, false);
                // Cable trays over the inner rows.
                RoomSlab(env, "Cable Tray", new Vector3(side * RackRowX, 2.75f, (HallNear + HallFar - 2f) / 2f),
                         new Vector3(0.45f, 0.06f, HallFar - HallNear - 6f), "FrameBlack");
            }
            // A row across the far end, facing back down the hall.
            for (float x = -3.4f; x <= 3.41f; x += RackPitch)
                Rack(env, rng, new Vector3(x, 0f, HallFar - 3f), Quaternion.identity, true);

            // Pillars along the walls, each with a strip of light on its inner face.
            for (float z = HallNear + 3f; z < HallFar - 1f; z += 6f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side * (HallHalfWidth - 0.6f);
                    RoomSlab(env, "Pillar", new Vector3(x, HallHeight / 2f, z), new Vector3(0.6f, HallHeight, 0.6f), "Pillar");
                    RoomSlab(env, "Pillar Light", new Vector3(x - side * 0.31f, HallHeight / 2f, z), new Vector3(0.02f, HallHeight * 0.85f, 0.05f), "StripLight");
                }
                // Beams across the ceiling between them.
                RoomSlab(env, "Ceiling Beam", new Vector3(0f, HallHeight - 0.3f, z), new Vector3(HallHalfWidth * 2f, 0.4f, 0.4f), "Pillar");
            }

            // Light strips running the length of the ceiling.
            foreach (float x in new[] { -2.6f, 0f, 2.6f })
                RoomSlab(env, "Ceiling Strip", new Vector3(x, HallHeight - 0.52f, zMid), new Vector3(0.06f, 0.03f, depth - 2f), "StripLight");

            // Signs. TextMesh ignores fog, so they stay bright at the far end like lit signage.
            TextMesh far = Text("Sign Far", env.transform, new Vector3(0f, 5.3f, HallFar - 0.1f), 0.9f,
                                TextAnchor.MiddleCenter, new Color(0.75f, 0.92f, 1f), "OCS  INTELLIGENCE  INFRASTRUCTURE");
            RoomSlab(env, "Sign Far Line", new Vector3(0f, 4.55f, HallFar - 0.1f), new Vector3(14f, 0.04f, 0.02f), "StripLight");
            TextMesh near = Text("Sign Near", env.transform, new Vector3(0f, 5.0f, HallNear + 0.1f), 0.7f,
                                 TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.55f), "RIG 2   ·   8 × GTX 1070");
            near.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            RoomSlab(env, "Sign Near Line", new Vector3(0f, 4.4f, HallNear + 0.1f), new Vector3(10f, 0.04f, 0.02f), "EdgeAmber");

            // Dust hanging in the air round the stage, catching the light.
            ParticleSystem dust = Particles("Dust", env.transform, "GlowBlob", 350, 16f,
                                            0.03f, new MinMaxCurve(0.008f, 0.02f), new Color(0.6f, 0.85f, 1f, 0.45f));
            dust.transform.localPosition = new Vector3(0f, 2.6f, 4f);
            var dustMain = dust.main;
            dustMain.prewarm = true;
            var dustEmission = dust.emission;
            dustEmission.rateOverTime = 22f;
            var dustShape = dust.shape;
            dustShape.enabled = true;
            dustShape.shapeType = ParticleSystemShapeType.Box;
            dustShape.scale = new Vector3(16f, 5f, 18f);

            foreach (Transform t in env.GetComponentsInChildren<Transform>())
                if (t.GetComponent<ParticleSystem>() == null && t.GetComponent<LineRenderer>() == null) t.gameObject.isStatic = true;
        }

        /// <summary>A row of racks along z at x, fronts facing faceX (+1 or -1).</summary>
        static void RackRow(GameObject env, System.Random rng, float x, float faceX, float zStart, float zEnd, bool bothFaces)
        {
            int n = 0;
            for (float z = zStart; z <= zEnd; z += RackPitch, n++)
            {
                if (n % 8 == 7) continue;                                   // a cross aisle
                // The rack's face is its local -z. Turning -90 about Y points that at +x.
                Quaternion face = Quaternion.Euler(0f, faceX > 0f ? -90f : 90f, 0f);
                Rack(env, rng, new Vector3(x, 0f, z), face, bothFaces);
            }
        }

        /// <summary>
        /// One rack: a dark cabinet with a lit face front and back. The face is one quad;
        /// the Rack Lights shader draws the servers and their blinking lights on it.
        /// </summary>
        static void Rack(GameObject env, System.Random rng, Vector3 position, Quaternion facing, bool bothFaces)
        {
            float height = 2.0f + 0.1f * rng.Next(0, 4);
            const float width = 0.6f, depth = 1.0f;
            GameObject rack = new GameObject("Rack");
            rack.transform.SetParent(env.transform, false);
            rack.transform.localPosition = position;
            rack.transform.localRotation = facing;

            Child(rack, "Cabinet", PrimitiveType.Cube, new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), "RackBody");
            // Facing -z locally, the way Unity's quad faces, then turned with the rack.
            Child(rack, "Face", PrimitiveType.Quad, new Vector3(0f, height / 2f, -depth / 2f - 0.003f),
                  new Vector3(width - 0.04f, height - 0.08f, 1f), "RackLights");
            if (bothFaces)
            {
                GameObject back = Child(rack, "Back", PrimitiveType.Quad, new Vector3(0f, height / 2f, depth / 2f + 0.003f),
                                        new Vector3(width - 0.04f, height - 0.08f, 1f), "RackLights");
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        static void RoomSlab(GameObject parent, string name, Vector3 position, Vector3 size, string material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            RemoveCollider(go);
            Paint(go, material);
        }

        /// <summary>
        /// Saves the scene's Environment object as a prefab. After this, rebuilding the scene
        /// uses your version of the room, so changes you make by hand are kept.
        /// </summary>
        [MenuItem("OCS/Save Environment As Prefab")]
        public static void SaveEnvironmentPrefab()
        {
            GameObject env = GameObject.Find("Environment");
            if (env == null)
            {
                Debug.LogError("[SceneBuilder] No object named Environment in the open scene. " +
                               "Run OCS > Build Experience Scene first.");
                return;
            }

            EnsureFolder("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAssetAndConnect(env, EnvironmentPrefabPath, InteractionMode.UserAction);
            Debug.Log("[SceneBuilder] Saved " + EnvironmentPrefabPath +
                      ". Rebuilding the scene will now use it instead of the default room.");
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
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    FaceFront(instance);
                    return instance;
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
                case PartKind.Cpu:         return Block("CPU Cooler", CoolerSize, "Steel");
                case PartKind.Ram:         return Block("RAM", RamSize, "Shroud");
                case PartKind.Cable:       return Block("PCIe Cables", HarnessSize, "BlackPlastic");
                case PartKind.Fan:         return Block("Fans", new Vector3(0.03f, 0.12f, 0.12f), "BlackPlastic");
                default:                   return Block("Part", Vector3.one * 0.1f, "Shroud");
            }
        }

        /// <summary>
        /// Models mark their front with an empty named Front: a card's bracket, the lettered
        /// side of a supply. Turns the part round if that points away from the viewer, so
        /// the parts come out the right way whichever way the FBX axis conversion went.
        /// </summary>
        static void FaceFront(GameObject part)
        {
            Transform front = FindDeep(part.transform, "Front");
            if (front == null) return;
            if (front.position.z - part.transform.position.z <= 0f) return;
            part.transform.rotation = Quaternion.AngleAxis(180f, Vector3.up) * part.transform.rotation;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
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
                case PartKind.Cpu:         return "cpu_cooler";
                case PartKind.Ram:         return "ram_stick";
                case PartKind.Cable:       return "pcie_cables";
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
                case PartKind.Cpu:         return CoolerSize;
                case PartKind.Ram:         return RamSize;
                case PartKind.Cable:       return HarnessSize;
                case PartKind.Fan:         return new Vector3(0.03f, 0.12f, 0.12f);
                default:                   return Vector3.one * 0.1f;
            }
        }

        /// <summary>
        /// Where each part ends up, relative to RigRoot. Parts sit on things: the board and
        /// supplies on the frame's lower rails, the cooler and RAM on the board, the risers
        /// on the upper rail under their cards. nth counts earlier steps of the same kind,
        /// so the second supply goes to the other end of the frame.
        /// </summary>
        static Vector3 SocketFor(AssemblyStep step, int nth, RigLayout layout)
        {
            float lower = BenchTop + LowerLevel;                        // top of the lower rails
            float board = lower + 0.006f + BoardSize.y / 2f;            // on standoffs
            float boardTop = board + BoardSize.y / 2f;
            float cardBottom = layout.firstCardOffset.y - CardSize.y / 2f;
            Vector3 first = layout.GetCardLocalPosition(0);
            Vector3 last = layout.GetCardLocalPosition(layout.cardCount - 1);
            Vector3 rowMiddle = (first + last) / 2f;
            switch (step.kind)
            {
                case PartKind.Chassis:     return new Vector3(0f, BenchTop + FrameSize.y / 2f, 0f);
                case PartKind.Motherboard: return new Vector3(0f, board, 0f);
                // On the CPU socket and in the first RAM slot, as the motherboard model has them.
                case PartKind.Cpu:         return new Vector3(0.02f, boardTop + 0.007f + CoolerSize.y / 2f, 0.05f);
                case PartKind.Ram:         return new Vector3(0.096f, boardTop + 0.004f + RamSize.y / 2f, 0.03f);
                // One at each end, clear of the board and inside the end posts.
                case PartKind.Psu:         return new Vector3(nth % 2 == 0 ? -0.275f : 0.275f, lower + PsuSize.y / 2f, -0.08f);
                case PartKind.Cable:       return rowMiddle + HarnessFromCardRow;
                case PartKind.Fan:         return new Vector3(0f, BenchTop + FrameSize.y + 0.03f, FrameSize.z / 2f);
                case PartKind.Riser:
                    // Under its card's PCIe fingers, which are 40 mm toward the bracket end,
                    // so the card visibly plugs into it.
                    Vector3 card = layout.IsValidIndex(step.cardIndex)
                        ? layout.GetCardLocalPosition(step.cardIndex)
                        : Vector3.zero;
                    return new Vector3(card.x, cardBottom - 0.012f, card.z - 0.04f);
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
                case PartKind.Cpu:
                case PartKind.Ram:
                case PartKind.Fan:         return 0;
                case PartKind.Motherboard:
                case PartKind.Psu:         return 1;
                case PartKind.Gpu:         return 2;
                case PartKind.Cable:       return 3;       // the harness is as wide as the card row
                default:                   return -1;
            }
        }

        /// <summary>
        /// Small parts wait on a table left of the bench, in rows so none overlap. The frame
        /// is too big for the table, so it waits on a stand to the right and slides across
        /// onto the bench at the same height. Returns the tray transform.
        /// </summary>
        static Transform LayOutTray(AssemblySequence sequence, Dictionary<string, GameObject> partObjects, Transform partsParent,
                                    out Transform frameStand)
        {
            const int rows = 4;
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
            Paint(table, "Plinth");
            Pedestal(tray.transform, trayWidth, trayDepth);

            // Its own object, top and pedestal together, so it can sink away as one.
            GameObject standRoot = new GameObject("Frame Stand");
            standRoot.transform.position = FrameStandPosition;
            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Top";
            stand.transform.SetParent(standRoot.transform, false);
            stand.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            stand.transform.localScale = new Vector3(1.05f, 0.06f, 0.6f);
            Paint(stand, "Plinth");
            Pedestal(standRoot.transform, 0.95f, 0.5f);
            frameStand = standRoot.transform;

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

        /// <summary>
        /// A dark block from the floor up to a surface, so tables don't float. A child of the
        /// surface, whose origin is its top centre, so the two move together.
        /// </summary>
        static void Pedestal(Transform top, float width, float depth)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Pedestal";
            block.transform.SetParent(top, false);
            float height = top.position.y - 0.06f;
            block.transform.localPosition = new Vector3(0f, -0.06f - height / 2f, 0f);
            block.transform.localScale = new Vector3(width, height, depth);
            Paint(block, "PlinthBase");
        }

        // ----------------------------------------------------------- placeholders

        /// <summary>Two fans, a shroud, a backplate and the lit logo, named like the model.</summary>
        static GameObject PlaceholderCard()
        {
            GameObject root = new GameObject("GPU (placeholder)");
            Child(root, "Body", PrimitiveType.Cube, new Vector3(-0.002f, 0f, 0f),
                  new Vector3(0.030f, CardSize.y - 0.004f, CardSize.z), "Shroud");
            Child(root, "Backplate", PrimitiveType.Cube, new Vector3(0.0175f, 0f, 0f),
                  new Vector3(0.002f, CardSize.y - 0.012f, CardSize.z - 0.01f), "Backplate");
            // Same names and places as the Blender model: the lit logo along the top edge,
            // toward the bracket end, and two fans on the shroud side.
            Child(root, "LED", PrimitiveType.Cube, new Vector3(-0.004f, CardSize.y / 2f + 0.001f, -0.05f),
                  new Vector3(0.006f, 0.002f, 0.14f), "LED");

            for (int f = 0; f < 2; f++)
            {
                Vector3 fanPos = new Vector3(-0.0185f, 0f, f == 0 ? -0.060f : 0.060f);

                GameObject fan = new GameObject("Fan" + f);
                fan.transform.SetParent(root.transform, false);
                fan.transform.localPosition = fanPos;

                GameObject disc = Child(fan, "Disc", PrimitiveType.Cylinder, Vector3.zero,
                                        new Vector3(0.086f, 0.002f, 0.086f), "FanBlack");
                disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // cylinder axis to X

                // Blades, so the spin is visible. A plain disc spinning looks still.
                for (int b = 0; b < 3; b++)
                {
                    GameObject blade = Child(fan, "Blade", PrimitiveType.Cube, new Vector3(-0.003f, 0f, 0f),
                                             new Vector3(0.002f, 0.078f, 0.012f), "ShroudAccent");
                    blade.transform.localRotation = Quaternion.Euler(60f * b, 0f, 0f);
                }
            }
            return root;
        }

        /// <summary>
        /// The two level frame, as the model has it: posts, a ring of rails at the bottom
        /// and at the card level, supports for the board, the riser rail, the bar the card
        /// brackets screw to, and rails tying the tops of the posts.
        /// </summary>
        static GameObject PlaceholderFrame()
        {
            GameObject root = new GameObject("Frame (placeholder)");
            const float t = 0.02f;
            float w = FrameSize.x, h = FrameSize.y, d = FrameSize.z;
            float yb = -h / 2f + t / 2f, yt = h / 2f - t / 2f;
            float cardBottom = -h / 2f + CardTier;
            float yTier = cardBottom - 0.013f - t / 2f;
            float yBar = cardBottom + CardSize.y - 0.02f;
            float[] xs = { -w / 2f + t / 2f, w / 2f - t / 2f };
            float[] zs = { -d / 2f + t / 2f, d / 2f - t / 2f };

            foreach (float x in xs)
                foreach (float z in zs)
                    Child(root, "Post", PrimitiveType.Cube, new Vector3(x, 0f, z), new Vector3(t, h, t), "FrameBlack");
            foreach (float y in new[] { yb, yTier })
            {
                foreach (float z in zs)
                    Child(root, "Rail", PrimitiveType.Cube, new Vector3(0f, y, z), new Vector3(w - 2f * t, t, t), "FrameBlack");
                foreach (float x in xs)
                    Child(root, "Rail", PrimitiveType.Cube, new Vector3(x, y, 0f), new Vector3(t, t, d - 2f * t), "FrameBlack");
            }
            foreach (float z in new[] { -0.10f, 0.10f })
                Child(root, "Board Rail", PrimitiveType.Cube, new Vector3(0f, yb, z), new Vector3(w - 2f * t, t, t), "FrameBlack");
            Child(root, "Riser Rail", PrimitiveType.Cube, new Vector3(0f, yTier, -0.085f), new Vector3(w - 2f * t, t, t), "FrameBlack");
            Child(root, "Bracket Bar", PrimitiveType.Cube, new Vector3(0f, yBar, zs[0]), new Vector3(w - 2f * t, t, t), "FrameBlack");
            foreach (float x in xs)
                Child(root, "Top Rail", PrimitiveType.Cube, new Vector3(x, yt, 0f), new Vector3(t, t, d - 2f * t), "FrameBlack");
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

            // The models' LED parts come in with a plain imported material. Swap in the
            // shared one with emission switched on, or the glow has nothing to drive.
            var leds = new List<Renderer>();
            for (int i = 0; i < card.transform.childCount; i++)
            {
                Transform child = card.transform.GetChild(i);
                if (!child.name.StartsWith("LED")) continue;
                Renderer r = child.GetComponent<Renderer>();
                if (r == null) continue;
                r.sharedMaterial = Materials["LED"];
                leds.Add(r);
            }
            visual.leds = leds.ToArray();
            return visual;
        }

        // -------------------------------------------------------------- materials

        static void CreateMaterials()
        {
            Materials.Clear();
            EnsureFolder(MaterialsDir);
            Mat("Plinth", new Color(0.10f, 0.105f, 0.12f), 0f, 0.45f);
            Mat("PlinthBase", new Color(0.035f, 0.038f, 0.045f), 0f, 0.3f);
            Mat("Shroud", new Color(0.035f, 0.035f, 0.04f), 0.2f, 0.5f);
            Mat("Backplate", new Color(0.03f, 0.03f, 0.035f), 0.6f, 0.55f);
            Mat("ShroudAccent", new Color(0.55f, 0.56f, 0.58f), 0.9f, 0.7f);
            Mat("FanBlack", new Color(0.03f, 0.03f, 0.03f), 0f, 0.4f);
            Mat("PCB", new Color(0.03f, 0.12f, 0.06f), 0f, 0.45f);
            Mat("Steel", new Color(0.62f, 0.63f, 0.65f), 1f, 0.65f);
            Mat("Aluminium", new Color(0.78f, 0.79f, 0.80f), 1f, 0.6f);
            Mat("BlackPlastic", new Color(0.05f, 0.05f, 0.05f), 0f, 0.3f);
            Mat("PSUBody", new Color(0.10f, 0.10f, 0.11f), 0.6f, 0.6f);
            // Faintly self lit, so the panels read against a dark room whatever the lighting.
            Mat("NodeAccentLit", Cyan, 0f, 0.5f, emissive: true, emission: new Color(0.4f, 1.3f, 1.9f));
            Mat("EdgeAmber", Amber, 0f, 0.5f, emissive: true, emission: new Color(1.6f, 0.75f, 0.2f));
            Mat("Dais", new Color(0.03f, 0.034f, 0.042f), 0.2f, 0.85f);
            Mat("Pillar", new Color(0.05f, 0.055f, 0.065f), 0.3f, 0.4f);
            Mat("RackBody", new Color(0.04f, 0.043f, 0.05f), 0.5f, 0.45f);
            Mat("StripLight", new Color(0.8f, 0.9f, 1.0f), 0f, 0.5f, emissive: true, emission: new Color(0.7f, 1.0f, 1.4f));

            // The custom shaders in Assets/Shaders.
            GlowMat("GlowBlob", 0f, 2f, Color.white);
            GlowMat("GlowBlobCyan", 0f, 2.2f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f));
            GlowMat("GlowBeam", 1f, 1.4f, Color.white);
            GlowMat("GlowBeamSoft", 1f, 2.5f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f));
            GlowMat("GlowRing", 2f, 1.5f, Color.white);
            ShaderMat("GridFloor", "OCS/Grid Floor");
            ShaderMat("RackLights", "OCS/Rack Lights");
            Mat("NodePanel", new Color(0.07f, 0.085f, 0.10f), 0.3f, 0.6f, emissive: true, emission: new Color(0.05f, 0.065f, 0.085f));
            Mat("NodeAccent", Cyan, 0f, 0.5f, emissive: true, emission: new Color(0.04f, 0.12f, 0.18f));
            Mat("LED", new Color(0.9f, 0.9f, 0.9f), 0f, 0.7f, emissive: true);
            Mat("Pulse", new Color(0.9f, 0.95f, 1.0f), 0f, 0.7f, emissive: true, emission: new Color(0.9f, 1.8f, 2.6f));
            Mat("RoomFloor", new Color(0.035f, 0.038f, 0.045f), 0f, 0.55f);
            Mat("RoomWall", new Color(0.03f, 0.032f, 0.038f), 0f, 0.1f);
            Mat("FrameBlack", new Color(0.025f, 0.025f, 0.028f), 0.3f, 0.4f);   // matte anodised
            // Sprites/Default blends by vertex colour, which is how lines and trails fade.
            Mat("Trail", Color.white, 0f, 0f, shader: "Sprites/Default");
            Mat("TimelineSegment", new Color(0.06f, 0.08f, 0.1f), 0f, 0.5f, emissive: true, emission: new Color(0.04f, 0.05f, 0.06f));
        }

        /// <summary>
        /// Creates the material the first time and rewrites its settings on every build, so
        /// a rebuild always gives the look this script describes. The asset itself is
        /// reused, which keeps its GUID and every reference to it. Change colours here,
        /// not in the inspector.
        /// </summary>
        static void Mat(string name, Color color, float metallic, float smoothness,
                        bool emissive = false, Color? emission = null,
                        string shader = "Universal Render Pipeline/Lit")
        {
            Shader found = Shader.Find(shader);
            if (found == null)
            {
                Debug.LogError("[SceneBuilder] Shader '" + shader + "' not found. Is this a URP project?");
                return;
            }

            string path = MaterialsDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(found);
            else if (mat.shader != found) mat.shader = found;

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

            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            Materials[name] = mat;
        }

        /// <summary>A material on one of the project's own shaders, made once, kept after.</summary>
        static Material ShaderMat(string name, string shader)
        {
            Shader found = Shader.Find(shader);
            if (found == null)
            {
                Debug.LogError("[SceneBuilder] Shader '" + shader + "' not found. It should be in Assets/Shaders; " +
                               "check the Console for a shader compile error.");
                return null;
            }
            string path = MaterialsDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(found);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != found)
            {
                mat.shader = found;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Materials[name] = mat;
            return mat;
        }

        /// <summary>An OCS/Glow material: shape 0 a soft blob, 1 a soft beam, 2 a ring.</summary>
        static void GlowMat(string name, float shape, float falloff, Color color)
        {
            Material mat = ShaderMat(name, "OCS/Glow");
            if (mat == null) return;
            mat.SetFloat("_Shape", shape);
            mat.SetFloat("_Falloff", falloff);
            mat.SetColor("_Color", color);
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
