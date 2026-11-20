using UnityEngine;

namespace RoadwaysGame
{
    public struct HermiteSample
    {
        public Vector3 position;
        public Vector3 velocity;
        public float headingAngleDeg; // Derived directly from velocity tangent
    }

    /// <summary>
    /// Cubic Hermite Spline Kinematics.
    /// Evaluates smooth automotive driving paths where heading angle theta is derived
    /// strictly from the velocity vector tangent: theta = atan2(Vx, -Vz).
    /// Prevents the vehicle from ever appearing "dragged".
    /// </summary>
    public static class HermiteKinematics
    {
        public static HermiteSample Evaluate(Vector3 p0, Vector3 t0, Vector3 p1, Vector3 t1, float t)
        {
            t = Mathf.Clamp01(t);
            float t2 = t * t;
            float t3 = t2 * t;

            // Hermite Basis Functions
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;

            Vector3 pos = (h00 * p0) + (h10 * t0) + (h01 * p1) + (h11 * t1);

            // Velocity Derivatives
            float dh00 = 6f * t2 - 6f * t;
            float dh10 = 3f * t2 - 4f * t + 1f;
            float dh01 = -6f * t2 + 6f * t;
            float dh11 = 3f * t2 - 2f * t;

            Vector3 vel = (dh00 * p0) + (dh10 * t0) + (dh01 * p1) + (dh11 * t1);

            // Velocity-Tangent Heading (XZ plane)
            // 0 deg is straight forward (+Z or screen up), positive turns right, negative turns left
            float heading = 0f;
            if (vel.sqrMagnitude > 0.0001f)
            {
                heading = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
            }

            return new HermiteSample
            {
                position = pos,
                velocity = vel,
                headingAngleDeg = heading
            };
        }
    }
}
