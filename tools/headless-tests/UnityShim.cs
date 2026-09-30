// Minimal UnityEngine + NUnit shim so the repo's real C# compiles and runs headless.
// Verification tool only. Not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public static float Distance(Vector3 a, Vector3 b) { float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z; return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz); }
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public Vector3 normalized
        {
            get
            {
                float m = (float)Math.Sqrt(x * x + y * y + z * z);
                return m <= 1e-8f ? new Vector3(0, 0, 0) : new Vector3(x / m, y / m, z / m);
            }
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        { t = Mathf.Clamp01(t); return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    public static class Mathf
    {
        public const float PI = (float)System.Math.PI;
        public static float Sin(float a) => (float)System.Math.Sin(a);
        public static float Cos(float a) => (float)System.Math.Cos(a);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Abs(float v) => v < 0f ? -v : v;
    }

    public static class Debug
    {
        public static List<string> Lines = new List<string>();
        public static void Log(object m) { Lines.Add("LOG  " + m); }
        public static void LogWarning(object m) { Lines.Add("WARN " + m); }
        public static void LogError(object m) { Lines.Add("ERR  " + m); }
    }

    public static class Time
    {
        public static float deltaTime = 1f / 72f; // Quest 2 floor
        public static float time = 0f;
    }

    public class Object
    {
        public string name = "";
        public static void DestroyImmediate(Object o) { }
        public static T FindFirstObjectByType<T>() where T : Object => null;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
        public static Color Lerp(Color a, Color b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
        public static Color black => new Color(0, 0, 0, 1);
        public static Color white => new Color(1, 1, 1, 1);
        public static Color yellow => new Color(1, 0.92f, 0.016f, 1);
    }

    public enum Space { World, Self }
    public enum LightShadows { None, Hard, Soft }
    public enum MaterialGlobalIlluminationFlags { None = 0, RealtimeEmissive = 1, BakedEmissive = 2, EmissiveIsBlack = 4 }

    public class Shader : Object
    {
        public static int PropertyToID(string n) => n.GetHashCode();
        public static Shader Find(string n) => new Shader();
        public static readonly Dictionary<int, float> GlobalFloats = new Dictionary<int, float>();
        public static void SetGlobalFloat(int id, float v) { GlobalFloats[id] = v; }
        public static void SetGlobalVector(int id, Vector4 v) { }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public class Material : Object
    {
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
        public Shader shader;
        public Material(Shader s) { shader = s; }
        public bool enableInstancing;
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float f) { }
        public void EnableKeyword(string k) { }
    }

    public class MaterialPropertyBlock
    {
        public readonly Dictionary<int, Color> Colors = new Dictionary<int, Color>();
        public void SetColor(int id, Color c) { Colors[id] = c; }
    }

    public class Component : Object
    {
        Transform _t;
        GameObject _go;
        public Transform transform => _t ?? (_t = this as Transform ?? new Transform());
        public GameObject gameObject => _go ?? (_go = new GameObject());
        public T GetComponent<T>() where T : class => null;
    }

    public class Renderer : Component
    {
        public bool enabled = true;
        public Material sharedMaterial;
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public MaterialPropertyBlock LastBlock;
        public void GetPropertyBlock(MaterialPropertyBlock b) { }
        public void SetPropertyBlock(MaterialPropertyBlock b) { LastBlock = b; }
    }

    public class Collider : Component { }

    public class LineRenderer : Renderer
    {
        public bool useWorldSpace;
        public int numCapVertices;
        int _count = 2;
        public int positionCount { get => _count; set { _count = value; Positions = new Vector3[value]; } }
        public bool loop;
        public LineAlignment alignment;
        public AnimationCurve widthCurve;
        public float widthMultiplier;
        public Color startColor, endColor;
        public Vector3[] Positions = new Vector3[2];
        public void SetPosition(int i, Vector3 p) { Positions[i] = p; }
    }

    public class TrailRenderer : Renderer
    {
        public float time, widthMultiplier, minVertexDistance;
        public Color startColor, endColor;
    }
    public class Light : Component { public LightShadows shadows; public float intensity; public Color color; }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    public static class RenderSettings
    {
        public static Material skybox;
        public static UnityEngine.Rendering.AmbientMode ambientMode;
        public static Color ambientLight;
        public static bool fog;
        public static FogMode fogMode;
        public static Color fogColor;
        public static float fogStartDistance, fogEndDistance;
    }
    public enum FogMode { Linear = 1, Exponential, ExponentialSquared }

    [AttributeUsage(AttributeTargets.Class)] public class ExecuteAlwaysAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class ColorUsageAttribute : Attribute { public ColorUsageAttribute(bool showAlpha, bool hdr) { } }
    public class TextAsset : Object { public string text; public TextAsset(string t) { text = t; } }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject, new() => new T(); }

    public class GameObject : Object
    {
        bool _active = true;
        public Transform transform;
        // Paired explicitly. Letting each construct the other recurses forever.
        public GameObject() { transform = new Transform(this); }
        public GameObject(string n) : this() { name = n; }
        internal GameObject(Transform t) { transform = t; }
        public void SetActive(bool v) { _active = v; }
        public bool activeSelf => _active;
        public T AddComponent<T>() where T : new() => new T();
        public T GetComponent<T>() where T : class => null;
        public T[] GetComponentsInChildren<T>() where T : class => new T[0];
        public bool isStatic;
        public static GameObject CreatePrimitive(PrimitiveType t) => new GameObject();
        public static GameObject Find(string n) => null;
    }

    public class Transform : Component
    {
        public Vector3 localPosition;
        public Vector3 position;
        public Vector3 localScale = Vector3.one;
        public Quaternion rotation;

        GameObject _go;
        public Transform() { }
        internal Transform(GameObject go) { _go = go; }
        public GameObject gameObject => _go ?? (_go = new GameObject(this));

        public Quaternion localRotation;
        public readonly List<Transform> Children = new List<Transform>();
        public float RotatedDegrees;
        public void SetParent(Transform p, bool worldPositionStays) { if (p != null) p.Children.Add(this); }
        public Vector3 TransformPoint(Vector3 local) => position + local;
        public int childCount => Children.Count;
        public Transform GetChild(int i) => Children[i];
        public Transform Find(string n) { foreach (var c in Children) if (c.name == n) return c; return null; }
        public void Rotate(Vector3 axis, float degrees, Space space) { RotatedDegrees += degrees; }
    }


    public enum PrimitiveType { Cube, Sphere, Plane, Capsule, Cylinder, Quad }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }

    public enum TextAlignment { Left, Center, Right }
    public enum LineAlignment { View, TransformZ }
    public struct Keyframe { public float time, value; public Keyframe(float t, float v) { time = t; value = v; } }
    public class AnimationCurve { public Keyframe[] keys; public AnimationCurve(params Keyframe[] k) { keys = k; } }

    public struct Quaternion
    {
        public static Quaternion Euler(float x, float y, float z) => new Quaternion();
        public static Quaternion identity => new Quaternion();
        public static Quaternion LookRotation(Vector3 forward) => new Quaternion();
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => new Quaternion();
        public static Quaternion AngleAxis(float angle, Vector3 axis) => new Quaternion();
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion();
    }

    public class TextMesh : Component
    {
        public Color color;
        public string text;
        public TextAnchor anchor;
        public TextAlignment alignment;
        public int fontSize;
        public float characterSize;
    }

    public class Camera : Object
    {
        public CameraClearFlags clearFlags;
        public float fieldOfView = 60f;
        public Color backgroundColor;
        public Transform transform = new Transform();
        public static Camera main => _main ?? (_main = new Camera());
        static Camera _main;
    }

    public class AudioClip : Object { }
    public class AudioSource : Object
    {
        public int PlayedCount;
        public void PlayOneShot(AudioClip c) { PlayedCount++; }
    }

    public class Coroutine { public IEnumerator E; public Stack<IEnumerator> Stack = new Stack<IEnumerator>(); public bool Done; public float WaitUntil; }
    public class WaitForSeconds { public float seconds; public WaitForSeconds(float s) { seconds = s; } }

    public class MonoBehaviour : Object
    {
        public Transform transform = new Transform();
        GameObject _go;
        public GameObject gameObject => _go ?? (_go = new GameObject());
        public T GetComponent<T>() where T : class => null;
        public static List<Coroutine> Routines = new List<Coroutine>();
        public static float Now;
        public Coroutine StartCoroutine(IEnumerator e)
        {
            var c = new Coroutine { E = e };
            c.Stack.Push(e);
            Routines.Add(c);
            return c;
        }
        public void StopCoroutine(Coroutine c) { if (c != null) { c.Done = true; Routines.Remove(c); } }
        // Drive all live coroutines to virtual time `Now`.
        public static void PumpCoroutines()
        {
            for (int i = Routines.Count - 1; i >= 0; i--)
            {
                var c = Routines[i];
                if (c.Done) { Routines.RemoveAt(i); continue; }
                while (!c.Done && c.WaitUntil <= Now)
                {
                    if (c.Stack.Count == 0) { c.Done = true; break; }
                    var top = c.Stack.Peek();
                    bool moved;
                    try { moved = top.MoveNext(); }
                    catch (Exception ex) { Debug.LogError("coroutine threw: " + ex.Message); c.Done = true; break; }
                    if (!moved) { c.Stack.Pop(); if (c.Stack.Count == 0) c.Done = true; continue; }
                    var y = top.Current;
                    if (y is WaitForSeconds w) { c.WaitUntil = Now + w.seconds; break; }
                    if (y is IEnumerator inner) { c.Stack.Push(inner); continue; }
                    break; // yield return null -> resume next pump
                }
                if (c.Done) Routines.RemoveAt(i);
            }
        }
        public static void ResetScheduler() { Routines.Clear(); Now = 0f; }
    }

    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TextAreaAttribute : Attribute { public TextAreaAttribute(int a, int b) { } }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeFieldAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }

    /// <summary>
    /// Mimics UnityEngine.JsonUtility closely enough to test the loader:
    /// public fields only, literal key-to-field-name mapping, unknown keys ignored,
    /// absent keys left at their field initializer value.
    /// </summary>
    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            int i = 0;
            object v = Parse(json, ref i);
            if (v == null) return default(T);
            return (T)Bind(v, typeof(T));
        }

        static object Bind(object node, Type t)
        {
            if (node == null) return null;
            if (t == typeof(string)) return node as string;
            if (t == typeof(float)) return Convert.ToSingle(node, CultureInfo.InvariantCulture);
            if (t == typeof(int)) return Convert.ToInt32(Convert.ToDouble(node, CultureInfo.InvariantCulture));
            if (t == typeof(bool)) return Convert.ToBoolean(node);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type et = t.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(t);
                if (node is List<object> arr) foreach (var e in arr) list.Add(Bind(e, et));
                return list;
            }
            // object: run the default ctor so field initializers (gpu_index = -1) survive
            object inst = Activator.CreateInstance(t);
            if (node is Dictionary<string, object> map)
            {
                foreach (var kv in map)
                {
                    FieldInfo f = t.GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (f == null) continue;            // unknown key -> ignored, like JsonUtility
                    if (kv.Value == null) continue;
                    f.SetValue(inst, Bind(kv.Value, f.FieldType));
                }
            }
            return inst;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Parse(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{')
            {
                i++; var d = new Dictionary<string, object>();
                Ws(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (i < s.Length)
                {
                    Ws(s, ref i);
                    string k = (string)Parse(s, ref i);
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;
                    object v = Parse(s, ref i);
                    if (k != null) d[k] = v;
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    break;
                }
                return d;
            }
            if (c == '[')
            {
                i++; var a = new List<object>();
                Ws(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return a; }
                while (i < s.Length)
                {
                    a.Add(Parse(s, ref i));
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    break;
                }
                return a;
            }
            if (c == '"')
            {
                i++; var sb = new StringBuilder();
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        i++;
                        char e = s[i++];
                        switch (e)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                            default: sb.Append(e); break;
                        }
                        continue;
                    }
                    sb.Append(s[i++]);
                }
                i++;
                return sb.ToString();
            }
            if (c == 't' && s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (c == 'f' && s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (c == 'n' && s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }
    }
}

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
    public static class Assert
    {
        public static void IsTrue(bool c, string m = null) { if (!c) throw new AssertionException("expected true. " + m); }
        public static void IsFalse(bool c, string m = null) { if (c) throw new AssertionException("expected false. " + m); }
        public static void IsNull(object o, string m = null) { if (o != null) throw new AssertionException("expected null. " + m); }
        public static void IsNotNull(object o, string m = null) { if (o == null) throw new AssertionException("expected not null. " + m); }
        public static void AreEqual(object a, object b, string m = null)
        {
            bool ok;
            if (a is float || b is float || a is double || b is double)
                ok = Math.Abs(Convert.ToDouble(a) - Convert.ToDouble(b)) < 1e-4;
            else ok = Equals(a, b);
            if (!ok) throw new AssertionException($"expected <{a}> but was <{b}>. {m}");
        }
        public static void AreEqual(double a, double b, double delta, string m = null)
        {
            if (Math.Abs(a - b) > delta) throw new AssertionException($"expected <{a}> +/- {delta} but was <{b}>. {m}");
        }
        public static void Less(float a, float b, string m = null) { if (!(a < b)) throw new AssertionException($"expected {a} < {b}. {m}"); }
        public static void Greater(float a, float b, string m = null) { if (!(a > b)) throw new AssertionException($"expected {a} > {b}. {m}"); }
    }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
}

