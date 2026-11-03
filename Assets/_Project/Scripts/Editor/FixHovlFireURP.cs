using UnityEngine;
using UnityEditor;
using System.IO;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta que convierte y repara todos los materiales de Hovl Studio / Procedural fire
    /// para que funcionen con total fidelidad en Universal Render Pipeline (URP).
    /// Elimina el error rosado (magenta) y el problema de los fondos negros opacos,
    /// aplicando modo Transparent + Additive/Alpha Blend y colores Cyan/Azul cibernético.
    /// </summary>
    [InitializeOnLoad]
    public static class FixHovlFireURP
    {
        private const string MaterialsDir = "Assets/Hovl Studio/Procedural fire/Materials";
        private const string TexturesDir = "Assets/Hovl Studio/Procedural fire/Textures";

        static FixHovlFireURP()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                // Si algún material de Hovl sigue usando shaders Built-in o no tiene URP, lo repara automáticamente
                if (NecesitaReparacion())
                {
                    RepararTodosLosMateriales(silent: true);
                }
            };
        }

        private static bool NecesitaReparacion()
        {
            string matPath = MaterialsDir + "/FireSphere1.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) return false;
            // Si no usa Universal Render Pipeline, necesita reparación
            return m.shader == null || !m.shader.name.StartsWith("Universal Render Pipeline");
        }

        [MenuItem("Tools/SudoKill9/Reparar Shaders Hovl Fire (URP)", priority = 20)]
        public static void RepararMenu()
        {
            RepararTodosLosMateriales(silent: false);
        }

        public static void RepararTodosLosMateriales(bool silent = false)
        {
            Shader urpParticle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticle == null)
            {
                Debug.LogError("[FixHovlFireURP] No se encontró el shader 'Universal Render Pipeline/Particles/Unlit'.");
                return;
            }

            Texture2D texFire1 = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/FireSphere1.png");
            Texture2D texFire2 = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/FireSphere2.png");
            Texture2D texSmoke = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/Smoke26.png");
            Texture2D texTrail = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/Trail67.png");

            Color cyanColor = new Color(0.12f, 0.90f, 1.0f, 1.0f);
            Color softCyan = new Color(0.20f, 0.75f, 0.95f, 0.6f);

            // 1. FireSphere1.mat (Fuego aditivo principal)
            ConfigurarMaterial(MaterialsDir + "/FireSphere1.mat", urpParticle, texFire1, cyanColor, additive: true);

            // 2. FireSphere2.mat (Fuego secundario / capas)
            ConfigurarMaterial(MaterialsDir + "/FireSphere2.mat", urpParticle, texFire2, cyanColor, additive: true);

            // 3. FireSphere3.mat (Fuego alternativo)
            ConfigurarMaterial(MaterialsDir + "/FireSphere3.mat", urpParticle, texFire2, cyanColor, additive: true);

            // 4. SparkSphere.mat (Chispas orbitales)
            ConfigurarMaterial(MaterialsDir + "/SparkSphere.mat", urpParticle, texFire1, Color.white, additive: true);

            // 5. Smoke26.mat (Humo - Alpha blend)
            ConfigurarMaterial(MaterialsDir + "/Smoke26.mat", urpParticle, texSmoke, softCyan, additive: false);

            // 6. Trail67.mat (Estelas de energía)
            ConfigurarMaterial(MaterialsDir + "/Trail67.mat", urpParticle, texTrail, cyanColor, additive: true);

            // 7. Trail68.mat (Estelas secundarias)
            ConfigurarMaterial(MaterialsDir + "/Trail68.mat", urpParticle, texTrail, cyanColor, additive: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                Debug.Log("<color=cyan>[FixHovlFireURP]</color> ¡Todos los materiales de Hovl Studio convertidos a URP Particles/Unlit Transparent Additive con éxito!");
            }
        }

        private static void ConfigurarMaterial(string path, Shader shader, Texture2D textura, Color colorBase, bool additive)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            // Modo Superficie Transparente
            mat.SetFloat("_Surface", 1.0f); // 1 = Transparent
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            if (additive)
            {
                // Modo Aditivo
                mat.SetFloat("_Blend", 1.0f); // 1 = Additive
                mat.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                // Modo Alpha Blend (Humo)
                mat.SetFloat("_Blend", 0.0f); // 0 = Alpha
                mat.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            // Asignación de Texturas y Colores en propiedades estándar URP
            if (textura != null)
            {
                mat.SetTexture("_BaseMap", textura);
                mat.mainTexture = textura;
            }

            mat.SetColor("_BaseColor", colorBase);
            mat.color = colorBase;

            // Habilitar Vertex Color para que las partículas de diferentes colores se tiñan correctamente
            mat.SetFloat("_ColorMode", 0); // Multiply

            EditorUtility.SetDirty(mat);
        }
    }
}
