using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(JsonManager))]
public class BuildManager : NetworkBehaviour
{
    [Header("Shared")]
    [SerializeField] private PrefabCatalog prefabCatalog;

    [Header("Tabletop opties")]
    [SerializeField] private Slider optionSlider;
    [SerializeField] private GameObject[] options;

    [Header("Tabletop")]
    [SerializeField] private string tabletopSceneName = "TabletopScene";
    [SerializeField] private string tabletopRootTag = "TabletopBuildRoot";
    [SerializeField] private Transform buildRoot;             // tabletop root in de tabletop scene

    [Header("Simulation")]
    [SerializeField] private string simulationSceneName = "SimulationScene";
    [SerializeField] private string simulationRootTag = "SimulationBuildRoot";
    [SerializeField] private float simulationScale = 10f;

    [Header("Snapshot (Tabletop)")]
    [SerializeField] private Camera snapshotCamera;     // sleep je tabletop camera hier
    //[SerializeField] private int snapshotWidth = 1920;
    //[SerializeField] private int snapshotHeight = 1080;
    //[SerializeField] private bool snapshotIncludeAlpha = false; // meestal false

    //[Header ("Saves")]
    //[SerializeField] private TMP_Dropdown savesDropdown;

    private JsonManager _json;
    private List<string> _saveNames = new();

    private enum PendingLoad { None, ToSimulation, ToTabletop }
    private PendingLoad _pendingLoad = PendingLoad.None;
    private bool _subscribed;

    private void Awake()
    {
        _json = GetComponent<JsonManager>();
    }

    

    [ServerRpc(RequireOwnership = false)]
    private void LoadBuild_ServerRpc(string buildName) => LoadBuild_Server(buildName);

    private void LoadBuild_Server(string buildName)
    {
        if (prefabCatalog == null || buildRoot == null)
        {
            Debug.LogError("[BuildManager] prefabCatalog of buildRoot niet ingesteld.");
            return;
        }

        if (!SaveSystem.TryLoadJson(buildName, out var json))
        {
            Debug.LogWarning($"[BuildManager] Save '{buildName}' niet gevonden.");
            return;
        }

        _json.LoadFromString(buildRoot, json, prefabCatalog, clearExisting: true);
        Debug.Log($"[BuildManager] Build '{buildName}' geladen via dropdown.");
    }


