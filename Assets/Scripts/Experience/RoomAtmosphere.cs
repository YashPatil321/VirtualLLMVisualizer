using UnityEngine;
using UnityEngine.Rendering;

namespace OCS.VR.Experience
{
    /// <summary>
    /// The room's mood: ambient light and the colour behind everything. Lives on the
    /// Environment root so it travels with the Environment prefab, instead of being scene
    /// settings that a rebuild would reset.
    ///
    /// Dark on purpose. The card LEDs and the request pulse are the things the viewer
    /// should notice, and they only stand out against a dim room.
    ///
    /// Runs in the editor too, so the Scene view matches what the headset shows.
    /// </summary>
    [ExecuteAlways]
    public class RoomAtmosphere : MonoBehaviour
    {
        [Tooltip("Flat ambient light. Keep it low so emissive parts read as light sources.")]
        public Color ambient = new Color(0.10f, 0.11f, 0.13f);

        [Tooltip("What the camera shows where there's nothing. Replaces the default sky.")]
        public Color background = new Color(0.02f, 0.022f, 0.028f);

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

        public void Apply()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = background;
            }
        }
    }
}
