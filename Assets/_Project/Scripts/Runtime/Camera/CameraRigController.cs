using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Static Camera Rig Controller.
    /// Locks camera at (0, 30, 42) looking at (0, 0, 8) with pitch ~35 degrees.
    /// Strictly prevents any orbital, panning, or touch displacement.
    /// </summary>
    [ExecuteAlways]
    public class CameraRigController : MonoBehaviour
    {
        [Header("Rig Coordinates")]
        [SerializeField] private Vector3 rigPosition = GameConstants.CameraPosition;
        [SerializeField] private Vector3 lookTarget = GameConstants.CameraLookTarget;

        [Header("Lock Status")]
        [SerializeField] private bool lockTransform = true;

        private void OnEnable()
        {
            ApplyRigTransform();
        }

        private void LateUpdate()
        {
            if (lockTransform)
            {
                ApplyRigTransform();
            }
        }

        [ContextMenu("Apply Rig Coordinates")]
        public void ApplyRigTransform()
        {
            transform.position = rigPosition;
            transform.LookAt(lookTarget, Vector3.up);
        }
    }
}
