using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlaceableData
{
    public string prefabName; // of ander ID// of prefab locatie in assets?
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
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
                position = child.position,
                rotation = child.rotation,
                scale = child.localScale
            };
            data.placeables.Add(pd);
        }

        return JsonUtility.ToJson(data, true);


    }
    public void LoadFromString(string json, Transform buildRoot, Dictionary<string, GameObject> prefabLookup)
    {
        BuildData data = JsonUtility.FromJson<BuildData>(json);

        foreach (var pd in data.placeables)
        {
            if (prefabLookup.TryGetValue(pd.prefabName, out var prefab))
            {
                GameObject go = Instantiate(prefab, pd.position, pd.rotation, buildRoot);
                go.transform.localScale = pd.scale;
            }
            else
            {
                Debug.LogWarning($"Prefab {pd.prefabName} not found in lookup.");
            }
        }
    }

}
