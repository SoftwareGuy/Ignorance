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
// Use with caution.
// -----------------

using UnityEngine;
using System.Collections;
using System;
using Debug = UnityEngine.Debug;
using System.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;
using ENet;
using Event = ENet.Event;
using EventType = ENet.EventType;
using System.Collections.Generic;

namespace Mirror
{
    public class IgnoranceThreaded : Transport, ISegmentTransport
    {
        // Client Queues
        static ConcurrentQueue<IncomingPacket> MirrorClientIncomingQueue = new ConcurrentQueue<IncomingPacket>();
        static ConcurrentQueue<OutgoingPacket> MirrorClientOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();

        // Server Queues
        static ConcurrentQueue<IncomingPacket> MirrorIncomingQueue = new ConcurrentQueue<IncomingPacket>();    // queue going into mirror from clients.
        static ConcurrentQueue<OutgoingPacket> MirrorOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();    // queue going to clients from Mirror.

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


        // private byte[] ClientPacketCache;
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
        public int MaxPacketSizeInKb = 64;

        [Header("Channel Definitions")]
        public IgnoranceChannelTypes[] Channels;

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
            return "Ignorance Threaded Edition";
        }

        public void LateUpdate()
        {
            if (enabled)
            {
                // Server will pump itself...
                if (ServerStarted) ProcessServerMessages();
                if (ClientStarted) ProcessClientMessages();
            }
        }

        private bool InitializeENET()
        {
            return Library.Initialize();
        }

        // Server processing loop.
        private bool ProcessServerMessages()
        {
            // Get to the queue! Check those corners!
            while (MirrorIncomingQueue.TryDequeue(out IncomingPacket pkt))
            {
                // print($"Firing a packet with type: {pkt.type} for connection ID: {pkt.connectionId}\n{(pkt.data == null ? "NO DATA" : BitConverter.ToString(pkt.data))}");
                switch (pkt.type)
                {
                    case MirrorPacketType.ServerClientConnected:
                        OnServerConnected?.Invoke(pkt.connectionId);
                        break;
                    case MirrorPacketType.ServerClientDisconnected:
                        OnServerDisconnected?.Invoke(pkt.connectionId);
                        break;
                    case MirrorPacketType.ServerClientSentData:
#if MIRROR_4_0_OR_NEWER
                        OnServerDataReceived?.Invoke(pkt.connectionId, new ArraySegment<byte>(pkt.data), pkt.channelId);
#else
                        OnServerDataReceived?.Invoke(pkt.connectionId, new ArraySegment<byte>(pkt.data));
#endif
                        break;
                    default:
                        // Nothing to see here.
                        break;
                }
            }

            // Flashbang though the window and race to the finish.
            return true;
        }

