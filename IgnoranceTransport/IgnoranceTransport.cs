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
    public class IgnoranceTransport : TransportLayer
    {
        // -- EXPOSED PUBLIC VARIABLES -- //
        public bool verboseLoggingEnabled = false;

        // -- TIMEOUTS -- //
        public bool useCustomPeerTimeout = false;   // Use custom peer timeouts?
        public uint peerBaseTimeout = 5000;        // 5000 ticks (5 seconds)
        public uint peerBaseTimeoutMultiplier = 3; // peerBaseTimemout * this value = maximum time waiting until client is removed

        // -- GENERAL VARIABLES -- //
        private const string TransportVersion = "v1.0.9.1";
        private bool libraryInitialized = false;
        // -- SERVER WORLD VARIABLES -- //
        private Host server;
        private Address serverAddress;

        private Dictionary<int, Peer> knownPeerConnections;   // Known connections dictonary since ENET is a little weird.
        private int serverConnectionCount = 1;                // Used by our dictionary to map ENET Peers to connections. Start at 1 just to be safe.

        // -- CLIENT WORLD VARIABLES -- //
        private Host client;
        private Peer clientPeer;

        // This section defines what classic UNET channels refer to.
        private PacketFlags[] packetSendMethods =
        {
            PacketFlags.Reliable,  // Channels.DefaultReliable
            PacketFlags.None       // Channels.DefaultUnreliable
        };

        // -- INITIALIZATION -- // 
        public IgnoranceTransport()
        {
            Debug.LogFormat("Ignorance Transport {0} ready! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance.", TransportVersion);
        }

        /// <summary>
        /// Gets the maximum packet size allowed. Introduced from Mirror upstream git commit: 1289dee8. <para />
        /// Please see https://github.com/nxrighthere/ENet-CSharp/issues/33 for more information.
        /// </summary>
        /// <returns>A integer with the maximum packet size.</returns>
        public int GetMaxPacketSize(int channelId)
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

            if (knownPeerConnections.ContainsKey(connectionId))
            {
                address = knownPeerConnections[connectionId].IP;
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
            if (!libraryInitialized) return false;

            if (server != null) return server.IsSet;
            else return false;
        }

        /// <summary>
        /// Disconnects a server connection.
        /// </summary>
        /// <param name="connectionId">The connection ID to evict.</param>
        /// <returns>True if the connection exists, false otherwise.</returns>
        public bool ServerDisconnect(int connectionId)
        {
            if (knownPeerConnections.ContainsKey(connectionId))
            {
                knownPeerConnections[connectionId].Disconnect(0);
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
                Debug.LogWarning("Ignorance Transport: Hold on, the server is not ready yet.");
                return false;
            }

            // Get the next message...
            server.Service(0, out incomingEvent);

            // What type is this?
            switch (incomingEvent.Type)
            {
                // Connections (Normal peer connects)
                case EventType.Connect:
                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: New connection with peer ID {0}, IP {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);

                    // Peer ID from ENet Wrapper will be a unsigned Int32. Since Mirror uses a signed int, we need to do a hacky work around.
                    // This sucks, but it has to be done for now. Give the new connection a fake connection ID, but also cache the Peer.
                    connectionId = serverConnectionCount;

                    // The peer object will allow us to do stuff with it later.
                    knownPeerConnections.Add(connectionId, incomingEvent.Peer);
                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: Mapped peer ID {1} (from IP {2}) => fake connID {0}", serverConnectionCount, incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    else Debug.LogFormat("Ignorance Transport: New connection from IP {0}. Peer ID {1} mapped to internal connection ID {2}", incomingEvent.Peer.IP, incomingEvent.Peer.ID, serverConnectionCount);
                    
                    // Increment the fake connection counter by one.
                    serverConnectionCount += 1;

                    // If we're using custom timeouts, then set the timeouts too.
                    if(useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);

                    // Report back saying we got a connection event.
                    transportEvent = TransportEvent.Connected;
                    break;

                // Disconnections (Normal peer disconnect and timeouts)
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ServerGetNextMessage(): {0} event, peer ID {1}, IP {2}", (incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout"), incomingEvent.Peer.ID, incomingEvent.Peer.IP);                    

                    // Peer ID from ENet Wrapper will be a unsigned Int32. Since Mirror uses a signed int, we need to do a hacky work around.
                    // Since our dictionary stores fake connection IDs, we need to go through and find the real Peer ID. This could be improved.
                    foreach (KeyValuePair<int, Peer> entry in knownPeerConnections)
                    {
                        if (entry.Value.ID == incomingEvent.Peer.ID)
                        {
                            connectionId = entry.Key;
 
                            if(verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: Evicting peer ID {0} (fake connID {1})", incomingEvent.Peer.ID, entry.Key);
                            else Debug.LogFormat("Ignorance Transport: Connection ID {0} {1}", entry.Key, (incomingEvent.Type == EventType.Disconnect ? "disconnected" : "timed out"));
                            knownPeerConnections.Remove(entry.Key);
                            // No need to keep going through the list. Halt.
                            break;
                        }
                    }

                    // Report back.
                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    if (verboseLoggingEnabled) Debug.LogWarningFormat("Ignorance Transport: ServerGetNextMessage(): Data channel {0} receiving {1} byte payload...", incomingEvent.ChannelID, incomingEvent.Packet.Length);

                    foreach (KeyValuePair<int, Peer> entry in knownPeerConnections)
                    {
                        if (entry.Value.ID == incomingEvent.Peer.ID)
                        {
                            // Debug.LogFormat("PeerID {0} maps to connection ID {1}", incomingEvent.Peer.ID, entry.Key);
                            connectionId = entry.Key;

                            // No need to keep going through the list. Halt.
                            break;
                        }
                    }

                    // Copy our data into our buffers.
                    data = new byte[incomingEvent.Packet.Length];
                    incomingEvent.Packet.CopyTo(data);
                    incomingEvent.Packet.Dispose();

                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ServerGetNextMessage() Payload:\n {0}", BitConverter.ToString(data));

                    break;
                case EventType.None:
                    // Nothing happened. Do nothing.
                    return false;
            }

            // We're done here. Bugger off.
            return true;
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
            bool wasTransmissionSuccessful;

            if (channelId >= packetSendMethods.Length)
            {
                Debug.LogError("Trying to use an unknown channel to send data");
                return false;
            }

            // This should fix that bloody AccessViolation
            // Issue reference: https://github.com/nxrighthere/ENet-CSharp/issues/28#issuecomment-436100923
            mailingPigeon.Create(data, packetSendMethods[channelId]);

            // More haxx. see https://github.com/nxrighthere/ENet-CSharp/issues/21 for some background info-ish.
            if (knownPeerConnections.ContainsKey(connectionId))
            {
                Peer target = knownPeerConnections[connectionId];
                if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ServerSend() to fakeConnID {0} on channel {1}\nPayload: {2}", connectionId, channelId, BitConverter.ToString(data));

                wasTransmissionSuccessful = target.Send(0, ref mailingPigeon);
                if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ServerSend() successful? {0}", wasTransmissionSuccessful);
                return wasTransmissionSuccessful;
            }
            else
            {
                if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ServerSend() to fakeConnID {0} on channel {1} failure", connectionId, channelId);
                return false;
            }
        }

        /// <summary>
        /// Start the server with the specified parameters.
        /// </summary>
        /// <param name="address">The address to bind to.</param>
        /// <param name="port">The port to use. Do not run more than one server on the same port.</param>
        /// <param name="maxConnections">How many connections can we have?</param>
        public void ServerStart(string address, int port, int maxConnections)
        {
            // Fire up ENET-C#'s backend.
            if (!libraryInitialized)
            {
                Library.Initialize();
                libraryInitialized = true;
            }

            server = new Host();
            serverAddress = new Address();
            knownPeerConnections = new Dictionary<int, Peer>();

            if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ServerStart(): {0}, {1}, {2}", address ?? "(null)", port, maxConnections);
            // Version 1.0.4: undo what 1.0.2/1.0.3 did regarding this.
            if (!string.IsNullOrEmpty(address))
            {
                Debug.LogFormat("Ignorance Transport: Binding to address {0}", address);
                serverAddress.SetHost(address);
            }

            // Setup the port.
            serverAddress.Port = (ushort)port;

            // Finally create the server.
            server.Create(serverAddress, maxConnections);
            
            // Log our best effort attempts
            Debug.LogFormat("Ignorance Transport: Attempted to create server with capacity of {0} connections on UDP port {1}", maxConnections, Convert.ToUInt16(port));
            Debug.LogFormat("Ignorance Transport: If you see this, the server most likely was successfully created and started! (This is good.)");
        }

        /// <summary>
        /// Start the websockets version of the server.
        /// NOT IMPLEMENTED AND PROBABLY NEVER WILL BE. DO NOT USE!
        /// </summary>
        /// <param name="address">The address to bind to.</param>
        /// <param name="port">The port to use. Do not run more than one server on the same port.</param>
        /// <param name="maxConnections">How many connections can we have?</param>
        public void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            // Websockets? Nani?
            throw new NotImplementedException("WebSockets with ENET are not possible and probably will never be implemented. Sorry to disappoint");
        }

        /// <summary>
        /// Called when the server stops.
        /// </summary>
        public void ServerStop()
        {
            if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ServerStop()");

            foreach (KeyValuePair<int, Peer> entry in knownPeerConnections)
            {
                entry.Value.DisconnectNow(0);
            }

            // Don't forget to dispose stuff.
            if (server != null)
            {
                server.Flush(); // testing
                server.Dispose();
            }
            server = null;
        }

        // -- CLIENT WORLD FUNCTIONS -- //
        /// <summary>
        /// Connects the client to a server.
        /// </summary>
        /// <param name="address">The connection address.</param>
        /// <param name="port">The connection port.</param>
        public void ClientConnect(string address, int port)
        {
            // Fire up ENET-C#'s backend.
            if (!libraryInitialized)
            {
                Library.Initialize();
                libraryInitialized = true;
            }

            if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ClientConnect({0}, {1})", address, port);
            client = new Host();          
            Address clientAddress = new Address();

            // Set hostname and port to connect to.
            clientAddress.SetHost(address);
            clientAddress.Port = (ushort)port; // Convert.ToUInt16(port);

            // Create the client.
            client.Create();

            // Connect the client to the server.
            clientPeer = client.Connect(clientAddress);

            // Debugging only
            if (verboseLoggingEnabled) Debug.LogWarning("Ignorance Transport: clientPeer isSet? " + clientPeer.IsSet);
        }

        /// <summary>
        /// Is the client connected currently?
        /// </summary>
        /// <returns>True if connected, False if not.</returns>
        public bool ClientConnected()
        {
            if (verboseLoggingEnabled) Debug.Log("Ignorance Transport: ClientConnected() called");
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public void ClientDisconnect()
        {
            if (verboseLoggingEnabled) Debug.Log("Ignorance Transport: ClientDisconnect()");

            // TODO: I dunno what to put here! nx has something about Reasons for the disconnection??
            if (clientPeer.IsSet)
            {
                clientPeer.DisconnectNow(0);
            }

            if(client != null)
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
            if (!client.IsSet)
            {
                Debug.LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return false;
            }

            // Get the next message...
            client.Service(0, out incomingEvent);

            // Debugging only
            if (verboseLoggingEnabled) Debug.Log("ClientGetNextMessage event: " + incomingEvent.Type);

            switch (incomingEvent.Type)
            {
                // Peer connects.
                case EventType.Connect:
                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage() connect; real ENET peerID {0}, address {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    else Debug.LogFormat("Ignorance Transport: Connection established with {0}", incomingEvent.Peer.IP);

                    // If we're using custom timeouts, then set the timeouts too.
                    if (useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);
                    transportEvent = TransportEvent.Connected;
                    break;

                // Peer disconnects/timeout.
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage() {0}; peerID {1}, address {2}", (incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout"), incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    else Debug.LogFormat("Ignorance Transport: Client encountered {0}", (incomingEvent.Type == EventType.Disconnect ? "disconnection" : "timeout"));
                    transportEvent = TransportEvent.Disconnected;
                    break;
                // Peer sends data to us.
                case EventType.Receive:
                    transportEvent = TransportEvent.Data;

                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage(): Data channel {0} receiving {1} byte payload...", incomingEvent.ChannelID, incomingEvent.Packet.Length);

                    data = new byte[incomingEvent.Packet.Length];
                    incomingEvent.Packet.CopyTo(data);
                    incomingEvent.Packet.Dispose();
                    if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage() Payload data:\n{0}", BitConverter.ToString(data));

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
            bool sendingPacketWasSuccessful;

            if (channelId >= packetSendMethods.Length)
            {
                Debug.LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return false;
            }

            if (!client.IsSet)
            {
                Debug.LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return false;
            }

            // Mailing Pigeons. Gotta love the birds.
            // Very useful in the wartime, as long as the enemy team didn't
            // shoot them down. At least you got a free dinner. Who doesn't
            // want a delicious pigeon pie? (I play too much Battlefield 1)
            mailingPigeon.Create(data, packetSendMethods[channelId]);

            if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ClientSend(): channelId {0}, data {1}", channelId, BitConverter.ToString(data));

            sendingPacketWasSuccessful = clientPeer.Send(0, ref mailingPigeon);
            if (verboseLoggingEnabled) Debug.LogFormat("Ignorance Transport: ClientSend successful? {0}", sendingPacketWasSuccessful);

            return sendingPacketWasSuccessful;
        }

        // -- END CLIENT FUNCTIONS -- //

        // -- SHUTDOWN FUNCTIONS -- //
        /// <summary>
        /// Shuts down the transport.
        /// </summary>
        public void Shutdown()
        {
            Debug.Log("Ignorance Transport: Going down for shutdown NOW!");

            // Shutdown the client first.
            if (client != null && client.IsSet)
            {
                if (verboseLoggingEnabled) Debug.Log("Sending the client process to the dumpster fire...");

                if (clientPeer.IsSet) clientPeer.DisconnectNow(0);

                client.Flush();
                client.Dispose();
            }

            // Shutdown the server.
            if (server != null && server.IsSet)
            {
                if (verboseLoggingEnabled) Debug.Log("Sending the server process to the dumpster fire...");

                server.Flush();
                server.Dispose();
            }

            Library.Deinitialize();
            libraryInitialized = false;
            Debug.Log("Ignorance Transport shutdown complete. Have a good one.");
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
            if (server != null && server.IsSet)
            {
                server.EnableCompression();
            }
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
            if(client != null && client.IsSet)
            {
                client.EnableCompression();
            }
        }

        // -- EXTRAS -- //
        /// <summary>
        /// Server-world Packets Sent Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets sent.</returns>
        public uint ServerGetPacketSentCount()
        {
            if (server != null && server.IsSet)
            {
                return server.PacketsSent;
            }

            return 0;
        }

        /// <summary>
        /// Server-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ServerGetPacketReceivedCount()
        {
            if (server != null && server.IsSet)
            {
                return server.PacketsReceived;
            }

            return 0;
        }

        /// <summary>
        /// Server-world packets loss counter.
        /// This is buggy. Please use with caution.
        /// </summary>
        /// <returns>The amount of packets lost.</returns>
        public uint ServerGetPacketLossCount()
        {
            if (server != null && server.IsSet)
            {
                // Safe guard against underflows.
                if ((server.PacketsSent - server.PacketsReceived) < 0) return 0;
                else return server.PacketsSent - server.PacketsReceived;
            }
            return 0;
        }

        /// <summary>
        /// Client-world Packets Sent Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets sent.</returns>
        public uint ClientGetPacketSentCount()
        {
            if (client != null && client.IsSet)
            {
                return client.PacketsSent;
            }

            return 0;
        }

        /// <summary>
        /// Client-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ClientGetPacketReceivedCount()
        {
            if (client != null && client.IsSet)
            {
                return client.PacketsReceived;
            }

            return 0;
        }

        /// <summary>
        /// Get the client's packet loss count, directly from ENET.
        /// </summary>
        /// <returns></returns>
        public uint ClientGetPacketLossCount()
        {
            if (clientPeer.IsSet)
            {
                // Safe guard against underflows.
                return clientPeer.PacketsLost;
            }

            return 0;
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
    }
}
