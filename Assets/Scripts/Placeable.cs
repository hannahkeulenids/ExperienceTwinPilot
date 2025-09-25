using UnityEngine;

public class Placeable : MonoBehaviour
{
    [SerializeField] private string prefabId;
    public string PrefabId => prefabId;
}
