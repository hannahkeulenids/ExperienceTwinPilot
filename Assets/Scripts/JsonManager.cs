using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlaceableSnapshot
{
    public string name;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}

[System.Serializable]
public class BuildSnapshot
{
    public string buildName;
    public int optionIndex;
    public List<PlaceableSnapshot> placeables = new();
}

public class JsonManager : MonoBehaviour
{
    // Wordt ALLEEN aangeroepen vanuit StartSimulation()
    public string CreateSnapshotJSON(Transform tabletopRoot, string buildName, int optionIndex)
    {
        BuildSnapshot snap = new BuildSnapshot();
        snap.buildName = buildName;
        snap.optionIndex = optionIndex;

        // Neem ALLE originele placeables
        var originals = GameObject.FindGameObjectsWithTag("Placeable");

        foreach (var o in originals)
        {
            if (!o.activeInHierarchy) continue;

            snap.placeables.Add(new PlaceableSnapshot()
            {
                name = o.name,
                position = o.transform.position,
                rotation = o.transform.rotation,
                scale = o.transform.localScale
            });
        }

        Debug.Log($"[JsonManager] Snapshot created: {snap.placeables.Count} placeables");
        return JsonUtility.ToJson(snap, true);
    }
}
