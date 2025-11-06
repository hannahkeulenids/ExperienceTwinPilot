using UnityEngine;
using Unity.Netcode;

public class NetBootstrap : MonoBehaviour
{
    private static bool _initialized;

    void Awake()
    {
        if (_initialized) { Destroy(gameObject); return; }
        _initialized = true;
        DontDestroyOnLoad(gameObject);

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[NetBootstrap] No NetworkManager in scene.");
            return;
        }

        // Ook de NetworkManager persistent houden
        DontDestroyOnLoad(nm.gameObject);

        // Al gestart? (bijv. terug van een scene)
        if (nm.IsListening)
        {
            Debug.Log("[NetBootstrap] Host already running.");
            return;
        }

        // Probeer eerst jouw LAN-host flow (als aanwezig)
        bool started = false;
        try
        {
            // Heb je een eigen manager zoals XRINetworkGameManager? Call ’m hier.
            // (Pas de namespace/klasse aan naar wat jij gebruikt)
            var lanMgr = FindFirstObjectByType<XRMultiplayer.XRINetworkGameManager>();
            if (lanMgr != null)
            {
                lanMgr.HostLocalConnection();   // jouw StartHostLan()
                started = nm.IsListening;
                Debug.Log("[NetBootstrap] HostLocalConnection called.");
            }
        }
        catch { /* negeer, we vallen terug op StartHost */ }

        // Fallback: gewoon StartHost()
        if (!started)
        {
            if (nm.StartHost())
                Debug.Log("[NetBootstrap] StartHost() OK.");
            else
                Debug.LogError("[NetBootstrap] StartHost() FAILED.");
        }
    }
}
