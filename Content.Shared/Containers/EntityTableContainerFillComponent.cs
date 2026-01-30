using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Shared.Containers;

/// <summary>
/// Version of <see cref="ContainerFillComponent"/> that utilizes <see cref="EntityTableSelector"/>
/// </summary>
[RegisterComponent, Access(typeof(ContainerFillSystem))]
public sealed partial class EntityTableContainerFillComponent : Component
{
    [DataField]
    public Dictionary<string, EntityTableSelector> Containers = new();

    //Starlight start
    /// <summary>
    ///   If true, when an entity cannot be inserted into the container, it will be left in the world and no error will be logged.
    ///   This is useful if overflow is the accepted behaviour, particularly if a spawn table is really random
    /// </summary>
    [DataField]
    public bool IgnoreIfCannotFit = false;
    //Starlight end
}
