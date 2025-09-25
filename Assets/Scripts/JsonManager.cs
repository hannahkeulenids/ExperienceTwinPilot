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

        // 1) Build lookup (key -> prefab); key = Placeable.PrefabId or prefab.name
        var lookup = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in catalog.prefabs)
        {
            if (prefab == null) continue;
            var p = prefab.GetComponent<Placeable>();
            string key = (p != null && !string.IsNullOrWhiteSpace(p.PrefabId)) ? p.PrefabId : prefab.name;
            if (!lookup.ContainsKey(key))
                lookup.Add(key, prefab);
        }

        // 2) Clear bestaande children indien gevraagd
        if (clearExisting)
            ClearChildrenServerAware(buildRoot);

        // 3) Instantiate + parent → local TRS
        var rootNO = buildRoot.GetComponent<NetworkObject>();
        bool isServer = NetworkManager.Singleton && NetworkManager.Singleton.IsServer;

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

            var go = Instantiate(prefab);
            go.SetActive(false);

            var netObj = go.GetComponent<NetworkObject>();

            // Eerst parenten
            if (netObj && isServer && rootNO)
                netObj.TrySetParent(rootNO, worldPositionStays: false);
            else
                go.transform.SetParent(buildRoot, worldPositionStays: false);

            // Dan local TRS toepassen
            go.transform.localPosition = pd.localPosition;
            go.transform.localRotation = pd.localRotation;
            go.transform.localScale = pd.localScale;

            // Activeren + eventueel spawn
            go.SetActive(true);
            if (netObj && isServer)
                netObj.Spawn(true);

            spawned++;
        }

        Debug.Log($"[JsonManager] Load done. Spawned: {spawned}, Missing: {missing}");
    }

    // --- Helpers ---
    private static string GetPrefabKey(GameObject go)
    {
        var p = go.GetComponent<Placeable>();
        if (p != null && !string.IsNullOrWhiteSpace(p.PrefabId))
            return p.PrefabId;

        return go.name.Replace("(Clone)", "");
    }

    private void ClearChildrenServerAware(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            var no = child.GetComponent<NetworkObject>();
            if (no != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                no.Despawn(true);
            else
                Destroy(child.gameObject);
        }
    }
}
