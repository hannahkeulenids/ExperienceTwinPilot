using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Config/Prefab Catalog")]
public class PrefabCatalog : ScriptableObject
{
    public List<GameObject> prefabs = new();
}
