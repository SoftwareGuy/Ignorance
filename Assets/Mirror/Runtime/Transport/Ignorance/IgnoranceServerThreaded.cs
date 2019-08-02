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
// EXPERIMENTAL SERVER THREADED VERSION
// The client is not threaded in anyway.
// -----------------
// DO NOT USE IN PRODUCTION! (Improvements welcome, don't delay - submit a PR today!)

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
namespace Mirror
{
    public class IgnoranceServerThreaded : Transport, ISegmentTransport
    {
        // Queues
        static ConcurrentQueue<IncomingPacket> MirrorIncomingQueue;    // queue going into mirror from clients.
        static ConcurrentQueue<OutgoingPacket> MirrorOutgoingQueue;    // queue going to clients from Mirror.
        // lookup and reverse lookup dictionaries
        static ConcurrentDictionary<int, Peer> ConnectionIDToPeers = new ConcurrentDictionary<int, Peer>();
        static ConcurrentDictionary<Peer, int> PeersToConnectionIDs = new ConcurrentDictionary<Peer, int>();

        // Threads
        static Thread serverWorker;
        static volatile bool serverShouldCeaseOperation;
        // Client stuffs.
        private Host ClientHost = new Host();
        private Peer ClientPeer = new Peer();
        private Address ClientAddress = new Address();
        private byte[] ClientPacketCache;
        // Standard stuffs
        private bool ENETInitialized = false;
        private bool ServerStarted = false;
        private bool ClientStarted = false;

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
        public ChannelTypes[] Channels;

        // Standard things
        public void Awake()
        {
            print($"Ignorance Server Threaded Transport has awakened...");
            Debug.LogWarning("Use of this threaded transport is strongly discouraged in production. If it breaks, report a bug on the GitHub with as much detail as possible.");

            if (MaximumPeerCCU > 4095)
            {
                Debug.LogWarning("WARNING: You cannot have more than 4096 peers with this transport. While this is an artificial limitation and more peers are technically supported, it is a limitation of the underlying C library.");
                Debug.LogWarning("Do not file a bug report regarding this. There's a valid reason why 4096 is the maximum limit.");
                MaximumPeerCCU = 4095;
            }
        }

