// ----------------------------------------
// Ignorance by Matt Coburn, 2018 - 2019
// This Transport uses other dependencies that you can
// find references to in the README.md of this package.
// ----------------------------------------
// Ignorance is MIT Licensed. It would be however
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
#if !IGNORANCE_NO_UPNP
using Open.Nat;
using System.Threading;
#endif

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

#if !IGNORANCE_NO_UPNP
#if UNITY_EDITOR
        [Rename("UPnP Enabled")]
        [Tooltip("Should the client attempt to port forward automatically?")]
#endif
        public bool m_ServerUPNPEnabled = false;
#if UNITY_EDITOR
        [Rename("UPnP Detection Timeout")]
        [Tooltip("How long should the server attempt to look for a NAT device for? (milliseconds)")]
#endif
        public int m_ServerUPNPTimeout = 10000;

#if UNITY_EDITOR
        [Rename("UPnP Rule Description")]
        [Tooltip("Keep this short and sweet, some routers have limited UPnP memory.")]
#endif
        public string m_ServerUPNPMappingDescription = "Ignorance";
#if UNITY_EDITOR
        [Rename("UPnP Requestor IP Address")]
        [Tooltip("This will need to be the server's IP.")]
#endif
        public string m_ServerUPNPIpAddress = string.Empty;
#if UNITY_EDITOR
        [Rename("UPnP Rule Lifetime")]
        [Tooltip("How long will this rule last? (seconds?)")]
#endif
        public int m_ServerUPNPRuleLifetime = 600;   // 1 hour?

        public bool m_CustomPacketBufferSize = false;
        public int m_PacketBufferSizeInKB = 64;

        private bool m_HasAlreadyConfiguredNat = false;
        private NatDiscoverer m_NATDiscoverer = null;
        private NatDevice m_NATDevice = null;
        private Mapping m_NewRuleMapping = null;
#endif
        #endregion

        #region Transport-level references, dictonaries, etc.
        // Explicitly give these new references on startup, just to make sure that we get no null reference exceptions.
        private Host m_Server = new Host();
        private Host m_Client = new Host();

        private Address m_ServerAddress = new Address();
        private Peer m_ClientPeer = new Peer();

        private string m_MyServerAddress = string.Empty;

        // Managed cache for incoming packets, size is max theoretical size for UDP packets
        private byte[] packetCache = new byte[65535];

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
            Log($"Thank you for using Ignorance v{TransportInfo.Version} for Mirror! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance. ENET Library Version: {Library.version}");
        }

        private void Awake()
        {
            GreetEveryone();

#if UNITY_EDITOR_OSX
            Debug.LogWarning("Hmm, looks like you're using Ignorance inside a Mac Editor instance. This is known to be problematic due to some Unity Mono bugs. " +
                "If you have issues using Ignorance, please try the Unity 2019.1 beta and let the developer know. Thanks!");
#endif
            // If the user sets this to -1, treat it as no limit.
            if (m_MaximumTotalConnections < 0) m_MaximumTotalConnections = 0;

            // 1.2.7: If we're using a custom packet buffer size, initialize it.
            if (m_CustomPacketBufferSize)
            {
                packetCache = new byte[m_PacketBufferSizeInKB * 1024];
                Log($"Ignorance: Initialized packet cache. Capacity: {packetCache.Length} byte.");
            }
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
            // 1.2.5
            if (!AlreadyInitialized)
            {
                if (!InitializeENET()) {
                    LogError("Ignorance FATAL ERROR: ENET-senpai won't initialize. Did someone put a bad native library in place of the one that should have shipped with this Transport?");
                    return;
                } else {
                    Log("Ignorance: ENET Initialized");
                    AlreadyInitialized = true;
                }
            }
            

            // Do not attempt to start more than one server.
            // Check if the server is active before attempting to create. If it returns true,
            // then we should not continue, and we'll emit a refusal error message.
            // This should be classified as a dirty hack and if it doesn't work then well, shit.
            if (ServerActive())
            {
                LogError("Ignorance: Refusing to start another server instance! There's already one running.");
                return;
            }

            // Make sure we're not trying to overflow the channel counts.
            if ((m_ChannelDefinitions.Count - 1) >= 255)
            {
                LogError("Ignorance: Too many channels. ENET-senpai can't handle them!");
                return;
            }

            // Prevent dirty memory issues later.
            m_Server = new Host();
            m_ServerAddress = new Address();
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

#if UNITY_EDITOR_OSX
            if(m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) {
                Log($"Ignorance: Server startup in MacOS Editor workaround mode on port {m_Port}");
            }

            LogWarning("Ignorance: Binding to a specific address is disabled on MacOS Editor due to some bugs. Please refer to https://github.com/nxrighthere/ENet-CSharp/issues/46 " +
                "for technical details. While you can disable this check, it will most likely bug out and mess connectivity up. You've been warned.");
            Log("Ignorance: Binding to ::0 as a workaround for Mac OS LAN Host");
            m_ServerAddress.SetHost("::0");
#else
            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
            {
                Log($"Ignorance: Server startup on port {m_Port}");
            }
            if (m_BindToAllInterfaces)
            {
                Log("Ignorance: Binding to all available interfaces.");
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
                    Log($"Ignorance: Using {networkAddress} as our specific bind address");
                    m_ServerAddress.SetHost(networkAddress);
                    m_MyServerAddress = networkAddress;
                }
                else if (!string.IsNullOrEmpty(m_BindToAddress))
                {
                    Log($"Ignorance: Using {m_BindToAddress} as our specific bind address");
                    m_ServerAddress.SetHost(m_BindToAddress);
                }
                else
                {
                    // No. Just no. Go back and try again, do not pass Go, do not collect $200.
                    LogError($"Ignorance: No bind address specified and you have disabled bind to all interfaces. Please go back and fix this, then start the server again.");
                    return;
                }
            }
