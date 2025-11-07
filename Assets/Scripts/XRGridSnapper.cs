using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class XRGridSnapper : MonoBehaviour
{
    [Header("Position Snapping")]
    public bool useGrid = false;
    public Vector3 size = Vector3.one;          // grid celgrootte per as
    public Vector3 offset = Vector3.zero;       // verschuiving van het grid-center (zie gridOrigin)
    public Transform gridOrigin = null;         // optioneel: bepaalt positie/rotatie van het grid

    [Header("Rotation")]
    public bool enableRotation = false;

    private void LateUpdate()
    {
        if (!useGrid) return;

        // --- POSITION ---
        Vector3 worldPos = transform.position;

        if (gridOrigin)
        {
            // Snap in LOCAL space van het grid, met offset als local verschuiving
            Vector3 local = gridOrigin.InverseTransformPoint(worldPos) - offset;

            local = new Vector3(
                Snap(local.x, size.x),
                Snap(local.y, size.y),
                Snap(local.z, size.z)
            );

            worldPos = gridOrigin.TransformPoint(local + offset);
        }
        else
        {
            // Snap in WORLD space; offset werkt ook in world space
            Vector3 p = worldPos - offset;

            p = new Vector3(
                Snap(p.x, size.x),
                Snap(p.y, size.y),
                Snap(p.z, size.z)
            );

            worldPos = p + offset;
        }

        // --- ROTATION ---
        Quaternion worldRot = transform.rotation;

        if (!enableRotation)
        {
            worldRot = Quaternion.identity;
        }

        // Toepassen (zonder fysica afhankelijkheid om het script simpel te houden)
        transform.SetPositionAndRotation(worldPos, worldRot);
    }

    // Helpers
    private static float Snap(float v, float step)
    {
        if (step <= 0f) return v;
        return Mathf.Round(v / step) * step;
    }

    private static float SnapAngle(float deg, float step)
    {
        if (step <= 0f) return deg;
        deg = Mathf.Repeat(deg, 360f);
        return Mathf.Round(deg / step) * step;
    }
}
