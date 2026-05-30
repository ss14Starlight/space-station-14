using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Whitelist;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Computers.RemoteEye;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedRemoteEyeSystem))]
public sealed partial class RemoteEyeConsoleComponent : Component
{
    [DataField(readOnly: true)]
    public EntProtoId RemoteEntityProto;

    [DataField]
    public EntityUid? RemoteEntity;

    [DataField(readOnly: true)]
    public EntityWhitelist? Whitelist;

    [DataField(readOnly: true)]
    public EntProtoId<ActionComponent>[] Actions = [];

    [DataField]
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// If true, instead of showing a UI, will set the camera's location to the console's location.
    /// Meant for structures like the Xenobiology console where the console's viewing area is the same as the user's location.
    /// </summary>
    [DataField(readOnly: true)]
    public bool ViewOnConsolePosition = false;

    /// <summary>
    /// Set of users currently using the console.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Users = new();
}