#endif
            // Setup the port.
            m_ServerAddress.Port = port;

            // Finally create the server.
            m_Server.Create(m_ServerAddress, m_MaximumTotalConnections, m_ChannelDefinitions.Count);

            // WILL BE REMOVED IN 1.2.8
            if (m_UseLZ4Compression)
            {
                Log("Ignorance: Server instance will use LZ4 Compression. If you get random client disconnections, PLEASE FILE A BUG WITH A REPO PROJECT!");
                m_Server.EnableCompression();
            }

            // Log our best effort attempts
            Log($"Ignorance: Attempted to create server on UDP port {m_Port}. If Ignorance immediately crashes after this line, please file a bug report on the GitHub.");

#if !IGNORANCE_NO_UPNP
            if (m_ServerUPNPEnabled && !m_HasAlreadyConfiguredNat)
            {
                Log("Ignorance: Dispatching the UPnP Port Forwarder.");
                DoServerPortForwarding();
            }
#endif
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
                Log("Ignorance: ServerStop()");
            }

            // This might be slow if we have lots of CCU.
            foreach (KeyValuePair<int, Peer> entry in knownConnIDToPeers) entry.Value.DisconnectNow(0);

            // Cleanup in the connection isle.
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

            if (IsValid(m_Server)) m_Server.Dispose();

            m_Server = null;

            Log("Ignorance: Server stopped.");
        }

        /// <summary>
        /// Get a peer from their associated connection ID
        /// </summary>
        /// <param name="connectionID">The id of the Peer's connection</param>
        /// <returns>The Peer</returns>
        public Peer GetPeerByConnection(int connectionID)
        {
            return knownConnIDToPeers[connectionID];
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
            mailingPigeon.Create(data.Array, data.Offset, data.Count + data.Offset, MapKnownChannelTypeToENETPacketFlag(m_ChannelDefinitions[channelId]));

            // More haxx. see https://github.com/nxrighthere/ENet-CSharp/issues/21 for some background info-ish.
            Peer target;
            if (knownConnIDToPeers.TryGetValue(connectionId, out target))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty)
                {
                    Log($"Ignorance: Server sending {data.Count} byte data on channel {channelId} to connection ID {connectionId}");
                    if (m_TransportVerbosity == TransportVerbosity.LogSpam)
                    {
                        Log($"Ignorance: Outgoing payload to connection {connectionId} on channel {channelId}\n{BitConverter.ToString(data.Array, data.Offset, data.Count)}");
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
                        LogWarning($"Ignorance: Send failure to connection ID {connectionId} on channel {channelId} ({(byte)channelId})");
                    }
                    return false;
                }
            }
            else
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty)
                {
                    LogWarning($"Ignorance: Send failure to connection ID {connectionId} on channel {channelId} ({(byte)channelId}). This could happen randomly due to the nature of UDP.");
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
            // 1.2.5
            if (!AlreadyInitialized)
            {
                if (!InitializeENET())
                {
                    Debug.LogError("Ignorance FATAL ERROR: ENET-senpai won't initialize. Did someone put a bad native library in place of the one that should have shipped with this Transport?");
                    return;
                }
                else
                {
                    Debug.Log("Ignorance: ENET Initialized");
                    AlreadyInitialized = true;
                }
            }

            // Make sure we're not trying to overflow the channel counts.
            if ((m_ChannelDefinitions.Count - 1) >= 255)
            {
                LogError("Ignorance: Too many channels. ENET-senpai can't handle them!");
                return;
            }

            Log($"Ignorance: Acknowledging connection request to {address}:{m_Port}");

            // TODO: Recycle clients?
            if (m_Client == null) m_Client = new Host();
            if (!m_Client.IsSet) m_Client.Create(null, 1, m_ChannelDefinitions.Count);

            // WILL BE REMOVED IN 1.2.8
            if (m_UseLZ4Compression)
            {
                Log("Ignorance: Client will use LZ4 Compression. If you get random disconnections, PLEASE FILE A BUG WITH A REPO PROJECT!");
                m_Client.EnableCompression();
            }

            Address clientAddress = new Address();

            // Set hostname and port to connect to.
            clientAddress.SetHost(address);
            clientAddress.Port = m_Port;

            // Connect the client to the server.
            m_ClientPeer = m_Client.Connect(clientAddress, m_ChannelDefinitions.Count);

            // Set the custom timeouts.
            if (m_UseCustomTimeout)
            {
                m_ClientPeer.Timeout(Library.throttleScale, m_BasePeerTimeout, m_BasePeerTimeout * m_BasePeerMultiplier);
            }

            // Debugging only
            if (m_TransportVerbosity >= TransportVerbosity.Paranoid)
            {
                Log($"Ignorance: Hey ENET, is our client peer is setup? ENET: {m_ClientPeer.IsSet}.");
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
                Log($"Ignorance: Mirror asks if I'm connected. The answer to that is { ((m_ClientPeer.State == PeerState.Connected) ? true : false) }. Note that if this a local client on the server instance, false may be a acceptable reply.");
            }
            return m_ClientPeer.IsSet && m_ClientPeer.State == PeerState.Connected;
        }

        /// <summary>
        /// Disconnect the client.
        /// </summary>
        public override void ClientDisconnect()
        {
            // Bug fix by c6burns as per the Discord server
            // ENET-CSharp doesn't track peers and does not guard against calls to Peer.Disconnect even if the array
            // has been freed using enet_host_destroy
            if (!IsValid(m_Client)) return; // c6: don't reference into peer without host

            if (m_TransportVerbosity == TransportVerbosity.Paranoid)
            {
                Log($"Ignorance: Client peer state before disconnect request fires: {m_ClientPeer.State}");
            }

            Log("Ignorance: Received disconnection request from Mirror. Acknowledged!");

            // Disconnect the client's peer object, only if it's not disconnected. This might fix a bad pointer or something.
            // Reference: https://github.com/SoftwareGuy/Ignorance/issues/20
            if (m_ClientPeer.IsSet)
            {
                m_ClientPeer.DisconnectNow(0);
            }

            if (IsValid(m_Client))
            {
                if (m_TransportVerbosity > TransportVerbosity.Chatty) Log("Ignorance: Flushing and disposing of the client...");
                m_Client.Flush();
                m_Client.Dispose();
            }

            // TODO: Don't null this, recycle the client?
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
            // 1.2.7: Never send any data if we're not initialized
            if(!AlreadyInitialized)
            {
                // 1.2.7.1: Shush, you!
                // LogError("Ignorance: Attempted to ClientSend when we're not initialized! (Please report this as a bug with a repro project. Might be Mirror being weird.)");
                return false;
            }

            Packet mailingPigeon = default;

            if (!m_Client.IsSet)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogWarning("Ignorance: Hold on, the client is not ready yet.");
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

            mailingPigeon.Create(data.Array, data.Offset, data.Count + data.Offset, MapKnownChannelTypeToENETPacketFlag(m_ChannelDefinitions[channelId]));

            if (m_TransportVerbosity > TransportVerbosity.Chatty)
            {
                Log($"Ignorance: Client sending byte {data.Count} payload on channel {channelId} to server...");
                if (m_TransportVerbosity == TransportVerbosity.Paranoid)
                {
                    Log($"Ignorance: Outgoing payload to server:\n{BitConverter.ToString(data.Array, data.Offset, data.Count)}");
                }
            }

            if (m_ClientPeer.Send((byte)channelId, ref mailingPigeon))
            {
                return true;
            }
            else
            {
                LogWarning("Ignorance: Outgoing packet sending wasn't successful. We might have disconnected or we're experiencing weirdness.");
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
        /// New, "improved" server-side message processor. Multi messages per LateUpdate tick.
        /// </summary>
        /// <returns></returns>
        public bool ProcessServerMessages()
        {
            // 1.2.7: Never send any data if we're not initialized
            // 1.2.7.1: I cucked this one up, I'm sorry
            if (!AlreadyInitialized)
            {
                // 1.2.7.1: Shush, you!
                // LogError("Ignorance: Attempted to ProcessServerMessages when we're not initialized! (Please report this as a bug with a repro project. Might be Mirror being weird.)");
                return false;
            }

            bool serverWasPolled = false;
            int deadPeerConnID, timedOutConnID, knownConnectionID;
            int newConnectionID = serverConnectionCnt;
            // Event networkEvent;

            // Don't attempt to process anything if the server is not active.
            if (!ServerActive()) return false;

            // Only process messages if the server is valid.
            if (!IsValid(m_Server)) return false;

            while (!serverWasPolled)
            {
                if (m_Server.CheckEvents(out Event networkEvent) <= 0)
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
                        if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance: New client connection to server. Address: {networkEvent.Peer.IP}:{networkEvent.Peer.Port}. (ENET Peer ID: {networkEvent.Peer.ID})");

                        // Map them into our dictonaries.
                        knownPeersToConnIDs.Add(networkEvent.Peer, newConnectionID);
                        knownConnIDToPeers.Add(serverConnectionCnt, networkEvent.Peer);

                        if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance: Peer ID {networkEvent.Peer.ID} becomes connection ID {serverConnectionCnt}.");
                        if (m_UseCustomTimeout) networkEvent.Peer.Timeout(Library.throttleScale, m_BasePeerTimeout, m_BasePeerTimeout * m_BasePeerMultiplier);

                        OnServerConnected.Invoke(serverConnectionCnt);

                        // Increment the connection counter.
                        serverConnectionCnt++;
                        break;
                    case EventType.Disconnect:
                        // A client disconnected.

                        if (knownPeersToConnIDs.TryGetValue(networkEvent.Peer, out deadPeerConnID))
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance: Connection ID {knownPeersToConnIDs[networkEvent.Peer]} ({networkEvent.Peer.IP}:{networkEvent.Peer.Port}) has disconnected.");
                            OnServerDisconnected.Invoke(deadPeerConnID);
                            PeerDisconnectedInternal(networkEvent.Peer);
                        }
                        else
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogWarning($"Ignorance: Unknown Peer with ID {networkEvent.Peer.ID} ({networkEvent.Peer.IP}:{networkEvent.Peer.Port}) has disconnected. Hmm...");
                        }
                        break;
                    case EventType.Receive:
                        if (m_TransportVerbosity > TransportVerbosity.Chatty) Log($"Ignorance: Server data channel {networkEvent.ChannelID} receiving a {networkEvent.Packet.Length} byte payload");

                        // Only process data from known peers.
                        if (knownPeersToConnIDs.TryGetValue(networkEvent.Peer, out knownConnectionID))
                        {
                            ProcessMessageData(networkEvent.Packet, true, knownConnectionID);
                        }
                        else
                        {
                            // Emit a warning and clean the packet. We don't want it in memory.
                            networkEvent.Packet.Dispose();

                            if (m_TransportVerbosity > TransportVerbosity.Chatty) LogWarning("Ignorance: Discarded a packet because it was from a unknown peer. " +
                                "If you see this message way too many times then you are likely a victim of a (D)DoS attack that is targeting your server connection port." +
                                " Ignorance will keep discarding packets but please do look into this. Failing to do so is risky and could potentially crash the server instance!");
                        }
                        break;
                    case EventType.Timeout:
                        // A client timed out. Note that this could be the same as the disconnected, but for now I'm going to seperate them for debugging reasons
                        if (knownPeersToConnIDs.TryGetValue(networkEvent.Peer, out timedOutConnID))
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance: Connection ID {knownPeersToConnIDs[networkEvent.Peer]} has timed out.");
                            OnServerDisconnected.Invoke(timedOutConnID);
                            PeerDisconnectedInternal(networkEvent.Peer);
                        }
                        else
                        {
                            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) LogWarning($"Ignorance: Unknown Peer with ID {networkEvent.Peer.ID} has timed out. Hmm...");
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
        public void ProcessMessageData(Packet sourcePacket, bool serverInvoke = false, int connectionID = 0)
        {
            if (m_TransportVerbosity == TransportVerbosity.Paranoid) Log($"Ignorance: Processing a {sourcePacket.Length} byte payload.");

            // 1.2.7: Drop if bigger than our buffer. Security risk otherwise.
            if(sourcePacket.Length > packetCache.Length)
            {
                LogError($"Ignorance: Dropping incoming packet. Packet size is {sourcePacket.Length} byte, our buffer is {packetCache.Length} byte. We could be under attack.");
                sourcePacket.Dispose();
            }

            // Copy umanaged buffer into local managed packet buffer
            sourcePacket.CopyTo(this.packetCache);
            int spLength = sourcePacket.Length;

            if (m_TransportVerbosity == TransportVerbosity.LogSpam) Log($"Ignorance: Packet payload:\n{ BitConverter.ToString(packetCache, 0, spLength) }");

            if (serverInvoke)
            {
                // Invoke the server if we're supposed to.
                OnServerDataReceived.Invoke(connectionID, new ArraySegment<byte>(this.packetCache, 0, spLength));
            }
            else
            {
                // Poke Mirror client instead.
                OnClientDataReceived.Invoke(new ArraySegment<byte>(this.packetCache, 0, spLength));
            }
        }

        /// <summary>
        /// New "improved" client message processor.
        /// </summary>
        /// <returns>True if successful, False if not.</returns>
        public bool ProcessClientMessages()
        {
            // 1.2.7: Never send any data if we're not initialized
            if (!AlreadyInitialized)
            {
                // 1.2.7.1: Silence, you!
                // LogError("Ignorance: Attempted to ProcessClientMessages when we're not initialized! (Please report this as a bug with a repro project. Might be Mirror being weird.)");
                return false;
            }

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
                    // 1.2.5: Change this to only LogSpam setting.
                    if (m_TransportVerbosity == TransportVerbosity.LogSpam) LogWarning("Ignorance: ProcessClientMessages() loop: client not valid.");
                    return false;
                }

                if (m_Client.CheckEvents(out networkEvent) <= 0)
                {
                    if (m_Client.Service(0, out networkEvent) <= 0) break;
                    clientWasPolled = true;
                }

                // Spam the logs if we're over paranoid levels.
                if (m_TransportVerbosity == TransportVerbosity.Paranoid) LogWarning($"Ignorance: ProcessClientMessages() processing {networkEvent.Type} event...");

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
                            Log($"Ignorance: Connected to peer {networkEvent.Peer.IP} with our ENET Peer ID {networkEvent.Peer.ID}");
                        }
                        else
                        {
                            Log($"Ignorance: Connection established! Host Peer IP: {networkEvent.Peer.IP}");
                        }

                        OnClientConnected.Invoke();
                        break;
                    case EventType.Disconnect:
                        // Client disconnected.
                        Log($"Ignorance: Connection disconnected from Host Peer IP: {networkEvent.Peer.IP}");
                        OnClientDisconnected.Invoke();
                        break;
                    case EventType.Receive:
                        // Client recieving some data.
                        if (m_TransportVerbosity >= TransportVerbosity.Paranoid) Log($"Ignorance: Client data channel {networkEvent.ChannelID} is receiving {networkEvent.Packet.Length} byte payload.");
                        // Don't panic, data processing and invoking is done in a new function.
                        ProcessMessageData(networkEvent.Packet);
                        break;
                    case EventType.Timeout:
                        Log($"Ignorance: Connection timed out while communicating with Host Peer IP {networkEvent.Peer.IP}");
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
            Log("Ignorance: Going down for shutdown NOW!");
