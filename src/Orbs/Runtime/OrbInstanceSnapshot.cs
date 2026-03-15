using DeVect.Orbs.Definitions;

namespace DeVect.Orbs.Runtime;

internal readonly struct OrbInstanceSnapshot
{
    public OrbInstanceSnapshot(OrbTypeId typeId, int slotIndex)
    {
        TypeId = typeId;
        SlotIndex = slotIndex;
    }

    public OrbTypeId TypeId { get; }

    public int SlotIndex { get; }
}
