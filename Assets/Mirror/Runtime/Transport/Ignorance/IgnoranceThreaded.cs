// Ignorance 1.3.x
// A Unity LLAPI Replacement Transport for Mirror Networking
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Ignorance Transport is licensed under the MIT license, however
// it comes with no warranty what-so-ever. However, if you do
// encounter a problem with Ignorance you can get support by
// dropping past the Mirror discord's #ignorance channel. Otherwise,
// open a issue ticket on the GitHub issues page. Ensure you provide
// lots of detail of what you were doing and the error/stack trace.
// -----------------
// Server & Client Threaded Version
// -----------------
using UnityEngine;
using Debug = UnityEngine.Debug;

// Used for threading.
using System;
using System.Threading;

using System.Collections.Concurrent;
using System.Collections.Generic;

// Very important these ones.
using ENet;
using Event = ENet.Event;
using EventType = ENet.EventType;
using System.Threading.Tasks;

namespace Mirror.ENet
{
    public class IgnoranceThreaded : Transport
    {
        // DO NOT TOUCH THIS.
        public override IEnumerable<string> Scheme => new[] { "enet" };

        public override bool Supported => Application.platform != RuntimePlatform.WebGLPlayer;

        // Client Queues
        static ConcurrentQueue<IncomingPacket> MirrorClientIncomingQueue = new ConcurrentQueue<IncomingPacket>();
        static ConcurrentQueue<OutgoingPacket> MirrorClientOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();

        // Server Queues
        static ConcurrentQueue<IncomingPacket> MirrorServerIncomingQueue = new ConcurrentQueue<IncomingPacket>();    // queue going into mirror from clients.
        static ConcurrentQueue<OutgoingPacket> MirrorServerOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();    // queue going to clients from Mirror.

        // lookup and reverse lookup dictionaries
        static ConcurrentDictionary<int, Peer> ConnectionIDToPeers = new ConcurrentDictionary<int, Peer>();
        static ConcurrentDictionary<Peer, int> PeersToConnectionIDs = new ConcurrentDictionary<Peer, int>();

        // Threads
        static Thread serverWorker;
        static Thread clientWorker;

        static volatile bool serverShouldCeaseOperation, clientShouldCeaseOperation;
        static volatile bool ServerStarted, ClientStarted;

        // Client stuffs.
        static volatile bool isClientConnected = false;
        static volatile string clientConnectionAddress = string.Empty;

        // Standard stuffs
        private bool ENETInitialized = false;
        // Properties
        public bool DebugEnabled;

        [Header("UDP Server and Client Settings")]
        public bool ServerBindAll = true;
        public string ServerBindAddress = "127.0.0.1";
        public int CommunicationPort = 7777;
        public int MaximumPeerCCU = 4095;

        [Header("Thread Settings")]
        public int EnetPollTimeout = 1;

        [Header("Security")]
        [UnityEngine.Serialization.FormerlySerializedAs("MaxPacketSize")]
        public int MaxPacketSizeInKb = 16;

        [Header("Channel Definitions")]
        public IgnoranceChannelTypes[] Channels;

        // Ping Calculation
        [Header("Statistics Calculation")]
        [Tooltip("This value (in seconds) controls how often the client stats will be retrieved from the ENET world. Note that too low values can actually harm performance due to excessive polling. " +
            "Keep it frequent, but not too frequent. 3 - 5 seconds should be OK. 0 to disable.")]
        public int StatisticsCalculationInterval = 3;
        public static volatile PeerStatistics statistics = new PeerStatistics();

        // API related to the Ping Calculations
        public static volatile uint CurrentClientPing; // Don't try setting this, it will be overwritten by the network thread.

