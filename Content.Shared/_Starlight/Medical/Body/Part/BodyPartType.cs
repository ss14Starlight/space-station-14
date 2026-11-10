using Content.Shared._Starlight.Body.Components;
using Content.Shared.Body.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Body.Part
{
    /// <summary>
    ///     Defines the type of a <see cref="SLBodyComponent"/>.
    /// </summary>
    [Serializable, NetSerializable]
    public enum BodyPartType
    {
        Other = 0,
        Torso,
        Head,
        Arm,
        Hand,
        Leg,
        Foot,
        Tail
    }
}
