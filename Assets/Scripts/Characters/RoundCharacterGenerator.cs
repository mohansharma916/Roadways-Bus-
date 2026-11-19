using UnityEngine;

namespace RoadwaysGame
{
    [ExecuteInEditMode]
    public class RoundCharacterGenerator : MonoBehaviour
    {
        [Header("Jelly Bean Crowd Blob Identity & Colors")]
        public string characterName = "Actual Red Bean";
        [Tooltip("Real actual color: Red, Light Green, Cyan, Violet, Brown")]
        public Color candyColor = new Color(0.93f, 0.07f, 0.07f); // Real Actual Red

        [Header("Material Glossiness (Candy / Clay-like Look)")]
        [Range(0f, 1f)]
        public float smoothness = 0.82f;
        [Range(0f, 1f)]
        public float metallic = 0.04f;

        private GameObject characterModel;

        private void Start()
        {
            if (transform.childCount == 0)
            {
                GenerateCharacter();
            }
        }

        [ContextMenu("Regenerate Jelly Bean Character")]
        public void GenerateCharacter()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            characterModel = new GameObject("CharacterContainer");
            characterModel.transform.SetParent(transform, false);

            // Soft, glossy candy clay material
            Material matCandy = CreatePBRMaterial($"Mat_Candy_{characterName}", candyColor, 1f - smoothness, metallic);

            // 1. BEAN / CAPSULE BODY (Fall Guys / Count Masters pill shape)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "RoundBody";
            body.transform.SetParent(characterModel.transform, false);
            body.transform.localPosition = new Vector3(0, 1.05f, 0);
            body.transform.localScale = new Vector3(0.85f, 0.45f, 0.85f);
            body.GetComponent<Renderer>().sharedMaterial = matCandy;

            // Top Dome of Bean Body
            GameObject topDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            topDome.name = "BodyTopDome";
            topDome.transform.SetParent(body.transform, false);
            topDome.transform.localPosition = new Vector3(0, 1f, 0);
            topDome.transform.localScale = new Vector3(1f, 1f, 1f);
            topDome.GetComponent<Renderer>().sharedMaterial = matCandy;

            // Bottom Dome of Bean Body
            GameObject btmDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            btmDome.name = "BodyBottomDome";
            btmDome.transform.SetParent(body.transform, false);
            btmDome.transform.localPosition = new Vector3(0, -1f, 0);
            btmDome.transform.localScale = new Vector3(1f, 1f, 1f);
            btmDome.GetComponent<Renderer>().sharedMaterial = matCandy;

            // 2. ROUND HEAD (Simple faceless sphere with NO facial features)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "RoundHead";
            head.transform.SetParent(characterModel.transform, false);
            head.transform.localPosition = new Vector3(0, 1.72f, 0);
            head.transform.localScale = new Vector3(0.84f, 0.84f, 0.84f);
            head.GetComponent<Renderer>().sharedMaterial = matCandy;

            // 3. STUBBY BEAN ARMS (Cute rounded pill limbs at sides)
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arm.name = (s == -1) ? "RoundHand_L" : "RoundHand_R";
                arm.transform.SetParent(characterModel.transform, false);
                arm.transform.localPosition = new Vector3(s * 0.55f, 1.15f, 0);
                arm.transform.localRotation = Quaternion.Euler(0, 0, s * -15f);
                arm.transform.localScale = new Vector3(0.24f, 0.22f, 0.24f);
                arm.GetComponent<Renderer>().sharedMaterial = matCandy;

                // Rounded arm cap
                GameObject armCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                armCap.name = (s == -1) ? "HandCap_L" : "HandCap_R";
                armCap.transform.SetParent(arm.transform, false);
                armCap.transform.localPosition = new Vector3(0, -1f, 0);
                armCap.transform.localScale = new Vector3(1f, 1f, 1f);
                armCap.GetComponent<Renderer>().sharedMaterial = matCandy;
            }

            // 4. STUBBY BEAN LEGS (Cute rounded stubby legs & blob feet)
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = (s == -1) ? "RoundLeg_L" : "RoundLeg_R";
                leg.transform.SetParent(characterModel.transform, false);
                leg.transform.localPosition = new Vector3(s * 0.24f, 0.28f, 0);
                leg.transform.localScale = new Vector3(0.26f, 0.2f, 0.26f);
                leg.GetComponent<Renderer>().sharedMaterial = matCandy;

                GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foot.name = (s == -1) ? "RoundFoot_L" : "RoundFoot_R";
                foot.transform.SetParent(leg.transform, false);
                foot.transform.localPosition = new Vector3(0, -1f, 0.25f);
                foot.transform.localScale = new Vector3(1.2f, 0.8f, 1.5f);
                foot.GetComponent<Renderer>().sharedMaterial = matCandy;
            }

            // Attach animator
            if (GetComponent<RoundCharacterAnimator>() == null)
            {
                gameObject.AddComponent<RoundCharacterAnimator>();
            }
        }

        private Material CreatePBRMaterial(string matName, Color color, float roughness, float metallic)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.name = matName;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f - roughness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            return mat;
        }
    }
}