        public override string ToString()
        {
            return "Ignorance (Threaded server experimental version)";
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
                    case MirrorPacketType.Connect:
                        OnServerConnected?.Invoke(pkt.connectionId);
                        break;
                    case MirrorPacketType.Disconnect:
                        OnServerDisconnected?.Invoke(pkt.connectionId);
                        break;
                    case MirrorPacketType.Data:
                        OnServerDataReceived?.Invoke(pkt.connectionId, new ArraySegment<byte>(pkt.data));
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
        // Client processing loop.
        private bool ProcessClientMessages()
        {
            // Never do anything when ENET is not initialized
            if (!ENETInitialized)
            {
                return false;
            }

            // Never do anything when ENET is in a different mode
            if (!IsValid(ClientHost) || ClientPeer.State == PeerState.Uninitialized || !ClientStarted)
            {
                return false;
            }

            bool clientWasPolled = false;

            // Only process messages if the client is valid.
            while (!clientWasPolled)
            {
                if (!IsValid(ClientHost)) return false;

                if (ClientHost.CheckEvents(out Event networkEvent) <= 0)
                {
                    if (ClientHost.Service(0, out networkEvent) <= 0) break;
                    clientWasPolled = true;
                }

                switch (networkEvent.Type)
                {
                    case EventType.Connect:
                        // Client connected.
                        OnClientConnected.Invoke();
                        break;
                    case EventType.Timeout:
                    case EventType.Disconnect:
                        // Client disconnected.
                        OnClientDisconnected.Invoke();
                        break;
                    case EventType.Receive:
                        // Client recieving some data.
                        if (networkEvent.Packet.Length > ClientPacketCache.Length)
                        {
                            if (DebugEnabled) Debug.Log($"Ignorance: Packet too big to fit in buffer. {networkEvent.Packet.Length} packet bytes vs {ClientPacketCache.Length} cache bytes {networkEvent.Peer.ID}.");
                            networkEvent.Packet.Dispose();
                        }
                        else
                        {
                            // invoke on the client.
                            networkEvent.Packet.CopyTo(ClientPacketCache);
                            int spLength = networkEvent.Packet.Length;
                            networkEvent.Packet.Dispose();

                            OnClientDataReceived.Invoke(new ArraySegment<byte>(ClientPacketCache, 0, spLength));
                        }
                        break;
                }
            }
            // We're done here. Return.
            return true;
        }

        // Is the client connected?
        public override bool ClientConnected()
        {
            return ClientPeer.IsSet && ClientPeer.State == PeerState.Connected;
        }

        public override void ClientConnect(string address)
        {
            ClientPacketCache = new byte[MaxPacketSizeInKb * 1024];
            if (DebugEnabled) Debug.Log($"Initialized new packet cache, {MaxPacketSizeInKb * 1024} bytes capacity.");

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

            if (DebugEnabled) Debug.Log($"Ignorance: DEBUGGING MODE - ClientConnect({address})");

            if (Channels.Length > 255)
            {
                Debug.LogError($"Ignorance: Too many channels. Channel limit is 255, you have {Channels.Length}. This would probably crash ENET. Aborting connection.");
                return;
            }

            if (CommunicationPort < ushort.MinValue || CommunicationPort > ushort.MaxValue)
            {
                Debug.LogError($"Ignorance: Bad communication port number. You need to set it between port 0 and 65535. Aborting connection.");
                return;
            }

            if (ClientHost == null || !ClientHost.IsSet) ClientHost.Create(null, 1, Channels.Length, 0, 0, ClientPacketCache.Length);
            if (DebugEnabled) Debug.Log($"Ignorance: DEBUGGING MODE - Created ENET Host object");

            ClientAddress.SetHost(address);
            ClientAddress.Port = (ushort)CommunicationPort;

            ClientPeer = ClientHost.Connect(ClientAddress, Channels.Length);

            // NOT IMPLEMENTED
            // if (CustomTimeoutLimit) ClientPeer.Timeout(Library.throttleScale, CustomTimeoutBaseTicks, CustomTimeoutBaseTicks * CustomTimeoutMultiplier);
            ClientStarted = true;

            if (DebugEnabled) Debug.Log($"Ignorance: DEBUGGING MODE - Client has been started!");
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            // redirect it to the ArraySegment version.
            return ClientSend(channelId, new ArraySegment<byte>(data));
        }

        public bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            // Log spam if you really want that...
            // if (DebugEnabled) Debug.Log($"Ignorance: DEBUGGING MODE - ClientSend({channelId}, ({data.Count} bytes not shown))");
            if (!ClientHost.IsSet) return false;
            if (channelId > Channels.Length)
            {
                Debug.LogWarning($"Ignorance: Attempted to send data on channel {channelId} when we only have {Channels.Length} channels defined");
                return false;
            }

            Packet payload = default;
            payload.Create(data.Array, data.Offset, data.Count + data.Offset, (PacketFlags)Channels[channelId]);

            if (ClientPeer.Send((byte)channelId, ref payload))
            {
                if (DebugEnabled) Debug.Log($"Ignorance: DEBUGGING MODE - Outgoing packet on channel {channelId} OK");
                return true;
            }
            else
            {
                if (DebugEnabled) Debug.Log($"Ignorance: DEBUGGING MODE - Outgoing packet on channel {channelId} FAIL");
                return false;
            }
        }

        public override void ClientDisconnect()
        {
            if (DebugEnabled) Debug.Log($"Ignorance: DEBUGGING MODE - ClientDisconnect()");

            if (ServerStarted)
            {
                Debug.LogWarning("MIRROR BUG: ClientDisconnect called even when we're in HostClient/Dedicated Server mode");
                return;
            }

            if (!IsValid(ClientHost)) return;
            if (ClientPeer.IsSet) ClientPeer.DisconnectNow(0);

            // Flush and free resources.
            if (IsValid(ClientHost))
            {
                ClientHost.Flush();
                ClientHost.Dispose();
            }
        }
        #endregion

