using UnityEngine;

namespace DeVect.Orbs.Runtime;

internal sealed class OrbSlotRuntime
{
    public OrbSlotRuntime(Transform anchor)
    {
        Anchor = anchor;
        InitialLocalPosition = anchor != null ? anchor.localPosition : Vector3.zero;
        CurrentLocalPosition = InitialLocalPosition;
        TargetLocalPosition = InitialLocalPosition;
        CurrentAngleDeg = Mathf.Atan2(InitialLocalPosition.y, InitialLocalPosition.x) * Mathf.Rad2Deg;
        TargetAngleDeg = CurrentAngleDeg;
        MotionRadius = InitialLocalPosition.magnitude;
        MotionDuration = 0f;
        MoveLerpT = 1f;
    }

    public Transform Anchor { get; }

    public Vector3 InitialLocalPosition { get; }

    public OrbInstance? Occupant { get; set; }

    public Vector3 CurrentLocalPosition { get; set; }

    public Vector3 TargetLocalPosition { get; set; }

    public float CurrentAngleDeg { get; set; }

    public float TargetAngleDeg { get; set; }

    public float MotionRadius { get; set; }

    public float MotionDuration { get; set; }

    public float MoveLerpT { get; set; }

    public bool IsOccupied => Occupant != null;

    public void Clear()
    {
        Occupant = null;
        Vector3 resetPosition = Anchor != null ? Anchor.localPosition : InitialLocalPosition;
        CurrentLocalPosition = resetPosition;
        TargetLocalPosition = resetPosition;
        CurrentAngleDeg = Mathf.Atan2(resetPosition.y, resetPosition.x) * Mathf.Rad2Deg;
        TargetAngleDeg = CurrentAngleDeg;
        MotionRadius = resetPosition.magnitude;
        MotionDuration = 0f;
        MoveLerpT = 1f;
    }
}
