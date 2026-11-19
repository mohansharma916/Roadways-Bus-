using UnityEngine;

namespace RoadwaysGame
{
    public enum ChibiVehicleType
    {
        CandyCruiser,
        BubbleVan,
        MicroBus,
        SchoolBuggy,
        EcoPod,
        DoubleDeckMaster
    }

    [System.Serializable]
    public struct ChibiBusConfig
    {
        public string modelName;
        public ChibiVehicleType busType;
        public Color primaryCandyColor;
        public Color roofColor;
        public Color accentColor;
        public float length;
        public float width;
        public float height;
        public bool isDoubleDeck;
        public bool hasHood;
    }

    [ExecuteInEditMode]
    public class BusModelGenerator : MonoBehaviour
    {
        [Header("Chibi Candy Vehicle Configuration")]
        public ChibiVehicleType selectedType = ChibiVehicleType.CandyCruiser;
        [Tooltip("Real actual color: Red, Light Green, Cyan, Violet, Brown")]
        public Color primaryColor = new Color(0.93f, 0.07f, 0.07f); // Real Actual Red
        public Color roofColor = Color.white;                        // Marshmallow White
        public Color accentColor = new Color(0.7f, 0f, 0f);

        [Header("Chibi Proportions (Chubby & Rounded)")]
        public float length = 3.8f;
        public float width = 2.1f;
        public float height = 1.85f;

        [Header("Wheel Configuration (Donut Clay Wheels)")]
        public float wheelRadius = 0.42f;
        public float wheelWidth = 0.32f;

        [Header("Candy / Clay Material Settings")]
        [Range(0f, 1f)]
        public float candySmoothness = 0.82f;
        [Range(0f, 1f)]
        public float candyMetallic = 0.05f;

        private GameObject generatedModel;

        private void Start()
        {
            if (transform.childCount == 0)
            {
                GenerateBus();
            }
        }

        [ContextMenu("Regenerate Chibi Candy Car")]
        public void GenerateBus()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            generatedModel = new GameObject("ChibiCarContainer");
            generatedModel.transform.SetParent(transform, false);

            Material matPrimary = CreatePBRMaterial("Mat_CandyPrimary", primaryColor, 1f - candySmoothness, candyMetallic);
            Material matRoof = CreatePBRMaterial("Mat_CandyRoof", roofColor, 1f - candySmoothness, 0.02f);
            Material matMarshmallow = CreatePBRMaterial("Mat_Marshmallow", Color.white, 0.25f, 0.02f);
            Material matGlass = CreatePBRMaterial("Mat_CandyGlass", new Color(0.08f, 0.12f, 0.18f), 0.1f, 0.85f);
            Material matDarkClay = CreatePBRMaterial("Mat_DarkClay", new Color(0.12f, 0.15f, 0.22f), 0.45f, 0.05f);
            Material matHeadlight = CreateEmissiveMaterial("Mat_GumdropHeadlight", Color.white, new Color(1f, 0.95f, 0.6f) * 2.2f);
            Material matTaillight = CreateEmissiveMaterial("Mat_GumdropTaillight", Color.red, Color.red * 2f);

            float wheelY = wheelRadius;

            // 1. Lower Chubby Chassis
            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Chassis";
            chassis.transform.SetParent(generatedModel.transform, false);
            chassis.transform.localPosition = new Vector3(0, wheelY + 0.21f, 0);
            chassis.transform.localScale = new Vector3(width * 0.96f, 0.42f, length * 0.92f);
            chassis.GetComponent<Renderer>().sharedMaterial = matDarkClay;

            // 2. Cabin Body
            float cabinH = height - 0.48f;
            float cabinY = wheelY + 0.42f + cabinH / 2f;

            if (selectedType == ChibiVehicleType.SchoolBuggy)
            {
                float hoodL = 1.0f;
                float mainL = length - hoodL;

                // Main Cabin
                GameObject mainCabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mainCabin.name = "MainCabin";
                mainCabin.transform.SetParent(generatedModel.transform, false);
                mainCabin.transform.localPosition = new Vector3(0, cabinY, -hoodL / 2f);
                mainCabin.transform.localScale = new Vector3(width, cabinH, mainL);
                mainCabin.GetComponent<Renderer>().sharedMaterial = matPrimary;

                // Rounded Front Hood
                float hoodH = cabinH * 0.58f;
                GameObject hood = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hood.name = "EngineHood";
                hood.transform.SetParent(generatedModel.transform, false);
                hood.transform.localPosition = new Vector3(0, wheelY + 0.42f + hoodH / 2f, mainL / 2f);
                hood.transform.localScale = new Vector3(width * 0.9f, hoodH, hoodL);
                hood.GetComponent<Renderer>().sharedMaterial = matPrimary;

                // Windshield
                GameObject ws = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ws.name = "Windshield";
                ws.transform.SetParent(generatedModel.transform, false);
                ws.transform.localPosition = new Vector3(0, wheelY + 0.42f + hoodH + (cabinH - hoodH) / 2f, mainL / 2f - 0.05f);
                ws.transform.localScale = new Vector3(width * 0.86f, cabinH - hoodH + 0.05f, 0.12f);
                ws.GetComponent<Renderer>().sharedMaterial = matGlass;
            }
            else
            {
                // Standard or Double-Decker Chubby Cabin
                GameObject mainCabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mainCabin.name = "MainCabin";
                mainCabin.transform.SetParent(generatedModel.transform, false);
                mainCabin.transform.localPosition = new Vector3(0, cabinY, 0);
                mainCabin.transform.localScale = new Vector3(width, cabinH, length);
                mainCabin.GetComponent<Renderer>().sharedMaterial = matPrimary;

                // Windshield
                float wsH = (selectedType == ChibiVehicleType.DoubleDeckMaster) ? cabinH * 0.78f : cabinH * 0.58f;
                GameObject ws = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ws.name = "Windshield";
                ws.transform.SetParent(generatedModel.transform, false);
                ws.transform.localPosition = new Vector3(0, cabinY + ((selectedType == ChibiVehicleType.DoubleDeckMaster) ? 0 : 0.06f), length / 2f + 0.01f);
                ws.transform.localScale = new Vector3(width * 0.92f, wsH, 0.14f);
                ws.GetComponent<Renderer>().sharedMaterial = matGlass;
            }

