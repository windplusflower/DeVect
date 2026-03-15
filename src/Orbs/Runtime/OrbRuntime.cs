using System.Collections.Generic;
using DeVect.Orbs.Definitions;
using DeVect.Visual;
using UnityEngine;

namespace DeVect.Orbs.Runtime;

internal sealed class OrbRuntime
{
    private static readonly Vector3 RootOffset = new(0f, 0.1f, 0f);
    private static readonly Vector3[] SlotLocalPositions = BuildSlotLocalPositions();

    private const float SlotFanRadius = 1.62f;
    private const float SlotFanCenterAngleDeg = 90f;
    private const float SlotFanSpreadDeg = 38f;
    private const float YellowOrbScale = 0.42f;
    private const float EvictedOrbScale = 0.42f;
    private const float SlotMoveDuration = 0.14f;
    private const float SpawnEntryDuration = 0.16f;
    private const float EvictedOrbLifetime = 0.2f;
    private const float SpawnEntryAngleOffsetDeg = 24f;
    private const float EvictedOrbExitAngleOffsetDeg = -28f;
    private const int RightSlotIndex = 2;
    private const int CenterSlotIndex = 1;
    private const int LeftSlotIndex = 0;

    private readonly OrbVisualService _visualService;
    private readonly OrbSlotRuntime[] _slots = new OrbSlotRuntime[3];
    private Transform? _heroTransform;
    private GameObject? _root;
    private bool _suppressSpawnEntryAnimation;

    public OrbRuntime(OrbVisualService visualService)
    {
        _visualService = visualService;
    }

    public void EnsureBuilt(Transform heroTransform, IReadOnlyList<OrbTypeId> persistedTypes, OrbDefinitionRegistry definitions)
    {
        if (_root != null && IsBoundTo(heroTransform))
        {
            return;
        }

        Dispose();

        _heroTransform = heroTransform;
        _root = new GameObject("DeVect_OrbRuntime");
        _root.transform.position = _heroTransform.position + RootOffset;
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;

        for (int i = 0; i < SlotLocalPositions.Length; i++)
        {
            GameObject slot = new($"DeVect_OrbSlot_{i}");
            slot.transform.SetParent(_root.transform, false);
            slot.transform.localPosition = SlotLocalPositions[i];
            _slots[i] = new OrbSlotRuntime(slot.transform);
            _visualService.BuildDashedRing(slot.transform);
            RefreshSlotVisual(_slots[i]);
        }

        _suppressSpawnEntryAnimation = true;
        for (int i = 0; i < persistedTypes.Count; i++)
        {
            TrySpawnOrbInSlot(GetFillOrder()[i], persistedTypes[i], definitions);
        }

        _suppressSpawnEntryAnimation = false;
    }

    public bool IsBoundTo(Transform heroTransform)
    {
        return _heroTransform == heroTransform;
    }

    public void TickFollow()
    {
        if (_root == null || _heroTransform == null)
        {
            return;
        }

        _root.transform.position = _heroTransform.position + RootOffset;
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;
    }

