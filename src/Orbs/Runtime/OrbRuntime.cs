using System;
using System.Collections.Generic;
using System.Linq;
using DeVect.Orbs.Definitions;
using DeVect.Visual;
using UnityEngine;

namespace DeVect.Orbs.Runtime;

internal sealed class OrbRuntime
{
    private static readonly Vector3 RootOffset = new(0f, 0.1f, 0f);

    private const float SlotFanRadius = 1.62f;
    private const float SlotFanCenterAngleDeg = 90f;
    private const float ThreeSlotSpreadDeg = 76f;
    private const float FourSlotSpreadDeg = 102f;
    private const float OrbScale = 0.42f;
    private const float EvictedOrbScale = 0.42f;
    private const float SlotMoveDuration = 0.14f;
    private const float SpawnEntryDuration = 0.16f;
    private const float EvictedOrbLifetime = 0.2f;
    private const float SpawnEntryAngleOffsetDeg = 24f;
    private const float EvictedOrbExitAngleOffsetDeg = -28f;

    private readonly OrbVisualService _visualService;
    private readonly List<OrbSlotRuntime> _slots = new();
    private Vector3[] _slotLocalPositions = Array.Empty<Vector3>();
    private Transform? _heroTransform;
    private GameObject? _root;
    private bool _suppressSpawnEntryAnimation;

    public OrbRuntime(OrbVisualService visualService)
    {
        _visualService = visualService;
    }

    public int Capacity { get; private set; }

    public void EnsureBuilt(Transform heroTransform, int capacity, IReadOnlyList<OrbInstanceSnapshot> persistedOrbs, OrbDefinitionRegistry definitions)
    {
        int normalizedCapacity = Mathf.Clamp(capacity, 3, 4);
        if (_root != null && IsBoundTo(heroTransform) && Capacity == normalizedCapacity)
        {
            return;
        }

        Dispose();

        Capacity = normalizedCapacity;
        _slotLocalPositions = BuildSlotLocalPositions(Capacity);
        _heroTransform = heroTransform;
        _root = new GameObject("DeVect_OrbRuntime");
        _root.transform.position = _heroTransform.position + RootOffset;
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;

        _slots.Clear();
        for (int i = 0; i < _slotLocalPositions.Length; i++)
        {
            GameObject slot = new($"DeVect_OrbSlot_{i}");
            slot.transform.SetParent(_root.transform, false);
            slot.transform.localPosition = _slotLocalPositions[i];
            OrbSlotRuntime runtimeSlot = new(slot.transform);
            _slots.Add(runtimeSlot);
            _visualService.BuildDashedRing(slot.transform);
            RefreshSlotVisual(runtimeSlot);
        }

        _suppressSpawnEntryAnimation = true;
        List<OrbInstanceSnapshot> orderedSnapshots = NormalizeSnapshotsForCapacity(persistedOrbs, Capacity)
            .OrderBy(snapshot => snapshot.SlotIndex)
            .ToList();
        for (int i = 0; i < orderedSnapshots.Count; i++)
        {
            TrySpawnOrbInSlot(orderedSnapshots[i].SlotIndex, orderedSnapshots[i].TypeId, orderedSnapshots[i].CurrentDamage, definitions);
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
            for (int i = 0; i < _slots.Count; i++)
            {
                OrbSlotRuntime slot = _slots[i];
                if (slot.Occupant == null)
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
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].IsOccupied)
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
        int[] fillOrder = GetFillOrder();
        for (int i = 0; i < fillOrder.Length; i++)
        {
            OrbSlotRuntime slot = _slots[fillOrder[i]];
            if (slot.Occupant != null)
            {
                snapshots.Add(new OrbInstanceSnapshot(slot.Occupant.TypeId, fillOrder[i], slot.Occupant.CurrentDamage));
            }
        }

        return snapshots;
    }