namespace UnityEngine
{
    // Just enough of ParticleSystem for the scripts and the scene builder to compile and
    // for the headless run to count what was emitted. Modules are structs that write
    // through to the system, as Unity's are.
    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemShapeType { Sphere = 0, Hemisphere = 2, Cone = 4, Box = 5, Circle = 10 }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }
    public enum ParticleSystemScalingMode { Hierarchical, Local, Shape }

    public struct MinMaxCurve
    {
        public float constant, constantMin, constantMax;
        public AnimationCurve curve;
        public MinMaxCurve(float c) { constant = constantMin = constantMax = c; curve = null; }
        public MinMaxCurve(float min, float max) { constant = max; constantMin = min; constantMax = max; curve = null; }
        public MinMaxCurve(float multiplier, AnimationCurve c) { constant = constantMin = constantMax = multiplier; curve = c; }
        public static implicit operator MinMaxCurve(float c) => new MinMaxCurve(c);
    }

    public struct GradientColorKey { public Color color; public float time; public GradientColorKey(Color c, float t) { color = c; time = t; } }
    public struct GradientAlphaKey { public float alpha, time; public GradientAlphaKey(float a, float t) { alpha = a; time = t; } }
    public class Gradient { public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { } }

    public struct MinMaxGradient
    {
        public Color color;
        public Gradient gradient;
        public MinMaxGradient(Color c) { color = c; gradient = null; }
        public MinMaxGradient(Gradient g) { color = default(Color); gradient = g; }
        public static implicit operator MinMaxGradient(Color c) => new MinMaxGradient(c);
        public static implicit operator MinMaxGradient(Gradient g) => new MinMaxGradient(g);
    }

    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode;
        public float minParticleSize, maxParticleSize = 0.5f;
    }

    public class ParticleSystem : Component
    {
        public float Rate;
        public int Emitted;
        public bool isPlaying { get; private set; }
        public void Play() { isPlaying = true; }
        public void Stop() { isPlaying = false; }
        public void Clear() { }
        public void Emit(EmitParams p, int count) { Emitted += count; }
        public void Emit(int count) { Emitted += count; }

        public struct EmitParams
        {
            public Vector3 position;
            public bool applyShapeToPosition;
            public Color32 startColor;
        }

        public MainModule main => new MainModule();
        public EmissionModule emission => new EmissionModule(this);
        public ShapeModule shape => new ShapeModule();
        public ColorOverLifetimeModule colorOverLifetime => new ColorOverLifetimeModule();
        public SizeOverLifetimeModule sizeOverLifetime => new SizeOverLifetimeModule();
        public VelocityOverLifetimeModule velocityOverLifetime => new VelocityOverLifetimeModule();

        public struct MainModule
        {
            public MinMaxCurve startLifetime { get; set; }
            public MinMaxCurve startSpeed { get; set; }
            public MinMaxCurve startSize { get; set; }
            public MinMaxGradient startColor { get; set; }
            public MinMaxCurve gravityModifier { get; set; }
            public int maxParticles { get; set; }
            public bool loop { get; set; }
            public bool playOnAwake { get; set; }
            public bool prewarm { get; set; }
            public float duration { get; set; }
            public ParticleSystemSimulationSpace simulationSpace { get; set; }
            public ParticleSystemScalingMode scalingMode { get; set; }
        }

        public struct EmissionModule
        {
            readonly ParticleSystem _ps;
            public EmissionModule(ParticleSystem ps) : this() { _ps = ps; }
            public bool enabled { get; set; }
            public MinMaxCurve rateOverTime { get => new MinMaxCurve(_ps.Rate); set { _ps.Rate = value.constant; } }
        }

        public struct ShapeModule
        {
            public bool enabled { get; set; }
            public ParticleSystemShapeType shapeType { get; set; }
            public float angle { get; set; }
            public float radius { get; set; }
            public Vector3 scale { get; set; }
            public Vector3 position { get; set; }
        }

        public struct ColorOverLifetimeModule { public bool enabled { get; set; } public MinMaxGradient color { get; set; } }
        public struct SizeOverLifetimeModule { public bool enabled { get; set; } public MinMaxCurve size { get; set; } }
        public struct VelocityOverLifetimeModule
        {
            public bool enabled { get; set; }
            public MinMaxCurve x { get; set; }
            public MinMaxCurve y { get; set; }
            public MinMaxCurve z { get; set; }
            public ParticleSystemSimulationSpace space { get; set; }
        }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(Mathf.Clamp01(c.a) * 255));
    }
}

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public class Mesh : Object
    {
        public Vector3[] vertices;
        public Vector2[] uv;
        public Color[] colors;
        public int[] triangles;
        public void RecalculateBounds() { }
    }

    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class MeshRenderer : Renderer { }
}
