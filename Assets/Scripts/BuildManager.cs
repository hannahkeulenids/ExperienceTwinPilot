using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BuildManager : NetworkBehaviour
{


    [Header("Parent for all objects build with")]
    [SerializeField] Transform buildRoot;

    JsonManager _json;
    //is onderstaande nodig? zit gwn buildroot op object?
    private NetworkObject buildRootNO;

    //[SerializeField] private List<GameObject> placeablePrefabs;
    //private Dictionary<string, GameObject> prefabRegistry;

    [SerializeField] private PrefabCatalog prefabCatalog;
    private Dictionary<string, GameObject> prefabRegistry;


    void Awake()
    {

        prefabRegistry = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var p in prefabCatalog.prefabs)
            if (p != null) prefabRegistry[p.name] = p;

        //prefabRegistry = new Dictionary<string, GameObject>();
       // foreach (var prefab in placeablePrefabs)
       // {
        //    prefabRegistry[prefab.name] = prefab;
       // }

        _json = GetComponent<JsonManager>();
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
        FixAllPlaceables_Server();

        if (_json == null || buildRoot == null)
        {
            Debug.LogWarning("[BuildManager] JsonManager of buildRoot mist.");
            return;
        }

        string json = _json.SaveToString(buildRoot, buildName);
        SaveSystem.SaveJson(buildName, json); //bestand weg schrijven via savesystem

        Debug.Log($"[BuildManager] Saved '{buildName}' ({json.Length} chars)");
        // Optioneel: File.WriteAllText(Application.persistentDataPath + $"/{buildName}.json", json);
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
        string json = SaveSystem.LoadJson(buildName);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"[BuildManager] No JSON found for build '{buildName}'");
            return;
        }

        if (_json != null && buildRoot != null)
        {
            _json.LoadFromString(buildRoot, json, prefabRegistry);
            Debug.Log($"[BuildManager] Loaded build '{buildName}'");
        }
        else
        {
            Debug.LogError("[BuildManager] Missing JsonManager or buildRoot.");
        }

    }

}
