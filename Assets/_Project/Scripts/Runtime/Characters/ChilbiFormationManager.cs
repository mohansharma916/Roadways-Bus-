using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Master Formation & Kinematics Manager for all 430 Chilbi characters.
    /// Manages:
    /// - 430-character squad data across 62 single-color rows
    /// - High-Speed Feeder Entrance Sprint (150 px/s, 19.5 Hz)
    /// - Junction Deceleration (smoothstep down to 46 px/s)
    /// - Ring Road Cruising Walk (46 px/s, 8.0 Hz) with exact 60-row tight closure (13.84px spacing)
    /// - Feeder Queue Hold System (10 Chilbis hold behind X = 30 stop line with 1.8 Hz idle waddles)
    /// - Cyan Lead Runner (14px ahead of Row 0)
    /// - Heat-Saver Development Toggle (render only lead runner in editor)
    /// - High-speed GPU Instanced batching (< 0.3ms frame budget)
    /// </summary>
    [ExecuteAlways]
    public class ChilbiFormationManager : MonoBehaviour
    {
        [Header("Development Heat-Saver Toggle")]
        [Tooltip("If true, renders only the single Cyan Lead Chilbi while running full 430 kinematics.")]
        [SerializeField] private bool renderOnlyLeadChilbi = false;

        [Header("Spline References")]
        [SerializeField] private TrackNetworkManager trackNetwork;
        [SerializeField] private SplineArcLengthTable circuitLut;
        [SerializeField] private SplineArcLengthTable feederLut;

        [Header("Formation Speeds & Frequencies")]
        [SerializeField] private float sprintSpeed = GameConstants.FeederEntranceSpeed; // 150 px/s
        [SerializeField] private float cruiseSpeed = GameConstants.CircuitCruiseSpeed;  // 46 px/s
        [SerializeField] private float sprintFrequency = 19.5f;
        [SerializeField] private float cruiseFrequency = 8.0f;
        [SerializeField] private float idleFrequency = 1.8f;
        [SerializeField] private float leadAdvanceDistance = 14.0f;

        [Header("GPU Instancer")]
        [SerializeField] private ChilbiInstancedRenderer instancer;

        [Header("Runtime State")]
        [SerializeField] private float simulationTime = 0f;
        [SerializeField] private float leadDistance = 0f;
        [SerializeField] private float rowSpacing = 13.84f; // 830.31 / 60

        // Per-character state tracking
        private ChilbiInstanceData[] formationData;
        private float[] characterDistances;
        private bool[] hasMergedIntoCircuit;
        private bool isInitialized = false;

        public bool RenderOnlyLeadChilbi
        {
            get => renderOnlyLeadChilbi;
            set => renderOnlyLeadChilbi = value;
        }

        private void Start()
        {
            InitializeFormation();
        }

        [ContextMenu("Initialize Formation")]
        public void InitializeFormation()
        {
            if (trackNetwork == null) trackNetwork = FindAnyObjectByType<TrackNetworkManager>();
            if (trackNetwork != null)
            {
                if (trackNetwork.CircuitLut == null || !trackNetwork.CircuitLut.IsValid)
                {
                    trackNetwork.BuildCompleteTrackNetwork();
                }
                circuitLut = trackNetwork.CircuitLut;
                feederLut = trackNetwork.GetComponentInChildren<FeederRoadController>()?.LeftFeederLut;
            }

            if (instancer == null)
            {
                instancer = GetComponent<ChilbiInstancedRenderer>();
                if (instancer == null) instancer = gameObject.AddComponent<ChilbiInstancedRenderer>();
            }
            instancer.Initialize();

            formationData = ChilbiFormationData.GenerateFormation();
            int count = formationData.Length;

            characterDistances = new float[count];
            hasMergedIntoCircuit = new bool[count];

            // Calculate exact closed loop row spacing
            if (circuitLut != null && circuitLut.IsValid)
            {
                rowSpacing = circuitLut.TotalLength / GameConstants.CircuitMaxRows; // 830.31 / 60 = 13.84
            }

            // Initialize row distances along feeder road
            // Row 0 starts at feeder entrance s = 0
            float leadOffset = leadAdvanceDistance;
            for (int i = 0; i < count; i++)
            {
                int row = formationData[i].rowIndex;
                // Characters queued sequentially along feeder road
                characterDistances[i] = -leadOffset - (row * rowSpacing);
                hasMergedIntoCircuit[i] = false;
            }

            isInitialized = true;
            Debug.Log($"<color=#2ecc71>[ChilbiFormationManager] Initialized 430 Chilbis across 62 rows. Row spacing: {rowSpacing:F2}px.</color>");
        }

        private void Update()
        {
            if (!isInitialized || formationData == null)
            {
                InitializeFormation();
                if (!isInitialized) return;
            }

            float dt = Application.isPlaying ? Time.deltaTime : 0.0166f;
            simulationTime += dt;

            UpdateKinematics(dt);
        }

        private void UpdateKinematics(float dt)
        {
            if (circuitLut == null || !circuitLut.IsValid)
            {
                InitializeFormation();
                if (circuitLut == null || !circuitLut.IsValid) return;
            }

            float feederLen = (feederLut != null && feederLut.IsValid) ? feederLut.TotalLength : 150f;
            float circuitLen = circuitLut.TotalLength;
            float queueStopDist = feederLen - 68.0f; // Behind X = 30 stop line

            int totalChilbis = formationData.Length;
            int renderCount = renderOnlyLeadChilbi ? 1 : totalChilbis;

            float scale = trackNetwork != null ? trackNetwork.WorldScale : 0.1f;

            for (int i = 0; i < totalChilbis; i++)
            {
                int row = formationData[i].rowIndex;
                bool isCircuitRow = row < GameConstants.CircuitMaxRows; // Rows 0-59 (420 Chilbis)
                bool isQueueBufferRow = !isCircuitRow;                   // Rows 60-61 (10 Chilbis)

                float dist = characterDistances[i];
                float currentSpeed = cruiseSpeed;
                float currentFreq = cruiseFrequency;
                ChilbiLocomotionState state = ChilbiLocomotionState.CruisingWalk;

                // State Machine: Feeder Sprint -> Junction Decel -> Circuit Cruise OR Queue Hold
                if (!hasMergedIntoCircuit[i])
                {
                    if (isQueueBufferRow && dist >= queueStopDist)
                    {
                        // Queue Hold behind stop bar
                        dist = queueStopDist - ((row - GameConstants.CircuitMaxRows) * rowSpacing);
                        currentSpeed = 0f;
                        currentFreq = idleFrequency;
                        state = ChilbiLocomotionState.IdleWaddle;
                    }
                    else
                    {
                        // Check merge transition near feeder end
                        if (dist >= feederLen)
                        {
                            hasMergedIntoCircuit[i] = true;
                            // Merge point on circuit is index 12 (s ~ 625px)
                            dist = dist - feederLen; // Continue onto circuit
                            currentSpeed = cruiseSpeed;
                            currentFreq = cruiseFrequency;
                            state = ChilbiLocomotionState.CruisingWalk;
                        }
                        else
                        {
                            // Sprinting along feeder road
                            currentSpeed = sprintSpeed;
                            currentFreq = sprintFrequency;
                            state = ChilbiLocomotionState.HighSpeedSprint;

                            // Junction Deceleration ease when approaching merge
                            float distToMerge = feederLen - dist;
                            if (distToMerge < 40f)
                            {
                                float tDecel = 1f - Mathf.Clamp01(distToMerge / 40f);
                                currentSpeed = Mathf.Lerp(sprintSpeed, cruiseSpeed, tDecel);
                                currentFreq = Mathf.Lerp(sprintFrequency, cruiseFrequency, tDecel);
                                state = ChilbiLocomotionState.JunctionDecel;
                            }

                            dist += currentSpeed * dt;
                        }
                    }
                }
                else
                {
                    // Cruising along closed circuit
                    currentSpeed = cruiseSpeed;
                    currentFreq = cruiseFrequency;
                    state = ChilbiLocomotionState.CruisingWalk;
                    dist = (dist + currentSpeed * dt) % circuitLen;
                }

                characterDistances[i] = dist;

                // Evaluate Spline Coordinates
                Vector3 splinePos, tangent, normal;
                if (!hasMergedIntoCircuit[i])
                {
                    if (feederLut != null && feederLut.IsValid)
                    {
                        feederLut.SampleAtDistance(Mathf.Max(0f, dist), out splinePos, out tangent, out normal);
                    }
                    else
                    {
                        splinePos = Vector3.left * 50f;
                        tangent = Vector3.right;
                        normal = Vector3.forward;
                    }
                }
                else
                {
                    circuitLut.SampleAtDistance(dist, out splinePos, out tangent, out normal);
                }

                // Add lateral lane offset across road
                float lateralOffset = formationData[i].laneOffset;
                Vector3 charWorldPos = splinePos + normal * lateralOffset;

                // Map to Unity world space
                Vector3 finalWorldPos = trackNetwork != null
                    ? trackNetwork.PrototypeToWorldPoint(charWorldPos.x, charWorldPos.z, 0f)
                    : charWorldPos * scale;

                // Bobbing calculation
                float visualScale = scale * 10f;
                float twoPiFT = 2f * Mathf.PI * currentFreq * simulationTime;
                float bobY = Mathf.Sin(twoPiFT * 2f) * 0.05f;

                // Facing direction
                float facingDirX = tangent.x;
                float sign = facingDirX >= 0f ? 1f : -1f;
                Vector3 charScale = new Vector3(visualScale * sign, visualScale, visualScale);

                // Figurine tilt facing overhead camera (camera pitch ~35-40 deg)
                Quaternion billboardRot = Quaternion.Euler(-35f, 0f, 0f);

                // Queue to GPU Instancer
                if (i < renderCount)
                {
                    instancer.SetInstanceTransform(i, finalWorldPos, billboardRot, charScale, formationData[i].colorType, bobY);
                }
            }

            // Draw all instances
            instancer.RenderInstances(renderCount);
        }
    }
}
