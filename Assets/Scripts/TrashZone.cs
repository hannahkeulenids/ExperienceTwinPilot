using UnityEngine;
using Unity.Netcode;
using XRMultiplayer; // voor NetworkBaseInteractable

[RequireComponent(typeof(Collider))]
public class TrashZone : NetworkBehaviour
{
    [Header("Optioneel: alleen deze layers opruimen")]
    [SerializeField] private LayerMask allowedLayers = ~0; // standaard alles
    //[SerializeField] public TagHandle tagHandle; 

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1) Layerfilter (optioneel)
        if ((allowedLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        // 2) Zoek een NetworkBaseInteractable in de parent chain
        var interactable = other.GetComponentInParent<NetworkBaseInteractable>();
        if (interactable == null)
            return;

        var netObj = interactable.NetworkObject;
        if (netObj == null)
            return;

        // 3) Server regelt de despawn, clients vragen het via RPC
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            DespawnNow(netObj);
        }
        else
        {
            Trash_RequestDespawnServerRpc(netObj);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void Trash_RequestDespawnServerRpc(NetworkObjectReference objRef)
    {
        if (!objRef.TryGet(out var netObj))
            return;

        DespawnNow(netObj);
    }

    private void DespawnNow(NetworkObject netObj)
    {
        if (netObj == null || !netObj.IsSpawned)
            return;
       
        netObj.Despawn(true);
        // optional: extra debug log
        Debug.Log($"[TrashZone] Despawned {netObj.name} in trash zone.");
    }
}
