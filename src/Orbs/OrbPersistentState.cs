using System.Collections.Generic;
using DeVect.Orbs.Definitions;
using DeVect.Orbs.Runtime;

namespace DeVect.Orbs;

internal sealed class OrbPersistentState
{
    public List<OrbInstanceSnapshot> FilledOrbs { get; } = new();

    public void ReplaceFromRuntime(IReadOnlyList<OrbInstanceSnapshot> snapshots)
    {
        FilledOrbs.Clear();
        for (int i = 0; i < snapshots.Count; i++)
        {
            FilledOrbs.Add(snapshots[i]);
        }
    }

    public int GetFilledCount()
    {
        return FilledOrbs.Count;
    }

    public void Clear()
    {
        FilledOrbs.Clear();
    }
}
