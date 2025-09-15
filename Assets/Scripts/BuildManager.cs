using UnityEngine;
using Unity.Netcode;

public class BuildManager : NetworkBehaviour
{
    [Header("Parent voor alle gebouwde objecten")]
    public Transform buildRoot;                  // wijs deze in Inspector
    private NetworkObject buildRootNO;

    void Awake()
    {
        if (buildRoot != null)
            buildRootNO = buildRoot.GetComponent<NetworkObject>();
    }

    // ===== Button handler =====
    public void FixAllPlaceablesButton()
    {
        Debug.Log($"FixAllPlaceablesButton pressed. IsServer={IsServer} IsClient={IsClient}");

        if (IsServer)
        {
            FixAllPlaceables_Server();           // host/server voert direct uit
        }
        else
        {
            FixAllPlaceables_ServerRpc();        // clients vragen het de server
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void FixAllPlaceables_ServerRpc()
    {
        FixAllPlaceables_Server();
    }

    // ===== Server only: de echte reparent =====
    private void FixAllPlaceables_Server()
    {
        if (buildRoot == null)
        {
            Debug.LogError("[BuildManager] buildRoot is null.");
            return;
        }

        var placeables = GameObject.FindGameObjectsWithTag("Placeable");
        Debug.Log($"[BuildManager] Server fixing {placeables.Length} placeables…");

        foreach (var go in placeables)
        {
            // Heeft het object een NetworkObject?
            if (go.TryGetComponent(out NetworkObject childNO))
            {
                // Als buildRoot ook een NetworkObject heeft, gebruik NGO-parenting
                if (buildRootNO != null)
                {
                    var ok = childNO.TrySetParent(buildRootNO, worldPositionStays: true);
                    if (!ok) Debug.LogWarning($"TrySetParent failed for {go.name}");
                }
                else
                {
                    // Fallback (niet-gesynchroniseerd): alleen hiërarchie netjes
                    go.transform.SetParent(buildRoot, true);
                }
            }
            else
            {
                // Niet-networked object → gewone parenting is prima
                go.transform.SetParent(buildRoot, true);
            }
        }

        Debug.Log("[BuildManager] Server reparent done.");
    }
}
