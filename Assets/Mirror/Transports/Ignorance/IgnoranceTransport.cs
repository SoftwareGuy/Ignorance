// ----------------------------------------
// Ignorance Transport by Matt Coburn, 2018.
// ENet-C# by nxrighthere, 2018
// ENet by the ENet developers, whenever - whenever.
// ----------------------------------------
// Ignorance Transport is MIT Licensed. It would be however
// nice to get some acknowledgement in your program/game's credits
// that Ignorance was used to build your network code. It would be 
// greatly appreciated if you reported bugs and donated coffee
// at https://github.com/SoftwareGuy/Ignorance. Remember, OSS is the
// way of the future!
// ----------------------------------------
// This version of Ignorance is compatible with both the master
// and 2018 versions of Mirror Networking.
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
    [HelpURL("https://github.com/SoftwareGuy/Ignorance/blob/master/README.md")]
    public class IgnoranceTransport : Transport
    {
        [Header("Verbosity Options")]
        [Tooltip("How do you like your debug logs?")]
        public TransportVerbosity m_TransportVerbosity = TransportVerbosity.Chatty;

        [Header("Experimental Options")]
        [Tooltip("If enabled, Ignorance will use a new packet processing engine.")]
        public bool m_UseNewPacketEngine = true;

        [Header("Compression Options")]
        [Tooltip("If enabled, LZ4 Compression will be used to reduce packet data sizes.")]
        public bool m_UseLZ4Compression = false;

        [Header("Bind Options")]
        /// <summary>
        /// Disabling this will bind to a specific IP Address. Otherwise it will bind to everything.
        /// </summary>
        [Tooltip("If set, this will bind to all interfaces rather than a specific IP address.")]
        public bool m_BindToAllInterfaces = true;
        /// <summary>
        /// This will be used if you turn off binding to all interfaces.
        /// </summary>
        [Tooltip("Set this to the IP address you want to bind to if you disable the Bind to All Interfaces option.")]
        public string m_BindToAddress = "127.0.0.1";
        /// <summary>
        /// The communication port used by the server and client. Can be anything between port 1 to 65535.
        /// </summary>
        [Tooltip("The communication port used by the server and client. Can be anything between port 1 to 65535.")]
        public ushort Port = 7777;
        // Compatibility.
        public ushort port { get { return Port; } set { Port = value; } }

        [Header("Timeout Configuration")]
        // -- TIMEOUTS -- //
        /// <summary>
        /// Use custom peer timeouts?
        /// </summary>
        [Tooltip("Tick to use a custom peer timeout.")]
        public bool useCustomPeerTimeout = false;
        /// <summary>
        /// Base timeout, default is 5000 ticks (5 seconds).
        /// </summary>
        [Tooltip("The base amount of ticks to wait for detecting if a client is timing out.")]
        public uint peerBaseTimeout = 5000;
        /// <summary>
        /// peerBaseTimeout * this value = maximum time waiting until client is removed
        /// </summary>
        [Tooltip("This value is multiplied by the base timeout for the maximum amount of ticks that we'll wait before evicting connections.")]
        public uint peerBaseTimeoutMultiplier = 3;
        /// <summary>
        /// Every peer that connects decrements this counter. So that means if you have 30 connections, it will be 970 connections available.
        /// When this hits 0, the server will not allow any new connections. Hard limit.
        /// </summary>
        [Header("Connection Hard Limit Configuration")]
        [Tooltip("This is not the same as Mirror's Maximum Connections! Leave alone if you don't know exactly what it does.")]
        public int m_MaximumTotalConnections = 1000;

        // -- SERVER WORLD VARIABLES -- //
        // Explicitly give these new references on startup, just to make sure that we get no null reference exceptions.
        private Host server = new Host();
        private Host client = new Host();

        private Address serverAddress = new Address();
        private Peer clientPeer = new Peer();

        private string m_MyServerAddress = string.Empty;

        /// <summary>
        /// Known connections dictonary since ENET is a little weird.
        /// </summary>
        private Dictionary<int, Peer> knownConnIDToPeers;
        /// <summary>
        /// Known reverse connections dictonary since ENET is a little weird.
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

        // -- Packet buffer -- //
        // Reverted in v1.2.0 RC3. To be investigated and improved at a (unknown) later date.
        // private byte[] m_PacketDataBuffer;

        /// <summary>
        /// Outputs a friendly message to the console log. Don't remove unless you hate things saying hello.
        /// You'll make the transport sad. :(
        /// </summary>
        private void GreetEveryone()
        {
            Log($"Thank you for using Ignorance Transport v{TransportInfo.Version} for Mirror! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance. ENET Library Version: {Library.version}");
        }

        // -- INITIALIZATION -- // 
        public IgnoranceTransport()
        {
            // Intentionally left blank.            
        }

        private void Awake()
        {
            GreetEveryone();
#if UNITY_EDITOR_OSX
            Debug.LogWarning("Hmm, looks like you're using Ignorance inside a Mac Editor instance. This is known to be problematic due to some Unity Mono bugs. " +
                "If you have issues using Ignorance, please try the Unity 2019.1 beta and let the developer know. Thanks!");
#endif
            Library.Initialize();

            // If the user sets this to -1, treat it as no limit.
            if (m_MaximumTotalConnections < 0) m_MaximumTotalConnections = 0;

            // Allocate memory working buffer. (Reverted in 1.2.0 RC3, this code is left in to remind me to come back to it).
            // Log($"Ignorance Transport is pre-allocating a {Library.maxPacketSize} byte memory buffer to help reduce stack memory allocations. This message is harmless - keep calm and carry on.");            
            // m_PacketDataBuffer = new byte[Library.maxPacketSize];
        }

        // TODO: Consult Mirror team and figure out best plan of attack for this deinitialization.
        public void OnDestroy()
        {
            Library.Deinitialize();
        }

        /// <summary>
        /// Gets the maximum packet size allowed. Introduced from Mirror upstream git commit: 1289dee8. <para />
        /// Please see https://github.com/nxrighthere/ENet-CSharp/issues/33 for more information.
        /// </summary>
        /// <returns>A integer with the maximum packet size.</returns>
        public override int GetMaxPacketSize(int channel)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes. Do not attempt to send more, ENET will likely catch fire. NX's orders.
        }

        // -- SERVER WORLD FUNCTIONS -- //

        /// <summary>
        /// Is the server active?
        /// </summary>
        /// <returns>True if the server is active, false otherwise.</returns>
        public override bool ServerActive()
        {
            return IsValid(server);
        }

        /// <summary>
        /// Disconnects a server connection.
        /// </summary>
        /// <param name="connectionId">The connection ID to evict.</param>
        /// <returns>True if the connection exists, false otherwise.</returns>
        public override bool ServerDisconnect(int connectionId)
        {
            Peer result;

            if (knownConnIDToPeers.TryGetValue(connectionId, out result))
            {
                result.DisconnectNow(0);
            }
            else
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// The meat and vegies of the transport on the server side. This is the packet pump.
        /// </summary>
        /// <returns>True if successful, False if unsuccessful.</returns>
        public bool ProcessServerMessage()
        {
            if (!ServerActive()) return false;

            int deadPeerConnID, knownConnectionID;
            int newConnectionID = serverConnectionCount;

            // The incoming Enet Event.        
            Event incomingEvent;

            if (!server.IsSet)
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport: Server is not ready.");
                return false;
            }

            // Get the next message...
            server.Service(0, out incomingEvent);

            // What type is this?
            switch (incomingEvent.Type)
            {
                // Connections (Normal peer connects)
                case EventType.Connect:
                    if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log(string.Format("Ignorance Transport: New connection with peer ID {0}, IP {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP));

                    // The peer object will allow us to do stuff with it later.
                    // Map them in our dictionaries
                    knownPeersToConnIDs.Add(incomingEvent.Peer, newConnectionID);
                    knownConnIDToPeers.Add(newConnectionID, incomingEvent.Peer);

                    if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log(string.Format("Ignorance Transport: New connection from IP {0}. Peer ID {1} mapped to internal connection ID {2}", incomingEvent.Peer.IP, incomingEvent.Peer.ID, newConnectionID));

                    // Increment the fake connection counter by one.
                    serverConnectionCount++;

                    // If we're using custom timeouts, then set the timeouts too.
                    if (useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);

                    // Report back saying we got a connection event.
                    OnServerConnected.Invoke(newConnectionID);
                    break;

                // Disconnections (Normal peer disconnect and timeouts)
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: ServerGetNextMessage(): {0} event, peer ID {1}, IP {2}",
                        (incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout"), incomingEvent.Peer.ID, incomingEvent.Peer.IP));

                    if (knownPeersToConnIDs.TryGetValue(incomingEvent.Peer, out deadPeerConnID))
                    {
                        Log(string.Format("Ignorance Transport: Acknowledging disconnection on connection ID {0}", deadPeerConnID));
                        PeerDisconnectedInternal(incomingEvent.Peer);
                        OnServerDisconnected.Invoke(deadPeerConnID);
                    }
                    break;

                case EventType.Receive:
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: ServerGetNextMessage(): Channel {0} receiving {1} byte payload", incomingEvent.ChannelID, incomingEvent.Packet.Length));

                    // Only process data from known peers.
                    if (knownPeersToConnIDs.TryGetValue(incomingEvent.Peer, out knownConnectionID))
                    {
                        NewMessageDataProcessor(incomingEvent.Packet, true, knownConnectionID);
                    }
                    else
                    {
                        // Emit a warning and clean the packet. We don't want it in memory.
                        incomingEvent.Packet.Dispose();

                        if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport WARNING: Discarded a packet because it was from a unknown peer. " +
                            "If you see this message way too many times then you are likely a victim of a DoS or DDoS attack that is targeting your server's connection port." +
                            " Ignorance will keep discarding packets but please do look into this. Failing to do so is risky and could potentially crash the server instance!");
                    }
                    break;

                case EventType.None:
                    // Nothing happened. Do nothing.
                    return false;
            }

            // We're done here. Bugger off.
            return true;
        }

        /// <summary>
        /// Internal function called when a peer disconnects.
        /// </summary>
        /// <param name="peer">Peer to disconnect</param>
        private void PeerDisconnectedInternal(Peer peer)
        {
            // Clean up dictionaries.
            knownConnIDToPeers.Remove(knownPeersToConnIDs[peer]);
            knownPeersToConnIDs.Remove(peer);
        }

        /// <summary>
        /// Send data on the server side.
        /// </summary>
        /// <param name="connectionId">The connection ID to send data to.</param>
        /// <param name="channelId">The channel ID to send data on. Must not be lower or greater than the values in the sendMethods array.</param>
        /// <param name="data">The payload to transmit.</param>
        /// <returns></returns>
        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return ServerSend(connectionId, channelId, new ArraySegment<byte>(data));
        }

        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
        {
            // Another mailing pigeon
            Packet mailingPigeon = default;

            if (channelId >= packetSendMethods.Length)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return false;
            }

            // This should fix that bloody AccessViolation
            // Issue reference: https://github.com/nxrighthere/ENet-CSharp/issues/28#issuecomment-436100923
            mailingPigeon.Create(data.Array, data.Offset, data.Count, packetSendMethods[channelId]);

            // More haxx. see https://github.com/nxrighthere/ENet-CSharp/issues/21 for some background info-ish.
            Peer target;
            if (knownConnIDToPeers.TryGetValue(connectionId, out target))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: Server sending data length {2} on channel {1} to connection ID {0}", connectionId, channelId, data.Count));
                if (m_TransportVerbosity == TransportVerbosity.LogSpam) Log(string.Format("Ignorance Transport sending payload: Connection {2} Channel {0}, Data:\n{1}", channelId, BitConverter.ToString(data.Array, data.Offset, data.Count), connectionId));

                if (target.Send((byte)channelId, ref mailingPigeon))
                {
                    return true;
                }
                else
                {
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning($"Ignorance Transport: Server-side packet to connection ID {connectionId} on channel {channelId} ({(byte)channelId}) wasn't successful.");
                    return false;
                }
            }
            else
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log($"Ignorance Transport: Failure sending data to connection ID {connectionId} on channel {channelId}. Did they disconnect?");
                return false;
            }
        }

        /// <summary>
        /// Start the server with the specified parameters.
        /// </summary>
        /// <param name="address">The address to bind to.</param>
        /// <param name="port">The port to use. Do not run more than one server on the same port.</param>
        /// <param name="maxConnections">How many connections can we have?</param>
        public void ServerStart(string networkAddress, ushort port, int maxConnections)
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

