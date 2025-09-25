using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BuildManager : NetworkBehaviour
{


    [Header("Parent of objects build with")]
    [SerializeField] Transform buildRoot;

    [Header("Catalog")]
    [SerializeField] private PrefabCatalog prefabCatalog;
    private Dictionary<string, GameObject> prefabRegistry;

    JsonManager _json;

    //is onderstaande nodig? 
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

        if (IsServer)
        {
            FixAndSave_Server(buildName);           // host/server voert direct uit
        }
        else
        {
            FixAndSave_ServerRpc(buildName);        // clients vragen het de server
        }
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
        //LoadBuildButton("MyBuild"); //default name of build
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

}
