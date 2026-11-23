using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>Shows one vehicle on a turntable under the slice's lighting, for judging the model on its own.</summary>
    public sealed class VehiclePreview : MonoBehaviour
    {
        public Color BodyColour = new Color32(0xF2, 0x45, 0x3D, 0xFF);
        public float SpinDegreesPerSecond = 20f;
        [Range(10f, 89f)] public float CameraPitch = 35f;

        private Transform car;

        private void Start()
        {
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xD5, 0xD9, 0xE6, 0xFF);
            Quaternion look = Quaternion.Euler(CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0.3f, 0f) - look * Vector3.forward * 4.2f, look);

            SliceWorld.SetupLighting(transform);
            SliceWorld.Slab(transform, "Floor", new Vector3(0f, -0.01f, 0f), new Vector3(30f, 0.02f, 30f), SliceWorld.Lit(SliceWorld.DepotFloor, 0.25f));
            car = ToyCar.Build(BodyColour, transform);
            car.rotation = Quaternion.Euler(0f, -35f, 0f); // three-quarter view to start
        }

        private void Update() => car.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f);
    }
}
