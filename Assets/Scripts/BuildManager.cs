using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Services.Multiplay.Authoring.Core.Assets;
using UnityEngine.SceneManagement;

public class BuildManager : NetworkBehaviour
{


    [Header("Building setup")]
    [SerializeField] Transform buildRoot;
    [SerializeField] private PrefabCatalog prefabCatalog;
    private Dictionary<string, GameObject> prefabRegistry;

   
    [Header("Simulation (scene name + spawn root tag")]
    [SerializeField] private string simulationSceneName = "SimulationScene";
    [SerializeField] private string simulationRootTag = "SimulationBuildRoot";

    private JsonManager _json;
    private NetworkObject buildRootNO;

    void Awake()
    {

        prefabRegistry = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var p in prefabCatalog.prefabs)
            if (p != null) prefabRegistry[p.name] = p;

        _json = GetComponent<JsonManager>();

        //moet wel een networkobject zijn 
        if (buildRoot != null)
            buildRootNO = buildRoot.GetComponent<NetworkObject>();
    }

    // ===== Button handler =====
    public void FixAndSaveButton(string buildName = "MyBuild")
    {
        Debug.Log($"FixAllPlaceablesButton pressed. IsServer={IsServer} IsClient={IsClient}");

        if (IsServer) FixAndSave_Server(buildName); // host/server voert direct uit
        else FixAndSave_ServerRpc(buildName); // clients vragen het de server
    }

    [ServerRpc(RequireOwnership = false)]
    private void FixAndSave_ServerRpc(string buildName)
    {
        FixAndSave_Server(buildName);
    }

    // ===== Server only: de echte reparent =====
    void FixAndSave_Server(string buildName)
    {
        // alle placeables als child onder buildroot zetten
        FixAllPlaceables_Server();
       
        if (_json == null || buildRoot == null)
        {
            Debug.LogWarning("[BuildManager] JsonManager of buildRoot mist.");
            return;
        }

        // serialize wegschrijven
        string json = _json.SaveToString(buildRoot, buildName);

        if (string.IsNullOrEmpty(json)) 
        { 
            Debug.LogError("[BuildManager] Save JSON empty.");
            return; 
        }

        SaveSystem.SaveJson(buildName, json); //bestand weg schrijven via savesystem

        Debug.Log($"[BuildManager] Saved '{buildName}' ({json.Length} chars)");
        // Optioneel: File.WriteAllText(Application.persistentDataPath + $"/{buildName}.json", json);
    }

    public void LoadLatestBuildButton()
    {
        if (IsServer)
        {
            LoadLatestBuild_Server();
        }

        else LoadLatestBuild_ServerRpc();
        
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadLatestBuild_ServerRpc()
    {
        LoadLatestBuild_Server();
    }

    private void LoadLatestBuild_Server()
    {
        if (prefabCatalog == null)
        {
            Debug.LogError("[BuildManager] PrefabCatalog not assigned.");
            return;
        }

        string latestName;
        string json = SaveSystem.LoadLatestJson(out latestName);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[BuildManager] No latest save found.");
            return;
        }

        // Important: JsonManager first erases the existing files (clearExisting = true)
        _json.LoadFromString(buildRoot, json, prefabCatalog, clearExisting: true);
        Debug.Log($"[BuildManager] Loaded latest build '{latestName}'");
    }


    private void FixAllPlaceables_Server()
    {
        if (buildRoot == null)
        {
            Debug.LogError("[BuildManager] buildRoot is null.");
            return;
        }

        //enomerator van makne?
        var placeables = GameObject.FindGameObjectsWithTag("Placeable");
        Debug.Log($"[BuildManager] Server fixing {placeables.Length} placeables…");

        foreach (var go in placeables)
        {
            // Heeft het object een NetworkObject?
            if (go.TryGetComponent(out NetworkObject childNO))
            {
                // Als buildRoot ook een NetworkObject heeft, gebruik NGO-parenting
                if (buildRootNO != null)
                {
                    var ok = childNO.TrySetParent(buildRootNO, worldPositionStays: true);
                    if (!ok) Debug.LogWarning($"TrySetParent failed for {go.name}");
                }
                else
                {
                    // Fallback (niet-gesynchroniseerd): alleen hiërarchie netjes
                    go.transform.SetParent(buildRoot, true);
                }
            }
            else
            {
                // Niet-networked object → gewone parenting is prima
                go.transform.SetParent(buildRoot, true);
            }
        }

        Debug.Log("[BuildManager] Server reparent done.");


    }
    //------ load button -------------
    public void LoadBuildButton(string buildName)
    {
        
        if (IsServer) LoadBuild_Server(buildName);
        else LoadBuild_ServerRpc(buildName);
    }


    [ServerRpc(RequireOwnership = false)]
    private void LoadBuild_ServerRpc(string buildName)
    {
        LoadBuild_Server(buildName);
    }

    private void LoadBuild_Server(string buildName)
    {
        string LatestName;
        string json = SaveSystem.LoadLatestJson(out LatestName);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"[BuildManager] No JSON found for build '{buildName}'");
            return;
        }

        if (_json != null && buildRoot != null)
        {
            //_json.LoadFromString(buildRoot, json, prefabRegistry);
            _json.LoadFromString(buildRoot, json, prefabCatalog, clearExisting: true);
            Debug.Log($"[BuildManager] Loaded build '{buildName}'");
        }
        else
        {
            Debug.LogError("[BuildManager] Missing JsonManager or buildRoot.");
        }

    }

    // ===================== START SIMULATIE =====================
    public void StartSimulationButton(string buildName = "MyBuild")
    {
        if (IsServer) StartSimulation_Server(buildName);
        else StartSimulation_ServerRpc(buildName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartSimulation_ServerRpc(string buildName)
    {
        StartSimulation_Server(buildName);
        
    }

    private void StartSimulation_Server(string buildName)
    {
        // 1) Sla huidige tabletop build in-memory op
        string json = _json.SaveToString(buildRoot, buildName);
        if (string.IsNullOrEmpty(json)) { Debug.LogError("[BuildManager] StartSimulation: empty JSON."); return; }
        BuildClipboard.LastJson = json;
        BuildClipboard.LastBuildName = buildName;

        // (optioneel) ook naar disk:
        // SaveSystem.SaveJson(SaveSystem.MakeTimestampedName(buildName), json);

        // 2) NGO Scene load voor alle peers
        var nsm = NetworkManager.SceneManager;
        if (nsm == null) { Debug.LogError("[BuildManager] No NetworkSceneManager."); return; }

        nsm.OnSceneEvent += OnSceneEvent_Server; // subscribe
        nsm.LoadScene(simulationSceneName, LoadSceneMode.Single);
    }

    // Wordt meerdere keren aangeroepen voor verschillende events; we filteren op LoadComplete + juiste scene
    private void OnSceneEvent_Server(SceneEvent e)
    {
        if (!IsServer) return;

        if (e.SceneEventType == SceneEventType.LoadComplete && e.SceneName == simulationSceneName)
        {
            // unsubscriben om dubbele calls te voorkomen
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent_Server;

            // 3) Zoek de simulation root in de nieuwe scene
            var simRootGO = GameObject.FindWithTag(simulationRootTag);
            if (simRootGO == null) { Debug.LogError($"[BuildManager] Simulation root with Tag '{simulationRootTag}' not found."); return; }
            var simRoot = simRootGO.transform;
            // x10 schaal op de root
            simRoot.localScale = Vector3.one * 10f;

            var rootNO = simRootGO.GetComponent<Unity.Netcode.NetworkObject>();
            Debug.Log($"[BuildManager] Sim root found. NO? {rootNO != null}, IsSpawned? {rootNO && rootNO.IsSpawned}");

            if (prefabCatalog == null) { Debug.LogError("[BuildManager] PrefabCatalog not assigned."); return; }



            if (!string.IsNullOrEmpty(BuildClipboard.LastJson))
            {
                // 4) JSON → bouwen onder simulation root (clearExisting = true voor een schone scene)
                _json.LoadFromString(simRoot, BuildClipboard.LastJson, prefabCatalog, clearExisting: true);
                Debug.Log($"[BuildManager] Simulation loaded: '{BuildClipboard.LastBuildName}'");
            }
            else
            {
                Debug.LogWarning("[BuildManager] No JSON in BuildClipboard.");
            }
        }
    }


}

