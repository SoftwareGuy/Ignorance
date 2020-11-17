// Ignorance 1.3.x
// Ignorance. It really kicks the Unity LLAPIs ass.
// https://github.com/SoftwareGuy/Ignorance
// -----------------
// Copyright (c) 2019 - 2020 Matt Coburn (SoftwareGuy/Coburn64)
// Ignorance Transport is licensed under the MIT license. Refer
// to the LICENSE file for more information.
// -----------------
// Ignorance Threaded Version
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

namespace Mirror
{
    public class IgnoranceThreaded : Transport
    {
        // --- Client Queues --- //
        static ConcurrentQueue<IncomingPacket> ClientIncomingQueue = new ConcurrentQueue<IncomingPacket>();
        static ConcurrentQueue<OutgoingPacket> ClientOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();

        // --- Server Queues --- //
        static ConcurrentQueue<IncomingPacket> ServerIncomingQueue = new ConcurrentQueue<IncomingPacket>();    // queue going into mirror from clients.
        static ConcurrentQueue<OutgoingPacket> ServerOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();    // queue going to clients from Mirror.

        // --- Dictionaries --- //
        static ConcurrentDictionary<int, Peer> ConnectionIDToPeers = new ConcurrentDictionary<int, Peer>();
        static ConcurrentDictionary<Peer, int> PeersToConnectionIDs = new ConcurrentDictionary<Peer, int>();

        // --- Thread and booleans --- //
        static Thread serverWorker, clientWorker;
        static volatile bool serverShouldCeaseOperation, clientShouldCeaseOperation, ServerStarted, ClientStarted;
        static volatile bool isClientConnected = false;

        // Various properties.
        static string clientConnectionAddress = string.Empty;

        public bool DebugEnabled = false;

        [Header("UDP Server and Client Settings")]
        public bool ServerBindAll = true;
        public string ServerBindAddress = "127.0.0.1";
        public int CommunicationPort = 7777;
        public int MaximumPeerCCU = 100;

        [Header("Thread Settings")]
        public int EnetServerPollTimeout = 1;
        public int EnetClientPollTimeout = 1;

        [Header("Security")]
        [Tooltip("To prevent ENet from having to fragment your packets try to keep your packet sizes below 1200 bytes. This is the recommended maximum MTU for UDP packets. Otherwise, " +
            "fragmented packets are sent reliably so that ENet can reassemble them at the remote end. Smaller packets also mean less bandwidth being burnt per client.")]
        public int MaximumPacketSize = 1200;

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
            print("Thanks for using Ignorance Threaded Edition! Experience a bug? Report it here on GitHub => https://github.com/SoftwareGuy/Ignorance.");

            if (MaximumPeerCCU > 4095)
            {
                Debug.LogWarning("WARNING: You cannot have more than 4096 peers with this transport. While this is an artificial limitation and more peers are technically supported, it is a limitation of the underlying C library.");
                Debug.LogWarning("Do not file a bug report regarding this. There's a valid reason why 4096 is the maximum limit.");
                MaximumPeerCCU = 4095;
            }
        }

        public override bool Available()
        {
#if UNITY_WEBGL
            // Ignorance is not available on these platforms.
            return false;
#else
            return true;
#endif
        }

        public override string ToString()
        {
            return $"Ignorance Threaded v{IgnoranceInternals.Version}";
        }

        // TODO: Don't use LateUpdate, because all network stuff will be 1 frame late.
        // TODO: Use FixedUpdate and some trickery. But that's for another day.
        public void LateUpdate()
        {
            if (enabled)
            {
                // Server will pump itself...
                if (ServerStarted) ProcessServerMessages();
                if (ClientStarted) ProcessClientMessages();
            }
        }

        // Server processing loop.
        private bool ProcessServerMessages()
        {
            // Get to the queue! Check those corners!
            while (ServerIncomingQueue.TryDequeue(out IncomingPacket pkt))
            {
                switch (pkt.type)
                {
                    case QueuePacketType.Server_ClientConnect:
                        OnServerConnected?.Invoke(pkt.mirrorClientId);
                        break;
                    case QueuePacketType.Server_ClientDisconnect:
                        OnServerDisconnected?.Invoke(pkt.mirrorClientId);
                        break;
                    case QueuePacketType.Server_IncomingData:
                        OnServerDataReceived?.Invoke(pkt.mirrorClientId, new ArraySegment<byte>(pkt.data, 0, pkt.length), pkt.channelId);
                        System.Buffers.ArrayPool<byte>.Shared.Return(pkt.data, true);
                        break;
                    default:
                        // Nothing to see here.
                        break;
                }

                // Some messages can disable the transport
                // If the transport was disabled by any of the messages, we have to break out of the loop and wait until we've been re-enabled.
                if (!enabled)
                {
                    break;
                }
            }

            // Flashbang though the window and race to the finish.
            return true;
        }

