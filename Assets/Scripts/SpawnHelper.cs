using UnityEngine;
using Unity.Netcode;
using XRMultiplayer;                           // NetworkBaseInteractable
using UnityEngine.XR.Templates.VRMultiplayer;  // NetworkInteractableSpawner
using System.Reflection;

[DisallowMultipleComponent]
public class SpawnHelper : MonoBehaviour
{
    [Header("Leeg laten")]
    [SerializeField] private NetworkInteractableSpawner spawner;

    void Awake()
    {
        if (spawner == null)
            spawner = GetComponent<NetworkInteractableSpawner>();

        if (spawner == null)
            Debug.LogError("[SpawnHelper] Geen NetworkInteractableSpawner gevonden op dit object.", this);
    }

    /// Wordt door de manager aangeroepen
    public void ApplyPrefabAndRefresh(NetworkBaseInteractable prefab)
    {
        SetSpawnPrefab(prefab);
        ClearCurrentAndForceRespawn();
    }

    public void SetSpawnPrefab(NetworkBaseInteractable prefab)
    {
        if (!spawner) return;
        spawner.spawnInteractablePrefab = prefab;
    }

    public void SetPreviewFromPrefab(NetworkBaseInteractable prefab)
    {
        // Nu nog niets – hier kun je later runtime previewlogica hangen.
    }

    /// Despawn huidige interactable in de slot en forceer dat de spawner direct een nieuwe maakt
    private void ClearCurrentAndForceRespawn()
    {
        if (!spawner) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return; // alleen de server mag network objects despawnen

        // 1) Zoek de dichtstbijzijnde NetworkBaseInteractable rondom de spawnTransform
        var all = FindObjectsOfType<NetworkBaseInteractable>();
        if (all == null || all.Length == 0) return;

        Vector3 center = spawner.spawnTransform.position;
        float maxRadius = spawner.distanceToSpawnNew * 0.9f; // iets kleiner dan spawn-radius
        float bestDist = float.MaxValue;
        NetworkBaseInteractable best = null;

        foreach (var nb in all)
        {
            float d = Vector3.Distance(nb.transform.position, center);
            if (d < bestDist && d <= maxRadius)
            {
                bestDist = d;
                best = nb;
            }
        }

        // 2) Despawn/destroy de huidige interactable in de slot (als we er een gevonden hebben)
        if (best != null)
        {
            var no = best.NetworkObject;
            if (no != null && no.IsSpawned)
                no.Despawn(true);
            else
                Destroy(best.gameObject);
        }

        // 3) Forceer dat de spawner meteen een nieuwe spawn doet (cooldown naar 0)
        var t = typeof(NetworkInteractableSpawner);
        var cooldownField = t.GetField("m_SpawnCooldownTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (cooldownField != null)
        {
            cooldownField.SetValue(spawner, 0f);
        }
    }
}