        // Standard things
        public void Awake()
        {
            print("Thanks for using Ignorance Threaded Edition! If you experience bugs with this version, please file a GitHub support ticket. https://github.com/SoftwareGuy/Ignorance");

            if (MaximumPeerCCU > 4095)
            {
                Debug.LogWarning("WARNING: You cannot have more than 4096 peers with this transport. While this is an artificial limitation and more peers are technically supported, it is a limitation of the underlying C library.");
                Debug.LogWarning("Do not file a bug report regarding this. There's a valid reason why 4096 is the maximum limit.");
                MaximumPeerCCU = 4095;
            }
        }

        public override string ToString()
        {
            return "Ignorance Threaded";
        }

        // TODO: Don't use LateUpdate, because all network stuff will be 1 frame late.
        // TODO: Use FixedUpdate and some trickery. But that's for another day.
        public void LateUpdate()
        {
            if (enabled)
            {
                // Server will pump itself...
                //if (ServerStarted) ProcessServerMessages();
                //if (ClientStarted) ProcessClientMessages();
            }
        }

        private bool InitializeENET()
        {
            return Library.Initialize();
        }


        //TODO: ProcessMessages is part of NetworkConnection (INetworkConnection) now
        //Should a new INetworkConnection implementation be made for Ignorance?
        // Server processing loop.
        //private bool ProcessServerMessages()
        //{
        //    // Get to the queue! Check those corners!
        //    while (MirrorServerIncomingQueue.TryDequeue(out IncomingPacket pkt))
        //    {
        //        switch (pkt.type)
        //        {
        //            case MirrorPacketType.ServerClientConnected:
        //                OnServerConnected?.Invoke(pkt.connectionId);
        //                break;
        //            case MirrorPacketType.ServerClientDisconnected:
        //                OnServerDisconnected?.Invoke(pkt.connectionId);
        //                break;
        //            case MirrorPacketType.ServerClientSentData:
        //                OnServerDataReceived?.Invoke(pkt.connectionId, new ArraySegment<byte>(pkt.data), pkt.channelId);
        //                System.Buffers.ArrayPool<byte>.Shared.Return(pkt.data, true);
        //                break;
        //            default:
        //                // Nothing to see here.
        //                break;
        //        }

        //        // Some messages can disable the transport
        //        // If the transport was disabled by any of the messages, we have to break out of the loop and wait until we've been re-enabled.
        //        if (!enabled)
        //        {
        //            break;
        //        }
        //    }

        //    // Flashbang though the window and race to the finish.
        //    return true;
        //}

        public override void Disconnect()
        {
            if (ClientStarted)
            {
                ClientDisconnect();
            }

            if (ServerStarted)
            {
                ServerStop();
            }
        }

        #region Client Portion
        //TODO: ProcessMessages is part of NetworkConnection (INetworkConnection) now
        //Should a new INetworkConnection implementation be made for Ignorance?
        //private bool ProcessClientMessages()
        //{
        //    while (MirrorClientIncomingQueue.TryDequeue(out IncomingPacket pkt))
        //    {
        //        switch (pkt.type)
        //        {
        //            case MirrorPacketType.ClientConnected:
        //                if (DebugEnabled) print($"Ignorance: We have connected!");
        //                isClientConnected = true;
        //                OnClientConnected?.Invoke();
        //                break;
        //            case MirrorPacketType.ClientDisconnected:
        //                if (DebugEnabled) print($"Ignorance: We have been disconnected.");
        //                isClientConnected = false;
        //                OnClientDisconnected?.Invoke();
        //                break;
        //            case MirrorPacketType.ClientGotData:
        //                OnClientDataReceived?.Invoke(new ArraySegment<byte>(pkt.data), pkt.channelId);
        //                System.Buffers.ArrayPool<byte>.Shared.Return(pkt.data, true);
        //                break;
        //        }
                
        //        // Some messages can disable the transport
        //        // If the transport was disabled by any of the messages, we have to break out of the loop and wait until we've been re-enabled.
        //        if (!enabled)
        //        {
        //            break;
        //        }
        //    }
        //    return true;
        //}

        // Is the client connected?
        public bool ClientConnected()
        {
            return isClientConnected;
        }

