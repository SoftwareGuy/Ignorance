// SoftwareGuy's Ignorance Reliable UDP Transport
// Uses ENET as the transport backend.
// ----------------------------------------
// Ignorance Transport by Coburn, 2018
// ENet-C# by nxrighthere, 2018
// ENet by the ENet developers, whenever - whenever.
// ----------------------------------------
// IGNORANCE TRANSPORT COMES WITH NO WARRANTY WHATSOEVER.
// BY USING THIS TRANSPORT FOR MIRROR YOU AGREE THAT YOU
// CANNOT AND WILL NOT HOLD THE DEVELOPER LIABLE FOR ANY
// LOSS OF GAME DEVELOPMENT PROGRESS, DATA LOSS OR OTHER
// PROBLEMS CAUSED DIRECTLY OR INDIRECTLY BY THIS CODE.
// ----------------------------------------
// It would be greatly appreciated if you reported bugs
// and donate coffee at https://github.com/SoftwareGuy/Ignorance.
// ----------------------------------------
// THIS IS THE MIRROR 2017 VERSION OF IGNORANCE.
// IF YOU ARE USING UNITY 2018, IT IS RECOMMENDED YOU USE
// MIRROR 2018 AND THE MIRROR 2018 BRANCH OF IGNORANCE.
// PLEASE SEE THE README FOR FURTHER INFORMATION.
// ----------------------------------------

