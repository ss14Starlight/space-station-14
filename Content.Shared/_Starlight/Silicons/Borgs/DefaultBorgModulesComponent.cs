using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// This component facilitates flagging a borg module as default for borgs that don't respect the defaults defined in borg_types.yml.
/// Will also fill in the modules if they do not exist in the borg already, behaving similar to <see cref="Containers.ContainerFillComponent"/>
/// </summary>
[RegisterComponent]
public sealed partial class DefaultBorgModulesComponent : Component
{
    /// <summary>
    /// List of module entity prototypes.
    /// </summary>
    [DataField] public List<EntProtoId> Modules;
}