        public void ClientConnect(string address)
        {
            // initialize
            if (!ENETInitialized)
            {
                if (InitializeENET())
                {
                    Debug.Log($"Ignorance successfully initialized ENET.");
                    ENETInitialized = true;
                }
                else
                {
                    Debug.LogError($"Ignorance failed to initialize ENET! Cannot continue.");
                    return;
                }
            }

            if (Channels.Length > 255)
            {
                Debug.LogError($"Ignorance: Too many channels. Channel limit is 255, you have {Channels.Length}. Aborting connection.");
                return;
            }

            if (CommunicationPort < ushort.MinValue || CommunicationPort > ushort.MaxValue)
            {
                Debug.LogError($"Ignorance: Bad communication port number. You need to set it between port 0 and 65535. Aborting connection.");
                return;
            }

            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError($"Ignorance: Null or empty address to connect to. Aborting connection.");
                return;
            }

            clientConnectionAddress = address;
            clientShouldCeaseOperation = false;

            // Important: clean the concurrentqueues
            MirrorClientIncomingQueue = new ConcurrentQueue<IncomingPacket>();
            MirrorClientOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();

            print($"Ignorance: Starting connection to {clientConnectionAddress}...");
            clientWorker = IgnoranceClientThread();
            clientWorker.Start();
        }

