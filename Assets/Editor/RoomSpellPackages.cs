using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class RoomSpellPackages
{
    static AddRequest request;
    static double deadline;

    public static void InstallInference()
    {
        request = Client.Add("com.unity.ai.inference@2.6.1");
        WaitForInstall();
    }

    public static void InstallInteraction()
    {
        request = Client.Add("com.meta.xr.sdk.interaction.ovr@207.0.0");
        WaitForInstall();
    }

    static void WaitForInstall()
    {
        deadline = EditorApplication.timeSinceStartup + 600;
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Poll;
        if (request.Status != StatusCode.Success)
        {
            Debug.LogError("RoomSpell inference install failed: " + request.Error?.message);
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("RoomSpell inference installed: " + request.Result.version);
        EditorApplication.Exit(0);
    }
}
