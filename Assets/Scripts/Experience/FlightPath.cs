using UnityEngine;

namespace OCS.VR.Experience
{
    /// <summary>
    /// How a part flies from the tray to its socket: up, across and down on a curve, fast
    /// out of the tray and settling into the socket with a small overshoot, turning as it
    /// goes and arriving square. Plain C#, tested.
    /// </summary>
    public static class FlightPath
    {
        /// <summary>
        /// Where the part is at t (0 to 1, already eased). A cubic curve whose middle two
        /// control points sit arcHeight above each end, so it lifts straight up out of the
        /// tray and comes straight down into the socket.
        /// </summary>
        public static Vector3 Position(Vector3 from, Vector3 to, float t, float arcHeight)
        {
            Vector3 up = new Vector3(0f, arcHeight, 0f);
            Vector3 p1 = from + up;
            Vector3 p2 = to + up;
            float u = 1f - t;
            return from * (u * u * u) + p1 * (3f * u * u * t) + p2 * (3f * u * t * t) + to * (t * t * t);
        }

        /// <summary>
        /// Higher arcs for longer flights, so a part crossing the room doesn't skim the
        /// bench, and a floor so short hops still visibly lift.
        /// </summary>
        public static float ArcFor(float distance) => Mathf.Max(0.15f, distance * 0.35f);

        /// <summary>
        /// Ease that runs slightly past 1 and comes back, so the part lands with a little
        /// settle instead of stopping dead. overshoot 0 is a plain ease out.
        /// </summary>
        public static float EaseOutBack(float t, float overshoot)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float s = t - 1f;
            return 1f + s * s * ((overshoot + 1f) * s + overshoot);
        }

        /// <summary>Degrees of turn left at t: all of it at the start, none on arrival.</summary>
        public static float SpinAt(float t, float degrees)
        {
            if (t <= 0f) return degrees;
            if (t >= 1f) return 0f;
            float u = 1f - t;
            return degrees * u * u * u;               // mostly done by the time it lands
        }
    }
}