    public void TickAnimations(float deltaTime)
    {
        if (_root != null)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                OrbSlotRuntime slot = _slots[i];
                if (slot == null || slot.Occupant == null)
                {
                    continue;
                }

                if (slot.MoveLerpT < 1f)
                {
                    float duration = slot.MotionDuration > 0f ? slot.MotionDuration : SlotMoveDuration;
                    slot.MoveLerpT = Mathf.Min(1f, slot.MoveLerpT + (deltaTime / duration));
                    float eased = Mathf.SmoothStep(0f, 1f, slot.MoveLerpT);
                    slot.Occupant.Renderer.transform.localPosition = EvaluateCircularArcPosition(slot.CurrentAngleDeg, slot.TargetAngleDeg, slot.MotionRadius, eased);
                    if (slot.MoveLerpT >= 1f)
                    {
                        slot.Occupant.Renderer.transform.localPosition = slot.TargetLocalPosition;
                        slot.CurrentLocalPosition = slot.TargetLocalPosition;
                        slot.CurrentAngleDeg = slot.TargetAngleDeg;
                    }
                }
            }
        }

        _visualService.TickTransientVisuals(deltaTime);
    }

    public int GetActiveOrbCount()
    {
        int count = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null && _slots[i].IsOccupied)
            {
                count++;
            }
        }

        return count;
    }

    public bool HasAnyActiveOrb()
    {
        return GetActiveOrbCount() > 0;
    }

    public IReadOnlyList<OrbInstanceSnapshot> SnapshotActiveOrbs()
    {
        List<OrbInstanceSnapshot> snapshots = new();
        foreach (int slotIndex in GetFillOrder())
        {
            OrbSlotRuntime slot = _slots[slotIndex];
            if (slot != null && slot.Occupant != null)
            {
                snapshots.Add(new OrbInstanceSnapshot(slot.Occupant.TypeId, slotIndex));
            }
        }

        return snapshots;
    }

    public IEnumerable<OrbInstance> EnumerateActiveOrbs()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            OrbSlotRuntime slot = _slots[i];
            if (slot != null && slot.Occupant != null)
            {
                yield return slot.Occupant;
            }
        }
    }

    public bool TrySpawnOrbInNextAvailableSlot(OrbTypeId typeId, OrbDefinitionRegistry definitions)
    {
        for (int i = 0; i < GetFillOrder().Length; i++)
        {
            int slotIndex = GetFillOrder()[i];
            if (_slots[slotIndex] == null || _slots[slotIndex].IsOccupied)
            {
                continue;
            }

            return TrySpawnOrbInSlot(slotIndex, typeId, definitions);
        }

        return false;
    }

    public bool TryForceInsertOrbFromLeft(OrbTypeId newTypeId, OrbDefinitionRegistry definitions, out OrbInstance? evictedOrb)
    {
        evictedOrb = null;
        if (_root == null || GetActiveOrbCount() < 3)
        {
            return false;
        }

        OrbSlotRuntime rightSlot = _slots[RightSlotIndex];
        OrbSlotRuntime centerSlot = _slots[CenterSlotIndex];
        OrbSlotRuntime leftSlot = _slots[LeftSlotIndex];
        if (rightSlot?.Occupant == null || centerSlot?.Occupant == null || leftSlot?.Occupant == null)
        {
            return false;
        }

        OrbInstance oldRight = rightSlot.Occupant;
        OrbInstance oldCenter = centerSlot.Occupant;
        OrbInstance oldLeft = leftSlot.Occupant;
        evictedOrb = oldRight;

        StartEvictedOrbAnimation(oldRight.Renderer);
        rightSlot.Clear();

        rightSlot.Occupant = oldCenter;
        StartSlotMove(rightSlot, RightSlotIndex, SlotMoveDuration);
        RefreshSlotVisual(rightSlot);

        centerSlot.Occupant = oldLeft;
        StartSlotMove(centerSlot, CenterSlotIndex, SlotMoveDuration);
        RefreshSlotVisual(centerSlot);

        IOrbDefinition newDefinition = definitions.Get(newTypeId);
        SpriteRenderer newRenderer = _visualService.CreateOrbRenderer("DeVect_YellowOrb_Inserted", newDefinition.OrbColor);
        newRenderer.transform.SetParent(_root.transform, false);
        newRenderer.transform.localPosition = EvaluateArcPoint(GetSpawnEntryStartAngleDeg(LeftSlotIndex), SlotFanRadius);
        newRenderer.transform.localScale = new Vector3(YellowOrbScale, YellowOrbScale, 1f);

        leftSlot.Occupant = new OrbInstance(newTypeId, newDefinition, newRenderer);
        StartSpawnEntry(leftSlot, LeftSlotIndex);
        RefreshSlotVisual(leftSlot);
        return true;
    }

    public void Dispose()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            OrbSlotRuntime slot = _slots[i];
            if (slot?.Occupant?.Renderer != null)
            {
                Object.Destroy(slot.Occupant.Renderer.gameObject);
            }

            slot?.Clear();
        }

        if (_root != null)
        {
            Object.Destroy(_root);
            _root = null;
        }

        _heroTransform = null;
        _visualService.DisposeTransientVisuals();
    }

    public void SuspendAndRemember(OrbPersistentState persistentState)
    {
        persistentState.ReplaceFromRuntime(SnapshotActiveOrbs());
        Dispose();
    }

    private bool TrySpawnOrbInSlot(int slotIndex, OrbTypeId typeId, OrbDefinitionRegistry definitions)
    {
        if (_root == null)
        {
            return false;
        }

        IOrbDefinition definition = definitions.Get(typeId);
        SpriteRenderer renderer = _visualService.CreateOrbRenderer($"DeVect_{definition.DisplayName}Orb_{slotIndex}", definition.OrbColor);
        renderer.transform.SetParent(_root.transform, false);
        renderer.transform.localPosition = SlotLocalPositions[slotIndex];
        renderer.transform.localScale = new Vector3(YellowOrbScale, YellowOrbScale, 1f);

        OrbSlotRuntime slot = _slots[slotIndex];
        slot.Occupant = new OrbInstance(typeId, definition, renderer);
        slot.CurrentLocalPosition = SlotLocalPositions[slotIndex];
        slot.TargetLocalPosition = SlotLocalPositions[slotIndex];
        slot.CurrentAngleDeg = GetSlotAngleDeg(slotIndex);
        slot.TargetAngleDeg = slot.CurrentAngleDeg;
        slot.MotionRadius = SlotFanRadius;
        slot.MotionDuration = SlotMoveDuration;
        if (_suppressSpawnEntryAnimation)
        {
            slot.MoveLerpT = 1f;
        }
        else
        {
            StartSpawnEntry(slot, slotIndex);
        }

        RefreshSlotVisual(slot);
        return true;
    }

    private static void SetDashedRingVisible(OrbSlotRuntime slot, bool visible)
    {
        if (slot?.Anchor == null)
        {
            return;
        }

        SpriteRenderer[] renderers = slot.Anchor.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible;
        }
    }

    private static void RefreshSlotVisual(OrbSlotRuntime slot)
    {
        if (slot == null)
        {
            return;
        }

        SetDashedRingVisible(slot, !slot.IsOccupied);
    }

    private float GetSpawnEntryStartAngleDeg(int slotIndex)
    {
        return GetSlotAngleDeg(slotIndex) + SpawnEntryAngleOffsetDeg;
    }

    private void StartSpawnEntry(OrbSlotRuntime slot, int slotIndex)
    {
        if (slot.Occupant == null)
        {
            return;
        }

        float startAngle = GetSpawnEntryStartAngleDeg(slotIndex);
        float targetAngle = GetSlotAngleDeg(slotIndex);
        Vector3 start = EvaluateArcPoint(startAngle, SlotFanRadius);
        slot.Occupant.Renderer.transform.localPosition = start;
        slot.CurrentLocalPosition = start;
        slot.TargetLocalPosition = SlotLocalPositions[slotIndex];
        slot.CurrentAngleDeg = startAngle;
        slot.TargetAngleDeg = targetAngle;
        slot.MotionRadius = SlotFanRadius;
        slot.MotionDuration = SpawnEntryDuration;
        slot.MoveLerpT = 0f;
    }

    private static Vector3[] BuildSlotLocalPositions()
    {
        return new[]
        {
            EvaluateSlotPosition(SlotFanCenterAngleDeg + SlotFanSpreadDeg, SlotFanRadius),
            EvaluateSlotPosition(SlotFanCenterAngleDeg, SlotFanRadius),
            EvaluateSlotPosition(SlotFanCenterAngleDeg - SlotFanSpreadDeg, SlotFanRadius)
        };
    }

    private static float GetSlotAngleDeg(int slotIndex)
    {
        return slotIndex switch
        {
            LeftSlotIndex => SlotFanCenterAngleDeg + SlotFanSpreadDeg,
            CenterSlotIndex => SlotFanCenterAngleDeg,
            RightSlotIndex => SlotFanCenterAngleDeg - SlotFanSpreadDeg,
            _ => SlotFanCenterAngleDeg
        };
    }

    private static Vector3 EvaluateSlotPosition(float angleDeg, float radius)
    {
        return EvaluateArcPoint(angleDeg, radius);
    }

    private static Vector3 EvaluateArcPoint(float angleDeg, float radius)
    {
        float radians = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f);
    }

    private static float NormalizeAngleDelta(float fromDeg, float toDeg)
    {
        return Mathf.Repeat((toDeg - fromDeg) + 180f, 360f) - 180f;
    }

    private static float GetAngleDegFromPosition(Vector3 localPosition)
    {
        return Mathf.Atan2(localPosition.y, localPosition.x) * Mathf.Rad2Deg;
    }

    private static Vector3 EvaluateCircularArcPosition(float startAngleDeg, float endAngleDeg, float radius, float t)
    {
        float angleDeg = startAngleDeg + (NormalizeAngleDelta(startAngleDeg, endAngleDeg) * t);
        return EvaluateArcPoint(angleDeg, radius);
    }

    private void StartSlotMove(OrbSlotRuntime slot, int targetSlotIndex, float duration)
    {
        if (slot.Occupant == null)
        {
            return;
        }

        slot.CurrentLocalPosition = slot.Occupant.Renderer.transform.localPosition;
        slot.CurrentAngleDeg = GetAngleDegFromPosition(slot.CurrentLocalPosition);
        slot.TargetLocalPosition = SlotLocalPositions[targetSlotIndex];
        slot.TargetAngleDeg = GetSlotAngleDeg(targetSlotIndex);
        slot.MotionRadius = SlotFanRadius;
        slot.MotionDuration = duration;
        slot.MoveLerpT = 0f;
    }

    private void StartEvictedOrbAnimation(SpriteRenderer renderer)
    {
        Vector3 worldPosition = renderer.transform.position;
        Vector3 arcCenter = _root != null ? _root.transform.position : worldPosition;
        Vector3 localOffset = worldPosition - arcCenter;
        float startAngleDeg = Mathf.Atan2(localOffset.y, localOffset.x) * Mathf.Rad2Deg;
        float arcRadius = localOffset.magnitude;
        renderer.transform.SetParent(null, true);
        renderer.transform.position = worldPosition;
        renderer.transform.localScale = new Vector3(EvictedOrbScale, EvictedOrbScale, 1f);
        TransientVisual visual = new(
            renderer.gameObject,
            renderer,
            EvictedOrbLifetime,
            new Vector3(4.2f, 0.85f, 0f),
            renderer.color,
            renderer.transform.localScale)
        {
            UseArcMotion = true,
            StartPosition = worldPosition,
            ArcCenter = arcCenter,
            ArcRadius = arcRadius,
            StartAngleDeg = startAngleDeg,
            EndAngleDeg = startAngleDeg + EvictedOrbExitAngleOffsetDeg,
            EndPosition = arcCenter + EvaluateArcPoint(startAngleDeg + EvictedOrbExitAngleOffsetDeg, arcRadius)
        };
        _visualService.TrackTransientVisual(
            visual);
    }

    private static int[] GetFillOrder()
    {
        return new[] { RightSlotIndex, CenterSlotIndex, LeftSlotIndex };
    }
}
