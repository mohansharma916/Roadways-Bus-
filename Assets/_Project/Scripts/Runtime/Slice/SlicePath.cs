using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>Catmull-Rom curve through ground points, resampled by arc length. Extrapolates straight past both ends.</summary>
    public sealed class SlicePath
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> lengths = new List<float>();

        public SlicePath(IList<Vector3> control, int samplesPerSegment = 16)
        {
            for (int i = 0; i < control.Count - 1; i++)
            {
                Vector3 p0 = control[Mathf.Max(i - 1, 0)], p1 = control[i], p2 = control[i + 1], p3 = control[Mathf.Min(i + 2, control.Count - 1)];
                for (int k = 0; k < samplesPerSegment; k++)
                    Add(CatmullRom(p0, p1, p2, p3, k / (float)samplesPerSegment));
            }
            Add(control[control.Count - 1]);
        }

        public float Length => lengths[lengths.Count - 1];

        public Vector3 PointAt(float s)
        {
            if (s <= 0f) return points[0] + TangentAt(0f) * s;
            if (s >= Length) return points[points.Count - 1] + TangentAt(Length) * (s - Length);
            int i = Index(s);
            float t = (s - lengths[i]) / Mathf.Max(lengths[i + 1] - lengths[i], 1e-5f);
            return Vector3.Lerp(points[i], points[i + 1], t);
        }

        public Vector3 TangentAt(float s)
        {
            int i = Index(Mathf.Clamp(s, 0f, Length));
            return (points[i + 1] - points[i]).normalized;
        }

        /// <summary>Unit vector pointing to the left of the direction of travel, on the ground plane.</summary>
        public Vector3 LeftAt(float s)
        {
            Vector3 t = TangentAt(s);
            return new Vector3(-t.z, 0f, t.x);
        }

        private void Add(Vector3 p)
        {
            lengths.Add(points.Count == 0 ? 0f : lengths[lengths.Count - 1] + Vector3.Distance(points[points.Count - 1], p));
            points.Add(p);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) =>
            0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (3f * p1 - p0 - 3f * p2 + p3) * (t * t * t));

        private int Index(float s)
        {
            int lo = 0, hi = lengths.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (lengths[mid] <= s) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
    }
}
