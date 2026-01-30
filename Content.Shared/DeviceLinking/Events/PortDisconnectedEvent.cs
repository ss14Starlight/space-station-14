namespace Content.Shared.DeviceLinking.Events
{
    public sealed class PortDisconnectedEvent : EntityEventArgs
    {
        public readonly string Port;

        public readonly EntityUid Source; // Starlight-edit

        public readonly EntityUid Sink; // Starlight-edit

        public PortDisconnectedEvent(string port, EntityUid source, EntityUid sink) // Starlight-edit
        {
            Port = port;
            Source = source; // Starlight-edit
            Sink = sink; // Starlight-edit
        }
    }
}
