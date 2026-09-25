using UnityEngine;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Shows whatever line the NarrationDirector picked, on a legacy TextMesh.
    ///
    /// TextMesh rather than TextMeshPro on purpose: TMP needs its essentials imported
    /// before anything renders, and this has to work in a scene built from a script with
    /// no package setup. Swap it for world space TMP at V7 without touching the director.
    /// </summary>
    public class NarrationLabel : MonoBehaviour
    {
        public NarrationDirector director;
        public TextMesh target;

        [Tooltip("Blank the label when a line times out.")]
        public bool clearOnTimeout = true;

        void Reset() => target = GetComponent<TextMesh>();

        void OnEnable()
        {
            if (director == null) return;
            director.LineChanged += OnLineChanged;
            director.LineCleared += OnLineCleared;
        }

        void OnDisable()
        {
            if (director == null) return;
            director.LineChanged -= OnLineChanged;
            director.LineCleared -= OnLineCleared;
        }

        void OnLineChanged(NarrationLine line)
        {
            if (target != null && line != null) target.text = line.text;
        }

        void OnLineCleared()
        {
            if (clearOnTimeout && target != null) target.text = string.Empty;
        }
    }
}