        #region Client Portion
        private bool ProcessClientMessages()
        {
            while (MirrorClientIncomingQueue.TryDequeue(out IncomingPacket pkt))
            {
                switch (pkt.type)
                {
                    case MirrorPacketType.ClientConnected:
                        if (DebugEnabled) print($"Ignorance: We have connected!");
                        isClientConnected = true;
                        OnClientConnected?.Invoke();
                        break;
                    case MirrorPacketType.ClientDisconnected:
                        if (DebugEnabled) print($"Ignorance: We have been disconnected.");
                        isClientConnected = false;
                        OnClientDisconnected?.Invoke();
                        break;
                    case MirrorPacketType.ClientGotData:
#if MIRROR_4_0_OR_NEWER
                        OnClientDataReceived?.Invoke(new ArraySegment<byte>(pkt.data), pkt.channelId);
#else
                        OnClientDataReceived?.Invoke(new ArraySegment<byte>(pkt.data), pkt.channelId);
#endif
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

#if MIRROR_4_0_OR_NEWER
        public override bool ClientSend(int channelId, ArraySegment<byte> data) {
            if (channelId > Channels.Length)
            {
                Debug.LogWarning($"Ignorance: Attempted to send data on channel {channelId} when we only have {Channels.Length} channels defined");
                return false;
            }

            OutgoingPacket opkt = default;
            opkt.channelId = (byte)channelId;

            Packet payload = default;
            payload.Create(data.Array, data.Offset, data.Count + data.Offset, (PacketFlags)Channels[channelId]);

            opkt.payload = payload;

            // Enqueue it.
            MirrorClientOutgoingQueue.Enqueue(opkt);

            return true;
        }

#else
        public override bool ClientSend(int channelId, byte[] data)
        {
            // redirect it to the ArraySegment version.
            return ClientSend(channelId, new ArraySegment<byte>(data));
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
            opkt.commandType = CommandPacketType.ClientDisconnectRequest;
            MirrorClientOutgoingQueue.Enqueue(opkt);

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

#if !MIRROR_4_0_OR_NEWER
        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return ServerSend(connectionId, channelId, new ArraySegment<byte>(data));
        }
#endif
        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
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

            MirrorOutgoingQueue.Enqueue(op);
            return true;
        }
        public override bool ServerDisconnect(int connectionId)
        {
            OutgoingPacket op = default;
            op.connectionId = connectionId;
            op.commandType = CommandPacketType.BootToTheFace;
            return true;
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return "UNKNOWN";
        }

        public override void ServerStop()
        {
            serverShouldCeaseOperation = true;
            Thread.Sleep(5);    // Allow it to have a micro-sleep
            if (serverWorker != null && serverWorker.IsAlive) serverWorker.Join();

            // IMPORTANT: Flush the queues. Get rid of the dead bodies.
            // c6: Do not use new, instead just while dequeue anything else in the queue
            // c6: helps avoid GC
            while (MirrorIncomingQueue.TryDequeue(out _))
            {
                ;
            }

            while (MirrorOutgoingQueue.TryDequeue(out _))
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

            if (ENETInitialized) Library.Deinitialize();
            ENETInitialized = false;
        }
        #endregion

        #region General Purpose
        public override int GetMaxPacketSize(int channelId = 0)
        {
            return MaxPacketSizeInKb * 1024;
        }

        // utility
        private bool IsValid(Host host)
        {
            return host != null && host.IsSet;
        }
        #endregion

        #region Client Threading
        private Thread IgnoranceClientThread()
        {
            Thread t = new Thread(() => ClientWorker(clientConnectionAddress, (ushort)CommunicationPort, Channels.Length, MaxPacketSizeInKb * 1024, EnetPollTimeout));
            return t;
        }

        private static void ClientWorker(string hostAddress, ushort port, int channelCount, int maxPacketSize = 16384, int serviceTimeout = 1)
        {
            // Drain anything in the queue...
            while (MirrorClientIncomingQueue.TryDequeue(out _))
            {
                ;
            }

            while (MirrorClientOutgoingQueue.TryDequeue(out _))
            {
                ;
            }

            byte[] workerPacketBuffer = new byte[maxPacketSize];
            Address cAddress = new Address();

            using (Host cHost = new Host())
            {
                try
                {
                    cHost.Create(null, 1, channelCount, 0, 0, maxPacketSize);
                    ClientStarted = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"EPIC FAILURE: {e.ToString()}");
                    return;
                }

                // Attempt to start connection...
                cAddress.SetHost(hostAddress);
                cAddress.Port = port;
                Peer cPeer = cHost.Connect(cAddress, channelCount);

                while (!clientShouldCeaseOperation)
                {
                    bool clientWasPolled = false;

                    while (!clientWasPolled)
                    {
                        if (cHost.CheckEvents(out Event networkEvent) <= 0)
                        {
                            if (cHost.Service(serviceTimeout, out networkEvent) <= 0) break;
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
                                if (networkEvent.Packet.Length > workerPacketBuffer.Length)
                                {
                                    print($"Ignorance: Packet too big to fit in buffer. {networkEvent.Packet.Length} packet bytes vs {workerPacketBuffer.Length} cache bytes {networkEvent.Peer.ID}.");
                                    networkEvent.Packet.Dispose();
                                }
                                else
                                {
                                    // invoke on the client.
                                    networkEvent.Packet.CopyTo(workerPacketBuffer);
                                    int spLength = networkEvent.Packet.Length;
                                    networkEvent.Packet.Dispose();

                                    IncomingPacket dataPkt = default;
                                    dataPkt.type = MirrorPacketType.ClientGotData;
                                    dataPkt.data = new byte[spLength];  // Grrr!!!
                                    networkEvent.Packet.CopyTo(dataPkt.data);

                                    MirrorClientIncomingQueue.Enqueue(dataPkt);
                                }
                                break;
                        }
                    }

                    // Outgoing stuff
                    while (MirrorClientOutgoingQueue.TryDequeue(out OutgoingPacket opkt))
                    {
                        if (opkt.commandType == CommandPacketType.ClientDisconnectRequest)
                        {
                            cPeer.DisconnectNow(0);
                            return;
                        }

                        int returnCode = cPeer.SendAndReturnStatusCode(opkt.channelId, ref opkt.payload);
                        if (returnCode != 0) print($"Could not send {opkt.payload.Length} bytes to server on channel {opkt.channelId}, error code {returnCode}");
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
            /* if (ServerBindAll)
            {
                if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX) bindAddress = "::0";
                else bindAddress = "0.0.0.0";
            }
            */

            Thread t = new Thread(() => ServerWorker(bindAddress, (ushort)CommunicationPort, Channels.Length, MaximumPeerCCU, MaxPacketSizeInKb * 1024, EnetPollTimeout));
            return t;
        }

        // Thread Wrapper.
        private static void ServerWorker(string bindAddress, ushort port, int channels = 255, int maxPeers = 4095, int maxPacketSize = 16384, int serviceTimeout = 1)
        {
            // Setup queues.
            MirrorIncomingQueue = new ConcurrentQueue<IncomingPacket>();
            MirrorOutgoingQueue = new ConcurrentQueue<OutgoingPacket>();

            // Setup our own packet buffer.
            byte[] workerPacketBuffer = new byte[maxPacketSize];

            // Connection ID.
            int nextConnectionId = 1;

            // Server address properties
            Address eAddress = new Address()
            {
                Port = port,
            };

            if (!string.IsNullOrEmpty(bindAddress)) eAddress.SetHost(bindAddress);

            using (Host serverWorkerHost = new Host())
            {
                try
                {
                    serverWorkerHost.Create(eAddress, maxPeers, channels, 0, 0, maxPacketSize);
                    ServerStarted = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"EPIC FAILURE: {e.ToString()}");
                    Debug.LogError("Server worker thread is dying now.");
                    return;
                }

                Debug.Log($"Server worker listening on UDP port {port}. Capacity: {maxPeers} peers. Channels: {channels}. Buffer size: {maxPacketSize} bytes");

                // The meat and potatoes.
                while (!serverShouldCeaseOperation)
                {
                    // Outgoing stuff
                    while (MirrorOutgoingQueue.TryDequeue(out OutgoingPacket opkt))
                    {
                        switch (opkt.commandType)
                        {
                            case CommandPacketType.BootToTheFace:
                                if (ConnectionIDToPeers.TryGetValue(opkt.connectionId, out Peer bootedPeer))
                                {
                                    bootedPeer.DisconnectLater(0);
                                }
                                break;

                            case CommandPacketType.Nothing:
                            default:
                                if (ConnectionIDToPeers.TryGetValue(opkt.connectionId, out Peer target))
                                {
                                    int returnCode = target.SendAndReturnStatusCode(opkt.channelId, ref opkt.payload);
                                    if (returnCode != 0) print($"Could not send {opkt.payload.Length} bytes to target peer {target.ID} on channel {opkt.channelId}, error code {returnCode}");
                                }
                                break;
                        }
                    }

                    // Flush here?
                    serverWorkerHost.Flush();

                    // Incoming stuffs now.
                    bool hasBeenPolled = false;

                    while (!hasBeenPolled)
                    {
                        if (serverWorkerHost.CheckEvents(out Event netEvent) <= 0)
                        {
                            if (serverWorkerHost.Service(serviceTimeout, out netEvent) <= 0)
                                break;

                            hasBeenPolled = true;
                        }

                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                break;

                            case EventType.Connect:
                                // print($"CONNECT EVENT!");
                                int connectionId = nextConnectionId;
                                nextConnectionId += 1;

                                // Add to dictionaries.
                                if (!PeersToConnectionIDs.TryAdd(netEvent.Peer, connectionId)) Debug.LogError($"ERROR: We already know this client in our Connection ID to Peer Mapping?!");
                                if (!ConnectionIDToPeers.TryAdd(connectionId, netEvent.Peer)) Debug.LogError($"ERROR: We already know this client in our Peer to ConnectionID Mapping?!");

                                // Send a message back to mirror.
                                IncomingPacket newConnectionPkt = default;
                                newConnectionPkt.connectionId = connectionId;
                                newConnectionPkt.type = MirrorPacketType.ServerClientConnected;
                                newConnectionPkt.ipAddress = netEvent.Peer.IP;

                                MirrorIncomingQueue.Enqueue(newConnectionPkt);
                                break;

                            case EventType.Disconnect:
                            case EventType.Timeout:
                                // print($"DISCONNECT EVENT!");
                                if (PeersToConnectionIDs.TryGetValue(netEvent.Peer, out int deadPeer))
                                {
                                    IncomingPacket disconnectionPkt = default;
                                    disconnectionPkt.connectionId = deadPeer;
                                    disconnectionPkt.type = MirrorPacketType.ServerClientDisconnected;
                                    disconnectionPkt.ipAddress = netEvent.Peer.IP;

                                    MirrorIncomingQueue.Enqueue(disconnectionPkt);
                                    ConnectionIDToPeers.TryRemove(deadPeer, out Peer _);
                                }

                                PeersToConnectionIDs.TryRemove(netEvent.Peer, out int _);
                                break;

                            case EventType.Receive:
                                int dataConnID = -1;

                                if (PeersToConnectionIDs.TryGetValue(netEvent.Peer, out dataConnID))
                                {
                                    if (netEvent.Packet.Length > maxPacketSize)
                                    {
                                        Debug.LogWarning($"WARNING: Packet too large for buffer; dropping. Packet {netEvent.Packet.Length} bytes; limit is {maxPacketSize} bytes.");
                                        netEvent.Packet.Dispose();
                                        return;
                                    }

                                    // Copy to the packet cache.
                                    netEvent.Packet.CopyTo(workerPacketBuffer);
                                    int spLength = netEvent.Packet.Length;
                                    // print($"spLength {spLength}");

                                    IncomingPacket dataPkt = default;
                                    dataPkt.connectionId = dataConnID;
                                    dataPkt.channelId = (int)netEvent.ChannelID;
                                    dataPkt.type = MirrorPacketType.ServerClientSentData;

                                    // TODO: Come up with a better method of doing this.
                                    dataPkt.data = new byte[spLength];
                                    Array.Copy(workerPacketBuffer, 0, dataPkt.data, 0, spLength);
                                    // Faulty .Array on the end seems to return the rest of the buffer as well instead of just 10 bytes or whatever
                                    // dataPkt.data = new ArraySegment<byte>(workerPacketBuffer, 0, spLength);
                                    dataPkt.ipAddress = netEvent.Peer.IP;

                                    MirrorIncomingQueue.Enqueue(dataPkt);
                                }
                                else
                                {
                                    // Kick the peer.
                                    netEvent.Peer.DisconnectNow(0);
                                }

                                netEvent.Packet.Dispose();
                                break;
                        }
                    }
                }

                // Disconnect everyone, we're done here.
                print($"Kicking all connected Peers...");
                foreach (System.Collections.Generic.KeyValuePair<int, Peer> kv in ConnectionIDToPeers) kv.Value.DisconnectNow(0);

                print("Flushing...");
                serverWorkerHost.Flush();
                ServerStarted = false;
            }
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

#if MIRROR_4_0_OR_NEWER
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

            foreach(int conn in connectionIds) {
                // Another sneaky hack
                ServerSend(conn, channelId, segment);
                /*
                    OutgoingPacket op = default;
                    op.connectionId = conn;
                    op.channelId = (byte)channelId;

                    Packet dataPayload = default;
                    dataPayload.Create(segment.Array, segment.Offset, segment.Count + segment.Offset, (PacketFlags)Channels[channelId]);
                    op.payload = dataPayload;

                    MirrorOutgoingQueue.Enqueue(op);
                */
            }

            return true;          
        }
#endif

        #endregion

        #region Structs, classes, etc
        // Structs to make life easier.
        private struct IncomingPacket
        {
            public int connectionId;
            public int channelId;
            public MirrorPacketType type;
            public byte[] data;
            public string ipAddress;
        }

        private struct OutgoingPacket
        {
            public int connectionId;
            public byte channelId;
            public Packet payload;
            public CommandPacketType commandType;
        }

        // -> Moved ChannelTypes enum to it's own file, so it's easier to maintain.

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

        public enum CommandPacketType
        {
            Nothing,
            BootToTheFace,
            ClientDisconnectRequest
        }

#endregion
    }


}
