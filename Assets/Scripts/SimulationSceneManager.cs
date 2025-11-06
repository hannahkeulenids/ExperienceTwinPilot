using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class SimulationSceneManager : NetworkBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string tabletopSceneName = "TabletopScene"; // pas aan naar jouw scene naam

    // Deze functie kun je vanuit een knop of XR-Interactable aanroepen
    public void BackToTabletop()
    {

        if (IsServer)
        {
            LoadTabletop_Server();
        }
        else
        {
            BackToTabletop_ServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void BackToTabletop_ServerRpc()
    {
        LoadTabletop_Server();
    }

    private void LoadTabletop_Server()
    {
        Debug.Log("[SimulationSceneManager] Terug naar tabletop scene...");
        var nsm = NetworkManager.SceneManager;

        if (nsm == null)
        {
            Debug.LogError("[SimulationSceneManager] Geen NetworkSceneManager gevonden!");
            return;
        }

        nsm.LoadScene(tabletopSceneName, LoadSceneMode.Single);
    }
}
