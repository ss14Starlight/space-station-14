using System.Numerics;
using Content.Shared._Starlight.Sound;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Threading;

namespace Content.Server._Starlight.Physics;

public struct HandleMobMovementJob(SLMoverController moverController) : IParallelRobustJob
{
    public readonly int BatchSize => 32;

    public float FrameTime;

    private List<Entity<InputMoverComponent>> _input = [];
    private Vector2?[] _velocities = [];
    private Angle?[] _rotations = [];
    private SoundEvent?[] _sounds = [];
    private bool[] _used = [];
    private PhysicsComponent?[] _bodies = [];
    private TransformComponent?[] _xforms = [];

    public void Prepare(List<Entity<InputMoverComponent>> entities)
    {
        _input = entities;
        EnsureCapacity(ref _velocities, entities.Count);
        EnsureCapacity(ref _rotations, entities.Count);
        EnsureCapacity(ref _sounds, entities.Count);
        EnsureCapacity(ref _used, entities.Count);
        EnsureCapacity(ref _bodies, entities.Count);
        EnsureCapacity(ref _xforms, entities.Count);
    }

    public readonly Span<Vector2?> Velocities => _velocities.AsSpan(.._input.Count);
    public readonly Span<Angle?> Rotations => _rotations.AsSpan(.._input.Count);
    public readonly Span<SoundEvent?> Sounds => _sounds.AsSpan(.._input.Count);
    public readonly Span<bool> Used => _used.AsSpan(.._input.Count);

    // stuff the workers already resolved, so the scatter loop doesn't query it all over again.
    public readonly Span<PhysicsComponent?> Bodies => _bodies.AsSpan(.._input.Count);
    public readonly Span<TransformComponent?> Xforms => _xforms.AsSpan(.._input.Count);

    public void Execute(int index)
    {
        try
        {
            var velocity = moverController.HandleAIMobMovement(_input[index], FrameTime, out var playSound, out var rotation, out var used, out var body, out var xform);
            _velocities[index] = velocity;
            _sounds[index] = playSound;
            _rotations[index] = rotation;
            _used[index] = used;
            _bodies[index] = body;
            _xforms[index] = xform;
        }
        catch (Exception)
        {

        }
    }

    static void EnsureCapacity<T>(ref T[] arr, int needed)
    {
        if (arr is null) { arr = new T[BitOperations.RoundUpToPowerOf2((uint)needed)]; return; }
        if (arr.Length >= needed) return;
        Array.Resize(ref arr, (int)BitOperations.RoundUpToPowerOf2((uint)needed));
    }
}