    public IEnumerable<OrbInstance> EnumerateActiveOrbs()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Occupant != null)
            {
                yield return _slots[i].Occupant!;
            }
        }
    }

    public bool TrySpawnOrbInNextAvailableSlot(OrbTypeId typeId, int initialDamage, OrbDefinitionRegistry definitions)
    {
        int[] fillOrder = GetFillOrder();
        for (int i = 0; i < fillOrder.Length; i++)
        {
            int slotIndex = fillOrder[i];
            if (_slots[slotIndex].IsOccupied)
            {
                continue;
            }

            return TrySpawnOrbInSlot(slotIndex, typeId, initialDamage, definitions);
        }

        return false;
    }

    public bool TryForceInsertOrbFromLeft(OrbTypeId newTypeId, int initialDamage, OrbDefinitionRegistry definitions, out OrbInstance? evictedOrb)
    {
        evictedOrb = null;
        if (_root == null || GetActiveOrbCount() < Capacity || _slots.Count == 0)
        {
            return false;
        }

        int rightmostSlotIndex = _slots.Count - 1;
        OrbSlotRuntime rightmostSlot = _slots[rightmostSlotIndex];
        if (rightmostSlot.Occupant == null)
        {
            return false;
        }

        evictedOrb = rightmostSlot.Occupant;
        StartEvictedOrbAnimation(evictedOrb.Renderer);
        rightmostSlot.Clear();
        RefreshSlotVisual(rightmostSlot);

        for (int slotIndex = rightmostSlotIndex - 1; slotIndex >= 0; slotIndex--)
        {
            OrbSlotRuntime fromSlot = _slots[slotIndex];
            if (fromSlot.Occupant == null)
            {
                return false;
            }

            OrbInstance movingInstance = fromSlot.Occupant;
            fromSlot.Clear();
            RefreshSlotVisual(fromSlot);
            AssignInstanceToSlot(_slots[slotIndex + 1], movingInstance, slotIndex + 1, SlotMoveDuration);
        }

        IOrbDefinition newDefinition = definitions.Get(newTypeId);
        SpriteRenderer newRenderer = _visualService.CreateOrbRenderer($"DeVect_{newDefinition.DisplayName}Orb_Inserted", newTypeId, newDefinition.OrbColor);
        newRenderer.transform.SetParent(_root.transform, false);
        newRenderer.transform.localPosition = EvaluateArcPoint(GetSpawnEntryStartAngleDeg(0), SlotFanRadius);
        newRenderer.transform.localScale = new Vector3(OrbScale, OrbScale, 1f);

        OrbSlotRuntime leftmostSlot = _slots[0];
        leftmostSlot.Occupant = new OrbInstance(newTypeId, newDefinition, newRenderer, initialDamage);
        StartSpawnEntry(leftmostSlot, 0);
        RefreshSlotVisual(leftmostSlot);
        return true;
    }

    public bool RemoveOrb(OrbInstance instance)
    {
        if (instance == null)
        {
            return false;
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            OrbSlotRuntime slot = _slots[i];
            if (slot.Occupant != instance)
            {
                continue;
            }

            if (instance.Renderer != null)
            {
                UnityEngine.Object.Destroy(instance.Renderer.gameObject);
            }

            slot.Clear();
            CollapseSlotsAfterRemoval(i);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            OrbSlotRuntime slot = _slots[i];
            if (slot.Occupant?.Renderer != null)
            {
                UnityEngine.Object.Destroy(slot.Occupant.Renderer.gameObject);
            }

            slot.Clear();
        }

        _slots.Clear();
        _slotLocalPositions = Array.Empty<Vector3>();

        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
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

    private bool TrySpawnOrbInSlot(int slotIndex, OrbTypeId typeId, int currentDamage, OrbDefinitionRegistry definitions)
    {
        if (_root == null || slotIndex < 0 || slotIndex >= _slots.Count)
        {
            return false;
        }

        IOrbDefinition definition = definitions.Get(typeId);
        SpriteRenderer renderer = _visualService.CreateOrbRenderer($"DeVect_{definition.DisplayName}Orb_{slotIndex}", typeId, definition.OrbColor);
        renderer.transform.SetParent(_root.transform, false);
        renderer.transform.localPosition = _slotLocalPositions[slotIndex];
        renderer.transform.localScale = new Vector3(OrbScale, OrbScale, 1f);

        OrbSlotRuntime slot = _slots[slotIndex];
        slot.Occupant = new OrbInstance(typeId, definition, renderer, currentDamage);
        slot.CurrentLocalPosition = _slotLocalPositions[slotIndex];
        slot.TargetLocalPosition = _slotLocalPositions[slotIndex];
        slot.CurrentAngleDeg = GetSlotAngleDeg(slotIndex, Capacity);
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

    private void CollapseSlotsAfterRemoval(int removedSlotIndex)
    {
        RefreshSlotVisual(_slots[removedSlotIndex]);

        for (int slotIndex = removedSlotIndex - 1; slotIndex >= 0; slotIndex--)
        {
            OrbSlotRuntime fromSlot = _slots[slotIndex];
            if (fromSlot.Occupant == null)
            {
                continue;
            }

            OrbInstance movingInstance = fromSlot.Occupant;
            fromSlot.Clear();
            RefreshSlotVisual(fromSlot);
            AssignInstanceToSlot(_slots[slotIndex + 1], movingInstance, slotIndex + 1, SlotMoveDuration);
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            RefreshSlotVisual(_slots[i]);
        }
    }

    private void AssignInstanceToSlot(OrbSlotRuntime slot, OrbInstance instance, int targetSlotIndex, float duration)
    {
        slot.Occupant = instance;
        StartSlotMove(slot, targetSlotIndex, duration);
        RefreshSlotVisual(slot);
    }

    private static void SetDashedRingVisible(OrbSlotRuntime slot, bool visible)
    {
        if (slot.Anchor == null)
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
        SetDashedRingVisible(slot, !slot.IsOccupied);
    }

    private float GetSpawnEntryStartAngleDeg(int slotIndex)
    {
        return GetSlotAngleDeg(slotIndex, Capacity) + SpawnEntryAngleOffsetDeg;
    }

    private void StartSpawnEntry(OrbSlotRuntime slot, int slotIndex)
    {
        if (slot.Occupant == null)
        {
            return;
        }

        float startAngle = GetSpawnEntryStartAngleDeg(slotIndex);
        float targetAngle = GetSlotAngleDeg(slotIndex, Capacity);
        Vector3 start = EvaluateArcPoint(startAngle, SlotFanRadius);
        slot.Occupant.Renderer.transform.localPosition = start;
        slot.CurrentLocalPosition = start;
        slot.TargetLocalPosition = _slotLocalPositions[slotIndex];
        slot.CurrentAngleDeg = startAngle;
        slot.TargetAngleDeg = targetAngle;
        slot.MotionRadius = SlotFanRadius;
        slot.MotionDuration = SpawnEntryDuration;
        slot.MoveLerpT = 0f;
    }

    private static Vector3[] BuildSlotLocalPositions(int capacity)
    {
        Vector3[] positions = new Vector3[capacity];
        for (int i = 0; i < capacity; i++)
        {
            positions[i] = EvaluateArcPoint(GetSlotAngleDeg(i, capacity), SlotFanRadius);
        }

        return positions;
    }

    private static float GetSlotAngleDeg(int slotIndex, int capacity)
    {
        if (capacity <= 1)
        {
            return SlotFanCenterAngleDeg;
        }

        float totalSpread = capacity >= 4 ? FourSlotSpreadDeg : ThreeSlotSpreadDeg;
        float step = totalSpread / (capacity - 1);
        float leftmostAngle = SlotFanCenterAngleDeg + (totalSpread * 0.5f);
        return leftmostAngle - (step * slotIndex);
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
        slot.TargetLocalPosition = _slotLocalPositions[targetSlotIndex];
        slot.TargetAngleDeg = GetSlotAngleDeg(targetSlotIndex, Capacity);
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
        _visualService.TrackTransientVisual(visual);
    }

    private int[] GetFillOrder()
    {
        int[] fillOrder = new int[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
        {
            fillOrder[i] = (_slots.Count - 1) - i;
        }

        return fillOrder;
    }

    private static IEnumerable<OrbInstanceSnapshot> NormalizeSnapshotsForCapacity(IReadOnlyList<OrbInstanceSnapshot> persistedOrbs, int targetCapacity)
    {
        if (persistedOrbs.Count == 0)
        {
            return Array.Empty<OrbInstanceSnapshot>();
        }

        List<OrbInstanceSnapshot> orderedSnapshots = persistedOrbs
            .OrderBy(snapshot => snapshot.SlotIndex)
            .ToList();

        int inferredCapacity = Mathf.Max(3, orderedSnapshots.Max(snapshot => snapshot.SlotIndex) + 1);
        if (inferredCapacity == targetCapacity)
        {
            return orderedSnapshots.Where(snapshot => snapshot.SlotIndex < targetCapacity);
        }

        int capacityDelta = targetCapacity - inferredCapacity;
        if (capacityDelta > 0)
        {
            return orderedSnapshots
                .Select(snapshot => new OrbInstanceSnapshot(snapshot.TypeId, snapshot.SlotIndex + capacityDelta, snapshot.CurrentDamage))
                .Where(snapshot => snapshot.SlotIndex < targetCapacity);
        }

        int removedLeftSlots = -capacityDelta;
        return orderedSnapshots
            .Where(snapshot => snapshot.SlotIndex >= removedLeftSlots)
            .Select(snapshot => new OrbInstanceSnapshot(snapshot.TypeId, snapshot.SlotIndex - removedLeftSlots, snapshot.CurrentDamage))
            .Where(snapshot => snapshot.SlotIndex < targetCapacity);
    }
}
