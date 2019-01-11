using Mirror;

public class IgnoranceNetworkManager : NetworkManager {
    // Override the transport.
    public override void InitializeTransport()
    {
        Transport.layer = new IgnoranceTransport();
    }
    // ... your code below ...
}
