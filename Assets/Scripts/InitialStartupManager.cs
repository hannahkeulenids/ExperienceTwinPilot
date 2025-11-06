using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement; // alleen voor de build-settings check

public class InitialStartupManager : MonoBehaviour
{
    [SerializeField] private string tabletopSceneName = "TabletopScene";

    public void OnStartTabletopClicked()
    {
        var nm = NetworkManager.Singleton;
        if (!nm)
        {
            Debug.LogError("[StartTabletop] Geen NetworkManager.Singleton gevonden.");
            return;
        }

        if (!nm.IsListening)
        {
            Debug.LogWarning("[StartTabletop] Netwerk luistert nog niet. Start eerst Host.");
            return;
        }

        if (!nm.IsServer)
        {
            Debug.LogWarning("[StartTabletop] Alleen de SERVER/HOST mag scenes laden. (Ben je client?)");
            return;
        }

        if (!IsSceneInBuildSettings(tabletopSceneName))
        {
            Debug.LogError($"[StartTabletop] Scene '{tabletopSceneName}' staat niet in Build Settings.");
            return;
        }

        if (nm.SceneManager == null)
        {
            Debug.LogError("[StartTabletop] NetworkSceneManager is null. Zet NGO Scene Management aan.");
            return;
        }

        Debug.Log($"[StartTabletop] Laden via NGO: {tabletopSceneName}");
        nm.SceneManager.LoadScene(tabletopSceneName, LoadSceneMode.Single);
    }

    private bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }
        return false;
    }
}