        #region Server Portion
        public override bool ServerActive()
        {
            return ServerStarted;
        }

        public override void ServerStart()
        {
            serverShouldCeaseOperation = false;

            serverWorker = IgnoranceServerThread();

            print($"Ignorance Threaded: Starting the server thread... Let's hope this doesn't break...");
            print($"If this was the last thing you see then file a bug report");
            serverWorker.Start();
            ServerStarted = true;
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return ServerSend(connectionId, channelId, new ArraySegment<byte>(data));
        }

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
            if(serverWorker != null && serverWorker.IsAlive) serverWorker.Join();

            ServerStarted = false;
            print($"Ignorance Threaded: Server stopped.");
        }

        public override void Shutdown()
        {
            serverShouldCeaseOperation = true;
            if (serverWorker != null && serverWorker.IsAlive) serverWorker.Join();

            ServerStarted = false;
            ClientStarted = false;

            if(ENETInitialized) Library.Deinitialize();
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

            Thread t = new Thread(() => ServerWorker(bindAddress, (ushort)CommunicationPort, Channels.Length, MaximumPeerCCU, MaxPacketSizeInKb * 1024));
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

            if(!string.IsNullOrEmpty(bindAddress)) eAddress.SetHost(bindAddress);

            using (Host serverWorkerHost = new Host())
            {
                try
                {
                    serverWorkerHost.Create(eAddress, maxPeers, channels, 0, 0, maxPacketSize);
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
                        switch(opkt.commandType)
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
                                    if (!target.Send(opkt.channelId, ref opkt.payload)) print($"Could not send {opkt.payload.Length} bytes to target peer {target.ID} on channel {opkt.channelId}");
                                }
                                break;
                        }
                    }

                    // Flush here?
                    serverWorkerHost.Flush();

                    // Incoming stuffs now.
                    bool hasBeenPolled = false;

                    // Reuseable reply packet.
                    // IncomingPacket reply = default;

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
                                newConnectionPkt.type = MirrorPacketType.Connect;
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
                                    disconnectionPkt.type = MirrorPacketType.Disconnect;
                                    disconnectionPkt.ipAddress = netEvent.Peer.IP;

                                    MirrorIncomingQueue.Enqueue(disconnectionPkt);
                                    ConnectionIDToPeers.TryRemove(deadPeer, out Peer _);
                                }

                                PeersToConnectionIDs.TryRemove(netEvent.Peer, out int _);
                                break;

                            case EventType.Receive:
                                // print($"DATA EVENT!");
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
                                    dataPkt.type = MirrorPacketType.Data;

                                    // TODO: Come up with a better method of doing this. FSE is gonna scream at me most likely. (and if he's reading this, hi there fse)
                                    // This allocates shit on the thread stack but ArraySegment was catching fire for some reason causing all sorts of mirror dupe connection bugs
                                    // Probably something silly i'm doing but since this is experimental, getting it working is priority. At least it's using a struct
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
                if (Channels[0] != ChannelTypes.Reliable) Channels[0] = ChannelTypes.Reliable;
                if (Channels[1] != ChannelTypes.Unreliable) Channels[1] = ChannelTypes.Unreliable;
            }
            else
            {
                Channels = new ChannelTypes[2]
                {
                    ChannelTypes.Reliable,
                    ChannelTypes.Unreliable
                };
            }
        }
        #endregion

        #region Structs, classes, etc
        // Structs to make life easier.
        private struct IncomingPacket
        {
            public int connectionId;
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

        [Serializable]
        public enum ChannelTypes
        {
            Reliable = PacketFlags.Reliable,
            ReliableUnsequenced = PacketFlags.Reliable | PacketFlags.Unsequenced,
            Unreliable = PacketFlags.Unsequenced,
            UnreliableFragmented = PacketFlags.UnreliableFragmented,
            UnreliableSequenced = PacketFlags.None,
            UnbundledInstant = PacketFlags.Instant,
        }

        [Serializable]
        public enum MirrorPacketType
        {
            Connect,
            Disconnect,
            Data
        }

        public enum CommandPacketType
        {
            Nothing,
            BootToTheFace
        }

        #endregion
    }


}
