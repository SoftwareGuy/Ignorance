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

            ClientShowerhead.OnClientConnected.AddListener(ClientConnectedToServer);
            ClientShowerhead.OnClientDataReceived.AddListener(ClientDataReceived);
            ClientShowerhead.OnClientDisconnected.AddListener(ClientDisconnectedFromServer);
            ClientShowerhead.OnClientError.AddListener(ClientErrored);

            ClientShowerhead.Start(address, Port);
        }

        public override bool ClientConnected()
        {
            return ClientShowerhead.IsClientConnected();
        }

        public override void ClientDisconnect()
        {
            ClientShowerhead.Stop();

            ClientShowerhead.OnClientConnected.RemoveListener(ClientConnectedToServer);
            ClientShowerhead.OnClientDataReceived.RemoveListener(ClientDataReceived);
            ClientShowerhead.OnClientDisconnected.RemoveListener(ClientDisconnectedFromServer);
            ClientShowerhead.OnClientError.RemoveListener(ClientErrored);
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

            Debug.Log("Queued a packet");
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
            Debug.Log("IsServerActive() polled");
            return ServerShowerhead.IsServerActive();
        }

        public override bool ServerDisconnect(int connectionId)
        {
            Debug.Log("ServerDisconnect() poked");
            return ServerShowerhead.DisconnectThatConnection(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            Debug.Log("ServerGetClientAddress() poked");
            return ServerShowerhead.GetClientAddress(connectionId);
        }

        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
        {
            Packet outPkt = default;

            // outPkt.Create(data.Array, data.Offset, data.Count, MapKnownChannelTypeToENETPacketFlag(m_ChannelDefinitions[channelId]));
            outPkt.Create(data.Array, data.Offset, data.Count, PacketFlags.Reliable);

            // TODO: Channels check.

            if (ServerShowerhead.IsConnectionIdKnown(connectionId))
            {
                ServerShowerhead.Outgoing.Enqueue(new QueuedOutgoingPacket()
                {
                    targetConnectionId = connectionId,
                    channelId = (byte)channelId,
                    contents = outPkt,
                });

                Debug.Log("Queued a packet");
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
            // Initialize our event handlers.
            ServerShowerhead.InitializeEventHandlers();

            // Attach them.
            ServerShowerhead.OnServerConnected.AddListener(ServerConnected);
            ServerShowerhead.OnServerDisconnected.AddListener(ServerDisconnected);
            ServerShowerhead.OnServerDataReceived.AddListener(ServerDataReceived);
            ServerShowerhead.OnServerError.AddListener(ServerErrored);

            // Start the server thread.
            ServerShowerhead.Start(Port);
        }

        public override void ServerStop()
        {
            ServerShowerhead.Stop();

            ServerShowerhead.OnServerConnected.RemoveListener(ServerConnected);
            ServerShowerhead.OnServerDisconnected.RemoveListener(ServerDisconnected);
            ServerShowerhead.OnServerDataReceived.RemoveListener(ServerDataReceived);
            ServerShowerhead.OnServerError.RemoveListener(ServerErrored);
        }

        public override void Shutdown()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Server packet queue processors
        private void ProcessQueuedServerIncomingPackets()
        {
            if (ServerShowerhead.Incoming != null && ServerShowerhead.Incoming.Count > 0)
            {
                while (ServerShowerhead.Incoming.Count > 0)
                {
                    QueuedIncomingPacket packet;

                    if (ServerShowerhead.Incoming.TryDequeue(out packet))
                    {
                        byte[] databuff = new byte[packet.contents.Length];
                        packet.contents.CopyTo(databuff);

                        OnServerDataReceived.Invoke(packet.connectionId, databuff);
                    }
                }
            }
        }
        #endregion

        #region Client packet queue processors 
        private void ProcessQueuedClientIncomingPackets()
        {
            if (ClientShowerhead.Incoming != null && ClientShowerhead.Incoming.Count > 0)
            {
                while (ClientShowerhead.Incoming.Count > 0)
                {
                    QueuedIncomingPacket packet;

                    if (ClientShowerhead.Incoming.TryDequeue(out packet))
                    {
                        byte[] databuff = new byte[packet.contents.Length];
                        packet.contents.CopyTo(databuff);

                        OnClientDataReceived.Invoke(databuff);
                    }
                }
            }
        }
        #endregion

        #region Update method
        private void LateUpdate()
        {
            if(enabled)
            {
                ProcessQueuedServerIncomingPackets();
                ProcessQueuedClientIncomingPackets();
            }
        }
        #endregion
        public override int GetMaxPacketSize(int channelId = 0)
        {
            return (int)Library.maxPacketSize;  // 33,554,432 bytes.
        }

        #region Mirror Event Wrappers
        private void ServerConnected(int connectionId)
        {
            Debug.Log("Got Poked");
            OnServerConnected.Invoke(connectionId);
        }

        private void ServerDisconnected(int connectionId)
        {
            Debug.Log("Got Poked");
            OnServerDisconnected.Invoke(connectionId);
        }

        private void ServerDataReceived(int arg0, byte[] arg1)
        {
            Debug.Log("Got Poked");
            OnServerDataReceived.Invoke(arg0, arg1);
        }

        private void ServerErrored(int arg0, Exception arg1)
        {
            Debug.Log("Got Poked");
            OnServerError.Invoke(arg0, arg1);
        }

        private void ClientConnectedToServer()
        {
            Debug.Log("Got Poked");
            OnClientConnected.Invoke();
        }

        private void ClientDisconnectedFromServer()
        {
            Debug.Log("Got Poked");
            OnClientDisconnected.Invoke();
        }

        private void ClientDataReceived(byte[] data)
        {
            Debug.Log("Got Poked");
            OnClientDataReceived.Invoke(data);
        }

        private void ClientErrored(Exception arg0)
        {
            Debug.Log("Got Poked");
            OnClientError.Invoke(arg0);
        }
        #endregion
    }
}
