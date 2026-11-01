using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Escucha la tecla Escape para regresar al menú principal desde la escena de gameplay.
/// Adjunta a un GameObject "GameManager" en la escena de juego.
/// </summary>
public class ReturnToMenu : MonoBehaviour
{
    [Tooltip("Nombre exacto de la escena de menú (debe coincidir con el Build Settings).")]
    [SerializeField] private string menuScene = "Inicio";

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            GoToMenu();
    }

    /// <summary>Libera el cursor y carga la escena de menú.</summary>
    public void GoToMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        SceneManager.LoadScene(menuScene);
    }
}
