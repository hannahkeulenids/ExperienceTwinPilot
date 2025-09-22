using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

[System.Serializable]
public class PlaceableData
{
    public string prefabName; // of ander ID// of prefab locatie in assets?
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

public class JsonManager : MonoBehaviour
{

    //[SerializeField] Transform buildRoot;
    //of moet ik buildroot uit ander script halen?

    //haal alles op onder rootbuild. Alle objecten + transform
    //naam geven aan saved build nog toevoegen?
    public string SaveToString(Transform buildRoot, string buildName)
    {

        if(buildRoot == null)
        {
            Debug.Log("[JsonManager] buildRoot is null in SaveToString.");
            return string.Empty;
        }

        var data = new BuildData { buildName = buildName };


        foreach (Transform child in buildRoot)
        {
            var pd = new PlaceableData
            {
                prefabName = child.gameObject.name.Replace("(Clone)", ""),
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            };
            data.placeables.Add(pd);
        }

        return JsonUtility.ToJson(data, true);


    }
    public void LoadFromString(Transform buildRoot, string json, Dictionary<string, GameObject> prefabRegistry)
    {
        var data = JsonUtility.FromJson<BuildData>(json);
        if (data == null) { Debug.LogError("[JsonManager] Parse fail"); return; }
        if (buildRoot == null) { Debug.LogError("[JsonManager] buildRoot null"); return; }
        if (prefabRegistry == null) { Debug.LogError("[JsonManager] prefabRegistry null"); return; }

        Debug.Log($"[JsonManager] Loading build '{data.buildName}', items: {data.placeables?.Count ?? -1}");
        Debug.Log($"[JsonManager] Registry keys: {string.Join(", ", prefabRegistry.Keys)}");

        int spawned = 0, missing = 0;
        foreach (var pd in data.placeables)
        {
            if (pd == null) continue;

            if (!prefabRegistry.TryGetValue(pd.prefabName, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[JsonManager] Missing prefab in registry: '{pd.prefabName}'");
                missing++;
                continue;
            }

            var go = Instantiate(prefab, buildRoot);
            var t = go.transform;
            t.SetLocalPositionAndRotation(pd.localPosition, pd.localRotation);
            t.localScale = pd.localScale;
            go.SetActive(true);

            // Netcode spawn (alleen op server)
            var netObj = go.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    netObj.Spawn(true);
                }
                else
                {
                    Debug.LogWarning($"[JsonManager] Not server; skipping Spawn for '{pd.prefabName}'");
                }
            }

            spawned++;
        }

        Debug.Log($"[JsonManager] Done. Spawned: {spawned}, MissingPrefabs: {missing}");
    }


}
