using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;
using SudoKill9.Cinematics;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Monta la cinemática final en Mapa_completo:
    ///   FinalCinematicDirector (VideoPlayer -> RenderTexture, audio directo)
    ///     └─ PantallaCielo (raíz escalada a 16:9, oculta hasta la muerte del boss)
    ///          ├─ Superficie (Quad, material URP Unlit con la RenderTexture)
    ///          └─ Marco      (Cubo oscuro detrás, sin collider)
    /// Es idempotente: si ya existe el director lo reconstruye.
    /// </summary>
    public static class SetupFinalCinematic
    {
        private const string ScenePath = "Assets/_Project/Scenes/GamePlay/Mapa_completo.unity";
        private const string VideoPath = "Assets/_Project/VideoPlay/bill.webm";
        private const string RenderTexturePath = "Assets/_Project/VideoPlay/Bill_SkyScreen.renderTexture";
        private const string ScreenMaterialPath = "Assets/_Project/VideoPlay/Mat_Bill_SkyScreen.mat";
        private const string BezelMaterialPath = "Assets/_Project/VideoPlay/Mat_SkyScreen_Bezel.mat";
        private const string DirectorName = "FinalCinematicDirector";

        [MenuItem("Tools/SudoKill9/Configurar Cinemática Final (Monitor en el cielo)", priority = 21)]
        public static void Configurar()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
            if (clip == null)
            {
                Debug.LogError($"[SetupFinalCinematic] No se encontró el VideoClip en {VideoPath}");
                return;
            }

            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt == null)
            {
                rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Bill_SkyScreen",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
            }

            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var screenMat = ObtenerMaterial(ScreenMaterialPath, unlit);
            screenMat.mainTexture = rt;
            if (screenMat.HasProperty("_BaseMap")) screenMat.SetTexture("_BaseMap", rt);
            if (screenMat.HasProperty("_BaseColor")) screenMat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(screenMat);

            var bezelMat = ObtenerMaterial(BezelMaterialPath, unlit);
            var bezelColor = new Color(0.04f, 0.04f, 0.05f);
            if (bezelMat.HasProperty("_BaseColor")) bezelMat.SetColor("_BaseColor", bezelColor);
            bezelMat.color = bezelColor;
            EditorUtility.SetDirty(bezelMat);

            // Reconstruir desde cero para que la herramienta se pueda relanzar sin duplicados.
            var previo = Object.FindFirstObjectByType<FinalCinematicDirector>(FindObjectsInactive.Include);
            if (previo != null) Undo.DestroyObjectImmediate(previo.gameObject);
            var huerfano = GameObject.Find(DirectorName);
            if (huerfano != null) Undo.DestroyObjectImmediate(huerfano);

            var root = new GameObject(DirectorName);
            Undo.RegisterCreatedObjectUndo(root, "Configurar cinemática final");

            var vp = root.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.source = VideoSource.VideoClip;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.aspectRatio = VideoAspectRatio.FitInside;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.skipOnDrop = true;

            // Pantalla: raíz escalada a 16:9; los hijos van en unidades locales 1x1.
            var pantalla = new GameObject("PantallaCielo").transform;
            pantalla.SetParent(root.transform, false);
            ColocarPantallaInicial(pantalla);

            var superficie = GameObject.CreatePrimitive(PrimitiveType.Quad);
            superficie.name = "Superficie";
            Object.DestroyImmediate(superficie.GetComponent<Collider>());
            superficie.transform.SetParent(pantalla, false);
            ConfigurarRenderer(superficie.GetComponent<MeshRenderer>(), screenMat);

            var marco = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marco.name = "Marco";
            Object.DestroyImmediate(marco.GetComponent<Collider>());
            marco.transform.SetParent(pantalla, false);
            // El Quad se ve desde -Z, así que el marco va detrás (+Z).
            marco.transform.localPosition = new Vector3(0f, 0f, 1.1f);
            marco.transform.localScale = new Vector3(1.05f, 1.08f, 2f);
            ConfigurarRenderer(marco.GetComponent<MeshRenderer>(), bezelMat);

            var director = root.AddComponent<FinalCinematicDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("pantalla").objectReferenceValue = pantalla;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;

            Debug.Log("<color=cyan>[SetupFinalCinematic]</color> Cinemática final configurada. " +
                      "Prueba: Play -> clic derecho en FinalCinematicDirector -> 'Probar cinemática'.");
        }

        /// <summary>
        /// Posición de edición: sobre el boss, en el cielo, mirando hacia el jugador.
        /// En runtime el director la recoloca frente a la cámara si reposicionarFrenteACamara está activo.
        /// </summary>
        private static void ColocarPantallaInicial(Transform pantalla)
        {
            var boss = Object.FindFirstObjectByType<WindowsBossAI>();
            var jugador = Object.FindFirstObjectByType<LinuxSkeletonController>();
            Vector3 centro = boss != null ? boss.transform.position : Vector3.zero;
            Vector3 desde = jugador != null ? jugador.transform.position : centro - Vector3.forward * 40f;

            Vector3 dir = Vector3.ProjectOnPlane(centro - desde, Vector3.up);
            if (dir.sqrMagnitude < 1f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 pos = desde + dir * 140f + Vector3.up * 85f;
            pantalla.position = pos;
            pantalla.rotation = Quaternion.LookRotation(pos - desde, Vector3.up);
            pantalla.localScale = new Vector3(96f, 54f, 1f); // 16:9
        }

        private static void ConfigurarRenderer(MeshRenderer r, Material m)
        {
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static Material ObtenerMaterial(string path, Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
