using UnityEngine;
using Unity.Netcode;

namespace XRMultiplayer
{
    public class NetworkUIManager : MonoBehaviour
    {
        public void StartAsHost()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("Started as Host");
            }
        }

        public void StartAsClient()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("Started as Client");
            }
        }

        public void StopConnection()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("Stopped connection");
            }
        }

        public void StartHostLan()
        {
            XRMultiplayer.XRINetworkGameManager.Instance.HostLocalConnection();
            Debug.Log()
        }

        public void StartClientLan(string hostIp)
        {
            var utp = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            utp.SetConnectionData(hostIp, utp.ConnectionData.Port);
            XRMultiplayer.XRINetworkGameManager.Instance.JoinLocalConnection();
        }
    }
}
