using UnityEngine;

public class GridSnapToggleManager : MonoBehaviour
{
    public static GridSnapToggleManager Instance;

    [Header("Global Enable")]
    public bool gridEnabled = false;

    [Header("Grid Origin")]
    public Transform globalGridOrigin;

    [Header("Position Snapping")]
    public Vector3 globalCellSize = new Vector3(0.5f, 0.5f, 0.5f);

    public bool globalSnapXPosition = false;
    public bool globalSnapYPosition = true;   // vooral Y snappen
    public bool globalSnapZPosition = false;

    public float globalFallbackMinY = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 👉 Deze wordt al aangeroepen door je UI-knop
    public void ToggleGridSnapping()
    {
        gridEnabled = !gridEnabled;
        ApplySettingsToAll();
        Debug.Log($"[GridSnapToggle] Grid snapping = {gridEnabled}");
    }

    public void ApplySettingsToSingle(XRGridSnapper s)
    {
        if (s == null) return;
        ApplySettings(s);
    }

    public void ApplySettingsToAll()
    {
        XRGridSnapper[] all = FindObjectsOfType<XRGridSnapper>(true);
        foreach (var s in all)
            ApplySettings(s);

        Debug.Log($"[GridSnapToggle] Applied settings to {all.Length} objects.");
    }

    private void ApplySettings(XRGridSnapper s)
    {
        s.useGrid = gridEnabled;

        s.gridOrigin = globalGridOrigin;
        s.cellSize = globalCellSize;

        s.snapXPosition = globalSnapXPosition;
        s.snapYPosition = globalSnapYPosition;
        s.snapZPosition = globalSnapZPosition;

        s.fallbackMinY = globalFallbackMinY;
    }
}
