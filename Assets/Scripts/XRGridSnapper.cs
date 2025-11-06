using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


public class XRGridSnapper : MonoBehaviour
{
    [Header("Grid")]
    public Transform gridOrigin;                // Leeg = world space grid
    public Vector3 cellSize = new Vector3(0.25f, 0.25f, 0.25f);
    public Vector3 cellOffset = Vector3.zero;   // Verschuiving t.o.v. origin

    [Header("Axes (positie)")]
    public bool snapX = true;
    public bool snapY = true;
    public bool snapZ = true;

    [Header("Rotatie (optioneel)")]
    public bool snapRotation = false;
    public Vector3 rotationStep = new Vector3(0f, 90f, 0f); // bv. 90� om Y

    public bool _snappingActive = false; // true tussen Activated en Deactivated

    private Rigidbody rb;

    private void Awake()
    {
        //rb = GetComponent<Rigidbody>();
    }

    // Gebruik Update voor kinematic of FixedUpdate voor non-kinematic RB.
    void Update()
    {
        //if (_snappingActive)
        //    ApplyPose();
    }

    void ApplyPose()
    {
        Vector3 pos = GetSnappedPosition(transform.position);
        Quaternion rot = snapRotation ? GetSnappedRotation(transform.rotation) : transform.rotation;

        if (rb && !rb.isKinematic)
        {
            // In Update kan dit wat stotteren; wil je strakker, verplaats naar FixedUpdate.
            rb.MovePosition(pos);
            rb.MoveRotation(rot);
        }
        else
        {
            transform.SetPositionAndRotation(pos, rot);
        }
    }

    Vector3 GetSnappedPosition(Vector3 worldPos)
    {
        if (gridOrigin)
        {
            Vector3 local = gridOrigin.InverseTransformPoint(worldPos) - cellOffset;

            local = new Vector3(
                snapX ? Snap(local.x, cellSize.x) : local.x,
                snapY ? Snap(local.y, cellSize.y) : local.y,
                snapZ ? Snap(local.z, cellSize.z) : local.z
            );

            return gridOrigin.TransformPoint(local + cellOffset);
        }
        else
        {
            Vector3 p = worldPos - cellOffset;
            p = new Vector3(
                snapX ? Snap(p.x, cellSize.x) : p.x,
                snapY ? Snap(p.y, cellSize.y) : p.y,
                snapZ ? Snap(p.z, cellSize.z) : p.z
            );
            return p + cellOffset;
        }
    }

    Quaternion GetSnappedRotation(Quaternion worldRot)
    {
        if (!snapRotation) return worldRot;

        Quaternion localRot = gridOrigin ? Quaternion.Inverse(gridOrigin.rotation) * worldRot : worldRot;
        Vector3 e = localRot.eulerAngles;

        e = new Vector3(
            rotationStep.x > 0f ? SnapAngle(e.x, rotationStep.x) : e.x,
            rotationStep.y > 0f ? SnapAngle(e.y, rotationStep.y) : e.y,
            rotationStep.z > 0f ? SnapAngle(e.z, rotationStep.z) : e.z
        );

        return gridOrigin ? gridOrigin.rotation * Quaternion.Euler(e) : Quaternion.Euler(e);
    }

    static float Snap(float v, float step)
    {
        if (step <= 0f) return v;
        return Mathf.Round(v / step) * step;
    }

    static float SnapAngle(float deg, float step)
    {
        if (step <= 0f) return deg;
        deg = Mathf.Repeat(deg, 360f);
        return Mathf.Round(deg / step) * step;
    }
}
