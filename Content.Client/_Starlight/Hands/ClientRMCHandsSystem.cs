using System.Linq;
using Content.Client.Hands.Systems;
using Content.Shared._Starlight.Input;
using Content.Shared.Hands.Components;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Hands;

public sealed partial class ClientRMCHandsSystem : EntitySystem
{
    [Dependency] private HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(CMKeyFunctions.SLInteractWithOtherHand, // Prefixes changed from RMC to SL to allow rebinds without affecting RMC or vice versa
                InputCmdHandler.FromDelegate(session =>
                    {
                        if (session?.AttachedEntity is not { } ent)
                            return;

                        if (!TryComp(ent, out HandsComponent? hands))
                            return;

                        if (!_hands.EnumerateHands((ent, hands)).Skip(1).TryFirstOrDefault(out var other))
                            return;

                        _hands.UIHandClick((ent, hands), other, false);
                    }))
            .Register<ClientRMCHandsSystem>();
    }
}
