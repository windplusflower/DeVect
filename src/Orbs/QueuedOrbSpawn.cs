using DeVect.Orbs.Definitions;

namespace DeVect.Orbs;

internal readonly struct QueuedOrbSpawn
{
    public QueuedOrbSpawn(OrbTypeId orbType, int count)
    {
        OrbType = orbType;
        Count = count;
    }

    public OrbTypeId OrbType { get; }

    public int Count { get; }
}
