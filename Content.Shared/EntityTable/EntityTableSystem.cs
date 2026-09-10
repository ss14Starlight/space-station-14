using System.Diagnostics.CodeAnalysis;
using Content.Shared.EntityTable.EntitySelectors;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.EntityTable;

public sealed partial class EntityTableSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    public IEnumerable<EntProtoId> GetSpawns(EntityTablePrototype entTableProto, System.Random? rand = null, EntityTableContext? ctx = null)
    {
        // convenient
        return GetSpawns(entTableProto.Table, rand, ctx);
    }

    public IEnumerable<EntProtoId> GetSpawns(EntityTableSelector? table, System.Random? rand = null, EntityTableContext? ctx = null)
    {
        if (table == null)
            return new List<EntProtoId>();

        rand ??= _random.GetRandom();
        ctx ??= new EntityTableContext();
        return table.GetSpawns(rand, EntityManager, _prototypeManager, ctx);
    }

    // TODO: Have this method be much better for entity tables
    public IEnumerable<EntProtoId> ListSpawns(EntityTableSelector? table, System.Random? rand = null, EntityTableContext? ctx = null)
    {
        if (table == null)
            return new List<EntProtoId>();

        rand ??= _random.GetRandom();
        ctx ??= new EntityTableContext();
        return table.ListSpawns(rand, EntityManager, _prototypeManager, ctx);
    }
}

/// <summary>
/// Context used by selectors and conditions to evaluate in generic gamestate information.
/// </summary>
public sealed class EntityTableContext
{
    private readonly Dictionary<string, object> _data = new();
    private readonly Dictionary<Type, object> _typedData = new(); // Starlight

    public EntityTableContext()
    {

    }

    public EntityTableContext(Dictionary<string, object> data)
    {
        _data = data;
    }

    #region Starlight
    /// <summary>
    /// Sets arbitrary context data for selectors and conditions that are evaluated later in the same table roll.
    /// </summary>
    [PublicAPI]
    public void SetData<T>([ForbidLiteral] string key, T value) where T : notnull
    {
        _data[key] = value;
    }

    /// <summary>
    /// Sets strongly-typed context data for selectors and conditions that are evaluated later in the same table roll.
    /// </summary>
    [PublicAPI]
    public void SetData<T>(T value) where T : notnull
    {
        _typedData[typeof(T)] = value;
    }
    #endregion

    /// <summary>
    /// Retrieves an arbitrary piece of data from the context based on a provided key.
    /// </summary>
    /// <param name="key">A string key that corresponds to the value we are searching for. </param>
    /// <param name="value">The value we are trying to extract from the context object</param>
    /// <typeparam name="T">The type of <see cref="value"/> that we are trying to retrieve</typeparam>
    /// <returns>If <see cref="key"/> has a corresponding value of type <see cref="T"/></returns>
    [PublicAPI]
    public bool TryGetData<T>([ForbidLiteral] string key, [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (!_data.TryGetValue(key, out var valueData) || valueData is not T castValueData)
            return false;

        value = castValueData;
        return true;
    }

    #region Starlight
    /// <summary>
    /// Retrieves strongly-typed context data.
    /// </summary>
    [PublicAPI]
    public bool TryGetData<T>([NotNullWhen(true)] out T? value)
    {
        value = default;
        if (!_typedData.TryGetValue(typeof(T), out var valueData) || valueData is not T castValueData)
            return false;

        value = castValueData;
        return true;
    }
    #endregion
}
