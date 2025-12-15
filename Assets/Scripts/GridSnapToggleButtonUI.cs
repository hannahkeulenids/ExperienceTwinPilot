using UnityEngine;
using TMPro; // of UnityEngine.UI als je geen TextMeshPro gebruikt

public class GridSnapToggleButtonUI : MonoBehaviour
{
    [Header("Label (TextMeshPro)")]
    [SerializeField] private TMP_Text label;

    [Header("Tekst voor de knop")]
    [SerializeField] private string textWhenOff = "Zet snapping aan";
    [SerializeField] private string textWhenOn = "Zet snapping uit";

    private void Start()
    {
        UpdateLabel();
    }

    /// <summary>
    /// Deze koppel je aan de Button OnClick().
    /// </summary>
    public void OnClickToggleSnapping()
    {
        if (GridSnapToggleManager.Instance == null)
        {
            Debug.LogError("[GridSnapToggleButtonUI] Geen GridSnapToggleManager.Instance gevonden!");
            return;
        }

        // Eerst de logica togglen
        GridSnapToggleManager.Instance.ToggleGridSnapping();

        // Daarna de knoptekst bijwerken
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (label == null)
        {
            Debug.LogWarning("[GridSnapToggleButtonUI] Geen label gekoppeld.");
            return;
        }

        if (GridSnapToggleManager.Instance != null && GridSnapToggleManager.Instance.gridEnabled)
            label.text = textWhenOn;
        else
            label.text = textWhenOff;
    }
}
