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

    //private void LateUpdate()
    //{
    //    transform.position = new Vector3(
    //        Mathf.Round(transform.position.x),
    //        Mathf.Round(transform.position.y),
    //        Mathf.Round(transform.position.z)
    //    );
    //}
}
