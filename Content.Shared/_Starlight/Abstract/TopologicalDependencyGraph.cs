namespace Content.Shared._Starlight.Abstract;

public sealed class TopologicalDependencyGraph<T>
    where T : notnull
{
    private readonly Dictionary<T, int> _inDegree = [];
    private readonly Dictionary<T, HashSet<T>> _edges = [];

    public void AddNode(T id)
    {
        _inDegree.TryAdd(id, 0);
        _edges.TryAdd(id, []);
    }

    public void AddEdge(T from, T to)
    {
        AddNode(from);
        AddNode(to);

        if (_edges[from].Add(to))
            _inDegree[to]++;
    }

    public TopologicalSortResult<T> Sort()
    {
        var inDegree = new Dictionary<T, int>(_inDegree);
        var order = new Dictionary<T, int>();
        var queue = new Queue<T>();

        foreach (var (id, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(id);
        }

        var nextOrder = 0;
        while (queue.TryDequeue(out var current))
        {
            order[current] = nextOrder++;
            foreach (var next in _edges[current])
            {
                if (--inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        List<T>? cyclic = null;
        foreach (var (id, degree) in inDegree)
        {
            if (degree > 0)
            {
                (cyclic ??= []).Add(id);
                order[id] = nextOrder++;
            }
        }

        return new TopologicalSortResult<T>(order, (IReadOnlyList<T>?)cyclic ?? []);
    }
}

public readonly record struct TopologicalSortResult<T>(
    Dictionary<T, int> Order,
    IReadOnlyList<T> CyclicNodes)
    where T : notnull;
