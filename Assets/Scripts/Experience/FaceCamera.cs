using UnityEngine;

namespace OCS.VR.Experience
{
    /// <summary>
    /// Keeps a label turned toward the viewer, so text stays readable wherever they stand.
    /// Turns about the vertical axis only by default: labels that tilt as you crouch read
    /// as swimming. No allocation.
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        [Tooltip("Also tilt up and down to face the viewer. Off keeps labels upright.")]
        public bool tilt = false;

        Transform _cam;

        void LateUpdate()
        {
            if (_cam == null)
            {
                Camera c = Camera.main;
                if (c == null) return;
                _cam = c.transform;
            }

            Vector3 away = transform.position - _cam.position;
            if (!tilt) away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) return;
            // TextMesh reads correctly when its forward points away from the viewer.
            transform.rotation = Quaternion.LookRotation(away);
        }
    }
}
