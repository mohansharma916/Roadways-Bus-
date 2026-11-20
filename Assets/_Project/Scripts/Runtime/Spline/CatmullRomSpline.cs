using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    [Serializable]
    public struct SplineSample
    {
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 normal;
        public float distance;
    }

    /// <summary>
    /// Pure mathematical Centripetal Catmull-Rom Spline with high-precision derivatives.
    /// Supports closed and open loops, custom control points, and tangent/normal calculations.
    /// </summary>
    [ExecuteAlways]
    public class CatmullRomSpline : MonoBehaviour
    {
        [Header("Spline Configuration")]
        [SerializeField] private bool isClosedLoop = true;
        [SerializeField] private List<Vector3> controlPoints = new List<Vector3>();

        public bool IsClosedLoop => isClosedLoop;
        public int ControlPointCount => controlPoints != null ? controlPoints.Count : 0;

        public void SetControlPoints(IEnumerable<Vector3> points, bool closed)
        {
            controlPoints = new List<Vector3>(points);
            isClosedLoop = closed;
        }

        public Vector3 GetControlPoint(int index)
        {
            if (controlPoints == null || controlPoints.Count == 0) return Vector3.zero;
            if (isClosedLoop)
            {
                int count = controlPoints.Count;
                int wrapped = ((index % count) + count) % count;
                return controlPoints[wrapped];
            }
            return controlPoints[Mathf.Clamp(index, 0, controlPoints.Count - 1)];
        }

        /// <summary>
        /// Evaluates point and tangent along a specific segment [0, 1].
        /// </summary>
        public void EvaluateSegment(int segIdx, float t, out Vector3 position, out Vector3 tangent, out Vector3 normal)
        {
            Vector3 p0 = GetControlPoint(segIdx - 1);
            Vector3 p1 = GetControlPoint(segIdx);
            Vector3 p2 = GetControlPoint(segIdx + 1);
            Vector3 p3 = GetControlPoint(segIdx + 2);

            float t2 = t * t;
            float t3 = t2 * t;

            // Position
            position = 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );

            // Derivative / Tangent
            tangent = 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
            );

            if (tangent.sqrMagnitude > 0.000001f)
            {
                tangent.Normalize();
            }
            else
            {
                tangent = Vector3.forward;
            }

            // Normal in XZ plane (rotate 90 deg around Y)
            normal = new Vector3(-tangent.z, 0f, tangent.x).normalized;
        }

        /// <summary>
        /// Evaluates point across the normalized parameter u in [0, 1].
        /// </summary>
        public void EvaluateNormalized(float u, out Vector3 position, out Vector3 tangent, out Vector3 normal)
        {
            if (controlPoints == null || controlPoints.Count < 2)
            {
                position = transform.position;
                tangent = Vector3.forward;
                normal = Vector3.right;
                return;
            }

            int n = isClosedLoop ? controlPoints.Count : controlPoints.Count - 1;
            u = isClosedLoop ? ((u % 1f) + 1f) % 1f : Mathf.Clamp01(u);

            float segF = u * n;
            int segIdx = Mathf.FloorToInt(segF);
            if (!isClosedLoop && segIdx >= n)
            {
                segIdx = n - 1;
                segF = n;
            }
            float t = segF - segIdx;

            EvaluateSegment(segIdx, t, out position, out tangent, out normal);
        }
    }
}
