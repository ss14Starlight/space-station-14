namespace Content.Server._Starlight.Physics.Diagnostics;

/// <summary>
/// Approximates the solver's islands among awake bodies: touching hard contacts plus joints.
/// Static bodies aren't added, so, as in the solver, they don't link islands.
/// </summary>
public sealed class AwakeIslandEstimator
{
    private readonly Dictionary<EntityUid, int> _index = new();
    private readonly List<EntityUid> _members = new();
    private readonly List<int> _parent = new();
    private readonly List<int> _size = new();

    /// <summary>
    /// Removes every body so the estimator can be reused for the next sample.
    /// </summary>
    public void Clear()
    {
        _index.Clear();
        _members.Clear();
        _parent.Clear();
        _size.Clear();
    }

    /// <summary>
    /// Adds a body as an island of its own. Adding a body twice does nothing.
    /// </summary>
    public void Add(EntityUid uid)
    {
        if (!_index.TryAdd(uid, _parent.Count))
            return;

        _members.Add(uid);
        _parent.Add(_parent.Count);
        _size.Add(1);
    }

    /// <summary>
    /// Merges the islands of two bodies. Does nothing if either body wasn't added, such as a static or paused one.
    /// </summary>
    public void Link(EntityUid a, EntityUid b)
    {
        if (!_index.TryGetValue(a, out var indexA) || !_index.TryGetValue(b, out var indexB))
            return;

        var rootA = Find(indexA);
        var rootB = Find(indexB);
        if (rootA == rootB)
            return;

        if (_size[rootA] < _size[rootB])
            (rootA, rootB) = (rootB, rootA);

        _parent[rootB] = rootA;
        _size[rootA] += _size[rootB];
    }

    /// <summary>
    /// Counts islands with more than one body and returns the members of the largest.
    /// </summary>
    public (int Islands, List<EntityUid> Largest) Summarize()
    {
        var islands = 0;
        var largestRoot = -1;

        for (var i = 0; i < _parent.Count; i++)
        {
            if (Find(i) != i || _size[i] < 2)
                continue;

            islands++;
            if (largestRoot < 0 || _size[i] > _size[largestRoot])
                largestRoot = i;
        }

        var largest = new List<EntityUid>();
        if (largestRoot < 0)
            return (islands, largest);

        for (var i = 0; i < _parent.Count; i++)
        {
            if (Find(i) == largestRoot)
                largest.Add(_members[i]);
        }

        return (islands, largest);
    }

    private int Find(int i)
    {
        while (_parent[i] != i)
        {
            _parent[i] = _parent[_parent[i]];
            i = _parent[i];
        }

        return i;
    }
}
