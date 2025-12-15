using UnityEngine;

public class XRGridSnapper : MonoBehaviour
{
    [Header("Enabled")]
    public bool useGrid = false;

    [Header("Grid Settings")]
    public Transform gridOrigin;          // wordt door de manager gezet
    public Vector3 cellSize = Vector3.one;

    [Tooltip("Op X-as naar grid snappen")]
    public bool snapXPosition = false;

    [Tooltip("Op Y-as naar grid snappen (hoogte)")]
    public bool snapYPosition = true;

    [Tooltip("Op Z-as naar grid snappen")]
    public bool snapZPosition = false;

    [Tooltip("Als er geen gridOrigin is, minimale Y-hoogte in world space")]
    public float fallbackMinY = 0f;


    private void Start()
    {
        // Haal globale settings op uit de manager
        if (GridSnapToggleManager.Instance != null)
            GridSnapToggleManager.Instance.ApplySettingsToSingle(this);
    }

    private void LateUpdate()
    {
        if (!useGrid) return;

        Vector3 worldPos = transform.position;

        if (gridOrigin != null)
        {
            // Snap in local space t.o.v. gridOrigin (bv. je tafel)
            Vector3 local = gridOrigin.InverseTransformPoint(worldPos);

            if (snapXPosition && cellSize.x > 0f)
                local.x = Snap(local.x, cellSize.x);

            if (snapYPosition && cellSize.y > 0f)
                local.y = Snap(local.y, cellSize.y);

            if (snapZPosition && cellSize.z > 0f)
                local.z = Snap(local.z, cellSize.z);

            worldPos = gridOrigin.TransformPoint(local);

            // Zorg dat hij nooit onder de tafel zakt
            float minY = gridOrigin.position.y;
            if (worldPos.y < minY)
                worldPos.y = minY;
        }
        else
        {
            // fallback: world space snapping
            if (snapXPosition && cellSize.x > 0f)
                worldPos.x = Snap(worldPos.x, cellSize.x);

            if (snapYPosition && cellSize.y > 0f)
                worldPos.y = Snap(worldPos.y, cellSize.y);

            if (snapZPosition && cellSize.z > 0f)
                worldPos.z = Snap(worldPos.z, cellSize.z);

            if (worldPos.y < fallbackMinY)
                worldPos.y = fallbackMinY;
        }

        transform.position = worldPos;
    }

    private static float Snap(float value, float step)
    {
        return Mathf.Round(value / step) * step;
    }
}
