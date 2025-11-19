using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

//
// Data classes
//
[System.Serializable]
public class PlaceableData
{
    public string prefabKey;           // Placeable.PrefabId indien aanwezig, anders prefab.name
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
        int kept = 0, skipped = 0;

        foreach (Transform child in buildRoot)
        {
            // ⬇️ alleen actieve objecten meenemen (dus de zichtbare / gekozen optie)
            if (!child.gameObject.activeInHierarchy) { skipped++; continue; }

            data.placeables.Add(new PlaceableData
            {
                prefabKey = GetPrefabKey(child.gameObject),
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            });

            kept++;
        }

        return JsonUtility.ToJson(data, true);
    }

    // --- LOAD (auto-lookup from PrefabCatalog) ---
    public void LoadFromString(Transform buildRoot, string json, PrefabCatalog catalog, bool clearExisting = true)
    {
        if (!buildRoot) { Debug.LogError("[JsonManager] buildRoot is null."); return; }
        if (!catalog) { Debug.LogError("[JsonManager] PrefabCatalog is null."); return; }
        if (string.IsNullOrEmpty(json)) { Debug.LogWarning("[JsonManager] Empty JSON."); return; }

        var data = JsonUtility.FromJson<BuildData>(json);
        if (data == null) { Debug.LogError("[JsonManager] Could not parse JSON."); return; }

        // 1) Prefab lookup (key -> prefab)
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

        // 3) Server flag en root NetworkObject (voor later TrySetParent)
        bool isServer = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        var rootNO = buildRoot.GetComponent<NetworkObject>();

        int spawned = 0, missing = 0;
        foreach (var pd in data.placeables)
        {
            if (pd == null) continue;

            if (!lookup.TryGetValue(pd.prefabKey, out var prefab) || !prefab)
            {
                Debug.LogWarning($"[JsonManager] Missing prefab for key '{pd.prefabKey}'");
                missing++;
                continue;
            }

            // Instantiate inactief (zodat we eerst parent + TRS kunnen zetten)
            var go = Instantiate(prefab);
            go.SetActive(false);

            var netObj = go.GetComponent<NetworkObject>();

            // (A) Altijd EERST lokaal parenten — zodat local TRS t.o.v. buildRoot klopt
            go.transform.SetParent(buildRoot, worldPositionStays: false);

            // (B) Local TRS uit JSON
            go.transform.localPosition = pd.localPosition;
            go.transform.localRotation = pd.localRotation;
            go.transform.localScale = pd.localScale;

            // (C) Activeren
            go.SetActive(true);

            // (D) Alleen server: Spawn en daarna netwerk-parent synchroniseren
            if (netObj != null && isServer)
            {
                netObj.Spawn(true);

                if (rootNO != null && rootNO.IsSpawned)
                {
                    var ok = netObj.TrySetParent(rootNO, worldPositionStays: false);
                    if (!ok) StartCoroutine(TryParentNextFrame(netObj, rootNO));
                }
                else
                {
                    // root nog niet spawned → volgende frame nogmaals proberen
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

        // van achter naar voren itereren om hiërarchie veilig te ruimen
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            var no = child.GetComponent<NetworkObject>();

            if (no != null && isServer && no.IsSpawned)
                no.Despawn(true);  // true = destroy
            else
                Destroy(child.gameObject); // end-of-frame destroy
        }
    }

    private System.Collections.IEnumerator TryParentNextFrame(NetworkObject child, NetworkObject rootNO)
    {
        yield return null; // 1 frame wachten tot scene-objects/roots zeker spawned zijn
        if (child != null && rootNO != null && rootNO.IsSpawned)
        {
            var ok = child.TrySetParent(rootNO, worldPositionStays: false);
            if (!ok) Debug.LogWarning($"[JsonManager] TrySetParent retry failed for {child.name}");
        }
    }


}
