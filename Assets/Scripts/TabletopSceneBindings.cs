using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Content.Interaction; // XRKnob

public class TabletopSceneBindings : MonoBehaviour
{
    [Header("UI voor opties")]
    [Tooltip("De UI Slider in de tabletop scene (optioneel, handig voor desktop/debug).")]
    public Slider optionSlider;

    [Tooltip("XRKnob in de tabletop scene die de opties kiest.")]
    public XRKnob optionKnob;

    [Header("Tabletop opties")]
    [Tooltip("Alle tabletop variant GameObjects (elk een andere configuratie/omgeving).")]
    public GameObject[] tabletopOptions;

    // (Optioneel) automatische vulling als je deze script
    // op een parent zet waar alle opties kind-objects van zijn.
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Alleen auto-fill doen als de array leeg is
        if (tabletopOptions == null || tabletopOptions.Length == 0)
        {
            // Hier pakken we alle directe children als 'opties'
            int childCount = transform.childCount;
            if (childCount > 0)
            {
                tabletopOptions = new GameObject[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    tabletopOptions[i] = transform.GetChild(i).gameObject;
                }
            }
        }
    }
#endif
}
