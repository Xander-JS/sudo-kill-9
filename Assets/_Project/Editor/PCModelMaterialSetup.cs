using UnityEngine;
using UnityEditor;
using System.IO;

namespace SudoKill9.Editor
{
    /// <summary>
    /// Herramienta para configurar automáticamente los Materiales y Texturas URP de los modelos de Inicio (PC y Armas).
    /// </summary>
    public static class PCModelMaterialSetup
    {
        private const string BaseDir = "Assets/_Project/Models/Environment/Inicio_modelos";
        private const string TexDir = BaseDir + "/Texture";
        private const string MatDir = BaseDir + "/Material";

        // Rutas PC
        private const string PcFbxPath = BaseDir + "/PC.fbx";
        private const string PcColorTexPath = TexDir + "/Color_f7774676-321d-4b59-9dd4-210118f72dd8.png";
        private const string PcNormalTexPath = TexDir + "/NormalGL_f7774676-321d-4b59-9dd4-210118f72dd8.png";
        private const string PcMatPath = MatDir + "/PC_Material.mat";

        // Rutas Armas
        private const string ArmasFbxPath = BaseDir + "/Armas.fbx";
        private const string ArmasColorTexPath = TexDir + "/Color_7979483c-aa5d-4184-b806-2b83ac2f29cb.png";
        private const string ArmasNormalTexPath = TexDir + "/NormalGL_7979483c-aa5d-4184-b806-2b83ac2f29cb.png";
        private const string ArmasMatPath = TexDir + "/tripo_material_7979483c-aa5d-4184-b806-2b83ac2f29cb.mat";

        [MenuItem("Tools/The Last Root/Setup All Inicio Models (PC & Armas)")]
        public static void SetupAllInicioModels()
        {
            SetupPCMaterial();
            SetupArmasMaterial();
        }

        [MenuItem("Tools/The Last Root/Setup Armas Model Material & Textures")]
        public static void SetupArmasMaterial()
        {
            AssetDatabase.Refresh();

            // 1. Configurar textura normal de Armas
            TextureImporter normalImporter = AssetImporter.GetAtPath(ArmasNormalTexPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            // 2. Cargar texturas de Armas
            Texture2D colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArmasColorTexPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArmasNormalTexPath);

            // 3. Crear o actualizar Material URP Lit para Armas
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ArmasMatPath);
            if (mat == null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(urpShader);
                AssetDatabase.CreateAsset(mat, ArmasMatPath);
            }

            if (colorTex != null)
            {
                mat.SetTexture("_BaseMap", colorTex);
                mat.color = Color.white;
            }

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0.3f);
            EditorUtility.SetDirty(mat);

            // 4. Mapear en Armas.fbx
            ModelImporter modelImporter = AssetImporter.GetAtPath(ArmasFbxPath) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                modelImporter.materialSearch = ModelImporterMaterialSearch.Local;
                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "tripo_material_7979483c-aa5d-4184-b806-2b83ac2f29cb"), mat);
                modelImporter.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[InicioModelMaterialSetup] ¡Material URP y texturas 4K de Armas.fbx configurados exitosamente!");
        }

        [MenuItem("Tools/The Last Root/Setup PC Model Material & Textures")]
        public static void SetupPCMaterial()
        {
            if (!Directory.Exists(MatDir))
            {
                Directory.CreateDirectory(MatDir);
            }

            AssetDatabase.Refresh();

            // 1. Configurar textura normal como Normal Map en Unity
            TextureImporter normalImporter = AssetImporter.GetAtPath(PcNormalTexPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            // 2. Cargar texturas
            Texture2D colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PcColorTexPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PcNormalTexPath);

            // 3. Crear o actualizar Material URP Lit
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PcMatPath);
            if (mat == null)
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(urpShader);
                AssetDatabase.CreateAsset(mat, PcMatPath);
            }

            if (colorTex != null)
            {
                mat.SetTexture("_BaseMap", colorTex);
                mat.color = Color.white;
            }

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.4f);
            EditorUtility.SetDirty(mat);

            // 4. Mapear el material en el ModelImporter de PC.fbx
            ModelImporter modelImporter = AssetImporter.GetAtPath(PcFbxPath) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                modelImporter.materialSearch = ModelImporterMaterialSearch.Local;
                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "colorReal.001"), mat);
                modelImporter.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[InicioModelMaterialSetup] ¡Material URP y texturas 4K de PC.fbx configurados exitosamente!");
        }
    }
}