#if UNITY_EDITOR_OSX
            if(verboseLoggingEnabled) Log(string.Format("Ignorance Transport: Server startup in MacOS Editor workaround mode on port {0} with capacity of {1} concurrent connections", Port, NetworkManager.singleton.maxConnections));

            LogWarning("Ignorance Transport: Binding to a specific address is disabled on MacOS Editor due to some bugs. Please refer to https://github.com/nxrighthere/ENet-CSharp/issues/46 " +
                "for technicial details. While you can disable this check, it will most likely bug out and mess connectivity up. You've been warned.");            
            Log("Ignorance Transport: Binding to ::0 as a workaround for Mac OS LAN Host");
            serverAddress.SetHost("::0");
#else
            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance Transport: Server startup on port {Port}");
            if (m_BindToAllInterfaces)
            {
                Log("Ignorance Transport: Binding to all available interfaces.");
                if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer) serverAddress.SetHost("::0");
                else serverAddress.SetHost("0.0.0.0");
            }
            else
            {
                // If one is specified, that takes priority.
                if (!string.IsNullOrEmpty(networkAddress))
                {
                    Log($"Ignorance Transport: Using {networkAddress} as our specific bind address");
                    serverAddress.SetHost(networkAddress);
                }
                else if (!string.IsNullOrEmpty(m_BindToAddress))
                {
                    Log($"Ignorance Transport: Using {m_BindToAddress} as our specific bind address");
                    serverAddress.SetHost(m_BindToAddress);
                }
                else
                {
                    // No. Just no. Go back and try again, do not pass Go, do not collect $200.
                    LogError($"Ignorance Transport: No bind address specified and you have disabled bind to all interfaces. Please go back and fix this, then start the server again.");
                    return;
                }
            }