        #region Client Portion
        private bool ProcessClientMessages()
        {
            while (ClientIncomingQueue.TryDequeue(out IncomingPacket pkt))
            {
                switch (pkt.type)
                {
                    case QueuePacketType.Client_ConnectedToServer:
                        if (DebugEnabled) print($"Ignorance: Client connected to server.");
                        isClientConnected = true;
                        OnClientConnected?.Invoke();
                        break;
                    case QueuePacketType.Client_DisconnectedFromServer:
                        if (DebugEnabled) print($"Ignorance: Client disconnected from server.");
                        isClientConnected = false;
                        OnClientDisconnected?.Invoke();
                        break;
                    case QueuePacketType.Client_IncomingData:
                        OnClientDataReceived?.Invoke(new ArraySegment<byte>(pkt.data, 0, pkt.length), pkt.channelId);
                        System.Buffers.ArrayPool<byte>.Shared.Return(pkt.data, true);
                        break;
                }

                // Some messages can disable the transport
                // If the transport was disabled by any of the messages, we have to break out of the loop and wait until we've been re-enabled.
                if (!enabled)
                {
                    break;
                }
            }
            return true;
        }

        // Is the client connected?
        public override bool ClientConnected()
        {
            return isClientConnected;
        }

        public override void ClientConnect(string address)
        {
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

            print($"Ignorance: Starting connection to {clientConnectionAddress}...");

            clientWorker = IgnoranceClientThread();
            clientWorker.Start();
        }

        // Client Sending: ArraySegment and classic byte array versions
#if MIRROR_26_0_OR_NEWER
        public override void ClientSend(int channel, ArraySegment<byte> data) => ENetClientQueueInternal(channel, data);
#else
        public override bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            return ENetClientQueueInternal(channelId, data);
        }
#endif
        public override void ClientDisconnect()
        {
            if (DebugEnabled) Debug.Log($"Ignorance: Client disconnection acknowledged");

            if (ServerStarted)
            {
                Debug.LogWarning("MIRROR BUG: ClientDisconnect called even when we're in HostClient/Dedicated Server mode");
                return;
            }

            OutgoingPacket opkt = default;
            opkt.commandType = CommandPacketType.Client_DisconnectNow;
            ClientOutgoingQueue.Enqueue(opkt);

            // ...
        }
        #endregion

        #region Server Portion
        public override bool ServerActive()
        {
            return ServerStarted;
        }

        public override void ServerStart()
        {
            print($"Ignorance Threaded: Starting server worker.");

            serverShouldCeaseOperation = false;
            serverWorker = IgnoranceServerThread();
            serverWorker.Start();
        }

        public override bool ServerDisconnect(int connectionId)
        {
            OutgoingPacket op = default;
            op.mirrorClientId = connectionId;
            op.commandType = CommandPacketType.Server_ClientKick;

            ServerOutgoingQueue.Enqueue(op);
            return true;
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return "UNKNOWN";
        }

#if MIRROR_26_0_OR_NEWER
        public override void ServerSend(int connection, int channelId, ArraySegment<byte> segment)
        {
            if (!ServerStarted)
            {
                Debug.LogError("Ignorance: Attempted to send while the server was not active");
                return;
            }

            if (channelId > Channels.Length)
            {
                Debug.LogWarning($"Ignorance: Attempted to send data on channel {channelId} when we only have {Channels.Length} channels defined");
                return;
            }

            EnqueuePacketForDelivery(connection, channelId, segment);
        }
#else
        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment)
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
                EnqueuePacketForDelivery(conn, channelId, segment);
            }

            return true;
        }
