using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// High-performance Arc-Length Lookup Table (LUT).
    /// Pre-samples spline into equidistant segments for O(1) or O(log N) constant linear speed evaluation.
    /// Eliminates parameter-speed distortion through chicane dips and curves.
    /// </summary>
    [ExecuteAlways]
    public class SplineArcLengthTable : MonoBehaviour
    {
        [Header("Sampling Configuration")]
        [SerializeField] private CatmullRomSpline spline;
        [SerializeField] private int sampleCount = 1000;

        [Header("Runtime Cache (Read-Only)")]
        [SerializeField] private float totalLength;
        [SerializeField] private List<SplineSample> table = new List<SplineSample>();

        public float TotalLength => totalLength;
        public int SampleCount => sampleCount;
        public bool IsValid => table != null && table.Count > 1 && totalLength > 0.001f;

        private void Awake()
        {
            if (spline == null) spline = GetComponent<CatmullRomSpline>();
            if (!IsValid) RebuildTable();
        }

        [ContextMenu("Rebuild Arc-Length Table")]
        public void RebuildTable()
        {
            if (spline == null) spline = GetComponent<CatmullRomSpline>();
            if (spline == null || spline.ControlPointCount < 2) return;

            table.Clear();
            totalLength = 0f;

            Vector3 prevPos = Vector3.zero;

            for (int i = 0; i <= sampleCount; i++)
            {
                float u = (float)i / sampleCount;
                spline.EvaluateNormalized(u, out Vector3 pos, out Vector3 tan, out Vector3 norm);

                if (i > 0)
                {
                    totalLength += Vector3.Distance(prevPos, pos);
                }

                table.Add(new SplineSample
                {
                    position = pos,
                    tangent = tan,
                    normal = norm,
                    distance = totalLength
                });

                prevPos = pos;
            }
        }

        /// <summary>
        /// Samples position, tangent, and normal at exact linear distance s along spline.
        /// </summary>
        public void SampleAtDistance(float s, out Vector3 position, out Vector3 tangent, out Vector3 normal)
        {
            if (!IsValid) RebuildTable();

            if (!IsValid)
            {
                position = transform.position;
                tangent = Vector3.forward;
                normal = Vector3.right;
                return;
            }

            bool isClosed = spline != null && spline.IsClosedLoop;
            if (isClosed)
            {
                s = ((s % totalLength) + totalLength) % totalLength;
            }
            else
            {
                s = Mathf.Clamp(s, 0f, totalLength);
            }

            // Binary search for segment enclosing s
            int low = 0;
            int high = table.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (table[mid].distance < s)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            int idx1 = Mathf.Clamp(low, 0, table.Count - 1);
            int idx0 = Mathf.Clamp(idx1 - 1, 0, table.Count - 1);

            SplineSample s0 = table[idx0];
            SplineSample s1 = table[idx1];

            float span = s1.distance - s0.distance;
            float frac = span > 0.00001f ? Mathf.Clamp01((s - s0.distance) / span) : 0f;

            position = Vector3.Lerp(s0.position, s1.position, frac);
            tangent = Vector3.Slerp(s0.tangent, s1.tangent, frac).normalized;
            normal = Vector3.Slerp(s0.normal, s1.normal, frac).normalized;
        }
    }
}
