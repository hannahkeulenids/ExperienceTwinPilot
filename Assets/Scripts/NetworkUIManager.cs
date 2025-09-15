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
    }
}
