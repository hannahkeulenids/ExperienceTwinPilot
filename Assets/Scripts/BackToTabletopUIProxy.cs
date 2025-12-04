using UnityEngine;

public class BackToTabletopUIProxy : MonoBehaviour
{
    public void OnClickBackToTabletop()
    {
        Debug.Log("[BackToTabletopUIProxy] Back button clicked.");

        if (BuildManager.Instance != null)
            BuildManager.Instance.OnBackToTabletopButton();
        else
            Debug.LogError("[BackToTabletopUIProxy] BuildManager.Instance niet gevonden.");
    }
}
