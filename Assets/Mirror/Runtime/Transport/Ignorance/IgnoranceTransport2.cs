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
using Mirror.Ignorance;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class IgnoranceTransport2 : Transport, ISegmentTransport
    {
        // Transport Port setting
        public string BindAddress = "";
        public ushort Port = 7778;
        public bool DebugEnabled = false;

        public List<KnownChannelTypes> ChannelDefinitions = new List<KnownChannelTypes>()
        {
            KnownChannelTypes.Reliable,     // Default channel 0, reliable
            KnownChannelTypes.Unreliable,   // Default channel 1, unreliable
        };

        public IgnoranceTransport2()
        {
            // Intentionally left blank.
        }

        private void Awake()
        {
            Debug.Log($"Ignorance Transport {IgnoranceConstants.TransportVersion} has awakened.");
#if UNITY_EDITOR_OSX
            Debug.LogWarning("MacOS Editor detected. Binding address workarounds engaged.");
#endif
            Library.Initialize();
        }

        private void OnDestroy()
        {
            ServerShowerhead.CeaseOperation = true;
            ClientShowerhead.CeaseOperation = true;

            System.Threading.Thread.Sleep(50);

            // Abort if these threads are runaway.
            // Check against null to ensure shit doesn't catch fire.
            if (ServerShowerhead.Nozzle != null && ServerShowerhead.Nozzle.IsAlive)
            {
                Debug.LogWarning("Ignorance Transport: Server worker thread has run away, exterminating...");
                ServerShowerhead.Nozzle.Abort();
            }
            // Check against null to ensure shit doesn't catch fire.
            if (ClientShowerhead.Nozzle != null && ClientShowerhead.Nozzle.IsAlive)
            {
                Debug.Log("Ignorance Transport: Client worker thread has run away, exterminating...");
                ClientShowerhead.Nozzle.Abort();
            }

            // Yank out the carpet.
            Library.Deinitialize();
        }

        public override string ToString()
        {
            return $"Ignorance on { (string.IsNullOrEmpty(ServerShowerhead.Address) ? $"all interfaces, port {Port}." : $"{ServerShowerhead.Address}, port {Port}")}";
        }

        #region Client World
        public override void ClientConnect(string address)
        {
            ClientShowerhead.NumChannels = ChannelDefinitions.Count;
            ClientShowerhead.DebugMode = DebugEnabled;

            ClientShowerhead.Start(address, Port);
        }

        public override bool ClientConnected()
        {
            return ClientShowerhead.IsClientConnected();
        }

        public override void ClientDisconnect()
        {
            ClientShowerhead.Stop();
        }

        public bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            Packet outPkt = default;
            outPkt.Create(data.Array, data.Offset, data.Count, MapKnownChannelTypeToENETPacketFlag(ChannelDefinitions[channelId]));

            // Failsafe.
            // outPkt.Create(data.Array, data.Offset, data.Count, PacketFlags.Reliable);

            ClientShowerhead.Outgoing.Enqueue(new QueuedOutgoingPacket()
            {
                channelId = (byte)channelId,
                contents = outPkt,
            });

            return true;
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            Packet outPkt = default;
            outPkt.Create(data, data.Length, MapKnownChannelTypeToENETPacketFlag(ChannelDefinitions[channelId]));

            ClientShowerhead.Outgoing.Enqueue(new QueuedOutgoingPacket()
            {
                channelId = (byte)channelId,
                contents = outPkt,
            });

            return true;
        }
        #endregion

        #region Server World
        public override bool ServerActive()
        {
            return ServerShowerhead.IsServerActive();
        }

        public override bool ServerDisconnect(int connectionId)
        {
            return ServerShowerhead.DisconnectThatConnection(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            if(KnownConnections.TryGetValue(connectionId, out PeerInfo value))
            {
                return value.PeerIp;
            }

            return "(unknown)";
        }

        public ushort ServerGetClientPort(int connectionId)
        {
            if(KnownConnections.TryGetValue(connectionId, out PeerInfo value))
            {
                return value.PeerPort;
            }
            
            return 0;
        }

        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
        {
            Packet outPkt = default;

            if (channelId >= ChannelDefinitions.Count)
            {
                Debug.LogWarning("NOQUEUE: Discarding a packet because the channel id is higher or equal to the amount of items in the channel list.");
                return false;
            }

            outPkt.Create(data.Array, data.Offset, data.Count, MapKnownChannelTypeToENETPacketFlag(ChannelDefinitions[channelId]));

            if (KnownConnections.ContainsKey(connectionId))
            {
                ServerShowerhead.Outgoing.Enqueue(new QueuedOutgoingPacket()
                {
                    targetConnectionId = connectionId,
                    // targetPeerId = KnownConnections[connectionId].PeerUniqueId,
                    channelId = (byte)channelId,
                    contents = outPkt,
                });
                return true;
            }

            return false;
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return ServerSend(connectionId, channelId, new ArraySegment<byte>(data));
        }

        public override void ServerStart()
        {
            if (ServerActive())
            {
                Debug.LogError("Ignorance Transport: The server is already running... Did you mean to stop it first?");
                return;
            }
            if ((ChannelDefinitions.Count - 1) >= 255)
            {
                Debug.LogError("Ignorance Transport: Too many channels. ENET-senpai can't handle them!");
                return;
            }

#if UNITY_EDITOR_OSX
            Debug.Log("Ignorance Transport: Binding to ::0 as a workaround for Mac OS LAN Host");
            m_ServerAddress.SetHost("::0");
#else
            if (string.IsNullOrEmpty(BindAddress))
            {
                if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                {
                    ServerShowerhead.Address = "::0";
                }
                else
                {
                    ServerShowerhead.Address = "0.0.0.0";
                }
            }
            else
            {
                ServerShowerhead.Address = BindAddress;
            }
#endif

            ServerShowerhead.NumChannels = ChannelDefinitions.Count;
            ServerShowerhead.DebugMode = DebugEnabled;

            // Start the server thread.
            ServerShowerhead.Start(Port);
        }

        public override void ServerStop()
        {
            ServerShowerhead.Stop();
        }

        public override void Shutdown()
        {
            ServerShowerhead.Shutdown();
            ClientShowerhead.Shutdown();
        }
        #endregion

        #region Server packet queue processors
        private void ProcessQueuedServerEvents()
        {
            QueuedIncomingEvent evt;
            while (ServerShowerhead.Incoming.TryDequeue(out evt))
            {
                switch (evt.eventType)
                {
                    case ENet.EventType.Connect:
                        if (DebugEnabled)
                        {
                            Debug.Log($"Main Thread: Server has a new client! Peer ID: {evt.peerId}, Mirror CID: {evt.connectionId}");
                        }

                        // Fun fact: Was supposed to be "Poi?" (Yuudachi's KanColle verbal tic) but I couldn't really find a use for the 'o'
                        PeerInfo pi = default;

                        pi.PeerUniqueId = evt.peerId;
                        pi.PeerIp = evt.peerIp;
                        pi.PeerPort = evt.peerPort;

                        AddToKnownConnections(evt.connectionId, pi);
                        OnServerConnected.Invoke(evt.connectionId);
                        break;

                    case ENet.EventType.Disconnect:
                        if (DebugEnabled)
                        {
                            Debug.Log($"Main Thread: Server had a client disconnect. Peer ID: {evt.peerId}");
                        }

                        if (evt.connectionId > 0)
                        {
                            OnServerDisconnected.Invoke(evt.connectionId);
                            RemoveFromKnownConnections(evt.connectionId);
                        }
                        break;

                    case ENet.EventType.Timeout:
                        if (DebugEnabled)
                        {
                            Debug.Log($"Main Thread: Server had a client timeout. ID: Peer ID: {evt.peerId}");
                        }

                        if (evt.connectionId > 0)
                        {
                            OnServerDisconnected.Invoke(evt.connectionId);
                        }
                        break;

                    case ENet.EventType.Receive:
                        if (evt.connectionId > 0)
                        {
                            OnServerDataReceived.Invoke(evt.connectionId, evt.databuff);
                        }
                        break;
                }
            }
        }
        #endregion

        #region Client packet queue processors 
        private void ProcessQueuedClientEvents()
        {
            QueuedIncomingEvent evt;

            while (ClientShowerhead.Incoming.TryDequeue(out evt))
            {
                switch (evt.eventType)
                {
                    case ENet.EventType.Connect:
                        OnClientConnected.Invoke();
                        break;
                    case ENet.EventType.Disconnect:
                    case ENet.EventType.Timeout:
                        OnClientDisconnected.Invoke();
                        break;
                    case ENet.EventType.Receive:
                        OnClientDataReceived.Invoke(evt.databuff);
                        break;
                }
            }
        }
        #endregion

        #region Update method
        private void LateUpdate()
        {
            if (enabled)
            {
                ProcessQueuedServerEvents();
                ProcessQueuedClientEvents();
            }
        }
        #endregion

        #region Packet Size (maximum)
        public override int GetMaxPacketSize(int channelId = 0)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes.
        }
        #endregion

        #region Helpers 
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

        // Adds a peer info struct to our dictonary.
        private void AddToKnownConnections(int connectionId, PeerInfo data)
        {
            KnownConnections.Add(connectionId, data);
        }

        // Removes a peer info struct from our dictonary.
        private void RemoveFromKnownConnections(int connectionId)
        {
            if(KnownConnections.ContainsKey(connectionId))
            {
                KnownConnections.Remove(connectionId);
            }
        }
        #endregion

        #region Internal database things 
        private Dictionary<int, PeerInfo> KnownConnections = new Dictionary<int, PeerInfo>();
        #endregion
    }
}
