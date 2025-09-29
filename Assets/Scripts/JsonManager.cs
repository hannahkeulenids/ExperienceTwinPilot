using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

//
// Data classes
//
[System.Serializable]
public class PlaceableData
{
    public string prefabKey;           // prefabId (als aanwezig) anders prefab.name
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 localScale;
}

[System.Serializable]
public class BuildData
{
    public string buildName;
    public List<PlaceableData> placeables = new();
}

//
// JsonManager
//
public class JsonManager : MonoBehaviour
{
    // --- SAVE ---
    public string SaveToString(Transform buildRoot, string buildName)
    {
        if (buildRoot == null)
        {
            Debug.LogError("[JsonManager] buildRoot is null in SaveToString.");
            return string.Empty;
        }

        var data = new BuildData { buildName = buildName };

        foreach (Transform child in buildRoot)
        {
            string key = GetPrefabKey(child.gameObject);

            data.placeables.Add(new PlaceableData
            {
                prefabKey = key,
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            });
        }

        return JsonUtility.ToJson(data, true);
    }

    // --- LOAD (auto-lookup from PrefabCatalog) ---
    public void LoadFromString(Transform buildRoot, string json, PrefabCatalog catalog, bool clearExisting = true)
    {
        if (buildRoot == null) { Debug.LogError("[JsonManager] buildRoot is null."); return; }
        if (catalog == null) { Debug.LogError("[JsonManager] PrefabCatalog is null."); return; }

        var data = JsonUtility.FromJson<BuildData>(json);
        if (data == null) { Debug.LogError("[JsonManager] Could not parse JSON."); return; }

        // 1) Lookup (key -> prefab)
        var lookup = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in catalog.prefabs)
        {
            if (!prefab) continue;
            var p = prefab.GetComponent<Placeable>();
            var key = (p != null && !string.IsNullOrWhiteSpace(p.PrefabId)) ? p.PrefabId : prefab.name;
            if (!lookup.ContainsKey(key)) lookup.Add(key, prefab);
        }

        // 2) Clear bestaande children indien gevraagd
        if (clearExisting) ClearChildrenServerAware(buildRoot);

        // 3) Bepaal één keer buiten de loop
        var rootNO = buildRoot.GetComponent<NetworkObject>();
        bool isServer = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        int spawned = 0, missing = 0;
        foreach (var pd in data.placeables)
        {
            if (pd == null) continue;

            if (!lookup.TryGetValue(pd.prefabKey, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[JsonManager] Missing prefab for key '{pd.prefabKey}'");
                missing++;
                continue;
            }

            // Instantiate inactief om hitches/awake-gedrag te minimaliseren
            var go = Instantiate(prefab);
            go.SetActive(false);

            var netObj = go.GetComponent<NetworkObject>();

            // Eerst parenten
            if (netObj != null && isServer && rootNO != null && rootNO.IsSpawned)
            {
                // NGO-parenting (synct over netwerk)
                var ok = netObj.TrySetParent(rootNO, worldPositionStays: false);
                if (!ok) Debug.LogWarning($"[JsonManager] TrySetParent failed now for '{pd.prefabKey}'");
            }
            else
            {
                // Lokale parenting (of fallback als rootNO nog niet spawned is)
                go.transform.SetParent(buildRoot, worldPositionStays: false);
            }

            // Daarna local TRS instellen
            go.transform.localPosition = pd.localPosition;
            go.transform.localRotation = pd.localRotation;
            go.transform.localScale = pd.localScale;

            // Activeren
            go.SetActive(true);

            // Netcode spawn (server only). Als nog niet geparent via NGO → opnieuw proberen na spawn.
            if (netObj != null && isServer)
            {
                netObj.Spawn(true);

                if (rootNO != null && rootNO.IsSpawned)
                {
                    // Zorg dat netwerk-parent klopt (voor het geval hierboven lokaal geparent werd)
                    var ok2 = netObj.TrySetParent(rootNO, worldPositionStays: false);
                    if (!ok2) StartCoroutine(TryParentNextFrame(netObj, rootNO));
                }
                else
                {
                    // Root nog niet spawned? Volgend frame proberen.
                    StartCoroutine(TryParentNextFrame(netObj, rootNO));
                }
            }

            spawned++;
        }

        Debug.Log($"[JsonManager] Load done. Spawned: {spawned}, Missing: {missing}");
    }

    // --- Helpers ---
    private static string GetPrefabKey(GameObject go)
    {
        var p = go.GetComponent<Placeable>();
        if (p != null && !string.IsNullOrWhiteSpace(p.PrefabId)) return p.PrefabId;
        return go.name.Replace("(Clone)", "");
    }

    private void ClearChildrenServerAware(Transform root)
    {
        bool isServer = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            var no = child.GetComponent<NetworkObject>();

            if (no != null && isServer && no.IsSpawned)
                no.Despawn(true);
            else
                Destroy(child.gameObject);
        }
    }

    private System.Collections.IEnumerator TryParentNextFrame(NetworkObject child, NetworkObject rootNO)
    {
        yield return null; // 1 frame wachten
        if (child != null && rootNO != null && rootNO.IsSpawned)
        {
            var ok = child.TrySetParent(rootNO, worldPositionStays: false);
            if (!ok) Debug.LogWarning($"[JsonManager] TrySetParent retry failed for {child.name}");
        }
    }
}
