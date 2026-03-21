namespace Content.Shared._Starlight.Movement.Events;

public sealed class ApplyMovementScaleModifierEvent (float? originalWalkSpeed, float? originalSprintSpeed) : EntityEventArgs
{
        public float OriginalWalkSpeedModifier { get; private set; } = originalWalkSpeed ?? 1.0f;
        public float OriginalSprintSpeedModifier { get; private set; } = originalSprintSpeed ?? 1.0f;

        public float? ChangedWalkSpeedModifier { get; set; }
        public float? ChangedSprintSpeedModifier { get; set; }
}