#endif
            // Setup the port.
            serverAddress.Port = port;

            // Finally create the server.
            server.Create(serverAddress, m_MaximumTotalConnections, packetSendMethods.Length, 0, 0);

            if (m_UseLZ4Compression) server.EnableCompression();

            if (m_UseNewPacketEngine) Log("Ignorance Transport: New experimental packet engine will be used.");

            // Log our best effort attempts
            Log($"Ignorance Transport: Attempted to create server on UDP port {Port}");
            Log("Ignorance Transport: If you see this, the server most likely was successfully created and started! (This is good.)");

            m_MyServerAddress = serverAddress.GetHost();
        }

        /// <summary>
        /// Called when the server stops.
        /// </summary>
        public override void ServerStop()
        {
            if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Ignorance Transport: ServerStop()");

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
        public override void ClientConnect(string address)
        {
            Log(string.Format("Ignorance Transport: Acknowledging connection request to {0}:{1}", address, Port));

            if (client == null) client = new Host();
            if (!client.IsSet) client.Create(null, 1, packetSendMethods.Length, 0, 0);
            if (m_UseLZ4Compression) client.EnableCompression();

            Address clientAddress = new Address();

            // Set hostname and port to connect to.
            clientAddress.SetHost(address);
            clientAddress.Port = Port;

            if (m_UseNewPacketEngine) Log("Ignorance Transport: Client will use new experimental packet engine.");

            // Connect the client to the server.
            if (useCustomPeerTimeout) clientPeer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);
            clientPeer = client.Connect(clientAddress);

            // Debugging only
            if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: Client Peer Set? {0}", clientPeer.IsSet));
        }

        /// <summary>
        /// Is the client connected currently?
        /// </summary>
        /// <returns>True if connected, False if not.</returns>
        public override bool ClientConnected()
        {
            if (m_TransportVerbosity > TransportVerbosity.Chatty) Log($"Ignorance Transport: Mirror asks if I'm connected. The answer to that is { ((clientPeer.State == PeerState.Connected) ? true : false) }. Note that if this a local client on the server instance, false may be a acceptable reply.");
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public override void ClientDisconnect()
        {
            Log(clientPeer.State);

            if (clientPeer.State == PeerState.Disconnected) return;

            Log("Ignorance Transport: Received disconnection request from Mirror. Acknowledged!");

            // Disconnect the client's peer object, only if it's not disconnected. This might fix a bad pointer or something.
            // Reference: https://github.com/SoftwareGuy/Ignorance/issues/20
            if (clientPeer.IsSet)
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Ignorance Transport: Disconnecting the client's peer...");
                if (clientPeer.State != PeerState.Disconnected) clientPeer.DisconnectNow(0);
            }

            if (IsValid(client))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Ignorance Transport: Flushing and disposing of the client...");
                client.Flush();
                client.Dispose();
            }

            // OnClientDisconnected.Invoke();
            client = null;
        }


        /// <summary>
        /// Get the next client data packet.
        /// </summary>
        /// <param name="transportEvent">The transport event to report back to Mirror.</param>
        /// <param name="data">The byte array of the data.</param>
        /// <returns></returns>
        public bool ProcessClientMessage()
        {
            // The incoming Enet Event.
            Event incomingEvent;

            // Safety check: if the client isn't created, then we shouldn't do anything. ENet might be warming up.
            if (!IsValid(client))
            {
                // LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
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
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: ClientGetNextMessage() connect; real ENET peerID {0}, address {1}", incomingEvent.Peer.ID, incomingEvent.Peer.IP));
                    else Log(string.Format("Ignorance Transport: Connection established with {0}", incomingEvent.Peer.IP));

                    // If we're using custom timeouts, then set the timeouts too.
                    // if (useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);
                    OnClientConnected.Invoke();
                    break;

                // Peer disconnects/timeout.
                case EventType.Disconnect:
                case EventType.Timeout:
                    // TODO: Should timeouts be a client error?
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: ClientGetNextMessage() {0}, peerID {1}, address {2}", incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout",
                         incomingEvent.Peer.ID, incomingEvent.Peer.IP));
                    else Log(string.Format("Ignorance Transport: Client encountered {0}", incomingEvent.Type == EventType.Disconnect ? "disconnection" : "timeout"));
                    OnClientDisconnected.Invoke();
                    break;
                // Peer sends data to us.
                case EventType.Receive:
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: ClientGetNextMessage(): Data channel {0} receiving {1} byte payload...", incomingEvent.ChannelID, incomingEvent.Packet.Length));
                    NewMessageDataProcessor(incomingEvent.Packet);
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
        public override bool ClientSend(int channelId, byte[] data)
        {
            return ClientSend(channelId, new ArraySegment<byte>(data));
        }

        public bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            Packet mailingPigeon = default;

            if (!client.IsSet)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return false;
            }

            if (channelId >= packetSendMethods.Length)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return false;
            }

            mailingPigeon.Create(data.Array, data.Offset, data.Count, packetSendMethods[channelId]);

            if (m_TransportVerbosity > TransportVerbosity.Chatty) Log(string.Format("Ignorance Transport: Client sending on channel {0} length {1}", channelId, data.Count));
            if (m_TransportVerbosity > TransportVerbosity.Paranoid) Log(string.Format("Ignorance Transport Client sending payload data:\n{0}", BitConverter.ToString(data.Array, data.Offset, data.Count)));

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
        public override void Shutdown()
        {
            Log("Ignorance Transport: Going down for shutdown NOW!");

            // Shutdown the client first.
            if (IsValid(client))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Sending the client process to the dumpster fire...");
                if (clientPeer.IsSet) clientPeer.DisconnectNow(0);

                client.Flush();
                client.Dispose();
            }

            // Shutdown the server.
            if (IsValid(server))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Sending the server process to the dumpster fire...");

                server.Flush();
                server.Dispose();
            }

            Library.Deinitialize();
            Log("Ignorance Transport shutdown complete. Have a good one.");
        }

        // Mirror master update loops.
        public void LateUpdate()
        {
            if (enabled)
            {
                if (m_UseNewPacketEngine)
                {
                    NewServerMessageProcessor();
                    NewClientMessageProcessor();
                }
                else
                {
                    while (ProcessServerMessage()) ;
                    while (ProcessClientMessage()) ;
                }
            }
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

        // Static helpers
        private void Log(object text)
        {
            Debug.Log(text);
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

        // -- Version 1.2 -- //
        public enum TransportVerbosity
        {
            SilenceIsGolden,
            Chatty,
            Paranoid,
            LogSpam
        }

        public bool NewClientMessageProcessor()
        {
            bool clientWasPolled = false;
            Event networkEvent;

            // Only process messages if the client is valid.
            while (!clientWasPolled)
            {
                if (!IsValid(client))
                {
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport: NewClientMessageProcessor() reports the client object is not valid.");
                    return false;
                }

                if (client.CheckEvents(out networkEvent) <= 0)
                {
                    if (client.Service(0, out networkEvent) <= 0) break;
                    clientWasPolled = true;
                }

                // Spam the logs if we're over paranoid levels.
                if (m_TransportVerbosity == TransportVerbosity.Paranoid) LogWarning($"Ignorance Transport: NewClientMessageProcessor() processing {networkEvent.Type} event...");

                switch (networkEvent.Type)
                {
                    case EventType.None:
                        // Nothing happened.
                        break;
                    case EventType.Connect:
                        // Connected to a host/server.
                        if (m_TransportVerbosity > TransportVerbosity.Chatty) Log($"Ignorance Transport: Connected to peer {networkEvent.Peer.IP} with our ENET Peer ID {networkEvent.Peer.ID}");
                        else Log(string.Format("Ignorance Transport: Connection established! Host Peer IP: {0}", networkEvent.Peer.IP));

                        // If we're using custom timeouts, then set the timeouts too.
                        // if (useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);
                        OnClientConnected.Invoke();
                        break;
                    case EventType.Disconnect:
                        // Client disconnected.
                        Log(string.Format("Ignorance Transport: Connection disconnected from Host Peer IP: {0}", networkEvent.Peer.IP));
                        OnClientDisconnected.Invoke();
                        break;
                    case EventType.Receive:
                        // Client recieving some data.
                        if (m_TransportVerbosity >= TransportVerbosity.Paranoid) Log($"Ignorance Transport: Client data channel {networkEvent.ChannelID} is receiving {networkEvent.Packet.Length} byte payload.");
                        // Don't panic, data processing and invoking is done in a new function.
                        NewMessageDataProcessor(networkEvent.Packet);
                        break;
                    case EventType.Timeout:
                        Log(string.Format("Ignorance Transport: Connection timeout while communicating with Host Peer IP: {0}", networkEvent.Peer.IP));
                        OnClientDisconnected.Invoke();
                        break;
                    default:
                        break;
                }
            }
            // We're done here. Return.
            return true;
        }

        public bool NewServerMessageProcessor()
        {
            bool serverWasPolled = false;
            int deadPeerConnID, timedOutConnID, knownConnectionID;
            int newConnectionID = serverConnectionCount;
            Event networkEvent;

            // Don't attempt to process anything if the server is not active.
            if (!ServerActive()) return false;

            // Only process messages if the server is valid.
            if (!IsValid(server))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport: NewServerMessageProcessor() reports the server host object is not valid.");
                return false;
            }

            while (!serverWasPolled)
            {
                if (server.CheckEvents(out networkEvent) <= 0)
                {
                    if (server.Service(0, out networkEvent) <= 0)
                        break;

                    serverWasPolled = true;
                }

                switch (networkEvent.Type)
                {
                    case EventType.None:
                        // Nothing happened.
                        break;
                    case EventType.Connect:
                        // A client connected to the server. Assign a new ID to them.
                        if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance Transport: New client connection to server. Peer ID: {networkEvent.Peer.ID}, IP: {networkEvent.Peer.IP}");

                        // Map them into our dictonaries.
                        knownPeersToConnIDs.Add(networkEvent.Peer, newConnectionID);
                        knownConnIDToPeers.Add(serverConnectionCount, networkEvent.Peer);

                        if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance Transport: Peer ID {networkEvent.Peer.ID} is now known as connection ID {serverConnectionCount}.");
                        OnServerConnected.Invoke(serverConnectionCount);

                        // Increment the connection counter.
                        serverConnectionCount++;
                        break;
                    case EventType.Disconnect:
                        // A client disconnected.

                        if (knownPeersToConnIDs.TryGetValue(networkEvent.Peer, out deadPeerConnID))
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance Transport: Connection ID {knownPeersToConnIDs[networkEvent.Peer]} has disconnected.");
                            OnServerDisconnected.Invoke(deadPeerConnID);
                            PeerDisconnectedInternal(networkEvent.Peer);
                        }
                        else
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogWarning($"Ignorance Transport: Unknown Peer with ID {networkEvent.Peer.ID} has disconnected. Hmm...");
                        }
                        break;
                    case EventType.Receive:
                        if (m_TransportVerbosity > TransportVerbosity.Chatty) Log($"Ignorance Transport: Server data channel {networkEvent.ChannelID} receiving a {networkEvent.Packet.Length} byte payload");

                        // Only process data from known peers.
                        if (knownPeersToConnIDs.TryGetValue(networkEvent.Peer, out knownConnectionID))
                        {
                            NewMessageDataProcessor(networkEvent.Packet, true, knownConnectionID);
                        }
                        else
                        {
                            // Emit a warning and clean the packet. We don't want it in memory.
                            networkEvent.Packet.Dispose();

                            if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport WARNING: Discarded a packet because it was from a unknown peer. " +
                                "If you see this message way too many times then you are likely a victim of a DoS or DDoS attack that is targeting your server's connection port." +
                                " Ignorance will keep discarding packets but please do look into this. Failing to do so is risky and could potentially crash the server instance!");
                        }
                        break;
                    case EventType.Timeout:
                        // A client timed out. Note that this could be the same as the disconnected, but for now I'm going to seperate them for debugging reasons
                        if (knownPeersToConnIDs.TryGetValue(networkEvent.Peer, out timedOutConnID))
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance Transport: Connection ID {knownPeersToConnIDs[networkEvent.Peer]} has timed out.");
                            OnServerDisconnected.Invoke(timedOutConnID);
                            PeerDisconnectedInternal(networkEvent.Peer);
                        }
                        else
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogWarning($"Ignorance Transport: Unknown Peer with ID {networkEvent.Peer.ID} has timed out. Hmm...");
                        }
                        break;
                    default:
                        // Do nothing?
                        break;
                }
            }

            // We're done here. Return.
            return true;
        }

        public void NewMessageDataProcessor(Packet sourcePacket, bool serverInvoke = false, int connectionID = 0)
        {
            if (m_TransportVerbosity == TransportVerbosity.Paranoid) Log($"Ignorance Transport: Processing a {sourcePacket.Length} byte payload.");

            // This will be improved on at a later date.
            byte[] dataBuf = new byte[sourcePacket.Length];
            // Copy our data into our buffers from ENET Native -> Ignorance Transport Managed world.
            sourcePacket.CopyTo(dataBuf);
            sourcePacket.Dispose();

            if (m_TransportVerbosity == TransportVerbosity.LogSpam) Log($"Ignorance Transport: Oi Mirror, data's arrived!");
            if (m_TransportVerbosity == TransportVerbosity.LogSpam) Log($"Ignorance Transport: Packet payload:\n{ BitConverter.ToString(dataBuf) }");

            if (serverInvoke) OnServerDataReceived.Invoke(connectionID, dataBuf);
            else OnClientDataReceived.Invoke(dataBuf);
        }

        /// <summary>
        //  Gets a client's address on the server. Could be used for bans and whatnot.
        /// </summary>
        /// <param name="connectionId">The connection ID to look up.</param>
        /// <returns>The Peer's IP if valid, otherwise it will return "(invalid)".</returns>
        public override string ServerGetClientAddress(int connectionId)
        {
            Peer result;
            if (knownConnIDToPeers.TryGetValue(connectionId, out result))
            {
                return result.IP;
            }

            return "(invalid)";
        }

        // Custom ServerStart() calls for Insight compatibility.
        // From https://github.com/SoftwareGuy/Ignorance/issues/23
        // Thanks uwee!!
        public override void ServerStart()
        {
            ServerStart(string.Empty, port, (m_MaximumTotalConnections <= 0 ? int.MaxValue : m_MaximumTotalConnections));
        }

        public void ServerStart(ushort port)
        {
            ServerStart(string.Empty, port, (m_MaximumTotalConnections <= 0 ? int.MaxValue : m_MaximumTotalConnections));
        }

        public void ServerStart(string networkAddress, ushort port)
        {
            ServerStart(networkAddress, port, (m_MaximumTotalConnections <= 0 ? int.MaxValue : m_MaximumTotalConnections));
        }

        // Prettify the output string, rather than IgnoranceTransport (Mirror.IgnoranceTransport)
        public override string ToString()
        {
            return $"Ignorance {(ServerActive() ? (m_BindToAllInterfaces ? $"bound to all interfaces, port {Port}" : $"bound to {m_MyServerAddress}, port {Port}") : "inactive")}";
        }

        public class TransportInfo
        {
            public const string Version = "1.2.0 Release Candidate 5";
        }
    }

    public static class IgnoranceExtensions
    {
        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

    }
}
