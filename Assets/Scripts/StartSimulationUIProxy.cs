using UnityEngine;

public class StartSimulationUIProxy : MonoBehaviour
{
    [SerializeField] private string buildName = "MyBuild";

    public void OnClickStartSimulation()
    {
        Debug.Log("[StartSimulationUIProxy] Start Simulation button clicked.");

        if (BuildManager.Instance != null)
            BuildManager.Instance.StartSimulation(buildName);
        else
            Debug.LogError("[StartSimulationUIProxy] Geen BuildManager.Instance gevonden.");
    }
}
