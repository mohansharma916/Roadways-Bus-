using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// GPU Instanced Renderer for the 430 Chilbi parade.
    /// Draws all 430 characters in single batched GPU draw calls via Graphics.RenderMeshInstanced,
    /// keeping mobile frame times well within the 60 FPS budget (< 16.6ms).
    /// </summary>
    [ExecuteAlways]
    public class ChilbiInstancedRenderer : MonoBehaviour
    {
        [Header("Mesh Assets")]
        [SerializeField] private Mesh bodyMesh;
        [SerializeField] private Mesh legMesh;
        [SerializeField] private Mesh armMesh;
        [SerializeField] private Mesh shadowMesh;

        [Header("Materials")]
        [SerializeField] private Material matteInstancedMaterial;
        [SerializeField] private Material shadowMaterial;

        // GPU Instancing Buffers (Batch size max 1023 in Unity)
        private Matrix4x4[] bodyMatrices;
        private Matrix4x4[] shadowMatrices;
        private Vector4[] baseColorArray;
        private Vector4[] accentColorArray;
        private MaterialPropertyBlock propBlock;

        private int baseColorId;
        private int accentColorId;
        private bool isInitialized = false;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized) return;

            baseColorId = Shader.PropertyToID("_BaseColor");
            accentColorId = Shader.PropertyToID("_AccentColor");
            propBlock = new MaterialPropertyBlock();

            int count = GameConstants.TotalChilbis;
            bodyMatrices = new Matrix4x4[count];
            shadowMatrices = new Matrix4x4[count];
            baseColorArray = new Vector4[count];
            accentColorArray = new Vector4[count];

            EnsureMeshesAndMaterials();
            isInitialized = true;
        }

        private void EnsureMeshesAndMaterials()
        {
            if (bodyMesh == null) bodyMesh = ChilbiMeshGenerator.CreateCandyBeanMesh(1.7f, 2.7f, 1.2f);
            if (legMesh == null) legMesh = ChilbiMeshGenerator.CreateLimbMesh(1.0f, 0.2f, true);
            if (armMesh == null) armMesh = ChilbiMeshGenerator.CreateLimbMesh(1.05f, 0.18f, false);
            if (shadowMesh == null) shadowMesh = ChilbiMeshGenerator.CreateOvalShadowMesh(0.85f, 0.45f);

            Shader matteShader = Shader.Find("Roadways/ChilbiMatte") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (matteInstancedMaterial == null)
            {
                matteInstancedMaterial = new Material(matteShader) { name = "M_ChilbiGPUInstanced" };
                matteInstancedMaterial.enableInstancing = true;
            }

            if (shadowMaterial == null)
            {
                shadowMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
                {
                    name = "M_ChilbiShadowInstanced",
                    color = new Color(0f, 0f, 0f, 0.22f)
                };
                shadowMaterial.SetFloat("_Surface", 1);
                shadowMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                shadowMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                shadowMaterial.SetInt("_ZWrite", 0);
                shadowMaterial.renderQueue = 3000;
                shadowMaterial.enableInstancing = true;
            }
        }

        /// <summary>
        /// Updates a single character's matrix and color in the instancing buffer.
        /// </summary>
        public void SetInstanceTransform(int index, Vector3 position, Quaternion rotation, Vector3 scale, ChilbiColorType colorType, float bobY)
        {
            if (index < 0 || index >= bodyMatrices.Length) return;

            // Body matrix (with bob offset)
            Vector3 bodyPos = position + rotation * new Vector3(0f, 1.4f + bobY, 0f);
            bodyMatrices[index] = Matrix4x4.TRS(bodyPos, rotation, scale);

            // Shadow matrix (flat on ground)
            float shadowScale = 1f - (bobY * 0.5f);
            Vector3 sScale = new Vector3(scale.x * shadowScale, 1f, scale.z * shadowScale);
            shadowMatrices[index] = Matrix4x4.TRS(position + Vector3.up * 0.01f, Quaternion.identity, sScale);

            Color primary = ColorPaletteDatabase.GetPrimaryColor(colorType);
            Color accent = ColorPaletteDatabase.GetAccentColor(colorType);

            baseColorArray[index] = primary;
            accentColorArray[index] = accent;
        }

        /// <summary>
        /// Renders all queued instances via Graphics.RenderMeshInstanced.
        /// </summary>
        public void RenderInstances(int count)
        {
            if (count <= 0) return;
            count = Mathf.Min(count, bodyMatrices.Length);

            propBlock.SetVectorArray(baseColorId, baseColorArray);
            propBlock.SetVectorArray(accentColorId, accentColorArray);

            RenderParams bodyParams = new RenderParams(matteInstancedMaterial)
            {
                matProps = propBlock,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };

            RenderParams shadowParams = new RenderParams(shadowMaterial)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };

            // Single GPU draw call for shadows
            Graphics.RenderMeshInstanced(shadowParams, shadowMesh, 0, shadowMatrices, count);

            // Single GPU draw call for candy bean bodies
            Graphics.RenderMeshInstanced(bodyParams, bodyMesh, 0, bodyMatrices, count);
        }
    }
}