    // Base options button call
    public void OptionSliderButton()
    {
        int index = Mathf.RoundToInt(optionSlider.value);
        for (int i = 0; i < options.Length; i++)
            options[i].SetActive(i == index);
    }
    // =============== SAVE ===============
    public void FixAndSaveButton(string buildName = "MyBuild")
    {
        if (IsServer) FixAndSave_Server(buildName);
        else FixAndSave_ServerRpc(buildName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void FixAndSave_ServerRpc(string buildName)
    {
        FixAndSave_Server(buildName);
    }

    private void FixAndSave_Server(string buildName)
    {
        FixAllPlaceables_Server();

        var json = _json.SaveToString(buildRoot, buildName);
        if (string.IsNullOrEmpty(json)) 
        { 
            Debug.LogError("[BuildManager] Save JSON empty."); return; 
        }
        //maak unieke bestandsnaam met tijdstempel
        string timestampedName = SaveSystem.MakeTimestampedName(buildName);

        SaveSystem.SaveJson(timestampedName, json);
        Debug.Log($"[BuildManager] Fix & Saved '{timestampedName}.json'");

        //opslaan png van topdowncamera
        SaveSnapshotPNG(timestampedName);

    }

    // =============== START SIMULATION ===============
    public void StartSimulationButton(string buildName = "MyBuild")
    {
        if (IsServer) StartSimulation_Server(buildName);
        else StartSimulation_ServerRpc(buildName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartSimulation_ServerRpc(string buildName) => StartSimulation_Server(buildName);

    private void StartSimulation_Server(string buildName)
    {
        // 1) zorg dat alles onder tabletop buildRoot zit
        FixAllPlaceables_Server();


        // 2) JSON in memory
        var json = _json.SaveToString(buildRoot, buildName);
        if (string.IsNullOrEmpty(json)) { Debug.LogError("[BuildManager] StartSimulation: empty JSON."); return; }
        BuildClipboard.LastJson = json;
        BuildClipboard.LastBuildName = buildName;

        // 3) scene wissel
        LoadSceneNetworked(simulationSceneName, PendingLoad.ToSimulation);
    }

    // =============== RETURN TO TABLETOP ===============
    public void ReturnToTabletopButton(string buildName = "MyBuild")
    {
        if (IsServer) ReturnToTabletop_Server(buildName);
        else ReturnToTabletop_ServerRpc(buildName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReturnToTabletop_ServerRpc(string buildName) => ReturnToTabletop_Server(buildName);

    private void ReturnToTabletop_Server(string buildName)
    {
        // 1) pak sim root en save current state
        var simRoot = GameObject.FindWithTag(simulationRootTag)?.transform;
        if (!simRoot) { Debug.LogError("[BuildManager] Return: Simulation root not found."); return; }

        var json = _json.SaveToString(simRoot, buildName);
        if (string.IsNullOrEmpty(json)) { Debug.LogError("[BuildManager] Return: empty JSON."); return; }
        BuildClipboard.LastJson = json;
        BuildClipboard.LastBuildName = buildName;

        // 2) scene wissel terug
        LoadSceneNetworked(tabletopSceneName, PendingLoad.ToTabletop);
    }

    // =============== SCENE HANDLING (SAFE MODE) ===============
    private void LoadSceneNetworked(string sceneName, PendingLoad mode)
    {
        var nsm = NetworkManager.SceneManager;
        if (nsm == null) { Debug.LogError("[BuildManager] No NetworkSceneManager."); return; }

        // altijd de- en resubscriben (voorkomt dubbele handlers)
        if (_subscribed)
        {
            nsm.OnSceneEvent -= OnSceneEvent_Server;
            _subscribed = false;
        }
        nsm.OnSceneEvent += OnSceneEvent_Server;
        _subscribed = true;

        _pendingLoad = mode;
        nsm.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private void OnSceneEvent_Server(SceneEvent e)
    {
        if (!IsServer) return;
        if (e.SceneEventType != SceneEventType.LoadComplete) return;

        // unhook direct
        if (_subscribed)
        {
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent_Server;
            _subscribed = false;
        }

        if (_pendingLoad == PendingLoad.ToSimulation && e.SceneName == simulationSceneName)
        {
            HandleSimulationLoaded();
            _pendingLoad = PendingLoad.None;
        }
        else if (_pendingLoad == PendingLoad.ToTabletop && e.SceneName == tabletopSceneName)
        {
            HandleTabletopLoaded();
            _pendingLoad = PendingLoad.None;
        }
    }

    private void HandleSimulationLoaded()
    {
        var simRootGO = GameObject.FindWithTag(simulationRootTag);
        if (!simRootGO) { Debug.LogError($"[BuildManager] Sim root tag '{simulationRootTag}' not found."); return; }

        // schaal de root (vereist NetworkTransform met Sync Scale)
        var simRoot = simRootGO.transform;
        simRoot.localScale = Vector3.one * simulationScale;
        var nt = simRootGO.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) nt.Teleport(simRoot.position, simRoot.rotation, simRoot.localScale);

        var rootNO = simRootGO.GetComponent<NetworkObject>();
        Debug.Log($"[BuildManager] Sim root found. NO? {rootNO != null}, IsSpawned? {rootNO && rootNO.IsSpawned}");

        if (!string.IsNullOrEmpty(BuildClipboard.LastJson) && prefabCatalog != null)
        {
            _json.LoadFromString(simRoot, BuildClipboard.LastJson, prefabCatalog, clearExisting: true);
            Debug.Log($"[BuildManager] Simulation loaded '{BuildClipboard.LastBuildName}' @ x{simulationScale}");
        }
        else Debug.LogWarning("[BuildManager] Missing JSON or PrefabCatalog for simulation load.");
    }

    private void HandleTabletopLoaded()
    {
        var tableRootGO = GameObject.FindWithTag(tabletopRootTag);
        if (!tableRootGO) { Debug.LogError($"[BuildManager] Tabletop root tag '{tabletopRootTag}' not found."); return; }

        var tableRoot = tableRootGO.transform;
        tableRoot.localScale = Vector3.one; // tabletop = 1

        var rootNO = tableRootGO.GetComponent<NetworkObject>();
        Debug.Log($"[BuildManager] Tabletop root found. NO? {rootNO != null}, IsSpawned? {rootNO && rootNO.IsSpawned}");

        if (!string.IsNullOrEmpty(BuildClipboard.LastJson) && prefabCatalog != null)
        {
            _json.LoadFromString(tableRoot, BuildClipboard.LastJson, prefabCatalog, clearExisting: true);
            Debug.Log($"[BuildManager] Tabletop reloaded '{BuildClipboard.LastBuildName}'.");
        }
        else Debug.LogWarning("[BuildManager] Missing JSON or PrefabCatalog for tabletop load.");
    }

    // =============== RE-PARENT ===============
    private void FixAllPlaceables_Server()
    {
        if (!buildRoot)
        {
            Debug.LogError("[BuildManager] buildRoot is null.");
            return;
        }

        var buildRootNO = buildRoot.GetComponent<NetworkObject>();
        var placeables = GameObject.FindGameObjectsWithTag("Placeable");
        foreach (var go in placeables)
        {
            if (!go) continue;

            if (go.TryGetComponent(out NetworkObject childNO))
            {
                if (buildRootNO != null)
                {
                    if (!childNO.TrySetParent(buildRootNO, worldPositionStays: true))
                        Debug.LogWarning($"[BuildManager] TrySetParent failed for {go.name}");
                }
                else go.transform.SetParent(buildRoot, worldPositionStays: true);
            }
            else go.transform.SetParent(buildRoot, worldPositionStays: true);
        }

    }

    private string SaveSnapshotPNG(string baseFileName)
    {
        if (snapshotCamera == null)
        {
            Debug.LogWarning("[BuildManager] No snapshotCamera assigned; skipping screenshot.");
            return null;
        }

        if (snapshotCamera.targetTexture == null)
        {
            Debug.LogError("[BuildManager] Snapshot camera has no RenderTexture assigned!");
            return null;
        }

        // Forceer één render naar de targettexture
        snapshotCamera.Render();

        // Lees pixels uit het toegewezen RenderTexture
        RenderTexture rt = snapshotCamera.targetTexture;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = null;

        // Schrijf PNG naar Saves-folder
        string folder = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, baseFileName + ".png");

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);

        Debug.Log($"[BuildManager] Snapshot saved → {path}");
        return path;
    }


}
