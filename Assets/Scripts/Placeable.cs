using UnityEngine;

public class Placeable : MonoBehaviour
{
    [SerializeField] private string prefabId;
    public string PrefabId => prefabId;

    private void OnTriggerExit(Collider other)
    {
        gameObject.tag = "Placeable";
        Debug.Log("changed tag");
    }
}
