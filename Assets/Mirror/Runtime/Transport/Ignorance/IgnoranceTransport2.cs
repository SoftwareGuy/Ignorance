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
using System.Threading;
using UnityEngine;

namespace Mirror
{
    public class IgnoranceTransport2 : Transport, ISegmentTransport
    {
        public ushort Port = 7778;

        public ushort ServerSendQueue = 4096;
        public ushort ServerReceiveQueue = 4096;

        public ushort ClientSendQueue = 4096;
        public ushort ClientReceiveQueue = 4096;

        public static volatile bool CeaseOperation = false;

        public static Thread Nozzle;

        public IgnoranceTransport2()
        {

        }

        private void Awake()
        {
            Debug.Log($"This is very experimental version of Ignorance and will likely catch fire.");

            Library.Initialize();
        }

        private void OnDestroy()
        {
            Library.Deinitialize();
        }

        public override string ToString()
        {
            return $"Ignorance on port {Port}.";
        }

        #region Client World
        public override void ClientConnect(string address)
        {
            ClientShowerhead.IncomingPacketQueueSize = ClientSendQueue;
            ClientShowerhead.OutgoingPacketQueueSize = ClientReceiveQueue;

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
            // outPkt.Create(data.Array, data.Offset, data.Count, MapKnownChannelTypeToENETPacketFlag(m_ChannelDefinitions[channelId]));
            outPkt.Create(data.Array, data.Offset, data.Count, PacketFlags.Reliable);

            ClientShowerhead.Outgoing.Enqueue(new QueuedOutgoingPacket()
            {
                channelId = (byte)channelId,
                contents = outPkt,
            });

            return true;
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            return ClientSend(channelId, new ArraySegment<byte>(data));
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

            // outPkt.Create(data.Array, data.Offset, data.Count, MapKnownChannelTypeToENETPacketFlag(m_ChannelDefinitions[channelId]));
            outPkt.Create(data.Array, data.Offset, data.Count, PacketFlags.Reliable);

            // TODO: Channels check.

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
            if (ServerShowerhead.Incoming != null && ServerShowerhead.Incoming.Count > 0)
            {
                while (ServerShowerhead.Incoming.Count > 0)
                {
                    QueuedIncomingEvent evt;

                    if (ServerShowerhead.Incoming.TryDequeue(out evt))
                    {
                        switch (evt.eventType)
                        {
                            case ENet.EventType.Connect:
                                Debug.Log($"Main Thread: Server has a new client! Peer ID: {evt.peerId}, Mirror CID: {ServerShowerhead.nextAvailableSlot}");

                                ServerShowerhead.knownPeersToConnIDs.Add(evt.peerId, ServerShowerhead.nextAvailableSlot);
                                ServerShowerhead.knownConnIDToPeers.Add(ServerShowerhead.nextAvailableSlot, evt.peerId);

                                OnServerConnected.Invoke(ServerShowerhead.nextAvailableSlot);
                                ServerShowerhead.nextAvailableSlot++;

                                break;

                            case ENet.EventType.Disconnect:
                                Debug.Log($"Main Thread: Server had a client disconnect. Peer ID: {evt.peerId}");
                                if (ServerShowerhead.knownPeersToConnIDs.TryGetValue(evt.peerId, out int deadPeerConnID))
                                {
                                    OnServerDisconnected.Invoke(deadPeerConnID);
                                    ServerShowerhead.PeerDisconnectedInternal(evt.peerId);
                                }
                                break;

                            case ENet.EventType.Timeout:
                                Debug.Log($"Main Thread: Server had a client timeout. ID: Peer ID: {evt.peerId}");
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
        }
        #endregion

        #region Client packet queue processors 
        private void ProcessQueuedClientEvents()
        {
            if (ClientShowerhead.Incoming != null && ClientShowerhead.Incoming.Count > 0)
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
        public override int GetMaxPacketSize(int channelId = 0)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes.
        }
    }
}
