"""
Compiles the HLSL in each .shader under a folder with glslang, against stand-ins for the
few URP functions and macros they use. Catches syntax and type errors, including the
fog on and fog off variants. Says nothing about how a shader looks, and the stand-ins are
not URP: a URP function that doesn't exist would still pass. Run by run.sh when
glslangValidator is installed (apt-get install glslang-tools).
"""
import re, subprocess, sys, os, glob, tempfile
STUB = r'''
#define real float
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(x)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(x)
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(x)
float4 _Time;
float4x4 unity_ObjectToWorld;
float4x4 unity_MatrixVP;
float3 TransformObjectToWorld(float3 p) { return mul(unity_ObjectToWorld, float4(p, 1)).xyz; }
float4 TransformWorldToHClip(float3 p) { return mul(unity_MatrixVP, float4(p, 1)); }
float4 TransformObjectToHClip(float3 p) { return TransformWorldToHClip(TransformObjectToWorld(p)); }
real ComputeFogFactor(float z) { return z; }
real ComputeFogIntensity(real f) { return f; }
half3 MixFog(half3 c, real f) { return c; }
'''
ok = True
for path in sorted(glob.glob(sys.argv[1] + "/*.shader")):
    src = open(path).read()
    body = re.search(r"HLSLPROGRAM(.*?)ENDHLSL", src, re.S).group(1)
    body = "\n".join(l for l in body.splitlines() if not l.strip().startswith(("#pragma", "#include")))
    for fog in ("", "#define FOG_LINEAR\n"):
        code = fog + STUB + body
        f = os.path.join(tempfile.mkdtemp(), "t.hlsl")
        open(f, "w").write(code)
        for stage, entry in (("vert", "vert"), ("frag", "frag")):
            r = subprocess.run(["glslangValidator", "-D", "-V", "-S", stage, "-e", entry, f, "-o", "/dev/null"],
                               capture_output=True, text=True)
            status = "ok" if r.returncode == 0 else "FAIL"
            if r.returncode != 0:
                ok = False
                print(r.stdout[-2000:])
            print(f"{os.path.basename(path):22s} {stage:4s} {'fog' if fog else 'nofog':5s} {status}")
sys.exit(0 if ok else 1)
