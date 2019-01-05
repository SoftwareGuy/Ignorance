using UnityEngine;
using Mirror;
using Mirror.Transport;

public class IgnoranceNetworkManager : NetworkManager
{
    // The server port to bind to, use this one. You can set it in the Unity Inspector.
    // Mirror went though some massive rewrites so this is needed.
    public ushort m_ServerUDPPort = 7779;

    /// <summary>
    /// Initialize the transport for use with Mirror.
    /// </summary>
    public override void InitializeTransport()
    {
        // Do not call the following unless you want to use TCP and WebSockets.
        // base.InitializeTransport();

        transport = new IgnoranceTransport(networkAddress, m_ServerUDPPort, (ushort)maxConnections);
        // Nothing more needs to be done. You're good to go.
    }

    // Rest of your code goes below...
}
