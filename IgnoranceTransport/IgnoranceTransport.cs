// SoftwareGuy's Ignorance Reliable UDP Transport
// Uses ENET as the transport backend.
// ----------------------------------------
// Ignorance Transport by Coburn (aka SoftwareGuy), 2018
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
// THIS IS THE MIRROR 2018 BRANCH OF IGNORANCE.
// DO NOT USE IF YOU ARE USING UNITY 2017 LTS.
// ----------------------------------------
using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityAsync;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Mirror.Transport
{
    /// <summary>
    /// Ignorance rUDP Transport is built upon the ENet-C# wrapper by nxrighthere.
    /// </summary>
    public class IgnoranceTransport : TransportLayer
    {
        // -- GENERAL VARIABLES -- //
        private const string TransportVersion = "2018-1.1.3";
        private bool libraryInitialized = false;

        // -- EXPOSED PUBLIC VARIABLES -- //
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
        private Address clientAddress = new Address();
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

        // -- IGNORANCE 2018 ASYNC ACTIONS -- //

        // Client
        public event Action OnClientConnect;
        public event Action<byte[]> OnClientData;
        public event Action<Exception> OnClientError;
        public event Action OnClientDisconnect;
        // Server
        public event Action<int> OnServerConnect;
        public event Action<int, byte[]> OnServerData;
        public event Action<int, Exception> OnServerError;
        public event Action<int> OnServerDisconnect;


        // -- INITIALIZATION -- // 
        private readonly string address;
        private readonly ushort port;
        private readonly ushort maxConnections;

        /// <summary>
        /// Initializes transport
        /// </summary>
        /// <param name="address">Server bind address</param>
        /// <param name="port">Server port</param>
        /// <param name="maxConnections">Connection limit (can't be higher than 4095)</param>
        public IgnoranceTransport(string address, ushort port, ushort maxConnections)
        {
            this.address = address;
            this.port = port;
            this.maxConnections = maxConnections;

            Log($"Thank you for using Ignorance Transport v{TransportVersion} for Mirror 2018! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance. \nENET Library Version: {Library.version}");

            Library.Initialize();
        }

        /// <summary>
        /// Initializes transport with custom channel list (should only be used if you directly use Send with channelid, for example with voice chat assets)
        /// </summary>
        /// <param name="address">Server bind address</param>
        /// <param name="port">Server port</param>
        /// <param name="maxConnections">Connection limit (can't be higher than 4095)</param>
        /// <param name="channelTypes">Channel list (specifies channel types). Element 0 is used in all Mirror code</param>
        public IgnoranceTransport(string address, ushort port, ushort maxConnections, IEnumerable<PacketFlags> channelTypes)
        {
            this.address = address;
            this.port = port;
            this.maxConnections = maxConnections;
            packetSendMethods = channelTypes.ToArray();

            Log($"Thank you for using Ignorance Transport v{TransportVersion} for Mirror 2018! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance. \nENET Library Version: {Library.version}");

            Library.Initialize();
        }

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

        public override string ToString()
        {
            if (ServerActive())
            {
                return $"Ignorance Server {address}:{port}";
            }

            if (ClientConnected())
            {
                return $"Ignorance Client {address}:{port}";
            }

            if (clientPeer.IsSet && clientPeer.State == PeerState.Connecting)
            {
                return $"Ignorance connecting to {address}:{port}";
            }

            return "Ignorance Transport idle";
        }

        // -- CLIENT WORLD -- //
        public virtual bool ClientConnected()
        {
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
        }

        public virtual void ClientConnect(string address, int port)
        {
            Log(clientPeer.State);

            // Setup our references.
            if (client == null) client = new Host();
            if (!client.IsSet) client.Create(null, 1, packetSendMethods.Length, 0, 0);

            clientAddress = new Address();
            clientAddress.SetHost(address);
            clientAddress.Port = (ushort)port;

            // Connect the client to the server by setting the address and start the client's data pump loop.
            Log($"Ignorance Transport: Client will attempt connection to server {address}:{port}");
            Log("Ignorance Transport: Starting client receive loop...");
            clientPeer = client.Connect(clientAddress);
            ClientReceiveLoop(client);
        }

        // Could something in here be messing up and causing a hang?
        // Hang as in 3 - 5 second freeze then connection actually happens.
        // Or is it a unity bug?
        private async void ClientReceiveLoop(Host clientHost)
        {
            try
            {
                while (clientHost != null && clientHost.IsSet && clientPeer.IsSet)
                {
                    Event incomingEvent;
                    // Get the next message from the client peer object.
                    clientHost.Service(0, out incomingEvent);
                    switch (incomingEvent.Type)
                    {
                        case EventType.Connect:
                            Log($"Ignorance Transport: Successfully connected to peer located at {incomingEvent.Peer.IP}");
                            OnClientConnect?.Invoke();
                            break;
                        case EventType.Disconnect:
                            // Nothing to do here, break the loop.
                            return;
                        case EventType.Timeout:
                            throw new TimeoutException("Ignorance Transport: Our peer connection timed out.");
                        case EventType.None:
                            // Nothing is happening, so we need to wait until the next frame.
                            await Await.NextUpdate();
                            break;
                        case EventType.Receive:
                            // Data coming in.
                            byte[] data = new byte[incomingEvent.Packet.Length];
                            incomingEvent.Packet.CopyTo(data);
                            incomingEvent.Packet.Dispose();
                            OnClientData?.Invoke(data);
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                // Something went wrong - just like Windows 10.
                LogError($"Client exception caught: {exception}");
                OnClientError?.Invoke(exception);
            }
            finally
            {
                // We disconnected, got fed an error message or we got a Disconnect.
                clientHost?.Flush();
                OnClientDisconnect?.Invoke();
                clientPeer = new Peer();
                Log("Ignorance Transport: Client receive loop finished at " + Time.time);
            }
        }

        public virtual void ClientSend(int channelId, byte[] data)
        {
            Packet mailingPigeon = default(Packet);

            if (!client.IsSet)
            {
                LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return;
            }

            if (channelId >= packetSendMethods.Length)
            {
                LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return;
            }

            mailingPigeon.Create(data, packetSendMethods[channelId]);
            // probably should get the bool result from this again
            if (!clientPeer.Send((byte)channelId, ref mailingPigeon)) LogWarning("Ignorance Transport: Packet sending apparently wasn't successful.");
        }

        public virtual void ClientDisconnect()
        {
            if (clientPeer.IsSet && clientPeer.State != PeerState.Disconnected) clientPeer.DisconnectNow(0);
            client?.Dispose();
            client = null;
        }

        // -- SERVER WORLD -- //
        public virtual bool ServerActive()
        {
            return libraryInitialized && server != null && server.IsSet;
        }

        public virtual void ServerStart()
        {
            // Do not attempt to start more than one server.
            // Check if the server is active before attempting to create. If it returns true,
            // then we should not continue, and we'll emit a refusal error message.
            // This should be classified as a dirty hack and if it doesn't work.
            if (ServerActive())
            {
                LogError("Ignorance Transport: Refusing to start another server instance! There's already one running.");
                return;
            }

            Log($"Ignorance Transport: Starting up server on port {port} with capacity of {maxConnections} connections.");
            // Fire up ENET-C#'s backend.
            if (!libraryInitialized)
            {
                Library.Initialize();
                libraryInitialized = true;
            }

            // Initialize our references.
            server = new Host();
            serverAddress = new Address();

            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

            // Bind if we have an address specified.
            if (!string.IsNullOrEmpty(address))
            {
                Log($"Ignorance Transport: Binding server instance to {address ?? "(null)"}");
                serverAddress.SetHost(address);
            }

            serverAddress.Port = port;

            // Finally create the server.
            server.Create(serverAddress, maxConnections, packetSendMethods.Length,0,0);

            Log("Ignorance Transport: Entering server receive loop...");
            ServerReceiveLoop(server);
        }

        private async void ServerReceiveLoop(Host serverobject)
        {
            try
            {
                while (serverobject.IsSet)
                {
                    Event incomingEvent;
                    serverobject.Service(0, out incomingEvent);

                    switch (incomingEvent.Type)
                    {
                        case EventType.Connect:
                            Log($"Ignorance Transport: New connection from {incomingEvent.Peer.IP}");

                            // New client connected, let's set them up.
                            int newClientConnectionId = serverConnectionCount;
                            // Increment our next server connection counter.
                            serverConnectionCount++;
                            // Map them in our dictionaries
                            knownPeersToConnIDs.Add(incomingEvent.Peer, newClientConnectionId);
                            knownConnIDToPeers.Add(newClientConnectionId, incomingEvent.Peer);
                            // Invoke the async callback.
                            OnServerConnect?.Invoke(newClientConnectionId);
                            break;
                        case EventType.Receive:
                            // Got data.
                            // Only process that data if the peer is known.
                            if (knownPeersToConnIDs.ContainsKey(incomingEvent.Peer))
                            {
                                byte[] data = new byte[incomingEvent.Packet.Length];

                                incomingEvent.Packet.CopyTo(data);
                                incomingEvent.Packet.Dispose();
                                OnServerData?.Invoke(knownPeersToConnIDs[incomingEvent.Peer], data);
                            }

                            break;
                        case EventType.Disconnect:
                            Log($"Ignorance Transport: Acknowledging disconnection on connection ID {knownPeersToConnIDs[incomingEvent.Peer]}");
                            if (knownPeersToConnIDs.ContainsKey(incomingEvent.Peer))
                            {
                                OnServerDisconnect?.Invoke(knownPeersToConnIDs[incomingEvent.Peer]);
                                PeerDisconnectedInternal(incomingEvent.Peer);
                            }
                            break;
                        case EventType.Timeout:
                            OnServerError?.Invoke(knownPeersToConnIDs[incomingEvent.Peer], new TimeoutException("Ignorance Transport: Timeout occurred on connection " + knownPeersToConnIDs[incomingEvent.Peer]));
                            OnServerDisconnect?.Invoke(knownPeersToConnIDs[incomingEvent.Peer]);
                            PeerDisconnectedInternal(incomingEvent.Peer);
                            break;
                        case EventType.None:
                            // Nothing is happening, so we need to wait until the next frame.
                            await Await.NextUpdate();
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                LogError($"Server exception caught: {exception}");
                OnServerError?.Invoke(-1, exception);
            }
            finally
            {
                serverobject.Flush();
                Log("Ignorance Transport: Server receive loop finished.");
            }
        }

        private void PeerDisconnectedInternal(Peer peer)
        {
            // Clean up dictionaries.
            if (knownConnIDToPeers.ContainsKey(knownPeersToConnIDs[peer])) knownConnIDToPeers.Remove(knownPeersToConnIDs[peer]);
            if (knownPeersToConnIDs.ContainsKey(peer)) knownPeersToConnIDs.Remove(peer);
        }

        public virtual void ServerSend(int connectionId, int channelId, byte[] data)
        {
            Packet mailingPigeon = default(Packet);

            if (channelId >= packetSendMethods.Length)
            {
                LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return;
            }

            if (knownConnIDToPeers.ContainsKey(connectionId))
            {
                Peer target = knownConnIDToPeers[connectionId];
                mailingPigeon.Create(data, packetSendMethods[channelId]);

                if (!target.Send((byte)channelId, ref mailingPigeon)) LogWarning("Ignorance Transport: Server-side packet sending apparently wasn't successful.");
            }
        }

        public virtual bool ServerDisconnect(int connectionId)
        {
            if (knownConnIDToPeers.ContainsKey(connectionId))
            {
                knownConnIDToPeers[connectionId].Disconnect(0);
                return true;
            }

            return false;
        }

        public virtual bool GetConnectionInfo(int connectionId, out string addressoutput)
        {
            addressoutput = "(invalid)";

            if (knownConnIDToPeers.ContainsKey(connectionId))
            {
                addressoutput = knownConnIDToPeers[connectionId].IP;
                return true;
            }

            return false;
        }

        public virtual void ServerStop()
        {
            foreach (KeyValuePair<int, Peer> entry in knownConnIDToPeers) entry.Value.DisconnectNow(0);

            // Don't forget to dispose stuff.
            knownConnIDToPeers = new Dictionary<int, Peer>();
            knownPeersToConnIDs = new Dictionary<Peer, int>();

            server?.Dispose();
            server = null;
        }

        public virtual void Shutdown()
        {
            Log("Ignorance Transport: Acknowledged shutdown request...");

            // Shutdown the client first.
            if (client != null && client.IsSet)
            {
                if (clientPeer.IsSet) clientPeer.DisconnectNow(0);

                client.Flush();
                client.Dispose();
            }

            // Shutdown the server.
            if (server != null && server.IsSet)
            {
                server.Flush();
                server.Dispose();
            }

            Library.Deinitialize();

            Log("Ignorance Transport: Shutdown complete.");
        }

        public int GetMaxPacketSize(int channelId)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes.
        }

        // --- END IGNORANCE ASYNC 2018 BRANCH CORE FUNCTIONS --- //

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
            if (client != null && client.IsSet)
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
            return server != null && server.IsSet ? server.PacketsSent : 0;
        }

        /// <summary>
        /// Server-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ServerGetPacketReceivedCount()
        {
            return server != null && server.IsSet ? server.PacketsReceived : 0;
        }

        /// <summary>
        /// Server-world packets loss counter.
        /// This is buggy. Please use with caution.
        /// </summary>
        /// <returns>The amount of packets lost.</returns>
        public uint ServerGetPacketLossCount()
        {
            return server != null && server.IsSet && server.PacketsSent >= server.PacketsReceived ? server.PacketsSent - server.PacketsReceived : 0;
        }

        /// <summary>
        /// Client-world Packets Sent Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets sent.</returns>
        public uint ClientGetPacketSentCount()
        {
            return client != null && client.IsSet ? client.PacketsSent : 0;
        }

        /// <summary>
        /// Client-world Packets Receive Counter, directly from ENET.
        /// </summary>
        /// <returns>The amount of packets received.</returns>
        public uint ClientGetPacketReceivedCount()
        {
            return client != null && client.IsSet ? client.PacketsReceived : 0;
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
    }
}