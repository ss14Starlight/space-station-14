namespace Content.Server._Starlight.Bed.Cryostorage;

// snapshot of a cryo'd player's stuff, held until someone takes their slot
public sealed class CryoLoadout
{
    // the cryo'd body the items still hang off of, parked on the paused map
    public EntityUid Body;

    // the pod they walked into, used as the forced spawn spot
    public EntityUid? Pod;

    // top level worn and held items grabbed when they went under
    public List<EntityUid> Items = [];
}
