using System;
using UnityEngine;
using OCS.VR.Rig;

namespace OCS.VR.Experience
{
    /// <summary>
    /// A name floating near one rig part, with a thin line to it. Appears the moment the
    /// part seats during assembly. Labels on parts the viewer will keep seeing stay; labels
    /// on parts the cards will cover fade before the cards arrive. Hides again if assembly
    /// restarts.
    ///
    /// Which parts get labels, and what they say, comes from AssemblySequence.asset.
    /// </summary>
    public class PartLabel : MonoBehaviour
    {
        public AssemblyPlayer assembly;
        public string stepId;

        [Tooltip("The part this label points at.")]
        public Transform target;

        [Tooltip("Where the label sits, in metres from the part's origin.")]
        public Vector3 offset = new Vector3(0f, 0.12f, 0f);

        [Tooltip("Seconds to stay after the part seats. 0 keeps it for good.")]
        public float hideAfter;

        [Tooltip("Where the leader line meets the part, metres above its origin.")]
        public float anchorHeight = 0.05f;

        public Renderer textRenderer;
        public LineRenderer leader;

        bool _visible;
        float _hideAt = -1f;

        void OnEnable()
        {
            if (assembly == null) return;
            assembly.StepEnded += OnStepEnded;
            assembly.Started += OnAssemblyStarted;
        }

        void OnDisable()
        {
            if (assembly == null) return;
            assembly.StepEnded -= OnStepEnded;
            assembly.Started -= OnAssemblyStarted;
        }

        void Start() => SetVisible(false);

        void OnStepEnded(AssemblyStep step, int index)
        {
            if (step == null || step.stepId != stepId) return;
            SetVisible(true);
            _hideAt = hideAfter > 0f ? Time.time + hideAfter : -1f;
        }

        void OnAssemblyStarted()
        {
            _hideAt = -1f;
            SetVisible(false);
        }

        public void SetVisible(bool on)
        {
            _visible = on;
            if (textRenderer != null) textRenderer.enabled = on;
            if (leader != null) leader.enabled = on;
        }

        void LateUpdate()
        {
            if (!_visible || target == null) return;
            if (_hideAt > 0f && Time.time >= _hideAt)
            {
                _hideAt = -1f;
                SetVisible(false);
                return;
            }

            Vector3 anchor = target.position + Vector3.up * anchorHeight;
            transform.position = target.position + offset;
            if (leader != null)
            {
                leader.SetPosition(0, transform.position - Vector3.up * 0.012f);
                leader.SetPosition(1, anchor);
            }
        }
    }
}
