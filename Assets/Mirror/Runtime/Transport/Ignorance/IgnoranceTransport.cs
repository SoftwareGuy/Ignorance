// ----------------------------------------
// Ignorance Transport by Matt Coburn, 2018 - 2019
// This Transport uses other dependencies that you can
// find references to in the README.md of this package.
// ----------------------------------------
// Ignorance Transport is MIT Licensed. It would be however
// nice to get some acknowledgement in your program/game's credits
// that Ignorance was used to build your network code. It would be 
// greatly appreciated if you reported bugs and donated coffee
// at https://github.com/SoftwareGuy/Ignorance. Remember, OSS is the
// way of the future!
// ----------------------------------------

using ENet;
#if UNITY_EDITOR
using Mirror.Ignorance.Editor;
#endif
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
    public class IgnoranceTransport : Transport, ISegmentTransport
    {
        #region Configuration Settings
        public List<KnownChannelTypes> m_ChannelDefinitions = new List<KnownChannelTypes>()
        {
            KnownChannelTypes.Reliable,     // Default channel 0, reliable
            KnownChannelTypes.Unreliable,   // Default channel 1, unreliable
        };

#if UNITY_EDITOR
        [Tooltip("How do you like your debug logs?")]
#endif
        public TransportVerbosity m_TransportVerbosity = TransportVerbosity.Chatty;
#if UNITY_EDITOR
        [Tooltip("If enabled, Ignorance will use a new packet processing engine.")]
        [Rename("New Packet Engine")]
#endif
        public bool m_UseNewPacketEngine = true;

#if UNITY_EDITOR
        [Tooltip("If enabled, LZ4 Compression will be used to reduce packet data sizes.")]
        [Rename("LZ4 Compression")]
#endif
        public bool m_UseLZ4Compression = false;

#if UNITY_EDITOR
        [Tooltip("If set, this will bind to all interfaces rather than a specific IP address.")]
#endif
        /// <summary>
        /// Disabling this will bind to a specific IP Address. Otherwise it will bind to everything.
        /// </summary>
#if UNITY_EDITOR
        [Rename("Bind to All")]
#endif
        public bool m_BindToAllInterfaces = true;
        /// <summary>
        /// This will be used if you turn off binding to all interfaces.
        /// </summary>
#if UNITY_EDITOR
        [Tooltip("Set this to the IP address you want to bind to if you disable the Bind to All Interfaces option.")]
        [Rename("Bind Address")]
#endif
        public string m_BindToAddress = "127.0.0.1";
        /// <summary>
        /// The communication port used by the server and client. Can be anything between port 1 to 65535.
        /// </summary>
#if UNITY_EDITOR
        [Rename("Bind Port")]
        [Tooltip("The communication port used by the server and client. Can be anything between port 1 to 65535.")]
#endif
        public ushort m_Port = 7777;

        // Backwards compatibility.
        public ushort port { get { return m_Port; } set { m_Port = value; } }

        /// <summary>
        /// Use custom peer timeouts?
        /// </summary>
#if UNITY_EDITOR
        [Rename("Custom Timeout")]
        [Tooltip("Tick to use a custom peer timeout.")]
#endif
        public bool m_UseCustomTimeout = false;
        /// <summary>
        /// Base timeout, default is 5000 ticks (5 seconds).
        /// </summary>
#if UNITY_EDITOR
        [Tooltip("The base amount of ticks to wait for detecting if a client is timing out.")]
#endif
        public uint m_BasePeerTimeout = 5000;
        /// <summary>
        /// peerBaseTimeout * this value = maximum time waiting until client is removed
        /// </summary>
#if UNITY_EDITOR
        [Tooltip("This value is multiplied by the base timeout for the maximum amount of ticks that we'll wait before evicting connections.")]
#endif
        public uint m_BasePeerMultiplier = 3;
        /// <summary>
        /// Every peer that connects decrements this counter. So that means if you have 30 connections, it will be 970 connections available.
        /// When this hits 0, the server will not allow any new connections. Hard limit.
        /// </summary>
#if UNITY_EDITOR
        [Rename("Max Server Conn.")]
        [Tooltip("This is not the same as Mirror's Maximum CCU! Leave alone if you don't know exactly what it does.")]
#endif
        public int m_MaximumTotalConnections = 1000;

        public string Version { get { return TransportInfo.Version; } }
        #endregion

        #region Transport-level references, dictonaries, etc.
        // Explicitly give these new references on startup, just to make sure that we get no null reference exceptions.
        private Host m_Server = new Host();
        private Host m_Client = new Host();

        private Address m_ServerAddress = new Address();
        private Peer m_ClientPeer = new Peer();

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
        private int serverConnectionCnt = 1;
        #endregion

        // -- Packet buffer -- //
        // Reverted in v1.2.0 RC3. To be investigated and improved at a (unknown) later date.
        // private byte[] m_PacketDataBuffer;
        #region Transport World Functions - Initialization & Deinitialization
        public IgnoranceTransport()
        {
            // Intentionally left blank.            
        }

        /// <summary>
        /// Outputs a friendly message to the console log. Don't remove unless you hate things saying hello.
        /// You'll make the transport sad. :(
        /// </summary>
        private void GreetEveryone()
        {
            Log($"Thank you for using Ignorance Transport v{TransportInfo.Version} for Mirror! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance. ENET Library Version: {Library.version}");
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
        }

        public void OnDestroy()
        {
            Library.Deinitialize();
        }

        #endregion

        #region Transport - Server Functions
        /// <summary>
        /// Is the server active?
        /// </summary>
        /// <returns>True if the server is active, false otherwise.</returns>
        public override bool ServerActive()
        {
            return IsValid(m_Server);
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

            // Make sure we're not trying to overflow the channel counts.
            if ((m_ChannelDefinitions.Count - 1) >= 255)
            {
                LogError("Ignorance Transport: Too many channels. ENET-senpai can't handle them!");
                return;
            }

            // Prevent dirty memory issues later.
            m_Server = new Host();
            m_ServerAddress = new Address();
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

#if UNITY_EDITOR_OSX
            if(m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) {
                Log($"Ignorance Transport: Server startup in MacOS Editor workaround mode on port {m_Port}");
            }

            LogWarning("Ignorance Transport: Binding to a specific address is disabled on MacOS Editor due to some bugs. Please refer to https://github.com/nxrighthere/ENet-CSharp/issues/46 " +
                "for technical details. While you can disable this check, it will most likely bug out and mess connectivity up. You've been warned.");
            Log("Ignorance Transport: Binding to ::0 as a workaround for Mac OS LAN Host");
            m_ServerAddress.SetHost("::0");
#else
            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
            {
                Log($"Ignorance Transport: Server startup on port {m_Port}");
            }
            if (m_BindToAllInterfaces)
            {
                Log("Ignorance Transport: Binding to all available interfaces.");
                if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                {
                    m_ServerAddress.SetHost("::0");
                    m_MyServerAddress = "::0";
                }
                else
                {
                    m_ServerAddress.SetHost("0.0.0.0");
                    m_MyServerAddress = "0.0.0.0";
                }
            }
            else
            {
                // If one is specified, that takes priority.
                if (!string.IsNullOrEmpty(networkAddress))
                {
                    Log($"Ignorance Transport: Using {networkAddress} as our specific bind address");
                    m_ServerAddress.SetHost(networkAddress);
                    m_MyServerAddress = networkAddress;
                }
                else if (!string.IsNullOrEmpty(m_BindToAddress))
                {
                    Log($"Ignorance Transport: Using {m_BindToAddress} as our specific bind address");
                    m_ServerAddress.SetHost(m_BindToAddress);
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
            m_ServerAddress.Port = port;

            // Finally create the server.
            m_Server.Create(m_ServerAddress, m_MaximumTotalConnections, m_ChannelDefinitions.Count);

            if (m_UseLZ4Compression)
            {
                Log("Ignorance: Server instance will use LZ4 Compression.");
                m_Server.EnableCompression();
            }

            if (m_UseNewPacketEngine)
            {
                Log("Ignorance Transport: Server instance will use the new multi-event-per-frame packet engine.");
            }

            // Log our best effort attempts
            Log($"Ignorance Transport: Attempted to create server on UDP port {m_Port}. If Ignorance immediately crashes after this line, please file a bug report on the GitHub.");
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

        /// <summary>
        /// Called when the server stops.
        /// </summary>
        public override void ServerStop()
        {
            if (m_TransportVerbosity > TransportVerbosity.Chatty)
            {
                Log("Ignorance Transport: ServerStop()");
            }

            // This might be slow if we have lots of CCU.
            foreach (KeyValuePair<int, Peer> entry in knownConnIDToPeers) entry.Value.DisconnectNow(0);

            // Cleanup in the connection isle.
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

            if (IsValid(m_Server)) m_Server.Dispose();

            m_Server = null;

            Log("Ignorance Transport: Server stopped.");
        }

        #endregion

        #region Transport - Outgoing Server Transmission Functions
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

            if (channelId >= m_ChannelDefinitions.Count)
            {
                LogError($"ERROR: Refusing to even attempt to send data on channel {channelId}. It is either greater than or equal to the channel definition count." +
                    $"If you think this is a bug, consider filing a bug report.");
                return false;
            }

            if (m_TransportVerbosity == TransportVerbosity.LogSpam)
            {
                Log($"DEBUG: m_ChannelDefinitions[{channelId}] => { m_ChannelDefinitions[channelId] }");
            }

            // This should fix that bloody AccessViolation
            // Issue reference: https://github.com/nxrighthere/ENet-CSharp/issues/28#issuecomment-436100923
            mailingPigeon.Create(data.Array, data.Offset, data.Count, MapKnownChannelTypeToENETPacketFlag(m_ChannelDefinitions[channelId]));

            // More haxx. see https://github.com/nxrighthere/ENet-CSharp/issues/21 for some background info-ish.
            Peer target;
            if (knownConnIDToPeers.TryGetValue(connectionId, out target))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty)
                {
                    Log($"Ignorance Transport: Server sending {data.Count} byte data on channel {channelId} to connection ID {connectionId}");
                    if (m_TransportVerbosity == TransportVerbosity.LogSpam)
                    {
                        Log($"Ignorance Transport: Outgoing payload to connection {connectionId} on channel {channelId}\n{BitConverter.ToString(data.Array, data.Offset, data.Count)}");
                    }
                }

                if (target.Send((byte)channelId, ref mailingPigeon))
                {
                    return true;
                }
                else
                {
                    if (m_TransportVerbosity > TransportVerbosity.Chatty)
                    {
                        LogWarning($"Ignorance Transport: Send failure to connection ID {connectionId} on channel {channelId} ({(byte)channelId})");
                    }
                    return false;
                }
            }
            else
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty)
                {
                    LogWarning($"Ignorance Transport: Send failure to connection ID {connectionId} on channel {channelId} ({(byte)channelId}). This could happen randomly due to the nature of UDP.");
                }
                return false;
            }
        }
        #endregion

        #region Transport - Client Functions
        /// <summary>
        /// Connects the client to a server.
        /// </summary>
        /// <param name="address">The connection address.</param>
        /// <param name="port">The connection port.</param>
        public override void ClientConnect(string address)
        {
            // Make sure we're not trying to overflow the channel counts.
            if ((m_ChannelDefinitions.Count - 1) >= 255)
            {
                LogError("Ignorance Transport: Too many channels. ENET-senpai can't handle them!");
                return;
            }

            Log($"Ignorance Transport: Acknowledging connection request to {address}:{m_Port}");

            if (m_Client == null) m_Client = new Host();
            if (!m_Client.IsSet) m_Client.Create(null, 1, m_ChannelDefinitions.Count);
            if (m_UseLZ4Compression) m_Client.EnableCompression();

            Address clientAddress = new Address();

            // Set hostname and port to connect to.
            clientAddress.SetHost(address);
            clientAddress.Port = m_Port;

            if (m_UseNewPacketEngine)
            {
                Log("Ignorance Transport: Client will use new multi-event-per-frame packet engine.");
            }

            // Connect the client to the server.
            // 1.2.1: GODDAMNIT MATT, WHY DID YOU OMIT THE CHANNEL COUNT?!
            // Also partially (like 1%) gonna blame NX, because documentation doesn't say anything about the second param
            // having to match the channel count on the server, or how many channels the client will feature too.
            // Don't hate me.
            m_ClientPeer = m_Client.Connect(clientAddress, m_ChannelDefinitions.Count);

            // Set the custom timeouts.
            if (m_UseCustomTimeout)
            {
                m_ClientPeer.Timeout(Library.throttleScale, m_BasePeerTimeout, m_BasePeerTimeout * m_BasePeerMultiplier);
            }

            // Debugging only
            if (m_TransportVerbosity > TransportVerbosity.Chatty)
            {
                Log($"Ignorance Transport: Hey ENET, is our client peer is setup? ENET: {m_ClientPeer.IsSet}.");
            }
        }

        /// <summary>
        /// Is the client connected currently?
        /// </summary>
        /// <returns>True if connected, False if not.</returns>
        public override bool ClientConnected()
        {
            if (m_TransportVerbosity >= TransportVerbosity.Paranoid)
            {
                Log($"Ignorance Transport: Mirror asks if I'm connected. The answer to that is { ((m_ClientPeer.State == PeerState.Connected) ? true : false) }. Note that if this a local client on the server instance, false may be a acceptable reply.");
            }
            return m_ClientPeer.IsSet && m_ClientPeer.State == PeerState.Connected;
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public override void ClientDisconnect()
        {
            if (m_TransportVerbosity >= TransportVerbosity.Paranoid)
            {
                Log($"Ignorance Transport: Client peer state before disconnect request fires: {m_ClientPeer.State}");
            }

            if (m_ClientPeer.State == PeerState.Disconnected)
            {
                // The client peer is already disconnected. Don't be dumb.
                return;
            }

            Log("Ignorance Transport: Received disconnection request from Mirror. Acknowledged!");

            // Disconnect the client's peer object, only if it's not disconnected. This might fix a bad pointer or something.
            // Reference: https://github.com/SoftwareGuy/Ignorance/issues/20
            if (m_ClientPeer.IsSet)
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Ignorance Transport: Disconnecting the client's peer...");
                if (m_ClientPeer.State != PeerState.Disconnected) m_ClientPeer.DisconnectNow(0);
            }

            if (IsValid(m_Client))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Ignorance Transport: Flushing and disposing of the client...");
                m_Client.Flush();
                m_Client.Dispose();
            }

            m_Client = null;
        }
        #endregion

        #region Transport - Outgoing client transmission functions
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

            if (!m_Client.IsSet)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return false;
            }

            if (channelId >= m_ChannelDefinitions.Count)
            {
                LogError($"ERROR: Refusing to even attempt to send data on channel {channelId}. It is either greater than or equal to the channel definition count." +
                    $"If you think this is a bug, consider filing a bug report.");
                return false;
            }

            if (m_TransportVerbosity == TransportVerbosity.LogSpam)
            {
                Log($"DEBUG: m_ChannelDefinitions[{channelId}] => { m_ChannelDefinitions[channelId] }");
            }

            mailingPigeon.Create(data.Array, data.Offset, data.Count, MapKnownChannelTypeToENETPacketFlag(m_ChannelDefinitions[channelId]));

            if (m_TransportVerbosity > TransportVerbosity.Chatty)
            {
                Log($"Ignorance Transport: Client sending byte {data.Count} payload on channel {channelId} to server...");
                if (m_TransportVerbosity == TransportVerbosity.Paranoid)
                {
                    Log($"Ignorance Transport: Outgoing payload to server:\n{BitConverter.ToString(data.Array, data.Offset, data.Count)}");
                }
            }

            if (m_ClientPeer.Send((byte)channelId, ref mailingPigeon))
            {
                return true;
            }
            else
            {
                LogWarning("Ignorance Transport: Outgoing packet sending wasn't successful. We might have disconnected or we're experiencing weirdness.");
                return false;
            }
        }
        #endregion

        #region Transport - Peer Management
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
        #endregion

        #region Transport - Server & Client Data Processing Functions
        /// <summary>
        /// Gets the maximum packet size allowed. Introduced from Mirror upstream git commit: 1289dee8. <para />
        /// Please see https://github.com/nxrighthere/ENet-CSharp/issues/33 for more information.
        /// </summary>
        /// <returns>A integer with the maximum packet size.</returns>
        public override int GetMaxPacketSize(int channel)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes. Do not attempt to send more, ENET will likely catch fire.
        }

        /// <summary>
        /// Deprecated, old "classic" server-side message processor. One message per LateUpdate tick.
        /// </summary>
        /// <returns>True if successful, False if unsuccessful.</returns>
        public bool OldServerMessageProcessor()
        {
            if (!ServerActive())
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
                {
                    LogError("Ignorance Transport: ProcessServerMessage was caught red-handed running when the server wasn't active.");
                }

                return false;
            }

            if (!m_Server.IsSet)
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport: Server is not ready.");
                return false;
            }

            int deadPeerConnID, knownConnectionID;
            int newConnectionID = serverConnectionCnt;
            Event incomingEvent;

            // Get the next message...
            m_Server.Service(0, out incomingEvent);

            // What type is this?
            switch (incomingEvent.Type)
            {
                // Connections (Normal peer connects)
                case EventType.Connect:
                    if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
                    {
                        Log($"Ignorance Transport: New connection with peer ID {incomingEvent.Peer.ID}, IP {incomingEvent.Peer.IP}");
                    }

                    // The peer object will allow us to do stuff with it later.
                    // Map them in our dictionaries
                    knownPeersToConnIDs.Add(incomingEvent.Peer, newConnectionID);
                    knownConnIDToPeers.Add(newConnectionID, incomingEvent.Peer);

                    if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
                    {
                        Log($"Ignorance Transport: New connection from IP {incomingEvent.Peer.ID}. Peer ID {incomingEvent.Peer.ID} mapped to internal connection ID {newConnectionID}");
                    }

                    // Increment the fake connection counter by one.
                    serverConnectionCnt++;

                    // If we're using custom timeouts, then set the timeouts too.
                    if (m_UseCustomTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, m_BasePeerTimeout, m_BasePeerTimeout * m_BasePeerMultiplier);

                    // Report back saying we got a connection event.
                    OnServerConnected.Invoke(newConnectionID);
                    break;

                // Disconnections (Normal peer disconnect and timeouts)
                case EventType.Disconnect:
                case EventType.Timeout:
                    if (m_TransportVerbosity > TransportVerbosity.Chatty)
                    {
                        Log($"Ignorance Transport: ServerGetNextMessage(): {(incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout")} event, peer ID {incomingEvent.Peer.ID}, IP {incomingEvent.Peer.IP}");
                    }
                    if (knownPeersToConnIDs.TryGetValue(incomingEvent.Peer, out deadPeerConnID))
                    {
                        Log($"Ignorance Transport: Acknowledging disconnection on connection ID {deadPeerConnID}");
                        PeerDisconnectedInternal(incomingEvent.Peer);
                        OnServerDisconnected.Invoke(deadPeerConnID);
                    }
                    break;

                case EventType.Receive:
                    if (m_TransportVerbosity > TransportVerbosity.Chatty)
                    {
                        Log($"Ignorance Transport: ServerGetNextMessage(): Channel {incomingEvent.ChannelID} receiving {incomingEvent.Packet.Length} byte payload");
                    }

                    // Only process data from known peers.
                    if (knownPeersToConnIDs.TryGetValue(incomingEvent.Peer, out knownConnectionID))
                    {
                        NewMessageDataProcessor(incomingEvent.Packet, true, knownConnectionID);
                    }
                    else
                    {
                        // Emit a warning and clean the packet. We don't want it in memory.
                        incomingEvent.Packet.Dispose();

                        if (m_TransportVerbosity > TransportVerbosity.Chatty)
                        {
                            LogWarning("Ignorance Transport WARNING: Discarded a packet because it was from a unknown peer. If you see this message way too many times then you " +
                                "are likely a victim of a DoS or DDoS attack that is targeting your server's connection port. Ignorance will keep discarding packets but please do " +
                                "look into this. Failing to do so is risky and could potentially crash the server instance!");
                        }
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
        /// New, "improved" server-side message processor. Multi messages per LateUpdate tick.
        /// </summary>
        /// <returns></returns>
        public bool NewServerMessageProcessor()
        {
            bool serverWasPolled = false;
            int deadPeerConnID, timedOutConnID, knownConnectionID;
            int newConnectionID = serverConnectionCnt;
            Event networkEvent;

            // Don't attempt to process anything if the server is not active.
            if (!ServerActive()) return false;

            // Only process messages if the server is valid.
            if (!IsValid(m_Server))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport: NewServerMessageProcessor() reports the server host object is not valid.");
                return false;
            }

            while (!serverWasPolled)
            {
                if (m_Server.CheckEvents(out networkEvent) <= 0)
                {
                    if (m_Server.Service(0, out networkEvent) <= 0)
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
                        knownConnIDToPeers.Add(serverConnectionCnt, networkEvent.Peer);

                        if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance Transport: Peer ID {networkEvent.Peer.ID} is now known as connection ID {serverConnectionCnt}.");
                        OnServerConnected.Invoke(serverConnectionCnt);

                        // Increment the connection counter.
                        serverConnectionCnt++;
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

        /// <summary>
        /// Processes a message's data payload.
        /// </summary>
        /// <param name="sourcePacket">The ENET Source packet.</param>
        /// <param name="serverInvoke">Is this intended to be invoked on the server instance?</param>
        /// <param name="connectionID">If it is intended to be invoked on the server, what connection ID to pass to Mirror?</param>
        public void NewMessageDataProcessor(Packet sourcePacket, bool serverInvoke = false, int connectionID = 0)
        {
            if (m_TransportVerbosity == TransportVerbosity.Paranoid) Log($"Ignorance Transport: Processing a {sourcePacket.Length} byte payload.");

            // This will be improved on at a later date.
            byte[] dataBuf = new byte[sourcePacket.Length];
            // Copy our data into our buffers from ENET Native -> Ignorance Transport Managed world.
            sourcePacket.CopyTo(dataBuf);
            sourcePacket.Dispose();

            if (m_TransportVerbosity == TransportVerbosity.LogSpam) Log($"Ignorance Transport: Oi Mirror, data's arrived! Packet payload:\n{ BitConverter.ToString(dataBuf) }");

            // Invoke the server if we're supposed to.
            if (serverInvoke)
            {
                OnServerDataReceived.Invoke(connectionID, dataBuf);
            }
            else
            {
                // Poke Mirror instead.
                OnClientDataReceived.Invoke(dataBuf);
            }
        }

        /// <summary>
        /// Deprecated, old "classic" server-side message processor. One message per LateUpdate tick.
        /// </summary>
        /// <param name="transportEvent">The transport event to report back to Mirror.</param>
        /// <param name="data">The byte array of the data.</param>
        /// <returns></returns>
        public bool OldClientMessageProcessor()
        {
            // The incoming Enet Event.
            Event incomingEvent;

            // Safety check: if the client isn't created, then we shouldn't do anything. ENet might be warming up.
            if (!IsValid(m_Client))
            {
                // LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return false;
            }

            // Get the next message...
            m_Client.Service(0, out incomingEvent);

            // Debugging only
            // if (verboseLoggingEnabled) Log($"ClientGetNextMessage event: {incomingEvent.Type}");

            switch (incomingEvent.Type)
            {
                // Peer connects.
                case EventType.Connect:
                    if (m_TransportVerbosity > TransportVerbosity.Chatty)
                    {
                        Log($"Ignorance Transport: ClientGetNextMessage() connect; real ENET peerID {incomingEvent.Peer.ID}, address {incomingEvent.Peer.IP}");
                    }
                    else
                    {
                        Log($"Ignorance Transport: Connection established with {incomingEvent.Peer.IP} !");
                    }

                    // If we're using custom timeouts, then set the timeouts too.
                    // 1.2.0+: TODO: Come back and check this out.
                    // if (useCustomPeerTimeout) incomingEvent.Peer.Timeout(Library.throttleScale, peerBaseTimeout, peerBaseTimeout * peerBaseTimeoutMultiplier);
                    OnClientConnected.Invoke();
                    break;

                // Peer disconnects/timeout.
                case EventType.Disconnect:
                case EventType.Timeout:
                    // TODO: Should timeouts be a client error?
                    if (m_TransportVerbosity > TransportVerbosity.Chatty)
                    {
                        Log($"Ignorance Transport: ClientGetNextMessage() {(incomingEvent.Type == EventType.Disconnect ? "disconnect" : "timeout")}, peerID {incomingEvent.Peer.ID}, address {incomingEvent.Peer.IP}");
                    }
                    else
                    {
                        LogWarning($"Ignorance Transport: Client encountered {(incomingEvent.Type == EventType.Disconnect ? "disconnection" : "timeout")}... Down we go!");
                    }
                    OnClientDisconnected.Invoke();
                    break;
                // Peer sends data to us.
                case EventType.Receive:
                    if (m_TransportVerbosity > TransportVerbosity.Chatty)
                    {
                        Log($"Ignorance Transport: Data channel {incomingEvent.ChannelID} receiving {incomingEvent.Packet.Length} byte payload...");
                    }
                    NewMessageDataProcessor(incomingEvent.Packet);
                    break;

                case EventType.None:
                    return false;
            }

            // We're done here. Bugger off.
            return true;
        }

        /// <summary>
        /// New "improved" client message processor.
        /// </summary>
        /// <returns>True if successful, False if not.</returns>
        public bool NewClientMessageProcessor()
        {
            if (!IsValid(m_Client) || m_ClientPeer.State == PeerState.Uninitialized)
            {
                return false;
            }

            bool clientWasPolled = false;
            Event networkEvent;

            // Only process messages if the client is valid.
            while (!clientWasPolled)
            {
                if (!IsValid(m_Client))
                {
                    if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance Transport: NewClientMessageProcessor() loop: client not valid.");
                    return false;
                }

                if (m_Client.CheckEvents(out networkEvent) <= 0)
                {
                    if (m_Client.Service(0, out networkEvent) <= 0) break;
                    clientWasPolled = true;
                }

                // Spam the logs if we're over paranoid levels.
                if (m_TransportVerbosity == TransportVerbosity.Paranoid) LogWarning($"Ignorance Transport: NewClientMessageProcessor() processing {networkEvent.Type} event...");

                switch (networkEvent.Type)
                {
                    default:
                    case EventType.None:
                        // Nothing happened.
                        break;
                    case EventType.Connect:
                        // Connected to a host/server.
                        if (m_TransportVerbosity > TransportVerbosity.Chatty)
                        {
                            Log($"Ignorance Transport: Connected to peer {networkEvent.Peer.IP} with our ENET Peer ID {networkEvent.Peer.ID}");
                        }
                        else
                        {
                            Log($"Ignorance Transport: Connection established! Host Peer IP: {networkEvent.Peer.IP}");
                        }

                        OnClientConnected.Invoke();
                        break;
                    case EventType.Disconnect:
                        // Client disconnected.
                        Log($"Ignorance Transport: Connection disconnected from Host Peer IP: {networkEvent.Peer.IP}");
                        OnClientDisconnected.Invoke();
                        break;
                    case EventType.Receive:
                        // Client recieving some data.
                        if (m_TransportVerbosity >= TransportVerbosity.Paranoid) Log($"Ignorance Transport: Client data channel {networkEvent.ChannelID} is receiving {networkEvent.Packet.Length} byte payload.");
                        // Don't panic, data processing and invoking is done in a new function.
                        NewMessageDataProcessor(networkEvent.Packet);
                        break;
                    case EventType.Timeout:
                        Log($"Ignorance Transport: Connection timeout while communicating with Host Peer IP {networkEvent.Peer.IP}");
                        OnClientDisconnected.Invoke();
                        break;
                }
            }
            // We're done here. Return.
            return true;
        }
        #endregion

        #region Transport - Finalization functions
        /// <summary>
        /// Shuts down the transport.
        /// </summary>
        public override void Shutdown()
        {
            Log("Ignorance Transport: Going down for shutdown NOW!");

            // Shutdown the client first.
            if (IsValid(m_Client))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty)
                {
                    Log("Sending the client process to the dumpster fire...");
                }
                if (m_ClientPeer.IsSet)
                {
                    m_ClientPeer.DisconnectNow(0);
                }

                m_Client.Flush();
                m_Client.Dispose();
            }

            // Shutdown the server.
            if (IsValid(m_Server))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Sending the server process to the dumpster fire...");

                m_Server.Flush();
                m_Server.Dispose();
            }

            Library.Deinitialize();
            Log("Ignorance Transport shutdown complete. Have a good one.");
        }
        #endregion

        #region Transport - Inherited functions from Mirror
        // Prettify the output string, rather than IgnoranceTransport (Mirror.IgnoranceTransport)
        public override string ToString()
        {
            return $"Ignorance {(ServerActive() ? (m_BindToAllInterfaces ? $"bound to all interfaces, port {m_Port}" : $"bound to {m_MyServerAddress}, port {m_Port}") : "inactive")}";
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
                    // note: we need to check enabled in case we set it to false
                    // when LateUpdate already started.
                    // (https://github.com/vis2k/Mirror/pull/379) 
                    while (enabled && OldServerMessageProcessor()) ;
                    while (enabled && OldClientMessageProcessor()) ;
                }
            }
        }

        // Sanity checks.
        private void OnValidate()
        {
            if (m_ChannelDefinitions.Count >= 2)
            {
                // Check to make sure that Channel 0 and 1 are correct.
                if(m_ChannelDefinitions[0] != KnownChannelTypes.Reliable)
                {
                    LogWarning("Ignorance Transport detected that channel 0 is not set to Reliable. This has been corrected.");
                    m_ChannelDefinitions[0] = KnownChannelTypes.Reliable;
                }

                if(m_ChannelDefinitions[1] != KnownChannelTypes.Unreliable)
                {
                    LogWarning("Ignorance Transport detected that channel 1 is not set to Unreliable. This has been corrected.");
                    m_ChannelDefinitions[1] = KnownChannelTypes.Unreliable;
                }
            }
            else
            {
                LogWarning("Ignorance Transport detected a configuration problem and will fix it for you. There needs to be at least 2 channels" +
                    " added at any time, and they must be Reliable and Unreliable.");

                m_ChannelDefinitions = new List<KnownChannelTypes>()
                {
                    KnownChannelTypes.Reliable,
                    KnownChannelTypes.Unreliable,
                };
            }
        }

        #endregion

        #region Transport - Statistics
        /// <summary>
        /// Server-world Packets Sent Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets sent.</returns>
        public uint ServerGetPacketSentCount()
        {
            return IsValid(m_Server) ? m_Server.PacketsSent : 0;
        }

        /// <summary>
        /// Server-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ServerGetPacketReceivedCount()
        {
            return IsValid(m_Server) ? m_Server.PacketsReceived : 0;
        }

        /// <summary>
        /// Server-world packets loss counter.
        /// This is buggy. Please use with caution.
        /// </summary>
        /// <returns>The amount of packets lost.</returns>
        public uint ServerGetPacketLossCount()
        {
            return IsValid(m_Server) && m_Server.PacketsSent >= m_Server.PacketsReceived ? m_Server.PacketsSent - m_Server.PacketsReceived : 0;
        }

        /// <summary>
        /// Client-world Packets Sent Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets sent.</returns>
        public uint ClientGetPacketSentCount()
        {
            return IsValid(m_Client) ? m_Client.PacketsSent : 0;
        }

        /// <summary>
        /// Client-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ClientGetPacketReceivedCount()
        {
            return IsValid(m_Client) ? m_Client.PacketsReceived : 0;
        }

        /// <summary>
        /// Get the client's packet loss count, directly from ENET.
        /// </summary>
        /// <returns></returns>
        public ulong ClientGetPacketLossCount()
        {
            return m_ClientPeer.IsSet ? m_ClientPeer.PacketsLost : 0;
        }
        #endregion

        #region Transport - Message Loggers
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
        #endregion

        #region Transport - Toolbox
        /// <summary>
        /// Checks if a host object is valid.
        /// </summary>
        /// <param name="host">The host object to check.</param>
        /// <returns>True if valid, false if not.</returns>
        private static bool IsValid(Host host)
        {
            return host != null && host.IsSet;
        }

        /// <summary>
        /// </summary>        
        public PacketFlags MapKnownChannelTypeToENETPacketFlag(KnownChannelTypes source)
        {
            switch (source)
            {
                case KnownChannelTypes.Reliable:
                    return PacketFlags.Reliable;            // reliable (tcp-like).
                case KnownChannelTypes.Unreliable:
                    return PacketFlags.Unsequenced;         // completely unreliable.
                case KnownChannelTypes.UnreliableFragmented:
                    return PacketFlags.UnreliableFragment;  // unreliable fragmented.
                case KnownChannelTypes.UnreliableSequenced:
                    return PacketFlags.None;                // unreliable, but sequenced.
                default:
                    return PacketFlags.Unsequenced;
            }
        }
        #endregion

        #region Transport - Custom Classes
        public enum TransportVerbosity
        {
            SilenceIsGolden,
            Chatty,
            Paranoid,
            LogSpam
        }

        public class TransportInfo
        {
            public const string Version = "1.2.1 In-Development";
        }
        #endregion

        [Serializable]
        public enum KnownChannelTypes
        {
            Reliable,
            Unreliable,
            UnreliableFragmented,
            UnreliableSequenced,
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