#endif

        public override void ServerStop()
        {
            serverShouldCeaseOperation = true;
            // Allow it to have a micro-sleep
            Thread.Sleep(5);

            if (serverWorker != null && serverWorker.IsAlive) serverWorker.Join();

            // IMPORTANT: Flush the queues. Get rid of the dead bodies.
            // c6: Do not use new, instead just while dequeue anything else in the queue
            // c6: helps avoid GC
            while (ServerIncomingQueue.TryDequeue(out _))
            {
                ;
            }

            while (ServerOutgoingQueue.TryDequeue(out _))
            {
                ;
            }

            print($"Ignorance Threaded: Server stopped.");
        }

        public override void Shutdown()
        {
            serverShouldCeaseOperation = true;
            clientShouldCeaseOperation = true;

            if (serverWorker != null && serverWorker.IsAlive) serverWorker.Join();
            if (clientWorker != null && clientWorker.IsAlive) clientWorker.Join();
        }
#endregion

#region General Purpose
        public override int GetMaxPacketSize(int channelId = 0)
        {
            return MaximumPacketSize;
        }
#endregion

#region Client Threading
        private Thread IgnoranceClientThread()
        {
            statistics = new PeerStatistics();
            Thread t = new Thread(() => ClientWorkerThread(clientConnectionAddress, (ushort)CommunicationPort, Channels.Length, EnetClientPollTimeout, MaximumPacketSize, StatisticsCalculationInterval));
            return t;
        }

        private static void ClientWorkerThread(string connectionAddress, ushort connectionPort, int maxChannels, int threadWaitTimeout, int maximumPacketSize, int statsInterval)
        {
            // Setup...
            uint nextStatsUpdate = 0;
            Address cAddress = new Address();

            // Drain anything in the queues...
            while (ClientIncomingQueue.TryDequeue(out _))
            {
                ;
            }

            while (ClientOutgoingQueue.TryDequeue(out _))
            {
                ;
            }

            // Thread Safety: Initialize ENet in its own thread.
            try
            {
                Library.Initialize();
                Debug.Log("Ignorance: ENet initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ignorance: ENet failed to initialize. Exception returned was: {ex}");
                return;
            }

            // This comment was actually left blank, but now it's not. You're welcome.
            using (Host cHost = new Host())
            {
                try
                {
                    cHost.Create(null, 1, maxChannels, 0, 0);
                    ClientStarted = true;
                }
                catch (Exception e)
                {
                    Debug.LogError("Ignorance: An fatal exception has occurred and the transport will be shut down. TTo help debug the issue, use a Debug DLL of ENet and look for a logfile in the root of the " +
                        $"application folder. If you believe you found a bug, please report it on the GitHub issue tracker. The exception returned was: {e}");
                    return;
                }

                // Attempt to start connection...
                if (!cAddress.SetHost(connectionAddress))
                {
                    Debug.LogError("Ignorance: Unable to set the hostname or address. Was this string even valid? Please check it and try again.");
                    return;
                }

                cAddress.Port = connectionPort;
                Peer cPeer = cHost.Connect(cAddress, maxChannels);

                while (!clientShouldCeaseOperation)
                {
                    bool clientWasPolled = false;

                    if (Library.Time >= nextStatsUpdate)
                    {
                        statistics.CurrentPing = cPeer.RoundTripTime;
                        statistics.BytesReceived = cPeer.BytesReceived;
                        statistics.BytesSent = cPeer.BytesSent;

                        statistics.PacketsLost = cPeer.PacketsLost;
                        statistics.PacketsSent = cPeer.PacketsSent;

                        // Library.Time is milliseconds, so we need to do some quick math.
                        nextStatsUpdate = Library.Time + (uint)(statsInterval * 1000);
                    }

                    while (!clientWasPolled)
                    {
                        if (cHost.CheckEvents(out Event netEvent) <= 0)
                        {
                            if (cHost.Service(threadWaitTimeout, out netEvent) <= 0) break;
                            clientWasPolled = true;
                        }

                        switch (netEvent.Type)
                        {
                            case EventType.Connect:
                                // Client connected to server. Tell Mirror about that.
                                IncomingPacket connPkt = default;
                                connPkt.type = QueuePacketType.Client_ConnectedToServer;
                                ClientIncomingQueue.Enqueue(connPkt);
                                break;

                            case EventType.Timeout:
                            case EventType.Disconnect:
                                // Client disconnected from server. Tell Mirror about that.
                                IncomingPacket disconnPkt = default;
                                disconnPkt.type = QueuePacketType.Client_DisconnectedFromServer;
                                ClientIncomingQueue.Enqueue(disconnPkt);
                                break;

                            case EventType.Receive:
                                // Client recieving some data.
                                if (!netEvent.Packet.IsSet)
                                {
                                    Debug.LogWarning("Ignorance: A incoming packet is not set correctly.");
                                    break;
                                }

                                if (netEvent.Packet.Length > maximumPacketSize)
                                {
                                    print($"Ignorance: Packet dropped, it was too large from the server. Your maximum packet allows only {maximumPacketSize} byte packets, this one was {netEvent.Packet.Length} byte(s).");
                                    netEvent.Packet.Dispose();
                                    break;
                                }
                                else
                                {
                                    // invoke on the client.
                                    try
                                    {
                                        IncomingPacket dataPkt = default;
                                        dataPkt.type = QueuePacketType.Client_IncomingData;
                                        dataPkt.channelId = netEvent.ChannelID;

                                        // Rent a new buffer from ArrayPool, copy it into that.
                                        // Disposal is later outside this try/catch.
                                        byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(netEvent.Packet.Length);
                                        netEvent.Packet.CopyTo(rentedBuffer);
                                        dataPkt.data = rentedBuffer;
                                        dataPkt.length = netEvent.Packet.Length;

                                        ClientIncomingQueue.Enqueue(dataPkt);
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogError($"Ignorance: Exception caught while trying to copy data from the unmanaged (ENet) world to Managed world. Please consider reporting this to the Ignorance developer on GitHub.\n" +
                                            $"Exception returned was: {e.Message}\n" +
                                            $"Debug details: {(netEvent.Packet.Data == null ? "NetEvent Packet payload NULL" : $"NetEvent Packet payload valid")}, {netEvent.Packet.Length} byte(s)\n" +
                                            $"Stack Trace: {e.StackTrace}");
                                        break;
                                    }

                                    netEvent.Packet.Dispose();
                                }
                                break;
                        }
                    }

                    // Outgoing packet processor
                    while (ClientOutgoingQueue.TryDequeue(out OutgoingPacket opkt))
                    {
                        switch (opkt.commandType) {
                            case CommandPacketType.Client_NoCommand:
                                int returnCode = cPeer.Send(opkt.channelId, ref opkt.payload);
                                if (returnCode != 0) print($"Ignorance: Could not send {opkt.payload.Length} bytes to server on channel {opkt.channelId}, error code {returnCode}");
                                break;

                            case CommandPacketType.Client_DisconnectNow:
                                // Client wants to disconnect right here, right now.
                                cPeer.DisconnectNow(0);
                                clientShouldCeaseOperation = true;
                                break;

                            case CommandPacketType.Client_DisconnectLater:
                                // Client wants to disconnect, but later on. Unknown use atm...
                                cPeer.DisconnectLater(0);
                                break;

                            default:
                                // Client can't do anything else.
                                Debug.LogWarning($"Ignorance: Client queue has a unknown command type: {opkt.commandType}");
                                break;
                        }
                    }
                }

                if (cPeer.State != PeerState.Disconnected) cPeer.DisconnectNow(0);
                cHost.Flush();
                ClientStarted = false;
            }

            Library.Deinitialize();

            Debug.Log("Ignorance: ENet Deinitialized.");
        }
