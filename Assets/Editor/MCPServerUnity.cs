using UnityEngine;
using UnityEditor;
using System.Net;
using System.IO;
using System.Text;

public class MCPServerUnity : EditorWindow
{
    private static HttpListener listener;
    private static bool isRunning = false;

    [MenuItem("Tools/MCP Unity/Server Control")]
    public static void ShowWindow()
    {
        GetWindow<MCPServerUnity>("MCP Unity Server");
    }

    private void OnGUI()
    {
        GUILayout.Label("Control del Servidor MCP para IA", EditorStyles.boldLabel);

        if (!isRunning)
        {
            if (GUILayout.Button("Start Server"))
            {
                StartServer();
            }
        }
        else
        {
            GUI.backgroundColor = Color.green;
            GUILayout.Button("Servidor Activo (Puerto 8080)");
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Stop Server"))
            {
                StopServer();
            }
        }
    }

    private static void StartServer()
    {
        if (isRunning) return;

        listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();
        isRunning = true;
        EditorApplication.update += ListenForRequests;
        Debug.Log("Servidor MCP de Unity iniciado en http://localhost:8080/");
    }

    private static void StopServer()
    {
        if (!isRunning) return;

        EditorApplication.update -= ListenForRequests;
        listener.Stop();
        listener.Close();
        isRunning = false;
        Debug.Log("Servidor MCP de Unity detenido.");
    }

    private static void ListenForRequests()
    {
        if (!listener.IsListening) return;

        if (listener.GetContextAsync().IsCompleted)
        {
            var context = listener.GetContext();
            var response = context.Response;

            string responseString = "{\"status\":\"success\",\"message\":\"Conectado a Unity\"}";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}