using ENet;
using System;
using System.Collections.Generic;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Mirror
{
    /// <summary>
    /// Ignorance rUDP Transport is built upon the ENet-C# wrapper by nxrighthere.
    /// </summary>
    public class IgnoranceTransport : MonoBehaviour, ITransport
    {
        // -- GENERAL VARIABLES -- //
        private const string TransportVersion = "2017-1.0.9.6";

        // -- EXPOSED PUBLIC VARIABLES -- //
        /// <summary>
        /// The server's address that it will listen on.
        /// </summary>
        public string ServerHostAddress = "127.0.0.1";
        /// <summary>
        /// The server's communication port. Can be anything between port 1 to 65535.
        /// </summary>
        public ushort CommunicationPort = 7777;

        [Header("Logging Options")]
        public bool verboseLoggingEnabled = false;
        public static bool enableLogging = true;

        // -- TIMEOUTS -- //
        /// <summary>
        /// Use custom peer timeouts?
        /// </summary>
        public bool useCustomPeerTimeout = false;
        /// <summary>
        /// 5000 ticks (5 seconds)
        /// </summary>
        public uint peerBaseTimeout = 5000;
        /// <summary>
        /// peerBaseTimemout * this value = maximum time waiting until client is removed
        /// </summary>
        public uint peerBaseTimeoutMultiplier = 3;

        // -- SERVER WORLD VARIABLES -- //
        // Explicitly give these new references on startup, just to make sure that we get no null reference exceptions.
        private Host server = new Host();
        private Host client = new Host();

        private Address serverAddress = new Address();
        private Peer clientPeer = new Peer();

        /// <summary>
        /// Known connections dictonary since ENET is a little weird.
        /// </summary>
        private Dictionary<int, Peer> knownConnIDToPeers;
        /// <summary>
        /// Known connections dictonary since ENET is a little weird.
        /// </summary>
        private Dictionary<Peer, int> knownPeersToConnIDs;

        /// <summary>
        /// Used by our dictionary to map ENET Peers to connections. Start at 1 just to be safe, connection 0 will be localClient.
        /// </summary>
        private int serverConnectionCount = 1;

        /// <summary>
        /// This section defines what classic UNET channels refer to.
        /// </summary>
        /// 
        public class QosType
        {
            /// <summary>
            /// A packet will not be sequenced with other packets and may be delivered out of order.
            /// </summary>
            public const PacketFlags Unreliable = PacketFlags.Unsequenced;
            /// <summary>
            /// A packet will be unreliably fragmented if it exceeds the MTU. By default packets larger than MTU fragmented reliably.
            /// </summary>
            public const PacketFlags UnreliableFragmented = PacketFlags.UnreliableFragment;
            /// <summary>
            /// Unreliable sequenced, delivery of packet is not guaranteed.
            /// </summary>
            public const PacketFlags UnreliableSequenced = PacketFlags.None;
            /// <summary>
            /// NOT SUPPORTED! Fallbacks to ReliableSequenced! Reliable sequenced, a packet must be received by the target peer and resend attempts should be made until the packet is delivered.
            /// </summary>
            public const PacketFlags Reliable = PacketFlags.Reliable;
            /// <summary>
            /// NOT SUPPORTED! Fallbacks to ReliableSequenced! Reliable sequenced, a packet must be received by the target peer and resend attempts should be made until the packet is delivered.
            /// </summary>
            public const PacketFlags ReliableFragmented = PacketFlags.Reliable;
            /// <summary>
            /// Reliable sequenced, a packet must be received by the target peer and resend attempts should be made until the packet is delivered.
            /// </summary>
            public const PacketFlags ReliableSequenced = PacketFlags.Reliable;
            /// <summary>
            /// NOT SUPPORTED! Fallbacks to Unreliable! A packet will not be sequenced with other packets and may be delivered out of order.
            /// </summary>
            public const PacketFlags StateUpdate = PacketFlags.Unsequenced;
            /// <summary>
            /// NOT SUPPORTED! Fallbacks to ReliableSequenced! Reliable sequenced, a packet must be received by the target peer and resend attempts should be made until the packet is delivered.
            /// </summary>
            public const PacketFlags ReliableStateUpdate = PacketFlags.Reliable;
            /// <summary>
            /// NOT SUPPORTED! Fallbacks to ReliableSequenced! Reliable sequenced, a packet must be received by the target peer and resend attempts should be made until the packet is delivered.
            /// </summary>
            public const PacketFlags AllCostDelivery = PacketFlags.Reliable;
            /// <summary>
            /// NOT SUPPORTED! Fallbacks to UnreliableSequenced! A packet will be unreliably fragmented if it exceeds the MTU. By default packets larger than MTU fragmented reliably.
            /// </summary>
            public const PacketFlags UnreliableFragmentedSequenced = PacketFlags.UnreliableFragment;
            /// <summary>
            /// NOT SUPPORTED! Fallbacks to ReliableSequenced! Reliable sequenced, a packet must be received by the target peer and resend attempts should be made until the packet is delivered.
            /// </summary>
            public const PacketFlags ReliableFragmentedSequenced = PacketFlags.Reliable;
        }

        /// <summary>
        /// Channel type list (channels are mapped to this array)
        /// </summary>
        private readonly PacketFlags[] packetSendMethods =
        {
            PacketFlags.Reliable,  // Channels.DefaultReliable
            PacketFlags.None       // Channels.DefaultUnreliable
        };

        /// <summary>
        /// Outputs a friendly message to the console log. Don't remove unless you hate things saying hello.
        /// You'll make the transport sad. :(
        /// </summary>
        private void GreetEveryone()
        {
            Log(string.Format("Thank you for using Ignorance Transport v{0} for Mirror 2017! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance" +
                "\nENET Library Version: {1}", TransportVersion, Library.version));
        }

        // -- INITIALIZATION -- // 
        public IgnoranceTransport()
        {
            GreetEveryone();
            Library.Initialize();
        }

        /// <summary>
        /// Gets the maximum packet size allowed. Introduced from Mirror upstream git commit: 1289dee8. <para />
        /// Please see https://github.com/nxrighthere/ENet-CSharp/issues/33 for more information.
        /// </summary>
        /// <returns>A integer with the maximum packet size.</returns>
        public int GetMaxPacketSize()
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes. Do not attempt to send more, ENET will likely catch fire. NX's orders.
        }

        public int GetMaxPacketSize(int channel)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes. Do not attempt to send more, ENET will likely catch fire. NX's orders.
        }

        // -- SERVER WORLD FUNCTIONS -- //
        /// <summary>
        /// Gets info about a connection via the connectionId. <para />
        /// Apparently it only gets the IP Address. What's up with that?
        /// </summary>
        /// <param name="connectionId">The connection ID to lookup.</param>
        /// <param name="address">The IP Address. This is what will be returned, don't fill this in!</param>
        /// <returns>The IP Address of the connection. Returns (invalid) if it cannot find it in the dictionary.</returns>
        public bool GetConnectionInfo(int connectionId, out string address)
        {
            address = "(invalid)";

            if (knownConnIDToPeers.ContainsKey(connectionId))
            {
                address = knownConnIDToPeers[connectionId].IP;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Is the server active?
        /// </summary>
        /// <returns>True if the server is active, false otherwise.</returns>
        public bool ServerActive()
        {
            return IsValid(server);
        }

        /// <summary>
        /// Disconnects a server connection.
        /// </summary>
        /// <param name="connectionId">The connection ID to evict.</param>
        /// <returns>True if the connection exists, false otherwise.</returns>
        public bool ServerDisconnect(int connectionId)
        {
            if (knownConnIDToPeers.ContainsKey(connectionId))
            {
                knownConnIDToPeers[connectionId].Disconnect(0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The meat and vegies of the transport on the server side. This is the packet pump.
        /// </summary>
        /// <param name="connectionId">The incoming connection ID.</param>
        /// <param name="transportEvent">What event is this? Connection? Data? Disconnection?</param>
        /// <param name="data">Byte array of the data payload being received.</param>
        /// <returns>True if successful, False if unsuccessful.</returns>
        public bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            // The incoming Enet Event.
            Event incomingEvent;

            // Mirror's transport event and data output.
            transportEvent = TransportEvent.Disconnected;
            data = null;
            connectionId = -1;

            if (!server.IsSet)
            {
                LogWarning("Ignorance Transport: Hold on, the server is not ready yet.");
                return false;
            }

            // Get the next message...
            server.Service(0, out incomingEvent);

            // What type is this?
            switch (incomingEvent.Type)
            {
                // Connections (Normal peer connects)
                case EventType.Connect:
                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: New connection with peer ID {0}, IP {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP));

                    // Peer ID from ENet Wrapper will be a unsigned Int32. Since Mirror uses a signed int, we need to do a hacky work around.
                    // This sucks, but it has to be done for now. Give the new connection a fake connection ID, but also cache the Peer.
                    connectionId = serverConnectionCount;

                    // The peer object will allow us to do stuff with it later.
                    // Map them in our dictionaries
                    knownPeersToConnIDs.Add(incomingEvent.Peer, connectionId);
                    knownConnIDToPeers.Add(connectionId, incomingEvent.Peer);

                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: Mapped peer ID {0} (from IP {1}) => server connID {2}", incomingEvent.Peer.ID, incomingEvent.Peer.IP, connectionId));
                    else Log(string.Format("Ignorance Transport: New connection from IP {0}. Peer ID {1} mapped to internal connection ID {2}", incomingEvent.Peer.IP, incomingEvent.Peer.ID, connectionId));

                    // Increment the fake connection counter by one.
                    serverConnectionCount++;

                    // If we're using custom timeouts, then set the timeouts too.
                    if (useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);

                    // Report back saying we got a connection event.
                    transportEvent = TransportEvent.Connected;
                    break;

                // Disconnections (Normal peer disconnect and timeouts)
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ServerGetNextMessage(): {0} event, peer ID {1}, IP {2}",
                        (incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout"), incomingEvent.Peer.ID, incomingEvent.Peer.IP));

                    Log(string.Format("Ignorance Transport: Acknowledging disconnection on connection ID {0}", knownPeersToConnIDs[incomingEvent.Peer]));

                    if (knownPeersToConnIDs.ContainsKey(incomingEvent.Peer))
                    {
                        PeerDisconnectedInternal(incomingEvent.Peer);
                    }

                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Receive:
                    transportEvent = TransportEvent.Data;

                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ServerGetNextMessage(): Data channel {0} receiving {1} byte payload...", incomingEvent.ChannelID, incomingEvent.Packet.Length));

                    // Only process data from known peers.
                    if (knownPeersToConnIDs.ContainsKey(incomingEvent.Peer))
                    {
                        connectionId = knownPeersToConnIDs[incomingEvent.Peer];

                        // Copy our data into our buffers.
                        data = new byte[incomingEvent.Packet.Length];
                        incomingEvent.Packet.CopyTo(data);
                        incomingEvent.Packet.Dispose();

                        if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ServerGetNextMessage() Payload:\n {0}", BitConverter.ToString(data)));
                    }
                    break;

                case EventType.None:
                    // Nothing happened. Do nothing.
                    return false;
            }

            // We're done here. Bugger off.
            return true;
        }

        private void PeerDisconnectedInternal(Peer peer)
        {
            // Clean up dictionaries.
            if (knownConnIDToPeers.ContainsKey(knownPeersToConnIDs[peer])) knownConnIDToPeers.Remove(knownPeersToConnIDs[peer]);
            if (knownPeersToConnIDs.ContainsKey(peer)) knownPeersToConnIDs.Remove(peer);
        }

        /// <summary>
        /// Send data on the server side.
        /// </summary>
        /// <param name="connectionId">The connection ID to send data to.</param>
        /// <param name="channelId">The channel ID to send data on. Must not be lower or greater than the values in the sendMethods array.</param>
        /// <param name="data">The payload to transmit.</param>
        /// <returns></returns>
        public bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            // Another mailing pigeon
            Packet mailingPigeon = default(Packet);

            if (channelId >= packetSendMethods.Length)
            {
                LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return false;
            }

            // This should fix that bloody AccessViolation
            // Issue reference: https://github.com/nxrighthere/ENet-CSharp/issues/28#issuecomment-436100923
            mailingPigeon.Create(data, packetSendMethods[channelId]);

            // More haxx. see https://github.com/nxrighthere/ENet-CSharp/issues/21 for some background info-ish.
            if (knownConnIDToPeers.ContainsKey(connectionId))
            {
                if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ServerSend() to connID {0} on channel {1}\nPayload: {2}", connectionId, channelId, BitConverter.ToString(data)));

                if (knownConnIDToPeers[connectionId].Send((byte)channelId, ref mailingPigeon))
                {
                    return true;
                }
                else
                {
                    LogWarning("Ignorance Transport: Server-side packet sending apparently wasn't successful.");
                    return false;
                }
            }
            else
            {
                if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ServerSend() to connID {0} on channel {1} failure", connectionId, channelId));
                return false;
            }
        }

        /// <summary>
        /// Start the server with the specified parameters.
        /// </summary>
        /// <param name="address">The address to bind to.</param>
        /// <param name="port">The port to use. Do not run more than one server on the same port.</param>
        /// <param name="maxConnections">How many connections can we have?</param>
        public void ServerStart()
        {
            // Do not attempt to start more than one server.
            // Check if the server is active before attempting to create. If it returns true,
            // then we should not continue, and we'll emit a refusal error message.
            // This should be classified as a dirty hack and if it doesn't work then well, shit.
            if (ServerActive())
            {
                LogError("Ignorance Transport: Refusing to start another server instance! There's already one running.");
                return;
            }

            server = new Host();
            serverAddress = new Address();
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

            if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ServerStart(): {0}, {1}, {2}", ServerHostAddress ?? "(null)",
                CommunicationPort, NetworkManager.singleton.maxConnections));

            if (!string.IsNullOrEmpty(ServerHostAddress))
            {
                Log(string.Format("Ignorance Transport: Binding to address {0}", ServerHostAddress));
                serverAddress.SetHost(ServerHostAddress);
            }

            // Setup the port.
            serverAddress.Port = CommunicationPort;

            // Finally create the server.
            server.Create(serverAddress, NetworkManager.singleton.maxConnections);

            // Log our best effort attempts
            Log(string.Format("Ignorance Transport: Attempted to create server with capacity of {0} connections on UDP port {1}", NetworkManager.singleton.maxConnections, CommunicationPort));
            Log("Ignorance Transport: If you see this, the server most likely was successfully created and started! (This is good.)");
        }

        /// <summary>
        /// Called when the server stops.
        /// </summary>
        public void ServerStop()
        {
            if (verboseLoggingEnabled) Log("Ignorance Transport: ServerStop()");

            foreach (KeyValuePair<int, Peer> entry in knownConnIDToPeers) entry.Value.DisconnectNow(0);

            // Don't forget to dispose stuff.
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

            // Don't forget to dispose stuff.
            if (IsValid(server)) server.Dispose();
            server = null;
        }

        // -- CLIENT WORLD FUNCTIONS -- //
        /// <summary>
        /// Connects the client to a server.
        /// </summary>
        /// <param name="address">The connection address.</param>
        /// <param name="port">The connection port.</param>
        public void ClientConnect(string address)
        {
            Log(string.Format("Ignorance Transport: Acknowledging connection request to {0}:{1}", address, CommunicationPort));

            if (client == null) client = new Host();
            if (!client.IsSet) client.Create(null, 1, packetSendMethods.Length, 0, 0);

            Address clientAddress = new Address();

            // Set hostname and port to connect to.
            clientAddress.SetHost(address);
            clientAddress.Port = CommunicationPort;

            // Connect the client to the server.
            clientPeer = client.Connect(clientAddress);

            // Debugging only
            if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: Is my peer object setup? {0}", clientPeer.IsSet));
        }

        /// <summary>
        /// Is the client connected currently?
        /// </summary>
        /// <returns>True if connected, False if not.</returns>
        public bool ClientConnected()
        {
            if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: Mirror asks if I'm connected. The answer to that is {0}", (clientPeer.State == PeerState.Connected) ? true : false));
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public void ClientDisconnect()
        {
            Log("Ignorance Transport: Acknowledging client disconnection.");

            // TODO: I dunno what to put here! nx has something about Reasons for the disconnection??
            if (clientPeer.IsSet)
            {
                clientPeer.DisconnectNow(0);
            }

            if (client != null)
            {
                client.Flush(); // testing
                client.Dispose();
            }

            client = null;
        }

        /// <summary>
        /// Get the next client data packet.
        /// </summary>
        /// <param name="transportEvent">The transport event to report back to Mirror.</param>
        /// <param name="data">The byte array of the data.</param>
        /// <returns></returns>
        public bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            // The incoming Enet Event.
            Event incomingEvent;

            // Mirror's transport event and data output.
            transportEvent = TransportEvent.Disconnected;
            data = null;

            // Safety check: if the client isn't created, then we shouldn't do anything. ENet might be warming up.
            if (!IsValid(client))
            {
                LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return false;
            }

            // Get the next message...
            client.Service(0, out incomingEvent);

            // Debugging only
            // if (verboseLoggingEnabled) Log($"ClientGetNextMessage event: {incomingEvent.Type}");

            switch (incomingEvent.Type)
            {
                // Peer connects.
                case EventType.Connect:
                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ClientGetNextMessage() connect; real ENET peerID {0}, address {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP));
                    else Log(string.Format("Ignorance Transport: Connection established with {0}", incomingEvent.Peer.IP));

                    // If we're using custom timeouts, then set the timeouts too.
                    if (useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);
                    transportEvent = TransportEvent.Connected;
                    break;

                // Peer disconnects/timeout.
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ClientGetNextMessage() {0}, peerID {1}, address {2}", incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout",
                        incomingEvent.Peer.ID, incomingEvent.Peer.IP));
                    else Log(string.Format("Ignorance Transport: Client encountered {0}", incomingEvent.Type == EventType.Disconnect ? "disconnection" : "timeout"));
                    transportEvent = TransportEvent.Disconnected;
                    break;
                // Peer sends data to us.
                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ClientGetNextMessage(): Data channel {0} receiving {1} byte payload...", incomingEvent.ChannelID, incomingEvent.Packet.Length));
                    data = new byte[incomingEvent.Packet.Length];
                    incomingEvent.Packet.CopyTo(data);
                    incomingEvent.Packet.Dispose();
                    if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ClientGetNextMessage() Payload data:\n{0}", BitConverter.ToString(data)));
                    break;

                case EventType.None:
                    return false;
            }

            // We're done here. Bugger off.
            return true;
        }

        /// <summary>
        /// Send client data to the server.
        /// </summary>
        /// <param name="channelId">The channel ID.</param>
        /// <param name="data">The data array.</param>
        /// <returns></returns>
        public bool ClientSend(int channelId, byte[] data)
        {
            Packet mailingPigeon = default(Packet);

            if (!client.IsSet)
            {
                LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return false;
            }

            if (channelId >= packetSendMethods.Length)
            {
                LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return false;
            }

            mailingPigeon.Create(data, packetSendMethods[channelId]);

            if (verboseLoggingEnabled) Log(string.Format("Ignorance Transport: ClientSend(): channel {0}, data {1}", channelId, BitConverter.ToString(data)));
            if (clientPeer.Send((byte)channelId, ref mailingPigeon))
            {
                return true;
            }
            else
            {
                LogWarning("Ignorance Transport: Packet sending apparently wasn't successful.");
                return false;
            }
        }

        // -- END CLIENT FUNCTIONS -- //

        // -- SHUTDOWN FUNCTIONS -- //
        /// <summary>
        /// Shuts down the transport.
        /// </summary>
        public void Shutdown()
        {
            Log("Ignorance Transport: Going down for shutdown NOW!");

            // Shutdown the client first.
            if (IsValid(client))
            {
                if (verboseLoggingEnabled) Log("Sending the client process to the dumpster fire...");
                if (clientPeer.IsSet) clientPeer.DisconnectNow(0);

                client.Flush();
                client.Dispose();
            }

            // Shutdown the server.
            if (IsValid(server))
            {
                if (verboseLoggingEnabled) Log("Sending the server process to the dumpster fire...");

                server.Flush();
                server.Dispose();
            }

            Library.Deinitialize();
            Log("Ignorance Transport shutdown complete. Have a good one.");
        }

        // -- TIMEOUT SETTINGS -- //
        /// <summary>
        /// Wall of text. Thanks NX for this detailed explaination.
        /// Sets a timeout parameters for the client. The timeout parameters control how and when a peer will timeout from a failure to acknowledge reliable traffic.<para />
        /// 
        /// Timeout values used in the semi-linear mechanism, where if a reliable packet is not acknowledged within an average round trip time plus a variance tolerance
        /// until timeout reaches a set limit.<para />If the timeout is thus at this limit and reliable packets have been sent but not acknowledged within a certain minimum time period,
        /// the peer will be disconnected. Alternatively, if reliable packets have been sent but not acknowledged for a certain maximum time period, the peer will be disconnected
        /// regardless of the current timeout limit value.
        /// </summary>
        /// <param name="timeoutLimit">The limit before a timeout happens, I guess?</param>
        /// <param name="timeoutMinimum">Minimum time period allowed.</param>
        /// <param name="timeoutMaximum">Maximum time period allowed.</param>
        public void ConfigureClientPingTimeout(uint timeoutLimit, uint timeoutMinimum, uint timeoutMaximum)
        {
            if (clientPeer.IsSet)
            {
                clientPeer.Timeout(timeoutLimit, timeoutMinimum, timeoutMaximum);
            }
        }


        // -- TIMEOUT SETTINGS -- //
        /// <summary>
        /// Enables or disables Custom Peer Timeout Settings.
        /// Also configures the minimum and maximum ticks to wait for connections to respond before timing out.
        /// Use ConfigureCustomPeerTimeoutSettings(min, max) to configure after enabling this.
        /// </summary>
        /// <param name="enable">Self-explainatory.</param>
        /// <param name="minTicks">The minimum almost of time you want to wait before saying it's timed out.</param>
        /// <param name="multiplier">The multiplier to use to calculate the maximum time to wait.</param>
        public void UseCustomPeerTimeoutSettings(bool enable, uint minTicks, uint multiplier)
        {
            useCustomPeerTimeout = enable;
            peerBaseTimeout = minTicks;
            peerBaseTimeoutMultiplier = multiplier;
        }

        /// <summary>
        /// Allows you to enable server-side compression via built-in ENET LZ4 methods. Please note that 
        /// you should only enable this before a server is started. NEVER TURN IT ON DURING
        /// A SERVER IS ACTIVE OR COMMUNICATION MAY BREAK!<para />
        /// 
        /// Note that you also need to ensure clients are using compression or funky things might happen.
        /// 
        /// Once enabled, you will need to restart the server to disable it.
        /// </summary>
        public void EnableCompressionOnServer()
        {
            if (IsValid(server)) server.EnableCompression();
        }

        /// <summary>
        /// Allows you to enable client-side compression via built-in ENET LZ4 methods. Please note that 
        /// you should only enable this before a client is started. NEVER TURN IT ON DURING
        /// A CLIENT IS ACTIVE OR COMMUNICATION MAY BREAK!<para />
        /// 
        /// Note that you also need to ensure the server is using compression or funky things might happen.
        /// 
        /// Once enabled, you will need to restart the client to disable it.
        /// </summary>
        public void EnableCompressionOnClient()
        {
            if (IsValid(client)) client.EnableCompression();
        }

        /// <summary>
        /// Server-world Packets Sent Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets sent.</returns>
        public uint ServerGetPacketSentCount()
        {
            return IsValid(server) ? server.PacketsSent : 0;
        }

        /// <summary>
        /// Server-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ServerGetPacketReceivedCount()
        {
            return IsValid(server) ? server.PacketsReceived : 0;
        }

        /// <summary>
        /// Server-world packets loss counter.
        /// This is buggy. Please use with caution.
        /// </summary>
        /// <returns>The amount of packets lost.</returns>
        public uint ServerGetPacketLossCount()
        {
            return IsValid(server) && server.PacketsSent >= server.PacketsReceived ? server.PacketsSent - server.PacketsReceived : 0;
        }

        /// <summary>
        /// Client-world Packets Sent Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets sent.</returns>
        public uint ClientGetPacketSentCount()
        {
            return IsValid(client) ? client.PacketsSent : 0;
        }

        /// <summary>
        /// Client-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ClientGetPacketReceivedCount()
        {
            return IsValid(client) ? client.PacketsReceived : 0;
        }

        /// <summary>
        /// Get the client's packet loss count, directly from ENET.
        /// </summary>
        /// <returns></returns>
        public uint ClientGetPacketLossCount()
        {
            return clientPeer.IsSet ? clientPeer.PacketsLost : 0;
        }

        // -- PARANOID MODE FUNCTIONS -- //
        /// <summary>
        /// Enables or disables Super Paranoid Logging Mode.
        /// WARNING: Unity Editor Logs will be very laggy when lots of activity is going on!
        /// </summary>
        /// <param name="enable">Self-explainatory.</param>
        public void EnableParanoidLogging(bool enable)
        {
            verboseLoggingEnabled = enable;
        }

        // Static helpers
        private static void Log(object text)
        {
            if (enableLogging) Debug.Log(text);
        }

        private static void LogError(string text)
        {
            Debug.LogError(text);
        }

        private static void LogWarning(string text)
        {
            Debug.LogWarning(text);
        }

        private static bool IsValid(Host host)
        {
            return host != null && host.IsSet;
        }
    }
}
