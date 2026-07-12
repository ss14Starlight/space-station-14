using System.Diagnostics.CodeAnalysis;
using Orleans;

namespace Content.Server._NullLink.Core;

public sealed partial class ActorRouter : IActorRouter, IDisposable
{
    public bool TryGetGrain<TGrainInterface>(
            Guid primaryKey,
            [NotNullWhen(true)] out TGrainInterface? grain,
            string? grainClassNamePrefix = null)
            where TGrainInterface : IGrainWithGuidKey
    {
        if (OrleansClientHolder.Client is { } client)
        {
            grain = client.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);
            return true;
        }

        grain = default;
        return false;
    }

    public bool TryGetGrain<TGrainInterface>(
            int primaryKey,
            [NotNullWhen(true)] out TGrainInterface? grain,
            string? grainClassNamePrefix = null)
            where TGrainInterface : IGrainWithIntegerKey
    {
        if (OrleansClientHolder.Client is { } client)
        {
            grain = client.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);
            return true;
        }

        grain = default;
        return false;
    }

    public bool TryGetGrain<TGrainInterface>(
            string primaryKey,
            [NotNullWhen(true)] out TGrainInterface? grain,
            string? grainClassNamePrefix = null)
            where TGrainInterface : IGrainWithStringKey
    {
        if (OrleansClientHolder.Client is { } client)
        {
            grain = client.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);
            return true;
        }

        grain = default;
        return false;
    }

    public bool TryCreateObjectReference<TGrainObserverInterface>(IGrainObserver obj, [NotNullWhen(true)] out TGrainObserverInterface? objectReference)
        where TGrainObserverInterface : IGrainObserver
    {
        if (OrleansClientHolder.Client is { } client)
        {
            objectReference = client.CreateObjectReference<TGrainObserverInterface>(obj);
            return true;
        }

        objectReference = default;
        return false;
    }
}
