using RoadwaysGame.Core;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>Shows all four vehicle models, each on its own turntable with its name and seats, under the slice's lighting.</summary>
    public sealed class VehiclePreview : MonoBehaviour
    {
        public float SpinDegreesPerSecond = 20f;
        [Range(10f, 89f)] public float CameraPitch = 35f;

        private static readonly (VehicleKind kind, string name, int colour)[] Lineup =
        {
            (VehicleKind.Coupe, "Car", 0), (VehicleKind.BigCar, "Big car", 2), (VehicleKind.Minivan, "Minivan", 3), (VehicleKind.Bus, "Bus", 1),
        };

        private readonly Transform[] models = new Transform[Lineup.Length];
        private Camera cam;

        private void Start()
        {
            cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xD3, 0xDA, 0xED, 0xFF);
            Quaternion look = Quaternion.Euler(CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0.3f, -0.5f) - look * Vector3.forward * 12f, look);

            SliceWorld.SetupLighting(transform);
            SliceWorld.Slab(transform, "Floor", new Vector3(0f, -0.01f, 0f), new Vector3(40f, 0.02f, 40f), SliceWorld.Lit(SliceWorld.DepotFloor, 0f));
            for (int i = 0; i < Lineup.Length; i++)
            {
                var spot = new Vector3(i % 2 == 0 ? -1.6f : 1.6f, 0f, i < 2 ? 1.6f : -1.6f); // 2 x 2, wide enough for a bus to turn
                models[i] = ToyVehicle.Build(Lineup[i].kind, ColorPaletteDatabase.GetPrimaryColor((ChilbiColorType)Lineup[i].colour), transform, out _);
                models[i].localPosition = spot;
                models[i].localRotation = Quaternion.Euler(0f, -35f, 0f); // three-quarter view to start
                Label(Lineup[i].name + "  " + new VehicleDef(Lineup[i].kind, 0, default, 0f).Capacity + " seats", spot + new Vector3(0f, 0.05f, -1.45f)); // in front, clear of a turning bus
            }
        }

        private void Label(string text, Vector3 at)
        {
            var go = new GameObject("Label", typeof(TextMesh));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.rotation = cam.transform.rotation; // face the camera
            var tm = go.GetComponent<TextMesh>();
            tm.font = SliceWorld.GameFont;
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = 0.32f * 10f / tm.fontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color32(0x2E, 0x34, 0x48, 0xFF);
        }

        private void Update()
        {
            foreach (Transform m in models) m.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f);
        }
    }
}
