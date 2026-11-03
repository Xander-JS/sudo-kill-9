#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(IsometricCameraController))]
public class IsometricCameraControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button("Guardar configuración", GUILayout.Height(36)))
        {
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(
                ((IsometricCameraController)target).gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[IsometricCamera] Configuración guardada.");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "Pulsa el botón para marcar la escena como modificada y guardar.\n" +
            "También puedes usar Ctrl+S para guardar toda la escena.",
            MessageType.Info);
    }
}
#endif
