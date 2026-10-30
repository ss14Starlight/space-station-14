using Content.Shared._Starlight.Radio;
using Content.Shared.Radio.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Radio.EntitySystems;

public sealed partial class EncryptionKeySystem
{
    [SubscribeLocalEvent]
    private void OnEncryptionKeyToggle(EntityUid uid, EncryptionKeyHolderComponent comp, ref EncryptionKeyToggleMessage args)
    {
        foreach (var key in comp.KeyContainer.ContainedEntities)
        {
            if (!TryComp<EncryptionKeyComponent>(key, out var keyComp))
                continue;

            if (keyComp.Channels.Contains(args.ProtoId))
            {
                keyComp.Channels.Remove(args.ProtoId);
                keyComp.MutedChannels.Add(args.ProtoId);
                Dirty(key, keyComp);
                UpdateChannels(uid, comp);
            }
            else if (keyComp.MutedChannels.Contains(args.ProtoId))
            {
                keyComp.MutedChannels.Remove(args.ProtoId);
                keyComp.Channels.Add(args.ProtoId);
                Dirty(key, keyComp);
                UpdateChannels(uid, comp);
            }
        }
    }

    private void UnmuteKey(EntityUid key)
    {
        if (!TryComp<EncryptionKeyComponent>(key, out var keyComp) || keyComp.MutedChannels.Count == 0)
            return;

        keyComp.Channels.UnionWith(keyComp.MutedChannels);
        keyComp.MutedChannels.Clear();
        Dirty(key, keyComp);
    }
}
