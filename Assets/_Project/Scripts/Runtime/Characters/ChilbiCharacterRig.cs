using UnityEngine;

namespace RoadwaysGame
{
    public enum ChilbiLocomotionState
    {
        IdleWaddle,
        HighSpeedSprint,
        JunctionDecel,
        CruisingWalk
    }

    /// <summary>
    /// 2.5D Rigged Chilbi Character Controller.
    /// Manages:
    /// - Continuous Candy Bean Silhouette (Dome Head + Torso)
    /// - Pelvic Leg Joints with synchronous stride cycles (+-28 deg)
    /// - Curved Bipedal Arms with counter-swings (+-25 deg)
    /// - Dynamic Body Bobbing & Tilting
    /// - Ground Contact Oval Shadow
    /// - 2.5D Billboard Facing Flip (facingX = dx >= 0 ? 1 : -1)
    /// </summary>
    [ExecuteAlways]
    public class ChilbiCharacterRig : MonoBehaviour
    {
        [Header("Color Theme")]
        [SerializeField] private ChilbiColorType colorType = ChilbiColorType.Cyan;

        [Header("Locomotion State")]
        [SerializeField] private ChilbiLocomotionState currentState = ChilbiLocomotionState.CruisingWalk;
        [SerializeField] private float speed = GameConstants.CircuitCruiseSpeed;
        [SerializeField] private float strideFrequency = 8.0f;
        [SerializeField] private float animTime;

        [Header("Rig Transforms")]
        [SerializeField] private Transform bodyGroup;
        [SerializeField] private Transform hipNear;
        [SerializeField] private Transform hipFar;
        [SerializeField] private Transform shoulderNear;
        [SerializeField] private Transform shoulderFar;
        [SerializeField] private Transform groundShadow;

        [Header("Materials & Renderers")]
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private MeshRenderer legNearRenderer;
        [SerializeField] private MeshRenderer legFarRenderer;
        [SerializeField] private MeshRenderer armNearRenderer;
        [SerializeField] private MeshRenderer armFarRenderer;
        [SerializeField] private MeshRenderer shadowRenderer;

        private MaterialPropertyBlock propBlock;
        private int baseColorId;
        private int accentColorId;

        public ChilbiColorType ColorType => colorType;

        private void Awake()
        {
            baseColorId = Shader.PropertyToID("_BaseColor");
            accentColorId = Shader.PropertyToID("_AccentColor");
            propBlock = new MaterialPropertyBlock();

            if (bodyGroup == null)
            {
                BuildRigHierarchy();
            }
            ApplyColorTheme(colorType);
        }

        public void SetLocomotion(ChilbiLocomotionState state, float currentSpeed, float freq, float facingDirX)
        {
            currentState = state;
            speed = currentSpeed;
            strideFrequency = freq;

            // 2.5D Facing Flip
            float sign = facingDirX >= 0f ? 1f : -1f;
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * sign, transform.localScale.y, transform.localScale.z);
        }

        public void UpdateRigAnimation(float dt)
        {
            animTime += dt;

            float f = strideFrequency;
            float twoPiFT = 2f * Mathf.PI * f * animTime;

            float strideAngle = 28f;
            float armAngle = 25f;
            float forwardLean = 0f;

            if (currentState == ChilbiLocomotionState.HighSpeedSprint)
            {
                strideAngle = 36f;
                armAngle = 42f;
                forwardLean = 8f; // Sprint torso lean
            }
            else if (currentState == ChilbiLocomotionState.IdleWaddle)
            {
                strideAngle = 10f;
                armAngle = 8f;
                forwardLean = 0f;
            }

            // Pelvic Leg Strides (opposite phases)
            float nearLegRot = Mathf.Sin(twoPiFT) * strideAngle;
            float farLegRot = -nearLegRot;

            if (hipNear != null) hipNear.localRotation = Quaternion.Euler(nearLegRot, 0f, 0f);
            if (hipFar != null) hipFar.localRotation = Quaternion.Euler(farLegRot, 0f, 0f);

            // Shoulder Arm Counter-Swings (opposite to legs)
            float nearArmRot = -nearLegRot * (armAngle / strideAngle);
            float farArmRot = -farLegRot * (armAngle / strideAngle);

            if (shoulderNear != null) shoulderNear.localRotation = Quaternion.Euler(nearArmRot, 0f, 0f);
            if (shoulderFar != null) shoulderFar.localRotation = Quaternion.Euler(farArmRot, 0f, 0f);

            // Body Bobbing & Tilting
            float bobY = Mathf.Sin(twoPiFT * 2f) * 0.18f;
            float tiltZ = Mathf.Cos(twoPiFT) * 2.5f;

            if (bodyGroup != null)
            {
                bodyGroup.localPosition = new Vector3(0f, 1.4f + bobY, 0f);
                bodyGroup.localRotation = Quaternion.Euler(forwardLean, 0f, tiltZ);
            }

            // Shadow scaling with bob
            if (groundShadow != null)
            {
                float shadowScale = 1f - (bobY * 0.5f);
                groundShadow.localScale = new Vector3(shadowScale, 1f, shadowScale);
            }
        }

