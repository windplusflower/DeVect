using DeVect.Orbs.Definitions;

namespace DeVect.Orbs.Runtime;

internal readonly struct OrbInstanceSnapshot
{
    public OrbInstanceSnapshot(OrbTypeId typeId, int slotIndex, int currentDamage)
    {
        TypeId = typeId;
        SlotIndex = slotIndex;
        CurrentDamage = currentDamage;
    }

    public OrbTypeId TypeId { get; }

    public int SlotIndex { get; }

    public int CurrentDamage { get; }
}
