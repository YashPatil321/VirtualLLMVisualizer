using UnityEngine;

namespace OCS.VR.Rig
{
    /// <summary>
    /// Spins a card's fans and drives its LED glow. Told what to show by RigCardDisplay
    /// (load) and RigPower (on or off). The rules for how fast and how bright live in
    /// CardVisualMath; this class only applies them.
    ///
    /// Works with both the Blender models from tools/blender and the scene builder's
    /// placeholders, because both name their parts the same way: Fan0, Fan1 and LED.
    ///
    /// No allocation in Update. The LED is driven through a MaterialPropertyBlock, so
    /// eight glowing cards share one material instead of creating eight copies.
    /// </summary>
    public class CardVisual : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("Spun about fanAxis. Filled from children named Fan... if left empty.")]
        public Transform[] fans;

        [Tooltip("Local axis the fans spin about. X for the card models and placeholders.")]
        public Vector3 fanAxis = Vector3.right;

        [Tooltip("Needs a material with emission enabled. Filled from a child named LED if left empty.")]
        public Renderer led;

        [Header("Look")]
        [ColorUsage(false, true)]
        public Color ledColor = new Color(0.3f, 1.6f, 0.6f);

        [Range(0f, 1f)]
        [Tooltip("Glow when powered but idle.")]
        public float idleGlow = 0.12f;

        public float idleFanRps = 4f;
        public float maxFanRps = 22f;

        [Tooltip("How quickly fans change speed, in revolutions per second, per second.")]
        public float fanAcceleration = 8f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _block;
        bool _powered;
        float _load;
        float _rps;
        float _appliedGlow = -1f;

        public bool Powered => _powered;
        public float Load => _load;
        public float CurrentRps => _rps;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (fans == null || fans.Length == 0) fans = FindFans();
            if (led == null) led = FindLed();
            ApplyGlow(0f);
        }

        public void SetPowered(bool on) => _powered = on;
        public void SetLoad(float load01) => _load = load01;

        void Update()
        {
            float targetRps = CardVisualMath.FanRps(_powered, _load, idleFanRps, maxFanRps);
            _rps = CardVisualMath.Approach(_rps, targetRps, fanAcceleration * Time.deltaTime);

            if (_rps > 0f && fans != null)
            {
                float degrees = _rps * 360f * Time.deltaTime;
                for (int i = 0; i < fans.Length; i++)
                {
                    if (fans[i] != null) fans[i].Rotate(fanAxis, degrees, Space.Self);
                }
            }

            ApplyGlow(CardVisualMath.Glow(_powered, _load, idleGlow));
        }

        void ApplyGlow(float glow)
        {
            if (led == null) return;
            // Skip the property block write when nothing visible changed.
            if (Mathf.Abs(glow - _appliedGlow) < 0.002f) return;
            _appliedGlow = glow;

            led.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, ledColor * glow);
            led.SetPropertyBlock(_block);
        }

        Transform[] FindFans()
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).name.StartsWith("Fan")) count++;

            var found = new Transform[count];
            int n = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Fan")) found[n++] = child;
            }
            return found;
        }

        Renderer FindLed()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "LED") return child.GetComponent<Renderer>();
            }
            return null;
        }
    }
}