#if !IGNORANCE_NO_UPNP
            // If NAT is configured, delete the port forwarding rule.
            if (m_HasAlreadyConfiguredNat)
            {
                if (m_NATDevice != null)
                {
                    if (m_NewRuleMapping != null)
                    {
                        try
                        {
                            Log("Ignorance: Deleting automatic port mapping from UPnP device");
                            m_NATDevice.DeletePortMapAsync(m_NewRuleMapping);
                        }
                        catch (Exception ex)
                        {
                            LogError($"Ignorance: Unable to delete port mapping, exception returned was: {ex.ToString()}");
                        }

                    }
                }
            }
#endif
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

            Log("Ignorance: Deinitializing ENET.");
            DeinitializeENET();
            Log("Ignorance: Shutdown complete. Have a good one.");
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
                ProcessServerMessages();
                ProcessClientMessages();
            }
        }

        // Sanity checks.
        private void OnValidate()
        {
            if (m_ChannelDefinitions.Count >= 2)
            {
                // Check to make sure that Channel 0 and 1 are correct.
                if (m_ChannelDefinitions[0] != KnownChannelTypes.Reliable)
                {
                    LogWarning("Ignorance detected that channel 0 is not set to Reliable. This has been corrected.");
                    m_ChannelDefinitions[0] = KnownChannelTypes.Reliable;
                }

                if (m_ChannelDefinitions[1] != KnownChannelTypes.Unreliable)
                {
                    LogWarning("Ignorance detected that channel 1 is not set to Unreliable. This has been corrected.");
                    m_ChannelDefinitions[1] = KnownChannelTypes.Unreliable;
                }
            }
            else
            {
                LogWarning("Ignorance detected a configuration problem and will fix it for you. There needs to be at least 2 channels " +
                    "added at any time, and they must be Reliable and Unreliable channel types respectively.");

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
            return (PacketFlags)source;
        }
        #endregion

        #region Transport - Custom
        public enum TransportVerbosity
        {
            SilenceIsGolden,
            Chatty,
            Paranoid,
            LogSpam
        }

        public class TransportInfo
        {
            public const string Version = "1.2.7.1";
        }

        [Serializable]
        public enum KnownChannelTypes
        {
            Reliable = PacketFlags.Reliable,
            ReliableUnsequenced = PacketFlags.Reliable | PacketFlags.Unsequenced,
            Unreliable = PacketFlags.Unsequenced,
            UnreliableFragmented = PacketFlags.UnreliableFragment,
            UnreliableSequenced = PacketFlags.None
        }
        #endregion

        #region UPnP - Automatic port forwarding
#if !IGNORANCE_NO_UPNP
        public async void DoServerPortForwarding()
        {
            if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
            {
                Log("Ignorance: Server Mode will attempt to automatically port forward.");
            }

            try
            {
                // Setup the NAT Discovery system...
                m_NATDiscoverer = new NatDiscoverer();
                CancellationTokenSource cts = new CancellationTokenSource(m_ServerUPNPTimeout);
                // Hello, router? It's-a me, Ignorance!
                m_NATDevice = await m_NATDiscoverer.DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts);
                // Get the external IP address...
                System.Net.IPAddress externalIP = await m_NATDevice.GetExternalIPAsync();
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance: Seems our external IP is {externalIP.ToString()}. Asking router to map local port {m_Port} to {externalIP.ToString()}:{m_Port}");
                // Ask router-senpai very nicely for her to port map. Gotta be nice, or she may crash.
                // Crappy devices require somewhat crappy workarounds.
                if (!string.IsNullOrEmpty(m_ServerUPNPIpAddress))
                {
                    // This is used on OpenWRT devices with miniUPnPd, since it will throw Bad Args if you don't give the requesting IP address.
                    if (System.Net.IPAddress.TryParse(m_ServerUPNPIpAddress, out System.Net.IPAddress requestIP))
                    {
                        m_NewRuleMapping = new Mapping(Protocol.Udp, requestIP, m_Port, m_Port, m_ServerUPNPRuleLifetime, m_ServerUPNPMappingDescription);
                        await m_NATDevice.CreatePortMapAsync(m_NewRuleMapping);
                    }
                    else
                    {
                        throw new System.FormatException("Server UPnP IP Address is invalid. Can't continue with UPnP Port Mapping.");
                    }
                }
                else
                {
                    // This might work for some el cheapo chinese router-senpais.
                    // They may not be as up to date with open source implementations.
                    m_NewRuleMapping = new Mapping(Protocol.Udp, m_Port, m_Port, m_ServerUPNPRuleLifetime, m_ServerUPNPMappingDescription);
                    await m_NATDevice.CreatePortMapAsync(m_NewRuleMapping);
                }

                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden) Log($"Ignorance: Seems that UPnP mapping went all according to plan... unless you got an exception. Which in that case, I can't do anything about that.");
                // Don't bother trying to do it again this server session.
                m_HasAlreadyConfiguredNat = true;
            }
            catch (NatDeviceNotFoundException)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
                {
                    LogError($"Ignorance: Sorry, automatic port fowarding has failed. The exception returned was: NAT Device Not Found (do you have a router with UPnP disabled?)");
                }
            }
            catch (MappingException ex)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
                {
                    LogError($"Ignorance: Sorry, automatic port fowarding has failed. The exception returned was: NAT Device rejected the UPnP request. {ex}");
                }
            }
            catch (Exception ex)
            {
                if (m_TransportVerbosity > TransportVerbosity.SilenceIsGolden)
                {
                    LogError($"Ignorance: Sorry, automatic port fowarding has failed. The exception returned was: Unknown Exception: {ex}");
                }
            }
        }
#endif
        #endregion

        public ushort port { get { return m_Port; } set { m_Port = value; } }   // Backwards compatibility.
        public string Version { get { return TransportInfo.Version; } }

        public ulong ClientGetBytesSentCount()
        {
            return m_ClientPeer.IsSet ? m_ClientPeer.BytesSent : 0;
        }

        public ulong ClientGetBytesReceivedCount()
        {
            return m_ClientPeer.IsSet ? m_ClientPeer.BytesReceived : 0;
        }

        private bool AlreadyInitialized = false;

        private bool InitializeENET()
        {
            if (AlreadyInitialized) return true;
            return Library.Initialize();
        }

        private void DeinitializeENET()
        {
            if (!AlreadyInitialized) return;
            Library.Deinitialize();
        }

    }
}
