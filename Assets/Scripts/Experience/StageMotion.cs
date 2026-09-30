namespace OCS.VR.Experience
{
    /// <summary>
    /// How far along a stage change is, from which act is playing and how long it has
    /// been playing. A change belongs to one act: before that act it hasn't happened,
    /// after it it has, and during it it eases in after a delay. Working it out from the
    /// act rather than from stored state means a restart or a skipped act always lands
    /// in the right place. Plain C#, tested.
    /// </summary>
    public static class StageMotion
    {
        /// <summary>0 before the change starts, 1 once it's done, eased in between.</summary>
        public static float Progress(Act current, float secondsIntoAct, Act cueAct, float delay, float duration)
        {
            if (current < cueAct) return 0f;
            if (current > cueAct) return 1f;
            if (duration <= 0f) return secondsIntoAct >= delay ? 1f : 0f;
            return Ease((secondsIntoAct - delay) / duration);
        }

        /// <summary>Smoothstep: starts and ends gently, so nothing jolts in a headset.</summary>
        public static float Ease(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
