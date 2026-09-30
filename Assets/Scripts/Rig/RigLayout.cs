using UnityEngine;

namespace OCS.VR.Rig
{
    /// <summary>
    /// Physical layout of the rig, as data. Slot positions are computed, not placed by
    /// hand in a scene, so changing card spacing is one field instead of eight transforms.
    /// Measure the real Rig 2 and put those numbers here.
    /// </summary>
    [CreateAssetMenu(fileName = "RigLayout", menuName = "OCS/Rig Layout")]
    public class RigLayout : ScriptableObject
    {
        [Header("Cards")]
        [Tooltip("Rig 2 has 8 GTX 1070s.")]
        public int cardCount = 8;

        [Tooltip("Metres between card centres. Rig 2's cards sit 75 mm apart.")]
        public float cardSpacing = 0.075f;

        [Tooltip("Position of card 0 relative to the rig root.")]
        public Vector3 firstCardOffset = new Vector3(-0.2625f, 0.2155f, -0.045f);

        [Tooltip("Direction the cards run in from card 0.")]
        public Vector3 cardAxis = Vector3.right;

        [Header("Thermal display")]
        [Tooltip("Temperature mapped to the coolest colour on the card.")]
        public float tempMinC = 35f;

        [Tooltip("Temperature mapped to the hottest colour. 1070s under load sit well under this.")]
        public float tempMaxC = 85f;

        public Vector3 GetCardLocalPosition(int index)
        {
            if (index < 0 || index >= cardCount)
            {
                Debug.LogWarning($"[RigLayout] Card index {index} is outside 0 to {cardCount - 1}.");
                index = Mathf.Clamp(index, 0, Mathf.Max(0, cardCount - 1));
            }

            return firstCardOffset + cardAxis.normalized * (cardSpacing * index);
        }

        /// <summary>0 at tempMinC, 1 at tempMaxC. Drives card colour or emission.</summary>
        public float NormalizedTemp(float tempC)
        {
            if (tempMaxC <= tempMinC) return 0f;
            return Mathf.Clamp01((tempC - tempMinC) / (tempMaxC - tempMinC));
        }

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < cardCount;
        }
    }
}
