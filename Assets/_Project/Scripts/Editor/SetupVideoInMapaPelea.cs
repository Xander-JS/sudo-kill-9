using System.IO;
using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;
using SudoKill9.Environment;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta Editor para integrar y configurar la pantalla gigante de video
    /// en la escena Mapa_Pelea_Windows_K con el video Bill.mp4.
    /// </summary>
    public static class SetupVideoInMapaPelea
    {
        private const string ScenePath = "Assets/_Project/Scenes/Experimento_K/Windows_K/Mapa_Pelea_Windows_K.unity";
        private const string VideoPath = "Assets/_Project/Scenes/Experimento_K/Windows_K/Video/Bill.mp4";
        private const string RenderTexturePath = "Assets/_Project/Scenes/Experimento_K/Windows_K/Video/Bill_RenderTexture.renderTexture";
        private const string MaterialPath = "Assets/_Project/Scenes/Experimento_K/Windows_K/Video/Materials/Bill_RenderTexture.mat";
        private const string BezelMaterialPath = "Assets/_Project/Materials/Blockout/Mat_Blockout_Storage.mat";

        [MenuItem("Tools/SudoKill9/Configurar Pantalla de Video Bill en Mapa", priority = 20)]
        public static void ConfigurarVideoEnMapa()
        {
            Debug.Log("<color=cyan>[SetupVideoInMapaPelea]</color> === Iniciando Configuración de Pantalla de Video ===");

            // 1. Abrir la escena si no es la activa
            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != ScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
                else
                {
                    Debug.LogWarning("[SetupVideoInMapaPelea] Operación cancelada por el usuario.");
                    return;
                }
            }

            // 2. Cargar VideoClip
            VideoClip billClip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
            if (billClip == null)
            {
                Debug.LogError($"[SetupVideoInMapaPelea] No se encontró el VideoClip en {VideoPath}");
                return;
            }

            // 3. Asegurar RenderTexture
            RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt == null)
            {
                rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Bill_RenderTexture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[SetupVideoInMapaPelea] RenderTexture creada en {RenderTexturePath}");
            }

            // 4. Asegurar Material
            Material screenMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                                 ?? Shader.Find("Universal Render Pipeline/Lit") 
                                 ?? Shader.Find("Unlit/Texture");

            if (screenMat == null)
            {
                screenMat = new Material(unlitShader)
                {
                    name = "Bill_RenderTexture"
                };
                string dir = Path.GetDirectoryName(MaterialPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(screenMat, MaterialPath);
            }
            else
            {
                if (unlitShader != null && screenMat.shader != unlitShader)
                {
                    screenMat.shader = unlitShader;
                }
            }

            // Asignar textura al material
            if (screenMat.HasProperty("_BaseMap"))
            {
                screenMat.SetTexture("_BaseMap", rt);
                screenMat.SetColor("_BaseColor", Color.white);
            }
            else if (screenMat.HasProperty("_MainTex"))
            {
                screenMat.SetTexture("_MainTex", rt);
                screenMat.SetColor("_Color", Color.white);
            }
            EditorUtility.SetDirty(screenMat);
            AssetDatabase.SaveAssets();

            // Cargar material del marco/bisel
            Material bezelMat = AssetDatabase.LoadAssetAtPath<Material>(BezelMaterialPath);
            if (bezelMat == null) bezelMat = screenMat;

            // 5. Crear o localizar el GameObject Bill_Arena_Display
            GameObject rootDisplay = GameObject.Find("Bill_Arena_Display");
            if (rootDisplay != null)
            {
                Undo.DestroyObjectImmediate(rootDisplay);
            }

            rootDisplay = new GameObject("Bill_Arena_Display");
            Undo.RegisterCreatedObjectUndo(rootDisplay, "Create Bill_Arena_Display");

            // Ubicación: detrás de Windows Boss (Z = 3.83), centrado con la arena (X = 44.0),
            // elevado a Y = 7.5 para vista de estadio jumbotron
            Vector3 displayPos = new Vector3(44.0f, 7.5f, -3.5f);
            rootDisplay.transform.position = displayPos;
            rootDisplay.transform.rotation = Quaternion.identity;
            rootDisplay.transform.localScale = Vector3.one;

            // 6. Configurar VideoPlayer en el root
            VideoPlayer vp = rootDisplay.AddComponent<VideoPlayer>();
            vp.playOnAwake = true;
            vp.isLooping = true;
            vp.source = VideoSource.VideoClip;
            vp.clip = billClip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.aspectRatio = VideoAspectRatio.FitHorizontally;
            vp.waitForFirstFrame = true;
            vp.skipOnDrop = true;

            // 7. Configurar AudioSource 3D
            AudioSource audio = rootDisplay.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.volume = 0.9f;
            audio.spatialBlend = 0.65f;
            audio.minDistance = 6.0f;
            audio.maxDistance = 55.0f;
            audio.rolloffMode = AudioRolloffMode.Linear;

            vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            vp.controlledAudioTrackCount = 1;
            vp.EnableAudioTrack(0, true);
            vp.SetTargetAudioSource(0, audio);

            // 8. Crear Pantalla Frontal (Quad orientado hacia la arena / jugador en +Z)
            GameObject screenFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenFront.name = "Screen_Front";
            screenFront.transform.SetParent(rootDisplay.transform, false);
            // Rotación: 180° en Y para mirar a +Z, 12° en X para inclinar hacia el suelo de la arena
            screenFront.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            screenFront.transform.localRotation = Quaternion.Euler(12f, 180f, 0f);
            screenFront.transform.localScale = new Vector3(10.67f, 6.0f, 1f); // Relación 16:9

            MeshRenderer frontRenderer = screenFront.GetComponent<MeshRenderer>();
            frontRenderer.sharedMaterial = screenMat;
            // Remover MeshCollider para evitar colisiones fantasma en disparos altos
            Collider frontCol = screenFront.GetComponent<Collider>();
            if (frontCol != null) Object.DestroyImmediate(frontCol);

            // 9. Crear Pantalla Posterior (Quad mirando hacia atrás -Z)
            GameObject screenBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenBack.name = "Screen_Back";
            screenBack.transform.SetParent(rootDisplay.transform, false);
            screenBack.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            screenBack.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            screenBack.transform.localScale = new Vector3(10.67f, 6.0f, 1f);

            MeshRenderer backRenderer = screenBack.GetComponent<MeshRenderer>();
            backRenderer.sharedMaterial = screenMat;
            Collider backCol = screenBack.GetComponent<Collider>();
            if (backCol != null) Object.DestroyImmediate(backCol);

            // 10. Crear Marco / Bisel de la Pantalla (Cube)
            GameObject frameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameObj.name = "Bezel_Frame";
            frameObj.transform.SetParent(rootDisplay.transform, false);
            frameObj.transform.localPosition = Vector3.zero;
            frameObj.transform.localRotation = Quaternion.Euler(12f, 180f, 0f);
            frameObj.transform.localScale = new Vector3(11.1f, 6.4f, 0.22f);
            frameObj.GetComponent<MeshRenderer>().sharedMaterial = bezelMat;

            // 11. Columnas de Soporte hacia el suelo (Pilares)
            CrearPilarSoporte("Pillar_Left", rootDisplay.transform, new Vector3(-4.8f, -4.5f, 0f), bezelMat);
            CrearPilarSoporte("Pillar_Right", rootDisplay.transform, new Vector3(4.8f, -4.5f, 0f), bezelMat);

            // 12. Componente Controlador ArenaVideoDisplay
            ArenaVideoDisplay controller = rootDisplay.AddComponent<ArenaVideoDisplay>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("videoClip").objectReferenceValue = billClip;
            so.FindProperty("renderTexture").objectReferenceValue = rt;
            so.FindProperty("screenRenderer").objectReferenceValue = frontRenderer;
            so.FindProperty("backRenderer").objectReferenceValue = backRenderer;
            so.FindProperty("autoPlay").boolValue = true;
            so.FindProperty("loop").boolValue = true;
            so.FindProperty("volume").floatValue = 0.9f;
            so.FindProperty("videoPlayer").objectReferenceValue = vp;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedProperties();

            // 13. Guardar escena
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);

            Debug.Log("<color=green>[SetupVideoInMapaPelea]</color> ¡Pantalla de video 'Bill_Arena_Display' configurada exitosamente en Mapa_Pelea_Windows_K!");
        }

        private static void CrearPilarSoporte(string nombre, Transform parent, Vector3 localPos, Material mat)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = nombre;
            pillar.transform.SetParent(parent, false);
            pillar.transform.localPosition = localPos;
            pillar.transform.localRotation = Quaternion.identity;
            pillar.transform.localScale = new Vector3(0.45f, 4.5f, 0.45f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
