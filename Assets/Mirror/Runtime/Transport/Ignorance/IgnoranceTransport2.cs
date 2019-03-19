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

        // Server's Send and Receive Queues
        public int ServerOutgoingQueueSize = 524288;
        public int ServerIncomingEventQueueSize = 524288;

        public int ClientOutgoingQueueSize = 524288;
        public int ClientIncomingEventQueueSize = 524288;

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
            Debug.Log($"Ignorance Transport, experimental version has awakened.");
#if UNITY_EDITOR_OSX
            Debug.LogWarning("MacOS Editor detected. Binding address workarounds engaged.");
#endif
            Library.Initialize();
        }

        private void OnDestroy()
        {
            Library.Deinitialize();
        }

        public override string ToString()
        {
            return $"Ignorance on { (string.IsNullOrEmpty(ServerShowerhead.Address) ? $"all interfaces, port {Port}." : $"{ServerShowerhead.Address}, port {Port}")}";
        }

        #region Client World
        public override void ClientConnect(string address)
        {
            if (ClientIncomingEventQueueSize > 0)
            {
                ClientShowerhead.IncomingEventQueueCapacity = ClientIncomingEventQueueSize;
            }

            if (ClientOutgoingQueueSize > 0)
            {
                ClientShowerhead.OutgoingPacketQueueCapacity = ClientOutgoingQueueSize;
            }

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
            return ServerShowerhead.GetClientAddress(connectionId);
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

            if (ServerShowerhead.knownConnIDToPeers.TryGetValue(connectionId, out uint peerId))
            {
                ServerShowerhead.Outgoing.Enqueue(new QueuedOutgoingPacket()
                {
                    targetPeerId = peerId,
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

            if (ClientIncomingEventQueueSize > 0)
            {
                ClientShowerhead.IncomingEventQueueCapacity = ClientIncomingEventQueueSize;
            }

            if (ClientOutgoingQueueSize > 0)
            {
                ClientShowerhead.OutgoingPacketQueueCapacity = ClientOutgoingQueueSize;
            }

            ServerShowerhead.ReceiveEventQueueSize = ServerIncomingEventQueueSize;
            ServerShowerhead.SendPacketQueueSize = ServerOutgoingQueueSize;
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
            if (ServerShowerhead.Incoming != null)
            {
                QueuedIncomingEvent evt;

                while (ServerShowerhead.Incoming.TryDequeue(out evt))
                {
                    switch (evt.eventType)
                    {
                        case ENet.EventType.Connect:
                            if (DebugEnabled)
                            {
                                Debug.Log($"Main Thread: Server has a new client! Peer ID: {evt.peerId}, Mirror CID: {ServerShowerhead.nextAvailableSlot}");
                            }

                            ServerShowerhead.knownPeersToConnIDs.Add(evt.peerId, ServerShowerhead.nextAvailableSlot);
                            ServerShowerhead.knownConnIDToPeers.Add(ServerShowerhead.nextAvailableSlot, evt.peerId);

                            OnServerConnected.Invoke(ServerShowerhead.nextAvailableSlot);
                            ServerShowerhead.nextAvailableSlot++;

                            break;

                        case ENet.EventType.Disconnect:
                            if (DebugEnabled)
                            {
                                Debug.Log($"Main Thread: Server had a client disconnect. Peer ID: {evt.peerId}");
                            }

                            if (ServerShowerhead.knownPeersToConnIDs.TryGetValue(evt.peerId, out int deadPeerConnID))
                            {
                                OnServerDisconnected.Invoke(deadPeerConnID);
                                ServerShowerhead.PeerDisconnectedInternal(evt.peerId);
                            }
                            break;

                        case ENet.EventType.Timeout:
                            if(DebugEnabled)
                            {
                                Debug.Log($"Main Thread: Server had a client timeout. ID: Peer ID: {evt.peerId}");
                            }
                            
                            if (ServerShowerhead.knownPeersToConnIDs.TryGetValue(evt.peerId, out int timedOutConnID))
                            {
                                OnServerDisconnected.Invoke(timedOutConnID);
                                ServerShowerhead.PeerDisconnectedInternal(evt.peerId);
                            }
                            break;

                        case ENet.EventType.Receive:
                            if (ServerShowerhead.knownPeersToConnIDs.TryGetValue(evt.peerId, out int connectionId))
                            {
                                OnServerDataReceived.Invoke(connectionId, evt.databuff);
                            }
                            break;
                    }
                }
            }
        }
        #endregion

        #region Client packet queue processors 
        private void ProcessQueuedClientEvents()
        {
            if (ClientShowerhead.Incoming != null)
            {
                while (ClientShowerhead.Incoming.Count > 0)
                {
                    QueuedIncomingEvent evt;

                    if (ClientShowerhead.Incoming.TryDequeue(out evt))
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
        #endregion
    }
}
