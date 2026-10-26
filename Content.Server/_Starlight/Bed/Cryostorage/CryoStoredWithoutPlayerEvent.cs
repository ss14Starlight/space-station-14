namespace Content.Server._Starlight.Bed.Cryostorage;

// raised on a body entering cryostorage when no player was found to return job slots for
[ByRefEvent]
public readonly record struct CryoStoredWithoutPlayerEvent;
