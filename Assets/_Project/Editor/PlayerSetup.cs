using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Builds a ready-to-test Player (linux-skeleton) and top-down camera in the open scene.</summary>
public static class PlayerSetup
{
    private const string ModelPath = "Assets/_Project/Models/Characters/linux-skeleton/linux.fbx";
    private const string PhysicsDir = "Assets/_Project/Physics";
    private const string MaterialPath = PhysicsDir + "/Player_NoFriction.asset";

    [MenuItem("Tools/The Last Root/Create Player (linux-skeleton)")]
    public static void CreatePlayer()
    {
        GameObject existing = GameObject.Find("Player_LinuxSkeleton");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject player = new GameObject("Player_LinuxSkeleton");
        player.transform.position = FindFreeSpawn();

        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.mass = 80f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        CapsuleCollider col = player.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.4f;
        col.center = new Vector3(0f, 1f, 0f);
        col.sharedMaterial = GetNoFrictionMaterial();

        player.AddComponent<LinuxSkeletonController>();

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset != null)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            foreach (Collider c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
            foreach (Rigidbody r in model.GetComponentsInChildren<Rigidbody>())
                Object.DestroyImmediate(r);
        }
        else
        {
            Debug.LogWarning("[PlayerSetup] linux.fbx not found; using a capsule placeholder.");
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.name = "Model";
            cap.transform.SetParent(player.transform, false);
            cap.transform.localPosition = new Vector3(0f, 1f, 0f);
        }

        Animator modelAnimator = player.GetComponentInChildren<Animator>();
        if (modelAnimator != null)
        {
            modelAnimator.runtimeAnimatorController = GetLocomotionController();
            modelAnimator.applyRootMotion = false;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            CameraController camCtrl = cam.GetComponent<CameraController>();
            if (camCtrl == null) camCtrl = cam.gameObject.AddComponent<CameraController>();
            SerializedObject so = new SerializedObject(camCtrl);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            cam.transform.position = player.transform.position + new Vector3(0f, 15f, -15f);
            cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        }
        else
        {
            Debug.LogWarning("[PlayerSetup] No camera tagged MainCamera found in the scene.");
        }

        Undo.RegisterCreatedObjectUndo(player, "Create Player");
        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[PlayerSetup] Player ready. Press Play.");
    }

    private static RuntimeAnimatorController GetLocomotionController()
    {
        return SetupPlayerAnimator.ConfigureController();
    }

    // Spiral outward from the origin until a 2m-tall capsule fits without touching anything.
    private static Vector3 FindFreeSpawn()
    {
        Physics.SyncTransforms();
        for (int i = 0; i < 200; i++)
        {
            float angle = i * 0.8f;
            float dist = i * 0.4f;
            Vector3 p = new Vector3(Mathf.Cos(angle) * dist, 0.3f, Mathf.Sin(angle) * dist);
            if (!Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.6f))
                return new Vector3(p.x, 0.5f, p.z);
        }
        return new Vector3(0f, 2f, 0f);
    }

    private static PhysicsMaterial GetNoFrictionMaterial()
    {
        PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
        if (mat != null) return mat;

        if (!AssetDatabase.IsValidFolder(PhysicsDir))
            AssetDatabase.CreateFolder("Assets/_Project", "Physics");

        mat = new PhysicsMaterial("Player_NoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }
}