        // Client Sending: ArraySegment and classic byte array versions
        public bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            return ENETClientQueueInternal(channelId, data);
        }

        public void ClientDisconnect()
        {
            if (DebugEnabled) Debug.Log($"Ignorance: Client disconnection acknowledged");

            if (ServerStarted)
            {
                Debug.LogWarning("MIRROR BUG: ClientDisconnect called even when we're in HostClient/Dedicated Server mode");
                return;
            }

            OutgoingPacket opkt = default;
            opkt.commandType = CommandPacketType.ClientDisconnectionRequest;
            MirrorClientOutgoingQueue.Enqueue(opkt);

            // ...
        }
        #endregion

        #region Server Portion
        public bool ServerActive()
        {
            return ServerStarted;
        }

        public override Task ListenAsync()
        {
            ServerStart();
            return Task.CompletedTask;
        }

        public override async Task<IConnection> AcceptAsync()
        {
            //TODO
            await Task.CompletedTask;
            return null;
        }

        public void ServerStart()
        {
            print($"Ignorance Threaded: Starting server worker.");

            serverShouldCeaseOperation = false;
            serverWorker = IgnoranceServerThread();
            serverWorker.Start();
        }

        // Can't deprecate this due to Dissonance...
        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
        {
            return ENETServerQueueInternal(connectionId, channelId, data);
        }

        public bool ServerDisconnect(int connectionId)
        {
            OutgoingPacket op = default;
            op.connectionId = connectionId;
            op.commandType = CommandPacketType.ServerWantsToDisconnectClient;

            MirrorServerOutgoingQueue.Enqueue(op);
            return true;
        }

        public string ServerGetClientAddress(int connectionId)
        {
            return "UNKNOWN";
        }

        public void ServerStop()
        {
            serverShouldCeaseOperation = true;
            Thread.Sleep(5);    // Allow it to have a micro-sleep

            if (serverWorker != null && serverWorker.IsAlive) serverWorker.Join();

            // IMPORTANT: Flush the queues. Get rid of the dead bodies.
            // c6: Do not use new, instead just while dequeue anything else in the queue
            // c6: helps avoid GC
            while (MirrorServerIncomingQueue.TryDequeue(out _))
            {
                ;
            }

            while (MirrorServerOutgoingQueue.TryDequeue(out _))
            {
                ;
            }

            print($"Ignorance Threaded: Server stopped.");
        }

        public void Shutdown()
        {
            serverShouldCeaseOperation = true;
            clientShouldCeaseOperation = true;

            if (serverWorker != null && serverWorker.IsAlive) serverWorker.Join();
            if (clientWorker != null && clientWorker.IsAlive) clientWorker.Join();

            if (ENETInitialized) Library.Deinitialize();
            ENETInitialized = false;
        }
        #endregion

        #region General Purpose
        public int GetMaxPacketSize(int channelId = 0)
        {
            return MaxPacketSizeInKb * 1024;
        }
        #endregion

        #region Client Threading
        private Thread IgnoranceClientThread()
        {
            statistics = new PeerStatistics();

            ThreadBootstrapStruct threadBootstrap = new ThreadBootstrapStruct
            {
                hostAddress = clientConnectionAddress,
                port = (ushort)CommunicationPort,
                maxChannels = Channels.Length,
                maxPacketSize = MaxPacketSizeInKb * 1024,
                threadPumpTimeout = EnetPollTimeout,
                pingUpdateInterval = StatisticsCalculationInterval,
            };

            Thread t = new Thread(() => ClientWorkerThread(threadBootstrap));
            return t;
        }

        private static void ClientWorkerThread(ThreadBootstrapStruct startupInfo)
        {
            // Setup...
            uint nextStatsUpdate = 0;

            byte[] workerPacketBuffer = new byte[startupInfo.maxPacketSize];
            Address cAddress = new Address();

            // Drain anything in the queues...
            while (MirrorClientIncomingQueue.TryDequeue(out _))
            {
                ;
            }

            while (MirrorClientOutgoingQueue.TryDequeue(out _))
            {
                ;
            }

            // This comment was actually left blank, but now it's not. You're welcome.
            using (Host cHost = new Host())
            {
                try
                {
                    cHost.Create(null, 1, startupInfo.maxChannels, 0, 0, startupInfo.maxPacketSize);
                    ClientStarted = true;
                }
                catch (Exception e)
                {
                    Debug.LogError("Ignorance encountered a fatal exception. To help debug the issue, use a Debug DLL of ENET and look for a 'enet_log.txt' file in the root of your " +
                        $"application folder.\nIf you believe you found a bug, please report it on the GitHub issue tracker. The exception returned was: {e}");
                    return;
                }

                // Attempt to start connection...
                cAddress.SetHost(startupInfo.hostAddress);
                cAddress.Port = startupInfo.port;
                Peer cPeer = cHost.Connect(cAddress, startupInfo.maxChannels);

                while (!clientShouldCeaseOperation)
                {
                    bool clientWasPolled = false;

                    if(Library.Time >= nextStatsUpdate)
                    {
                        statistics.CurrentPing = cPeer.RoundTripTime;
                        statistics.BytesReceived = cPeer.BytesReceived;
                        statistics.BytesSent = cPeer.BytesSent;

                        statistics.PacketsLost = cPeer.PacketsLost;
                        statistics.PacketsSent = cPeer.PacketsSent;

                        // Library.Time is milliseconds, so we need to do some quick math.
                        nextStatsUpdate = Library.Time + (uint)(startupInfo.pingUpdateInterval * 1000);
                    }

                    while (!clientWasPolled)
                    {
                        if (cHost.CheckEvents(out Event networkEvent) <= 0)
                        {
                            if (cHost.Service(startupInfo.threadPumpTimeout, out networkEvent) <= 0) break;
                            clientWasPolled = true;
                        }

                        switch (networkEvent.Type)
                        {
                            case EventType.Connect:
                                // Client connected.
                                IncomingPacket connPkt = default;
                                connPkt.type = MirrorPacketType.ClientConnected;
                                MirrorClientIncomingQueue.Enqueue(connPkt);
                                break;
                            case EventType.Timeout:
                            case EventType.Disconnect:
                                // Client disconnected.
                                IncomingPacket disconnPkt = default;
                                disconnPkt.type = MirrorPacketType.ClientDisconnected;
                                MirrorClientIncomingQueue.Enqueue(disconnPkt);
                                break;
                            case EventType.Receive:
                                // Client recieving some data.
                                if (!networkEvent.Packet.IsSet)
                                {
                                    print("Ignorance WARNING: A incoming packet is not set correctly.");
                                    break;
                                }

                                if (networkEvent.Packet.Length > workerPacketBuffer.Length)
                                {
                                    print($"Ignorance: Packet too big to fit in buffer. {networkEvent.Packet.Length} packet bytes vs {workerPacketBuffer.Length} cache bytes {networkEvent.Peer.ID}.");
                                    networkEvent.Packet.Dispose();
                                    break;
                                }
                                else
                                {
                                    // invoke on the client.
                                    try
                                    {
                                        IncomingPacket dataPkt = default;
                                        dataPkt.type = MirrorPacketType.ClientGotData;
                                        dataPkt.channelId = networkEvent.ChannelID;

                                        byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(networkEvent.Packet.Length);
                                        networkEvent.Packet.CopyTo(rentedBuffer);                                                                                                                      
                                        dataPkt.data = rentedBuffer;

                                        MirrorClientIncomingQueue.Enqueue(dataPkt);
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogError($"Ignorance caught an exception while trying to copy data from the unmanaged (ENET) world to managed (Mono/IL2CPP) world. Please consider reporting this to the Ignorance developer on GitHub.\n" +
                                            $"Exception returned was: {e.Message}\n" +
                                            $"Debug details: {(workerPacketBuffer == null ? "packet buffer was NULL" : $"{workerPacketBuffer.Length} byte work buffer")}, {networkEvent.Packet.Length} byte(s) network packet length\n" +
                                            $"Stack Trace: {e.StackTrace}");
                                        break;
                                    }

                                    networkEvent.Packet.Dispose();
                                }
                                break;
                        }
                    }

                    // Outgoing stuff
                    while (MirrorClientOutgoingQueue.TryDequeue(out OutgoingPacket opkt))
                    {
                        if (opkt.commandType == CommandPacketType.ClientDisconnectionRequest)
                        {
                            cPeer.DisconnectNow(0);
                            return;
                        }

                        int returnCode = cPeer.SendAndReturnStatusCode(opkt.channelId, ref opkt.payload);
                        if (returnCode != 0) print($"Ignorance: Could not send {opkt.payload.Length} bytes to server on channel {opkt.channelId}, error code {returnCode}");
                    }
                }

                cPeer.DisconnectNow(0);
                cHost.Flush();
                ClientStarted = false;
            }
        }
        #endregion

        #region Server Threading
        // Server thread.
        private Thread IgnoranceServerThread()
        {
            string bindAddress = string.Empty;

            ThreadBootstrapStruct startupInformation = new ThreadBootstrapStruct()
            {
                hostAddress = bindAddress,
                port = (ushort)CommunicationPort,
                maxPacketSize = MaxPacketSizeInKb * 1024,
                maxPeers = MaximumPeerCCU,
                maxChannels = Channels.Length,
                threadPumpTimeout = EnetPollTimeout
            };

            Thread t = new Thread(() => ServerWorkerThread(startupInformation));
            return t;
        }

        private static void ServerWorkerThread(ThreadBootstrapStruct startupInformation)
        {
            // Worker buffer.
            byte[] workerPacketBuffer = new byte[startupInformation.maxPacketSize];
            // Connection ID.
            // TODO: Change the name of this.
            int nextConnectionId = 1;
            // Return code from the send function
            int returnCode = 0;

            // Server address properties
            Address eAddress = new Address()
            {
                Port = startupInformation.port,
            };

            // Bind on everything or not?
            if (!string.IsNullOrEmpty(startupInformation.hostAddress)) eAddress.SetHost(startupInformation.hostAddress);

            using (Host serverWorkerHost = new Host())
            {
                try
                {
                    serverWorkerHost.Create(eAddress, startupInformation.maxPeers, startupInformation.maxChannels, 0, 0);
                    ServerStarted = true;
                }
                catch (Exception e)
                {
                    Debug.LogError("Ignorance encountered a fatal exception. I'm sorry, but I gotta bail - if you believe you found a bug, please report it on the GitHub.\n" +
                        $"The exception returned was: {e}");
                    return;
                }

                Debug.Log($"Ignorance Server worker thread is ready for connections! I'm listening on UDP port {startupInformation.port}.\n" +
                    $"Capacity: {startupInformation.maxPeers} peers with {startupInformation.maxChannels} channels. My buffer size is {startupInformation.maxPacketSize} bytes");

                // The meat and potatoes.
                while (!serverShouldCeaseOperation)
                {
                    // Outgoing stuff
                    while (MirrorServerOutgoingQueue.TryDequeue(out OutgoingPacket opkt))
                    {
                        switch (opkt.commandType)
                        {
                            case CommandPacketType.ServerWantsToDisconnectClient:
                                if (ConnectionIDToPeers.TryGetValue(opkt.connectionId, out Peer bootedPeer))
                                {
                                    bootedPeer.DisconnectLater(0);
                                }
                                break;

                            case CommandPacketType.Nothing:
                            default:
                                if (ConnectionIDToPeers.TryGetValue(opkt.connectionId, out Peer target))
                                {                                    
                                    returnCode = target.SendAndReturnStatusCode(opkt.channelId, ref opkt.payload);
                                    if (returnCode != 0) print($"Error code {returnCode} returned trying to send {opkt.payload.Length} bytes to Peer {target.ID} on channel {opkt.channelId}");
                                }
                                break;
                        }
                    }

                    // Get everything out the door.
                    serverWorkerHost.Flush();

                    // Incoming stuffs now.
                    bool hasBeenPolled = false;

                    while (!hasBeenPolled)
                    {
                        if (serverWorkerHost.CheckEvents(out Event netEvent) <= 0)
                        {
                            if (serverWorkerHost.Service(startupInformation.threadPumpTimeout, out netEvent) <= 0)
                                break;

                            hasBeenPolled = true;
                        }

                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                break;

                            case EventType.Connect:
                                // int connectionId = nextConnectionId;

                                // Add to dictionaries.
                                if (!PeersToConnectionIDs.TryAdd(netEvent.Peer, nextConnectionId))
                                {
                                    Debug.LogWarning($"It seems we already know this client in our Connection ID to Peer Mapping. Replacing.");
                                    PeersToConnectionIDs[netEvent.Peer] = nextConnectionId;
                                }
                                if (!ConnectionIDToPeers.TryAdd(nextConnectionId, netEvent.Peer))
                                {
                                    Debug.LogWarning($"It seems we already know this client in our Peer to ConnectionID Mapping. Replacing.");
                                    ConnectionIDToPeers[nextConnectionId] = netEvent.Peer;
                                }

                                // Send a message back to mirror.
                                IncomingPacket newConnectionPkt = default;
                                newConnectionPkt.connectionId = nextConnectionId;
                                newConnectionPkt.type = MirrorPacketType.ServerClientConnected;
                                newConnectionPkt.ipAddress = netEvent.Peer.IP;

                                MirrorServerIncomingQueue.Enqueue(newConnectionPkt);
                                nextConnectionId++;
                                break;

                            case EventType.Disconnect:
                            case EventType.Timeout:
                                if (PeersToConnectionIDs.TryGetValue(netEvent.Peer, out int deadPeer))
                                {
                                    IncomingPacket disconnectionPkt = default;
                                    disconnectionPkt.connectionId = deadPeer;
                                    disconnectionPkt.type = MirrorPacketType.ServerClientDisconnected;
                                    disconnectionPkt.ipAddress = netEvent.Peer.IP;

                                    MirrorServerIncomingQueue.Enqueue(disconnectionPkt);
                                    ConnectionIDToPeers.TryRemove(deadPeer, out Peer _);
                                }

                                PeersToConnectionIDs.TryRemove(netEvent.Peer, out int _);
                                break;

                            case EventType.Receive:
                                int dataConnID = -1;

                                if (PeersToConnectionIDs.TryGetValue(netEvent.Peer, out dataConnID))
                                {
                                    if (!netEvent.Packet.IsSet)
                                    {
                                        Debug.LogWarning("Ignorance: A incoming packet is not set correctly - attempting to continue!");
                                        return;
                                    }

                                    if (netEvent.Packet.Length > startupInformation.maxPacketSize)
                                    {
                                        Debug.LogWarning($"Ignorance: Packet too large for buffer; dropping. Packet {netEvent.Packet.Length} bytes > {startupInformation.maxPacketSize} byte limit");
                                        netEvent.Packet.Dispose();
                                        return;
                                    }

                                    // Copy to the packet cache.
                                    try
                                    {
                                        IncomingPacket dataPkt = default;
                                        dataPkt.connectionId = dataConnID;
                                        dataPkt.channelId = netEvent.ChannelID;
                                        dataPkt.type = MirrorPacketType.ServerClientSentData;
                                        dataPkt.ipAddress = netEvent.Peer.IP;

                                        byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(netEvent.Packet.Length);
                                        netEvent.Packet.CopyTo(rentedBuffer);

                                        dataPkt.data = rentedBuffer;

                                        MirrorServerIncomingQueue.Enqueue(dataPkt);

                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogError($"Ignorance caught an exception while trying to copy data from the unmanaged (ENET) world to managed (Mono/IL2CPP) world. Please consider reporting this to the Ignorance developer on GitHub.\n" +
                                            $"Exception returned was: {e.Message}\n" +
                                            $"Debug details: {(workerPacketBuffer == null ? "packet buffer was NULL" : $"{workerPacketBuffer.Length} byte work buffer")}, {netEvent.Packet.Length} byte(s) network packet length\n" +
                                            $"Stack Trace: {e.StackTrace}");

                                        return;
                                    }
                                }
                                else
                                {
                                    // Kick the peer.
                                    netEvent.Peer.DisconnectNow(0);
                                }

                                // Dispose of the packet - we're done.
                                netEvent.Packet.Dispose();
                                break;
                        }
                    }
                }

                // Disconnect everyone, we're done here.
                print($"Server thread is now kicking all connected peers...");
                foreach (KeyValuePair<int, Peer> kv in ConnectionIDToPeers) kv.Value.DisconnectNow(0);

                print("Server thread is now flushing and cleaning up...");
                serverWorkerHost.Flush();

                // BUGFIX issue #59: "Player crash on second server client connection"
                // https://github.com/SoftwareGuy/Ignorance/issues/59
                ConnectionIDToPeers.Clear();
                PeersToConnectionIDs.Clear();

                // Server is no longer started.
                ServerStarted = false;
            }
        }
        #endregion

        #region Mirror 6.2+ - URI Support
        public override IEnumerable<Uri> ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = "enet",
                Host = ServerBindAddress,
                Port = CommunicationPort
            };
            return new[] { builder.Uri };
        }
		
        public override async Task<IConnection> ConnectAsync(Uri uri)
        {
            if (uri.Scheme != "enet")
                throw new ArgumentException($"Invalid uri {uri}, use {Scheme}://host:port instead", nameof(uri));

            if (!uri.IsDefaultPort)
            {
                // Set the communication port to the one specified.
                CommunicationPort = uri.Port;
            }

            ClientConnect(uri.Host);

            await Task.CompletedTask; //TODO

            return new ENetConnection(null); //TODO
        }
        #endregion

        #region Unity Editor and Sanity Checks
        // Sanity checks.
        private void OnValidate()
        {
            if (Channels != null && Channels.Length >= 2)
            {
                // Check to make sure that Channel 0 and 1 are correct.
                if (Channels[0] != IgnoranceChannelTypes.Reliable) Channels[0] = IgnoranceChannelTypes.Reliable;
                if (Channels[1] != IgnoranceChannelTypes.Unreliable) Channels[1] = IgnoranceChannelTypes.Unreliable;
            }
            else
            {
                Channels = new IgnoranceChannelTypes[2]
                {
                    IgnoranceChannelTypes.Reliable,
                    IgnoranceChannelTypes.Unreliable
                };
            }
        }
        public bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
        {
            if (!ServerStarted)
            {
                Debug.LogError("Attempted to send while the server was not active");
                return false;
            }

            if (channelId > Channels.Length)
            {
                Debug.LogWarning($"Ignorance: Attempted to send data on channel {channelId} when we only have {Channels.Length} channels defined");
                return false;
            }

            foreach (int conn in connectionIds)
            {
                // Another sneaky hack
                ENETServerQueueInternal(conn, channelId, segment);
            }

            return true;
        }

        /// <summary>
        /// Enqueues a packet for ENET worker to pick up and dispatch.
        /// Hopefully should make it easier to fix things.
        /// </summary>
        /// <param name="channelId">The channel id you wish to send the packet on. Must be within 0 and the count of the channels array.</param>
        /// <param name="dataPayload">The array segment containing the data to send to ENET.</param>
        /// <returns></returns>
        private bool ENETClientQueueInternal(int channelId, ArraySegment<byte> dataPayload)
        {
            if (channelId > Channels.Length)
            {
                Debug.LogWarning($"Ignorance: Attempted to send data on channel {channelId} when we only have {Channels.Length} channels defined");
                return false;
            }

            OutgoingPacket opkt = default;
            opkt.channelId = (byte)channelId;

            Packet payload = default;
            payload.Create(dataPayload.Array, dataPayload.Offset, dataPayload.Count + dataPayload.Offset, (PacketFlags)Channels[channelId]);

            opkt.payload = payload;

            // Enqueue it.
            MirrorClientOutgoingQueue.Enqueue(opkt);

            return true;
        }

        private bool ENETServerQueueInternal(int connectionId, int channelId, ArraySegment<byte> data)
        {
            if (!ServerStarted)
            {
                Debug.LogError("Attempted to send while the server was not active");
                return false;
            }

            if (channelId > Channels.Length)
            {
                Debug.LogWarning($"Ignorance: Attempted to send data on channel {channelId} when we only have {Channels.Length} channels defined");
                return false;
            }

            OutgoingPacket op = default;
            op.connectionId = connectionId;
            op.channelId = (byte)channelId;

            Packet dataPayload = default;
            dataPayload.Create(data.Array, data.Offset, data.Count + data.Offset, (PacketFlags)Channels[channelId]);

            op.payload = dataPayload;

            MirrorServerOutgoingQueue.Enqueue(op);
            return true;
        }
        #endregion

        #region Structs, classes, etc
        // Incoming packet struct.
        private struct IncomingPacket
        {
            public int connectionId;
            public int channelId;
            public MirrorPacketType type;
            public byte[] data;
            public string ipAddress;
        }
        // Outgoing packet struct
        private struct OutgoingPacket
        {
            public int connectionId;
            public byte channelId;
            public Packet payload;
            public CommandPacketType commandType;
        }

        // Packet Type Struct. Not to be confused with the ENET Packet Type.
        [Serializable]
        public enum MirrorPacketType
        {
            ServerClientConnected,
            ServerClientDisconnected,
            ServerClientSentData,
            ClientConnected,
            ClientDisconnected,
            ClientGotData
        }

        // Command Packet Type Struct.
        public enum CommandPacketType
        {
            Nothing,
            ServerWantsToDisconnectClient,
            ClientDisconnectionRequest
        }

        // -> Moved ChannelTypes enum to it's own file, so it's easier to maintain.

        public struct ThreadBootstrapStruct
        {
            public string hostAddress;
            public ushort port;

            public int threadPumpTimeout;

            public int maxPacketSize;
            public int maxChannels;
            public int maxPeers;

            // Client only
            public int pingUpdateInterval;
        }
        #endregion
    }
}
