using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Dynamic Portrait FOV Controller.
    /// Adapts the camera's vertical field of view to keep the gameplay area consistently framed
    /// across varying mobile screen aspect ratios (from 16:9 up to 20:9+ tall aspect ratios).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class ResponsiveCameraFOV : MonoBehaviour
    {
        [Header("Reference Dimensions")]
        [Tooltip("Reference aspect ratio (Default 9:16 = 0.5625)")]
        [SerializeField] private float referenceAspectRatio = GameConstants.ReferenceAspectRatio;

        [Tooltip("Base vertical field of view in degrees at reference aspect ratio")]
        [SerializeField] private float baseVerticalFOV = GameConstants.BaseVerticalFOV;

        private Camera targetCamera;
        private float lastAspectRatio = -1f;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            UpdateFOV();
        }

        private void Update()
        {
            UpdateFOV();
        }

        public void UpdateFOV()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) return;
            }

            float currentAspect = (float)Screen.width / Mathf.Max(1f, (float)Screen.height);
            if (Mathf.Approximately(currentAspect, lastAspectRatio)) return;

            lastAspectRatio = currentAspect;

            // Mathematical formula: FOV_v = 2 * atan(tan(baseFOV / 2) * (refAspect / currentAspect))
            float halfFOVRad = (baseVerticalFOV * 0.5f) * Mathf.Deg2Rad;
            float targetHalfFOVRad = Mathf.Atan(Mathf.Tan(halfFOVRad) * (referenceAspectRatio / currentAspect));
            float newVerticalFOV = targetHalfFOVRad * 2f * Mathf.Rad2Deg;

            targetCamera.fieldOfView = Mathf.Clamp(newVerticalFOV, 30f, 90f);
        }
    }
}