            // 3. Side Windows
            GameObject sideWin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideWin.name = "SideWindows";
            sideWin.transform.SetParent(generatedModel.transform, false);
            sideWin.transform.localPosition = new Vector3(0, cabinY + 0.08f, -0.15f);
            sideWin.transform.localScale = new Vector3(width + 0.06f, cabinH * 0.46f, length * 0.76f);
            sideWin.GetComponent<Renderer>().sharedMaterial = matGlass;

            // 4. Curved Candy Bubble Roof (Sphere Dome scaled)
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            roof.name = "BubbleRoof";
            roof.transform.SetParent(generatedModel.transform, false);
            roof.transform.localPosition = new Vector3(0, wheelY + height - 0.04f, 0);
            roof.transform.localScale = new Vector3(width * 1.04f, 0.48f, length * 1.02f);
            roof.GetComponent<Renderer>().sharedMaterial = matRoof;

            // Roof Marshmallow Pod
            GameObject pod = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pod.name = "RoofPod";
            pod.transform.SetParent(generatedModel.transform, false);
            pod.transform.localPosition = new Vector3(0, wheelY + height + 0.22f, -0.2f);
            pod.transform.localScale = new Vector3(width * 0.65f, 0.35f, length * 0.45f);
            pod.GetComponent<Renderer>().sharedMaterial = matMarshmallow;

            // 5. Bubbly Marshmallow Bumpers
            GameObject fBuffer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fBuffer.name = "FrontBumper";
            fBuffer.transform.SetParent(generatedModel.transform, false);
            fBuffer.transform.localPosition = new Vector3(0, wheelY + 0.22f, length / 2f + 0.12f);
            fBuffer.transform.localRotation = Quaternion.Euler(0, 0, 90);
            fBuffer.transform.localScale = new Vector3(0.36f, width * 0.52f, 0.36f);
            fBuffer.GetComponent<Renderer>().sharedMaterial = matMarshmallow;

            GameObject rBuffer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rBuffer.name = "RearBumper";
            rBuffer.transform.SetParent(generatedModel.transform, false);
            rBuffer.transform.localPosition = new Vector3(0, wheelY + 0.22f, -length / 2f - 0.12f);
            rBuffer.transform.localRotation = Quaternion.Euler(0, 0, 90);
            rBuffer.transform.localScale = new Vector3(0.36f, width * 0.52f, 0.36f);
            rBuffer.GetComponent<Renderer>().sharedMaterial = matMarshmallow;

            // 6. Round Gumdrop Headlights
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject hl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hl.name = (s == -1) ? "GumdropHeadlight_L" : "GumdropHeadlight_R";
                hl.transform.SetParent(generatedModel.transform, false);
                hl.transform.localPosition = new Vector3(s * (width * 0.38f), wheelY + 0.46f, length / 2f + 0.08f);
                hl.transform.localScale = new Vector3(0.4f, 0.4f, 0.25f);
                hl.GetComponent<Renderer>().sharedMaterial = matHeadlight;

                GameObject tl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tl.name = (s == -1) ? "GumdropTaillight_L" : "GumdropTaillight_R";
                tl.transform.SetParent(generatedModel.transform, false);
                tl.transform.localPosition = new Vector3(s * (width * 0.38f), wheelY + 0.46f, -length / 2f - 0.06f);
                tl.transform.localScale = new Vector3(0.32f, 0.32f, 0.2f);
                tl.GetComponent<Renderer>().sharedMaterial = matTaillight;
            }

            // 7. Chunky Donut Candy Wheels
            float[] wheelZOffsets = new float[] { length * 0.32f, -length * 0.32f };
            foreach (float zPos in wheelZOffsets)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    GameObject wheelObj = new GameObject($"DonutWheel_{(s == -1 ? "L" : "R")}_{zPos:F1}");
                    wheelObj.transform.SetParent(generatedModel.transform, false);
                    wheelObj.transform.localPosition = new Vector3(s * (width / 2f + 0.05f), wheelRadius, zPos);

                    // Donut Tire
                    GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tire.name = "Tire";
                    tire.transform.SetParent(wheelObj.transform, false);
                    tire.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    tire.transform.localScale = new Vector3(wheelRadius * 2f, wheelWidth, wheelRadius * 2f);
                    tire.GetComponent<Renderer>().sharedMaterial = matDarkClay;

                    // Candy Hubcap
                    GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    hub.name = "CandyHubcap";
                    hub.transform.SetParent(wheelObj.transform, false);
                    hub.transform.localPosition = new Vector3(s * (wheelWidth * 0.42f), 0, 0);
                    hub.transform.localScale = new Vector3(0.15f, wheelRadius * 1.15f, wheelRadius * 1.15f);
                    hub.GetComponent<Renderer>().sharedMaterial = matPrimary;
                }
            }
        }

        private Material CreatePBRMaterial(string matName, Color color, float roughness, float metallic)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(standardShader);
            mat.name = matName;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f - roughness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        private Material CreateEmissiveMaterial(string matName, Color baseColor, Color emissiveColor)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(standardShader);
            mat.name = matName;
            mat.color = baseColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissiveColor);
            }
            return mat;
        }
    }
}
