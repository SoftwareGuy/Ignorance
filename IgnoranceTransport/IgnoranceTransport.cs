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
// THIS IS THE MIRROR 2018 BRANCH OF IGNORANCE.
// DO NOT USE IF YOU ARE USING UNITY 2017 LTS.
// ----------------------------------------
using ENet;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityAsync;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace Mirror
{
    /// <summary>
    /// Ignorance rUDP Transport is built upon the ENet-C# wrapper by nxrighthere.
    /// </summary>
    public class IgnoranceTransport : TransportLayer
    {
        // -- GENERAL VARIABLES -- //
        private const string TransportVersion = "2018-1.1.0";
        private bool libraryInitialized = false;

        // -- EXPOSED PUBLIC VARIABLES -- //
        public bool verboseLoggingEnabled = false;

        // -- TIMEOUTS -- //
        public bool useCustomPeerTimeout = false;   // Use custom peer timeouts?
        public uint peerBaseTimeout = 5000;        // 5000 ticks (5 seconds)
        public uint peerBaseTimeoutMultiplier = 3; // peerBaseTimemout * this value = maximum time waiting until client is removed

        // -- SERVER WORLD VARIABLES -- //
        private Host server;
        private Host client;

        private Address serverAddress;
        private Peer clientPeer;

        private Dictionary<int, Peer> knownPeerConnections;   // Known connections dictonary since ENET is a little weird.
        private int serverConnectionCount = 1;                // Used by our dictionary to map ENET Peers to connections. Start at 1 just to be safe.

        // This section defines what classic UNET channels refer to.
        private readonly PacketFlags[] packetSendMethods =
        {
            PacketFlags.Reliable,  // Channels.DefaultReliable
            PacketFlags.None       // Channels.DefaultUnreliable
        };

        // -- IGNORANCE 2018 ASYNC ACTIONS -- //
        public event Action OnClientConnect;
        public event Action<byte[]> OnClientData;
        public event Action<Exception> OnClientError;
        public event Action OnClientDisconnect;

        public event Action<int> OnServerConnect;
        public event Action<int, byte[]> OnServerData;
        public event Action<int, Exception> OnServerError;
        public event Action<int> OnServerDisconnect;

        // -- INITIALIZATION -- // 
        public IgnoranceTransport()
        {
            Debug.LogFormat("EXPERIMENTAL Ignorance build {0} for Mirror 2018 ready! Report bugs and donate coffee at https://github.com/SoftwareGuy/Ignorance.", TransportVersion);
        }

        // -- CLIENT WORLD -- //
        public virtual bool ClientConnected() {
            return clientPeer.IsSet && clientPeer.State == PeerState.Connected;
        }

        public virtual void ClientConnect(string address, int port) {
            // Fire up ENET-C#'s backend.
            if (!libraryInitialized)
            {
                Library.Initialize();
                libraryInitialized = true;
            }

            // Setup our references.
            client = new Host();
            Address clientAddress = new Address();

            clientAddress.SetHost(address);
            clientAddress.Port = (ushort)port;

            // Create the client.
            client.Create();

            // Connect the client to the server.
            clientPeer = client.Connect(clientAddress);

            // Start the client's receive loop.
            ClientReceiveLoop(client);
        }

        private async void ClientReceiveLoop(Host clientHost) {
            try
            {
                while (true)
                {
                    // Get the next message from the client peer object.
                    clientHost.Service(0, out Event incomingEvent);
                    switch (incomingEvent.Type)
                    {
                        case EventType.Connect:
                            // TODO: Emit a message when this happens.

                            OnClientConnect?.Invoke();
                            break;
                        case EventType.Disconnect:
                            return;
                        case EventType.Timeout:
                            throw new TimeoutException("Ignorance Transport: Our peer connection timed out.");
                        case EventType.None:
                            // Nothing is happening, so we need to wait until the next frame.
                            await Await.NextUpdate();
                            break;
                        case EventType.Receive:
                            // Got data.
                            byte[] data = new byte[incomingEvent.Packet.Length];
                            incomingEvent.Packet.CopyTo(data);
                            incomingEvent.Packet.Dispose();
                            OnClientData?.Invoke(data);
                            break;
                    }
                }
            } catch (Exception exception) {
                // Something went wrong - just like Windows 10.
                OnClientError?.Invoke(exception);
            } finally {
                // We disconnected, got fed an error message or we got a Disconnect.
                OnClientDisconnect?.Invoke();
            }
        }

        public virtual void ClientSend(int channelId, byte[] data) {
            Packet mailingPigeon = default(Packet);
            bool sendingPacketWasSuccessful;    // really needed?

            if (!client.IsSet)
            {
                Debug.LogWarning("Ignorance Transport: Hold on, the client is not ready yet.");
                return;
            }

            if (channelId >= packetSendMethods.Length)
            {
                Debug.LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return;
            }

            mailingPigeon.Create(data, packetSendMethods[channelId]);
            // probably should get the bool result from this again
            sendingPacketWasSuccessful = clientPeer.Send(0, ref mailingPigeon);
            
            return;
        }

        public virtual void ClientDisconnect() {
            if (clientPeer.IsSet) clientPeer.DisconnectNow(0);
            if (client != null) client.Dispose();
            client = null;
        }

        // -- SERVER WORLD -- //
        public virtual bool ServerActive() {
            if (!libraryInitialized) return false;

            if (server != null) return server.IsSet;
            else return false;
        }

        public virtual void ServerStart(string address, int port, int maxConnections)
        {
            Debug.LogFormat("Ignorance Transport: Starting up server. {0} port {1} with {2} connection capacity.", address ?? "(null)", port, maxConnections);
            // Fire up ENET-C#'s backend.
            if (!libraryInitialized)
            {
                Library.Initialize();
                libraryInitialized = true;
            }

            // Initialize our references.
            server = new Host();
            serverAddress = new Address();
            knownPeerConnections = new Dictionary<int, Peer>();

            // Bind if we have an address specified.
            if (!string.IsNullOrEmpty(address))
            {
                Debug.LogFormat("Ignorance Transport: Binding to {0}", address);
                serverAddress.SetHost(address);
            }

            serverAddress.Port = (ushort)port;

            // Finally create the server.
            server.Create(serverAddress, maxConnections);

            Debug.Log("Ignorance Transport: Entering server receive loop...");

            ServerReceiveLoop(server);
        }

        private async void ServerReceiveLoop(Host server)
        {
            try
            {
               while(true) {
                    server.Service(0, out Event incomingEvent);

                    switch (incomingEvent.Type)
                    {
                        case EventType.Connect:
                            // TODO: Emit a message when this happens.

                            // New client connected, let's set them up.
                            int newClientConnectionId = serverConnectionCount;
                            // Increment our next server connection counter.
                            serverConnectionCount++;
                            // Map them in our dictionary.
                            knownPeerConnections.Add(newClientConnectionId, incomingEvent.Peer);
                            // Invoke the async callback.
                            OnServerConnect?.Invoke(newClientConnectionId);
                            break;
                        case EventType.Receive:
                            // Got data.
                            byte[] data = new byte[incomingEvent.Packet.Length];

                            // Come up with a better way of doing this, Coburn...
                            foreach (KeyValuePair<int, Peer> entry in knownPeerConnections)
                            {
                                if (entry.Value.ID == incomingEvent.Peer.ID)
                                {
                                    incomingEvent.Packet.CopyTo(data);
                                    incomingEvent.Packet.Dispose();
                                    OnServerData?.Invoke(entry.Key, data);

                                    // No need to keep going through the list. Halt.
                                    break;
                                }
                            }
                            break;
                        case EventType.Disconnect:
                        case EventType.Timeout:
                            // TODO: Emit a message when this happens.
                            // Look them up. Not the easiest way of doing this, though.

                            foreach (KeyValuePair<int, Peer> entry in knownPeerConnections)
                            {
                                if (entry.Value.ID == incomingEvent.Peer.ID)
                                {
                                    int disconnectedConnectionId = entry.Key;
                                    OnServerDisconnect?.Invoke(disconnectedConnectionId);
                                    knownPeerConnections.Remove(entry.Key);
                                    break;
                                }
                            }
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
                // What would go here??
                Debug.LogFormat("Server exception caught: {0}", exception.ToString());
            }
            finally
            {
                // What would go here??
                Debug.Log("FIXME: I dunno what to do here");
            }
        }

        public virtual void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            Debug.LogError("Ignorance Transport does not support Websockets. Aborting...");
            return;
        }

        public virtual void ServerSend(int connectionId, int channelId, byte[] data) {
            Packet mailingPigeon = default(Packet);
            bool wasTransmissionSuccessful;

            if (channelId >= packetSendMethods.Length)
            {
                Debug.LogError("Ignorance Transport ERROR: Trying to use an unknown channel to send data");
                return;
            }

            if (knownPeerConnections.ContainsKey(connectionId)) {                
                Peer target = knownPeerConnections[connectionId];
                mailingPigeon.Create(data, packetSendMethods[channelId]);

                wasTransmissionSuccessful = target.Send(0, ref mailingPigeon);
                return;
            }
        }

        public virtual bool ServerDisconnect(int connectionId)
        {
            if (knownPeerConnections.ContainsKey(connectionId)) {
                knownPeerConnections[connectionId].Disconnect(0);
                return true;
            }

            return false;
        }

        public virtual bool GetConnectionInfo(int connectionId, out string address) {
            address = "(invalid)";

            if (knownPeerConnections.ContainsKey(connectionId))
            {
                address = knownPeerConnections[connectionId].IP;
                return true;
            }

            return false;
        }

        public virtual void ServerStop() {
            foreach (KeyValuePair<int, Peer> entry in knownPeerConnections)
            {
                entry.Value.DisconnectNow(0);
            }

            // Don't forget to dispose stuff.
            if (server != null) server.Dispose();
            server = null;
        }

        public virtual void Shutdown()
        {
            Debug.Log("Ignorance Transport: Acknowledged shutdown request...");

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
            libraryInitialized = false;

            Debug.Log("Ignorance Transport: Shutdown complete.");
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
