// SoftwareGuy's Ignorance Reliable UDP Transport
// Uses ENET as the transport backend.
// ----------------------------------------
// Ignorance Transport by Coburn, 2018
// ENet-C# by nxrighthere, 2018
// ENet by the ENet developers, whenever - whenever.
// ----------------------------------------
// THIS IS EXPERIMENTAL PROGRAM CODE
// ----------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using ENet;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Mirror
{
    /// <summary>
    /// Ignorance rUDP Transport is built upon the ENet-C# wrapper by nxrighthere.
    /// </summary>
    public class IgnoranceTransport : TransportLayer
    {
        // -- GENERAL VARIABLES
        private const string TransportVersion = "v1.0.5";

        // -- SERVER WORLD VARIABLES -- //
        private Host server;
        private Address serverAddress;

        private Dictionary<int, Peer> knownPeersServerDictionary;
        private int serverFakeConnectionCounter = 1;  // Used by our dictionary to map ENET Peers to connections.

        // -- CLIENT WORLD VARIABLES -- //
        Host client;
        Peer clientPeer;

        // -- v1.0.5: Disabled to prevent spammage. -- //
        private bool superParanoidMode = false;

        ENet.PacketFlags[] sendMethods =
        {
            ENet.PacketFlags.Reliable,  //Channels.DefaultReliable
            ENet.PacketFlags.None       //Channels.DefaultUnreliable
        };

        // -- INITIALIZATION -- // 
        public IgnoranceTransport()
        {
            Library.Initialize();

            Debug.LogFormat("This is the Ignorance Transport {0} reporting in for duty.", TransportVersion);
            Debug.Log("Remember to keep up to date with the latest releases at: https://github.com/SoftwareGuy/Ignorance/releases and report bugs there too!");
            Debug.Log("Please note that this uses a lot of magic and may cause your game to spontanously combust into a violent dumpster fire.");
        }

        // -- SERVER WORLD FUNCTIONS -- //
        public bool GetConnectionInfo(int connectionId, out string address)
        {
            address = "(invalid)";

            if (knownPeersServerDictionary.ContainsKey(connectionId))
            {
                address = knownPeersServerDictionary[connectionId].IP;
                return true;
            }

            return false;
        }

        public bool ServerActive()
        {
            if (server != null) return server.IsSet;
            else return false;
        }

        public bool ServerDisconnect(int connectionId)
        {
            // TODO: Probably revise this?
            if (knownPeersServerDictionary.ContainsKey(connectionId))
            {
                knownPeersServerDictionary[connectionId].Disconnect(0);
                return true;
            }

            return false;
        }

        public bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            // Setup some basic things
            // version 1.0.1 optimization: uncomment this for v1.0.0 behaviour
            // byte[] newDataPacketContents;

            // The incoming Enet Event.
            Event incomingEvent;

            // Mirror's transport event and data output.
            transportEvent = TransportEvent.Disconnected;
            data = null;

            connectionId = -1;

            if (!server.IsSet)
            {
                Debug.LogWarning("Server is not ready yet.");
                return false;
            }

            // Get the next message...
            server.Service(0, out incomingEvent);

            // What type is this?
            switch (incomingEvent.Type)
            {
                // Connections (Normal peer connects)
                case EventType.Connect:
                    if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerGetNextMessage(): New connection with peer ID {0}, IP {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);

                    // Peer ID from ENet Wrapper will be a unsigned Int32. Since Mirror uses a signed int, we need to do a hacky work around.
                    // This sucks, but honestly if done right it should work as intended. Here's hoping my magic doesn't shaft me at my own game.

                    // Give the new connection a fake connection ID, but also cache the Peer.
                    connectionId = serverFakeConnectionCounter;

                    // The peer object will allow us to do stuff with it later.
                    knownPeersServerDictionary.Add(connectionId, incomingEvent.Peer);
                    Debug.LogFormat("Ignorance Transport: ServerGetNextMessage(): setup fake ConnectionID. Mapped new fake connID {0} to peer ID {1} from IP {2}", serverFakeConnectionCounter, incomingEvent.Peer.ID, incomingEvent.Peer.IP);

                    // Increment the fake connection counter by one.
                    serverFakeConnectionCounter += 1;

                    // Report back saying we got a connection event.
                    transportEvent = TransportEvent.Connected;
                    break;

                // Disconnections (Normal peer disconnect and timeouts)
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerGetNextMessage(): {0} event, peer ID {1}, IP {2}", (incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout"), incomingEvent.Peer.ID, incomingEvent.Peer.IP);                    

                    // Peer ID from ENet Wrapper will be a unsigned Int32. Since Mirror uses a signed int, we need to do a hacky work around.
                    // Since our dictionary stores fake connection IDs, we need to go through and find the real Peer ID. This could be improved.
                    foreach (KeyValuePair<int, Peer> entry in knownPeersServerDictionary)
                    {
                        if (entry.Value.ID == incomingEvent.Peer.ID)
                        {
                            connectionId = entry.Key;

                            Debug.LogFormat("Evicting peer ID {0} (fake connID {1})", incomingEvent.Peer.ID, entry.Key);
                            knownPeersServerDictionary.Remove(entry.Key);
                            // No need to keep going through the list. Halt.
                            break;
                        }
                    }

                    // Report back.
                    transportEvent = TransportEvent.Disconnected;
                    break;

                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    if (superParanoidMode) Debug.LogWarningFormat("Ignorance Transport: ServerGetNextMessage(): Data! channel {0}, data length: {1}", incomingEvent.ChannelID, incomingEvent.Packet.Length);

                    foreach (KeyValuePair<int, Peer> entry in knownPeersServerDictionary)
                    {
                        if (entry.Value.ID == incomingEvent.Peer.ID)
                        {
                            // Debug.LogFormat("PeerID {0} maps to connection ID {1}", incomingEvent.Peer.ID, entry.Key);
                            connectionId = entry.Key;

                            // No need to keep going through the list. Halt.
                            break;
                        }
                    }

                    // Try to be safe. We could do better.
                    // version 1.0.1 optimization: uncomment this for v1.0.0 behaviour, and comment out the one below it.
                    // newDataPacketContents = new byte[incomingEvent.Packet.Length];
                    data = new byte[incomingEvent.Packet.Length];
                    incomingEvent.Packet.CopyTo(data);
                    incomingEvent.Packet.Dispose();

                    // version 1.0.1 optimization: uncomment this for v1.0.0 behaviour, and comment out the one below it.
                    // if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ServerGetNextMessage(): data: {0}", BitConverter.ToString(newDataPacketContents));
                    if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerGetNextMessage(): Incoming data: {0}", BitConverter.ToString(data));
                    // version 1.0.1 optimization: uncomment this for v1.0.0 behaviour.
                    // data = newDataPacketContents;
                    break;
                case EventType.None:
                    // Nothing happened. Do nothing.
                    return false;
            }

            // We're done here. Bugger off.
            return true;
        }

        public bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            if (channelId >= sendMethods.Length)
            {
                Debug.LogError("Trying to use an unknown channel to send data");
                return false;
            }

            // Another mailing pigeon
            Packet mailingPigeon = default(Packet);
            // This should fix that bloody AccessViolation
            // Issue reference: https://github.com/nxrighthere/ENet-CSharp/issues/28#issuecomment-436100923
            mailingPigeon.Create(data, sendMethods[channelId]);
            // see https://github.com/nxrighthere/ENet-CSharp/issues/21
            if (knownPeersServerDictionary.ContainsKey(connectionId))
            {
                Peer target = knownPeersServerDictionary[connectionId];
                if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerSend(): fakeConnID {0} channelId {1} data {2}", connectionId, channelId, BitConverter.ToString(data));

                bool wasSuccessful = target.Send(0, ref mailingPigeon);
                if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerSend was successful? {0}", wasSuccessful);
                return wasSuccessful;
            }
            else
            {
                if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerSend failure fakeConnID {0} channelId {1} data {2}", connectionId, channelId, BitConverter.ToString(data));
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
            server = new Host();
            serverAddress = new Address();
            knownPeersServerDictionary = new Dictionary<int, Peer>();

            if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerStart(): {0}, {1}, {2}", address ?? "(null)", port, maxConnections);
            // Version 1.0.4: undo what 1.0.2/1.0.3 did regarding this.
            if (!string.IsNullOrEmpty(address))
            {
                Debug.LogFormat("Ignorance Transport: Will bind to address {0}", address);
                serverAddress.SetHost(address);
            }

            // Setup the port.
            serverAddress.Port = Convert.ToUInt16(port);

            // Finally create the server.
            server.Create(serverAddress, maxConnections);
            Debug.LogFormat("Ignorance Transport: Attempted to create server with capacity of {0} connections on UDP port {1}", maxConnections, Convert.ToUInt16(port));
            Debug.LogFormat("Ignorance Transport: If you see this message the server most likely was successfully created and started! (This is good.)");
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called when the server stops.
        /// </summary>
        public void ServerStop()
        {
            if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ServerStop()");

            foreach (KeyValuePair<int, Peer> entry in knownPeersServerDictionary)
            {
                entry.Value.DisconnectNow(0);
            }

            // Don't forget to dispose stuff.
            if (server != null) server.Dispose();
        }

        // -- CLIENT WORLD FUNCTIONS -- //
        /// <summary>
        /// Connects the client to a server.
        /// </summary>
        /// <param name="address">The connection address.</param>
        /// <param name="port">The connection port.</param>
        public void ClientConnect(string address, int port)
        {
            client = new Host();

            if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ClientConnect({0}, {1})", address, port);

            Address clientAddress = new Address();

            // Set hostname and port to connect to.
            clientAddress.SetHost(address);
            clientAddress.Port = Convert.ToUInt16(port);

            // Create the client.
            client.Create();

            // Connect the client to the server.
            clientPeer = client.Connect(clientAddress);

            // Debugging only
            if (superParanoidMode) Debug.LogWarning("Ignorance Transport: clientPeer isSet? " + clientPeer.IsSet);
        }

        /// <summary>
        /// Is the client connected currently?
        /// </summary>
        /// <returns>True if connected, False if not.</returns>
        public bool ClientConnected()
        {
            if (superParanoidMode) Debug.Log("Ignorance Transport: ClientConnected() called");
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public void ClientDisconnect()
        {
            if (superParanoidMode) Debug.Log("Ignorance Transport: ClientDisconnect()");

            // TODO: I dunno what to put here! nx has something about Reasons for the disconnection??
            if (clientPeer.IsSet)
            {
                clientPeer.DisconnectNow(0);
            }
            client.Dispose();
        }

        /// <summary>
        /// Get the next client data packet.
        /// </summary>
        /// <param name="transportEvent">The transport event to report back to Mirror.</param>
        /// <param name="data">The byte array of the data.</param>
        /// <returns></returns>
        public bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            // Setup some basic things
            // v1.0.1: Some minor optimization. For v1.0.0 behaviour, uncomment this.
            // byte[] newDataPacketContents;

            // The incoming Enet Event.
            Event incomingEvent;

            // Mirror's transport event and data output.
            transportEvent = TransportEvent.Disconnected;
            data = null;

            // Safety check: if the client isn't created, then we shouldn't do anything. ENet might be warming up.
            if (!client.IsSet)
            {
                Debug.LogWarning("Client is not ready yet.");
                return false;
            }

            // Get the next message...
            client.Service(0, out incomingEvent);

            // Debugging only
            if (superParanoidMode) Debug.Log("ClientGetNextMessage event: " + incomingEvent.Type);

            switch (incomingEvent.Type)
            {
                // Peer connects.
                case EventType.Connect:
                    if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage() connect; real ENET peerID {0}, address {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    transportEvent = TransportEvent.Connected;
                    break;

                // Peer disconnects/timeout.
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage() {0}; peerID {1}, address {2}", (incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout"), incomingEvent.Peer.ID, incomingEvent.Peer.IP);
                    transportEvent = TransportEvent.Disconnected;
                    break;
                // Peer sends data to us.
                case EventType.Receive:
                    transportEvent = TransportEvent.Data;
                    if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage() data; channel {0}, length: {1}", incomingEvent.ChannelID, incomingEvent.Packet.Length);

                    // v1.0.1: Some minor optimization. For v1.0.0 behaviour, uncomment the next line and comment out the line below the newly uncommented line.
                    // newDataPacketContents = new byte[incomingEvent.Packet.Length];
                    data = new byte[incomingEvent.Packet.Length];
                    incomingEvent.Packet.CopyTo(data);
                    incomingEvent.Packet.Dispose();

                    // v1.0.1: Some minor optimization. For v1.0.0 behaviour, uncomment the next lines and comment out the line under the newly uncommented lines.
                    // if (superParanoidMode) Debug.LogFormat("Ignorance rUDP Transport ClientGetNextMessage() Incoming data: {0}", BitConverter.ToString(newDataPacketContents));
                    // data = newDataPacketContents;
                    if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ClientGetNextMessage: Incoming data contents: {0}", BitConverter.ToString(data));
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
            if (channelId >= sendMethods.Length)
            {
                Debug.LogError("Trying to use an unknown channel to send data");
                return false;
            }

            if (!client.IsSet)
            {
                Debug.LogWarning("Client is not ready yet.");
                return false;
            }

            // Mailing Pigeons. Gotta love the birds.
            // Very useful in the wartime, as long as the enemy team didn't
            // shoot them down. At least you got a free dinner. Who doesn't
            // want a delicious pigeon pie?
            Packet mailingPigeon = default(Packet);
            mailingPigeon.Create(data, sendMethods[channelId]);

            if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ClientSend(): channelId {0}, data {1}", channelId, BitConverter.ToString(data));

            bool wasSuccessful = clientPeer.Send(0, ref mailingPigeon);
            if (superParanoidMode) Debug.LogFormat("Ignorance Transport: ClientSend successful? {0}", wasSuccessful);

            return wasSuccessful;
        }

        // -- END CLIENT FUNCTIONS -- //

        // -- SHUTDOWN FUNCTIONS -- //
        /// <summary>
        /// Shuts down the transport.
        /// </summary>
        public void Shutdown()
        {
            Debug.Log("The Ignorance Transport is going down for shutdown NOW!");

            if (client != null && client.IsSet)
            {
                if (superParanoidMode) Debug.Log("Sending the client process to the dumpster fire...");

                client.Flush();
                client.Dispose();
            }

            if (server != null && server.IsSet)
            {
                if (superParanoidMode) Debug.Log("Sending the server process to the dumpster fire...");

                server.Flush();
                server.Dispose();
            }

            Library.Deinitialize();

            Debug.Log("Ignorance Transport shutdown complete.");
        }

        // -- VERSION 1.0.1 EXPOSED FUNCTIONS -- //
        /// <summary>
        /// Wall of text. Thanks NX for this detailed explaination.
        /// Sets a timeout parameters for the client. The timeout parameters control how and when a peer will timeout from a failure to acknowledge reliable traffic.
        /// 
        /// Timeout values used in the semi-linear mechanism, where if a reliable packet is not acknowledged within an average round trip time plus a variance tolerance
        /// until timeout reaches a set limit. If the timeout is thus at this limit and reliable packets have been sent but not acknowledged within a certain minimum time period,
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

        /// <summary>
        /// Allows you to enable server-side compression via built-in ENET LZ4 methods. Please note that 
        /// you should only enable this before a server is started. NEVER TURN IT ON DURING
        /// A SERVER IS ACTIVE OR COMMUNICATION MAY BREAK!
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
        /// A CLIENT IS ACTIVE OR COMMUNICATION MAY BREAK!
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
        public void EnableParanoidLogging(bool enable)
        {
            superParanoidMode = enable;
        }
    }
}