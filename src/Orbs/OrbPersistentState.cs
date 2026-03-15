using System.Collections.Generic;
using DeVect.Orbs.Definitions;
using DeVect.Orbs.Runtime;

namespace DeVect.Orbs;

internal sealed class OrbPersistentState
{
    public List<OrbTypeId> FilledOrbSequence { get; } = new();

    public void ReplaceFromRuntime(IReadOnlyList<OrbInstanceSnapshot> snapshots)
    {
        FilledOrbSequence.Clear();
        for (int i = 0; i < snapshots.Count; i++)
        {
            FilledOrbSequence.Add(snapshots[i].TypeId);
        }
    }

    public int GetFilledCount()
    {
        return FilledOrbSequence.Count;
    }

    public void Clear()
    {
        FilledOrbSequence.Clear();
    }
}
