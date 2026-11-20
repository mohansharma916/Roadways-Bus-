using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Master Coordinator for the Track & Roadway Spline Network.
    /// Owns the 16-point circuit spline, feeder roads, highway neck, and mesh generation.
    /// Centers and scales the track network to fit the mobile camera view frustum.
    /// </summary>
    [ExecuteAlways]
    public class TrackNetworkManager : MonoBehaviour
    {
        [Header("World Mapping & Framing")]
        [Tooltip("Scale factor mapping prototype coordinate space (0-500) to Unity 3D world units.")]
        [SerializeField] private float worldScale = 0.1f;

        [Tooltip("Center offset in prototype units before scaling (Default: X=250, Z=170).")]
        [SerializeField] private Vector2 centerOffset = new Vector2(250f, 170f);

        [Tooltip("World position target in Unity space (matches Camera Look Target (0, 0, 8)).")]
        [SerializeField] private Vector3 worldAnchor = new Vector3(0f, 0f, 8f);

        [Header("Components")]
        [SerializeField] private CatmullRomSpline circuitSpline;
        [SerializeField] private SplineArcLengthTable circuitLut;
        [SerializeField] private RoadMeshBuilder roadMeshBuilder;
        [SerializeField] private ApexKerbGenerator apexKerbGenerator;
        [SerializeField] private FeederRoadController feederController;
        [SerializeField] private HighwayNeckBuilder highwayNeckBuilder;

        public CatmullRomSpline CircuitSpline => circuitSpline;
        public SplineArcLengthTable CircuitLut => circuitLut;
        public float WorldScale => worldScale;

        // 16 Exact Control Points from 3D_Showcase_Preview.html
        private static readonly Vector2[] Base16Points = new Vector2[]
        {
            new Vector2(250.0f, 236.0f),
            new Vector2(312.0f, 234.0f),
            new Vector2(368.0f, 216.0f),
            new Vector2(408.0f, 178.0f),
            new Vector2(412.0f, 134.0f),
            new Vector2(382.0f, 94.0f),
            new Vector2(335.0f, 80.0f),
            new Vector2(295.0f, 96.0f),
            new Vector2(250.0f, 114.0f),
            new Vector2(205.0f, 96.0f),
            new Vector2(165.0f, 80.0f),
            new Vector2(118.0f, 94.0f),
            new Vector2(88.0f,  134.0f), // Merge junction
            new Vector2(92.0f,  178.0f),
            new Vector2(132.0f, 216.0f),
            new Vector2(188.0f, 234.0f)
        };

        private void Start()
        {
            if (transform.childCount == 0 || circuitLut == null || !circuitLut.IsValid)
            {
                BuildCompleteTrackNetwork();
            }
        }

        [ContextMenu("Build Complete Track Network")]
        public void BuildCompleteTrackNetwork()
        {
            ApplyTransformScaling();
            SetupCircuitSpline();

            if (roadMeshBuilder == null) roadMeshBuilder = GetComponentInChildren<RoadMeshBuilder>(true);
            if (apexKerbGenerator == null) apexKerbGenerator = GetComponentInChildren<ApexKerbGenerator>(true);
            if (feederController == null) feederController = GetComponentInChildren<FeederRoadController>(true);
            if (highwayNeckBuilder == null) highwayNeckBuilder = GetComponentInChildren<HighwayNeckBuilder>(true);

            if (roadMeshBuilder != null) roadMeshBuilder.BuildRoadNetworkMeshes();
            if (apexKerbGenerator != null) apexKerbGenerator.BuildApexKerbs();
            if (feederController != null) feederController.BuildFeederRoads();
            if (highwayNeckBuilder != null) highwayNeckBuilder.BuildHighwayNeck();

            Debug.Log($"<color=#2ecc71>[TrackNetworkManager] Built track network. Circuit Total Length: {circuitLut?.TotalLength:F1} units.</color>");
        }

        private void ApplyTransformScaling()
        {
            // Position root at anchor and set scale
            transform.position = worldAnchor;
            transform.localScale = new Vector3(worldScale, 1f, -worldScale); // -Z because screen Y increases downward
        }

        private void SetupCircuitSpline()
        {
            GameObject splineObj;
            if (circuitSpline == null)
            {
                Transform child = transform.Find("TrackSpline");
                if (child != null)
                {
                    splineObj = child.gameObject;
                }
                else
                {
                    splineObj = new GameObject("TrackSpline");
                    splineObj.transform.SetParent(transform, false);
                }

                circuitSpline = splineObj.GetComponent<CatmullRomSpline>() ?? splineObj.AddComponent<CatmullRomSpline>();
                circuitLut = splineObj.GetComponent<SplineArcLengthTable>() ?? splineObj.AddComponent<SplineArcLengthTable>();
                roadMeshBuilder = splineObj.GetComponent<RoadMeshBuilder>() ?? splineObj.AddComponent<RoadMeshBuilder>();
                apexKerbGenerator = splineObj.GetComponent<ApexKerbGenerator>() ?? splineObj.AddComponent<ApexKerbGenerator>();
            }
            else
            {
                splineObj = circuitSpline.gameObject;
            }

            // Transform raw points relative to center offset
            List<Vector3> pts3D = new List<Vector3>(Base16Points.Length);
            for (int i = 0; i < Base16Points.Length; i++)
            {
                float x = Base16Points[i].x - centerOffset.x;
                float z = Base16Points[i].y - centerOffset.y;
                pts3D.Add(new Vector3(x, 0f, z));
            }

            circuitSpline.SetControlPoints(pts3D, true);
            circuitLut.RebuildTable();

            // Setup Feeder and Highway Neck child GameObjects if not present
            if (feederController == null)
            {
                Transform fChild = transform.Find("FeederRoads");
                GameObject fObj = fChild != null ? fChild.gameObject : new GameObject("FeederRoads");
                fObj.transform.SetParent(transform, false);
                feederController = fObj.GetComponent<FeederRoadController>() ?? fObj.AddComponent<FeederRoadController>();
            }

            if (highwayNeckBuilder == null)
            {
                Transform hChild = transform.Find("HighwayNeck");
                GameObject hObj = hChild != null ? hChild.gameObject : new GameObject("HighwayNeck");
                hObj.transform.SetParent(transform, false);
                highwayNeckBuilder = hObj.GetComponent<HighwayNeckBuilder>() ?? hObj.AddComponent<HighwayNeckBuilder>();
            }
        }

        /// <summary>
        /// Converts prototype (x, y) coordinates to Unity world space.
        /// </summary>
        public Vector3 PrototypeToWorldPoint(float px, float py, float heightY = 0f)
        {
            float localX = (px - centerOffset.x) * worldScale;
            float localZ = -(py - centerOffset.y) * worldScale;
            return worldAnchor + new Vector3(localX, heightY, localZ);
        }
    }
}
