using UnityEngine;

namespace OCS.VR.Experience
{
    /// <summary>
    /// A board of facts standing in the hall: a title and a few lines. Data, so the facts
    /// can be corrected without touching the scene builder.
    /// </summary>
    [CreateAssetMenu(menuName = "OCS/Info Board", fileName = "InfoBoard")]
    public class InfoBoard : ScriptableObject
    {
        public string title;

        [Tooltip("One fact per line. Keep each under about 40 characters so it fits.")]
        public string[] lines;
    }
}