        public void ApplyColorTheme(ChilbiColorType type)
        {
            colorType = type;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            Color primary = ColorPaletteDatabase.GetPrimaryColor(type);
            Color accent = ColorPaletteDatabase.GetAccentColor(type);

            propBlock.SetColor(baseColorId, primary);
            propBlock.SetColor(accentColorId, accent);

            if (bodyRenderer != null) bodyRenderer.SetPropertyBlock(propBlock);
            if (armNearRenderer != null) armNearRenderer.SetPropertyBlock(propBlock);
            if (armFarRenderer != null) armFarRenderer.SetPropertyBlock(propBlock);

            // Limbs use slightly deeper accent tone
            MaterialPropertyBlock limbProps = new MaterialPropertyBlock();
            limbProps.SetColor(baseColorId, accent);
            limbProps.SetColor(accentColorId, accent * 0.85f);

            if (legNearRenderer != null) legNearRenderer.SetPropertyBlock(limbProps);
            if (legFarRenderer != null) legFarRenderer.SetPropertyBlock(limbProps);
        }

        [ContextMenu("Build Rig Hierarchy")]
        public void BuildRigHierarchy()
        {
            // Clear children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

            Shader matteShader = Shader.Find("Roadways/ChilbiMatte") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material bodyMat = new Material(matteShader) { name = "M_ChilbiBodyMatte" };

            // 1. Ground Shadow
            GameObject shadowObj = new GameObject("GroundShadow");
            shadowObj.transform.SetParent(transform, false);
            shadowObj.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            MeshFilter mfShadow = shadowObj.AddComponent<MeshFilter>();
            mfShadow.sharedMesh = ChilbiMeshGenerator.CreateOvalShadowMesh(0.85f, 0.45f);
            shadowRenderer = shadowObj.AddComponent<MeshRenderer>();
            Material shadowMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
            {
                color = new Color(0f, 0f, 0f, 0.22f)
            };
            shadowMat.SetFloat("_Surface", 1);
            shadowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            shadowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            shadowMat.SetInt("_ZWrite", 0);
            shadowMat.renderQueue = 3000;
            shadowRenderer.sharedMaterial = shadowMat;
            groundShadow = shadowObj.transform;

            // 2. Body Group
            GameObject bGroup = new GameObject("BodyGroup");
            bGroup.transform.SetParent(transform, false);
            bGroup.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            bodyGroup = bGroup.transform;

            // Candy Bean Mesh
            GameObject beanObj = new GameObject("CandyBeanSilhouette");
            beanObj.transform.SetParent(bodyGroup, false);
            MeshFilter mfBean = beanObj.AddComponent<MeshFilter>();
            mfBean.sharedMesh = ChilbiMeshGenerator.CreateCandyBeanMesh(1.7f, 2.7f, 1.2f);
            bodyRenderer = beanObj.AddComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = bodyMat;

            // 3. Pelvic Hips & Legs
            GameObject hipNearObj = new GameObject("Hip_Near");
            hipNearObj.transform.SetParent(bodyGroup, false);
            hipNearObj.transform.localPosition = new Vector3(0.35f, -0.6f, 0f);
            hipNear = hipNearObj.transform;

            GameObject legNear = new GameObject("Leg_Near");
            legNear.transform.SetParent(hipNear, false);
            MeshFilter mfLegN = legNear.AddComponent<MeshFilter>();
            mfLegN.sharedMesh = ChilbiMeshGenerator.CreateLimbMesh(1.0f, 0.2f, true);
            legNearRenderer = legNear.AddComponent<MeshRenderer>();
            legNearRenderer.sharedMaterial = bodyMat;

            GameObject hipFarObj = new GameObject("Hip_Far");
            hipFarObj.transform.SetParent(bodyGroup, false);
            hipFarObj.transform.localPosition = new Vector3(-0.35f, -0.6f, 0f);
            hipFar = hipFarObj.transform;

            GameObject legFar = new GameObject("Leg_Far");
            legFar.transform.SetParent(hipFar, false);
            MeshFilter mfLegF = legFar.AddComponent<MeshFilter>();
            mfLegF.sharedMesh = ChilbiMeshGenerator.CreateLimbMesh(1.0f, 0.2f, true);
            legFarRenderer = legFar.AddComponent<MeshRenderer>();
            legFarRenderer.sharedMaterial = bodyMat;

            // 4. Shoulders & Arms
            GameObject shNearObj = new GameObject("Shoulder_Near");
            shNearObj.transform.SetParent(bodyGroup, false);
            shNearObj.transform.localPosition = new Vector3(0.9f, 0.4f, 0f);
            shoulderNear = shNearObj.transform;

            GameObject armNear = new GameObject("Arm_Near");
            armNear.transform.SetParent(shoulderNear, false);
            MeshFilter mfArmN = armNear.AddComponent<MeshFilter>();
            mfArmN.sharedMesh = ChilbiMeshGenerator.CreateLimbMesh(1.05f, 0.18f, false);
            armNearRenderer = armNear.AddComponent<MeshRenderer>();
            armNearRenderer.sharedMaterial = bodyMat;

            GameObject shFarObj = new GameObject("Shoulder_Far");
            shFarObj.transform.SetParent(bodyGroup, false);
            shFarObj.transform.localPosition = new Vector3(-0.9f, 0.4f, 0f);
            shoulderFar = shFarObj.transform;

            GameObject armFar = new GameObject("Arm_Far");
            armFar.transform.SetParent(shoulderFar, false);
            MeshFilter mfArmF = armFar.AddComponent<MeshFilter>();
            mfArmF.sharedMesh = ChilbiMeshGenerator.CreateLimbMesh(1.05f, 0.18f, false);
            armFarRenderer = armFar.AddComponent<MeshRenderer>();
            armFarRenderer.sharedMaterial = bodyMat;
        }
    }
}
