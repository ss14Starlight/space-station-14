using Content.Shared._Starlight.VanguardSuit;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.VanguardSuit;

public sealed class HandcannonDeploymentSystem : SharedHandcannonDeploymentSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    protected override void SpawnHandcannon(Entity<HandcannonDeploymentComponent> ent, EntityUid wearer)
    {
        // Spawn the handcannon
        var handcannon = Spawn(ent.Comp.HandcannonPrototype, Transform(wearer).Coordinates);

        // Try to put it in their hands
        if (Hands.TryGetEmptyHand(wearer, out var hand))
        {
            Hands.TryPickup(wearer, handcannon, hand);
            Popup.PopupEntity(Loc.GetString("handcannon-deploy-success"), wearer, wearer);
            _audio.PlayPvs("/Audio/Effects/phasein.ogg", wearer);
        }
        else
        {
            // If no empty hands, just drop it
            Popup.PopupEntity(Loc.GetString("handcannon-deploy-dropped"), wearer, wearer);
        }
    }
}
