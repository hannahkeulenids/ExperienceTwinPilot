using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class DontDestroyXRManager : MonoBehaviour
{
    private static bool _created = false;

    private void Awake()
    {
        // voorkom dubbele managers bij scene reloads
        if (_created)
        {
            Destroy(gameObject);
            return;
        }

        _created = true;
        DontDestroyOnLoad(gameObject);
    }
}