#endregion

#region Server Threading
        // Server thread.
        private Thread IgnoranceServerThread()
        {
            string bindAddress = string.Empty;

            if (!ServerBindAll)
            {
                bindAddress = ServerBindAddress;
            }

            Thread t = new Thread(() => ServerWorkerThread(bindAddress, (ushort)CommunicationPort, Channels.Length, MaximumPeerCCU, MaximumPacketSize, EnetServerPollTimeout));
            return t;
        }

        private static void ServerWorkerThread(string bindAddress, ushort port, int channels, int peers, int maxPacketSize, int threadWaitTimeout)
        {
            // Thread Safety: Initialize ENet in its own thread.
            try
            {
                Library.Initialize();
                Debug.Log("Ignorance: ENet initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ignorance: ENet failed to initialize. Exception returned was: {ex}");
                return;
            }

            // Connection ID.
            // TODO: Change the name of this.
            int nextConnectionId = 1;

            // Server address properties
            Address eAddress = new Address()
            {
                Port = port,
            };

            // Bind on everything or not?
            if (!string.IsNullOrEmpty(bindAddress))
            {
                // Set it explicitly.
                eAddress.SetHost(bindAddress);
            }

            using (Host serverWorkerHost = new Host())
            {
                try
                {
                    serverWorkerHost.Create(eAddress, peers, channels, 0, 0);
                    ServerStarted = true;
                }
                catch (Exception e)
                {
                    Debug.LogError("Ignorance: An fatal exception has occurred and the transport will be shut down. TTo help debug the issue, use a Debug DLL of ENet and " +
                        $"look for a logfile in the root of the application folder. If you believe you found a bug, please report it on the GitHub issue tracker. The exception returned was: {e}");
                    return;
                }

                Debug.Log($"Ignorance: Server started. Listening on {(string.IsNullOrEmpty(bindAddress) ? $"port {port}" : $"{bindAddress}, port {port}")}, {peers} peers each with {channels} channels. {maxPacketSize} byte max packets.");

                // The meat and potatoes.
                while (!serverShouldCeaseOperation)
                {
                    // Outgoing stuff
                    while (ServerOutgoingQueue.TryDequeue(out OutgoingPacket opkt))
                    {
                        switch (opkt.commandType)
                        {
                            case CommandPacketType.Server_NoCommand:
                                // Twiddle thumbs.
                                print("Ignorance: Server is twiddling thumbs this cycle...");
                                break;

                            case CommandPacketType.Server_SendData:
                                if (ConnectionIDToPeers.TryGetValue(opkt.mirrorClientId, out Peer target))
                                {
                                    // Return code from the send function
                                    int returnCode = target.Send(opkt.channelId, ref opkt.payload);
                                    if (returnCode != 0) print($"Error code {returnCode} returned trying to send {opkt.payload.Length} bytes to Peer {target.ID} on channel {opkt.channelId}");
                                }
                                break;

                            case CommandPacketType.Server_ClientKick:
                                if (ConnectionIDToPeers.TryGetValue(opkt.mirrorClientId, out Peer bootedPeer))
                                {
                                    bootedPeer.DisconnectLater(0);
                                }
                                break;

                            default:
                                Debug.LogWarning($"Ignorance: Server queue has unknown command type: {opkt.commandType}");
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
                            if (serverWorkerHost.Service(threadWaitTimeout, out netEvent) <= 0)
                                break;

                            hasBeenPolled = true;
                        }

                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                break;

                            case EventType.Connect:
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
                                newConnectionPkt.mirrorClientId = nextConnectionId;
                                newConnectionPkt.type = QueuePacketType.Server_ClientConnect;
                                newConnectionPkt.ipAddress = netEvent.Peer.IP;

                                ServerIncomingQueue.Enqueue(newConnectionPkt);
                                nextConnectionId++;
                                break;

                            case EventType.Disconnect:
                            case EventType.Timeout:
                                if (PeersToConnectionIDs.TryGetValue(netEvent.Peer, out int deadPeer))
                                {
                                    IncomingPacket disconnectionPkt = default;
                                    disconnectionPkt.mirrorClientId = deadPeer;
                                    disconnectionPkt.type = QueuePacketType.Server_ClientDisconnect;
                                    disconnectionPkt.ipAddress = netEvent.Peer.IP;

                                    ServerIncomingQueue.Enqueue(disconnectionPkt);
                                    ConnectionIDToPeers.TryRemove(deadPeer, out _);
                                }

                                PeersToConnectionIDs.TryRemove(netEvent.Peer, out _);
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

                                    if (netEvent.Packet.Length > maxPacketSize)
                                    {
                                        print($"Ignorance: Packet dropped, it was too large from the server. Your maximum packet allows only {maxPacketSize} byte packets, this one was {netEvent.Packet.Length} byte(s).");
                                        netEvent.Packet.Dispose();
                                        return;
                                    }

                                    // Copy to the packet cache.
                                    try
                                    {
                                        // Pack it up and ready to ship it to Mirror.
                                        IncomingPacket dataPkt = default;
                                        dataPkt.mirrorClientId = dataConnID;
                                        dataPkt.channelId = netEvent.ChannelID;

                                        dataPkt.type = QueuePacketType.Server_IncomingData;
                                        dataPkt.ipAddress = netEvent.Peer.IP;

                                        byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(netEvent.Packet.Length);
                                        netEvent.Packet.CopyTo(rentedBuffer);

                                        dataPkt.data = rentedBuffer;
                                        dataPkt.length = netEvent.Packet.Length;

                                        ServerIncomingQueue.Enqueue(dataPkt);

                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogError($"Ignorance caught an exception while trying to copy data from the unmanaged (ENet) world to managed (Mono/IL2CPP) world. Please consider reporting this to the Ignorance developer on GitHub.\n" +
                                            $"Exception returned was: {e.Message}\n" +
                                            $"Debug details: {(netEvent.Packet.Data == null ? "NetEvent Packet payload was NULL" : $"NetEvent Packet payload was valid")}, {netEvent.Packet.Length} byte(s) packet\n" +
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

                Library.Deinitialize();
                Debug.Log("Ignorance has deinitialized ENet.");
            }
        }
#endregion

#region Mirror 6.2+ - URI Support
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder
            {
                Scheme = IgnoranceInternals.Scheme,
                Host = ServerBindAddress,
                Port = CommunicationPort
            };
            return builder.Uri;
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != IgnoranceInternals.Scheme)
                throw new ArgumentException($"Invalid uri {uri}, use {IgnoranceInternals.Scheme}://host:port instead", nameof(uri));

            if (!uri.IsDefaultPort)
            {
                // Set the communication port to the one specified.
                CommunicationPort = uri.Port;
            }

            ClientConnect(uri.Host);
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

        /// <summary>
        /// Enqueues a packet for ENET worker to pick up and dispatch.
        /// Hopefully should make it easier to fix things.
        /// </summary>
        /// <param name="channelId">The channel id you wish to send the packet on. Must be within 0 and the count of the channels array.</param>
        /// <param name="dataPayload">The array segment containing the data to send to ENET.</param>
        /// <returns></returns>
        private bool ENetClientQueueInternal(int channelId, ArraySegment<byte> dataPayload)
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
            ClientOutgoingQueue.Enqueue(opkt);

            return true;
        }

        private bool EnqueuePacketForDelivery(int connectionId, int channelId, ArraySegment<byte> data)
        {
            if (!ServerStarted)
            {
                Debug.LogError("Ignorance: Attempted to send while the server was not active");
                return false;
            }

            if (channelId > Channels.Length)
            {
                Debug.LogWarning($"Ignorance: Attempted to send data on channel {channelId} when we only have {Channels.Length} channels defined");
                return false;
            }

            OutgoingPacket op = default;

            op.commandType = CommandPacketType.Server_SendData;
            op.mirrorClientId = connectionId;
            op.channelId = (byte)channelId;

            Packet dataPayload = default;
            dataPayload.Create(data.Array, data.Offset, data.Count + data.Offset, (PacketFlags)Channels[channelId]);

            op.payload = dataPayload;

            ServerOutgoingQueue.Enqueue(op);
            return true;
        }
        #endregion


        // Deprecated shit.
#if !MIRROR_26_0_OR_NEWER
        // Can't deprecate this due to Dissonance...
        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
        {
            return EnqueuePacketForDelivery(connectionId, channelId, data);
        }
#endif

        #region Structs, classes, etc
        // Incoming packet struct.
        private struct IncomingPacket
        {
            public int mirrorClientId;              // Which Mirror connection did this come from?
            public int enetPeerId;                  // (Not yet used.)
            public int channelId;                   // What channel is this coming in on?
            public QueuePacketType type;            // What type of packet is this?
            public byte[] data;                     // The packet data payload (later recycled via BufferPool)
            public string ipAddress;                // The IP address of the client that sent us the packet.
            internal int length;
        }

        // Outgoing packet struct
        private struct OutgoingPacket
        {
            public int mirrorClientId;              // Which connection is this going to?
            public byte channelId;                  // Which channel was this received on?
            public Packet payload;                  // Packet payload.
            public CommandPacketType commandType;   // What was the packet telling us?
        }

        // Queue Packet Type Struct. Not to be confused with the ENET Packet Type.
        [Serializable]
        public enum QueuePacketType
        {
            // Server Messages
            Server_ClientConnect,
            Server_ClientDisconnect,
            Server_IncomingData,

            // Client Messages
            Client_ConnectedToServer,
            Client_DisconnectedFromServer,
            Client_IncomingData
        }

        // Command Packet Type Struct.
        public enum CommandPacketType
        {
            Client_NoCommand,
            Client_DisconnectNow,
            Client_DisconnectLater,

            Server_NoCommand,
            Server_ClientKick,
            Server_SendData,
        }

        // -> Moved ChannelTypes enum to it's own file, so it's easier to maintain.
        #endregion
    }


}
