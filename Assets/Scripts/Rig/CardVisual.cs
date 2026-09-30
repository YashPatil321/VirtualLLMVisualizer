using UnityEngine;

namespace OCS.VR.Rig
{
    /// <summary>
    /// Spins a card's fans and drives its LED glow. Told what to show by RigCardDisplay
    /// (load) and RigPower (on or off). The rules for how fast and how bright live in
    /// CardVisualMath; this class only applies them.
    ///
    /// Works with both the Blender models from tools/blender and the scene builder's
    /// placeholders, because both name their parts the same way: Fan0 and Fan1 spin, and
    /// every child whose name starts with LED glows. Idle cards glow a cool cyan; a card
    /// under load shifts toward amber, so the one doing the work stands out.
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

        [Tooltip("Need a material with emission enabled. Filled from children whose names " +
                 "start with LED if left empty: the light bar and the fan rings.")]
        public Renderer[] leds;

        [Header("Look")]
        [ColorUsage(false, true)]
        [Tooltip("Glow colour when idle. Keep green equal to hotColor's so brightness " +
                 "comes from glow alone and only the hue says how hard the card works.")]
        public Color idleColor = new Color(0.2f, 0.9f, 1.6f);

        [ColorUsage(false, true)]
        [Tooltip("Glow colour at full load.")]
        public Color hotColor = new Color(2.4f, 0.9f, 0.2f);

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
        float _appliedHeat = -1f;

        public bool Powered => _powered;
        public float Load => _load;
        public float CurrentRps => _rps;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (fans == null || fans.Length == 0) fans = FindChildren<Transform>("Fan");
            if (leds == null || leds.Length == 0) leds = FindChildren<Renderer>("LED");
            ApplyGlow(0f, 0f);
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

            ApplyGlow(CardVisualMath.Glow(_powered, _load, idleGlow), CardVisualMath.Heat(_powered, _load));
        }

        void ApplyGlow(float glow, float heat)
        {
            if (leds == null) return;
            // Skip the property block writes when nothing visible changed.
            if (Mathf.Abs(glow - _appliedGlow) < 0.002f && Mathf.Abs(heat - _appliedHeat) < 0.002f) return;
            _appliedGlow = glow;
            _appliedHeat = heat;

            Color emission = Color.Lerp(idleColor, hotColor, heat) * glow;
            for (int i = 0; i < leds.Length; i++)
            {
                if (leds[i] == null) continue;
                leds[i].GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, emission);
                leds[i].SetPropertyBlock(_block);
            }
        }

        /// <summary>Direct children whose names start with prefix. Called once, in Awake.</summary>
        T[] FindChildren<T>(string prefix) where T : Component
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith(prefix) && child.GetComponent<T>() != null) count++;
            }

            var found = new T[count];
            int n = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith(prefix)) continue;
                T c = child.GetComponent<T>();
                if (c != null) found[n++] = c;
            }
            return found;
        }
    }
}
